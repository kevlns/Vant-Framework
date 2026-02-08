using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Vant.Resources;
using Vant.System;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using Vant.Core;

namespace Vant.System.Scene
{
    /// <summary>
    /// 场景管理器
    /// Addressables 为主，内置场景为辅
    /// </summary>
    public interface ISceneManager
    {
        UniTask LoadSceneAsync(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, global::System.Action<float> onProgress = null, global::System.Action onCompleted = null, CancellationToken cancellationToken = default);
        UniTask UnloadSceneAsync(string sceneKey);
        UniTask ActivateSceneAsync(string sceneKey, global::System.Action<float> onProgress = null, CancellationToken cancellationToken = default);
        bool IsSceneLoaded(string sceneKey);
    }

    public class SceneManager : ISceneManager
    {
        private class LoadInFlight
        {
            public LoadContext Context;
            public readonly List<global::System.Action<float>> ProgressCallbacks = new List<global::System.Action<float>>();
            public readonly UniTaskCompletionSource<bool> CompletionSource = new UniTaskCompletionSource<bool>();
            public bool IsCanceled;
        }

        private class SceneLoadState
        {
            public LoadContext Context;
            public bool HasContext;

            public bool IsUnloading;

            public bool IsAddressable;
            public bool HasAddressableHandle;
            public AsyncOperationHandle<SceneInstance> AddressableHandle;

            public AsyncOperation BuiltinLoadOp;
            public bool PendingActivationAvailable;

            public bool PendingActivateRequest;

            public LoadInFlight LoadInFlight;
            public LoadInFlight ActivateInFlight;

            public CancellationTokenSource LoadCts;
            public CancellationTokenSource ActivateCts;
        }

        private struct LoadContext
        {
            public LoadSceneMode Mode;
            public bool ActivateOnLoad;

            public bool Equals(LoadContext other)
            {
                return Mode == other.Mode && ActivateOnLoad == other.ActivateOnLoad;
            }
        }

        public static SceneManager Instance { get; private set; }
        private AppCore _appCore;

        private readonly IAssetManager _assetManager;
        private readonly Dictionary<string, SceneLoadState> _sceneStates = new Dictionary<string, SceneLoadState>();

        public SceneManager(AppCore appCore, IAssetManager assetManager)
        {
            Instance = this;
            _appCore = appCore;
            _assetManager = assetManager;
        }

        private static void AddProgressCallback(LoadInFlight inflight, global::System.Action<float> onProgress)
        {
            if (inflight == null || onProgress == null) return;
            inflight.ProgressCallbacks.Add(onProgress);
        }

        private static void InvokeProgress(LoadInFlight inflight, float value)
        {
            if (inflight == null || inflight.IsCanceled || inflight.ProgressCallbacks.Count == 0) return;
            var snapshot = inflight.ProgressCallbacks.ToArray();
            foreach (var cb in snapshot)
            {
                cb?.Invoke(value);
            }
        }

        private static void CancelInFlight(LoadInFlight inflight)
        {
            if (inflight == null) return;
            inflight.IsCanceled = true;
            inflight.ProgressCallbacks.Clear();
            inflight.CompletionSource.TrySetResult(false);
        }

        private void ResetState(string sceneKey, SceneLoadState state, bool remove, bool releaseAddressableHandle)
        {
            if (state == null) return;

            state.IsUnloading = false;

            CancelInFlight(state.LoadInFlight);
            CancelInFlight(state.ActivateInFlight);
            state.LoadInFlight = null;
            state.ActivateInFlight = null;

            if (state.LoadCts != null)
            {
                state.LoadCts.Cancel();
                state.LoadCts.Dispose();
                state.LoadCts = null;
            }

            if (state.ActivateCts != null)
            {
                state.ActivateCts.Cancel();
                state.ActivateCts.Dispose();
                state.ActivateCts = null;
            }

            state.PendingActivateRequest = false;
            state.PendingActivationAvailable = false;
            state.BuiltinLoadOp = null;
            state.HasContext = false;
            state.Context = default;

            if (releaseAddressableHandle && state.HasAddressableHandle && state.AddressableHandle.IsValid())
            {
                Addressables.Release(state.AddressableHandle);
            }

            state.HasAddressableHandle = false;
            state.AddressableHandle = default;
            state.IsAddressable = false;

            if (remove)
            {
                _sceneStates.Remove(sceneKey);
            }
        }

        private SceneLoadState GetOrCreateState(string sceneKey)
        {
            if (_sceneStates.TryGetValue(sceneKey, out var state))
            {
                return state;
            }

            state = new SceneLoadState();
            _sceneStates[sceneKey] = state;
            return state;
        }

        private bool EnsureContext(SceneLoadState state, string sceneKey, LoadContext requestContext, string sourceLabel)
        {
            if (!state.HasContext)
            {
                state.Context = requestContext;
                state.HasContext = true;
                return true;
            }

            if (!state.Context.Equals(requestContext))
            {
                Debug.LogWarning($"[SceneManager] LoadSceneAsync rejected ({sourceLabel}). SceneKey={sceneKey} Params mismatch.");
                return false;
            }

            return true;
        }

        public async UniTask LoadSceneAsync(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, global::System.Action<float> onProgress = null, global::System.Action onCompleted = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneKey))
            {
                Debug.LogError("[SceneManager] LoadSceneAsync called with empty sceneKey.");
                return;
            }

            var requestContext = new LoadContext { Mode = mode, ActivateOnLoad = activateOnLoad };
            var state = GetOrCreateState(sceneKey);

            if (state.IsUnloading)
            {
                Debug.LogWarning($"[SceneManager] LoadSceneAsync rejected (unloading). SceneKey={sceneKey}");
                return;
            }

            if (state.HasAddressableHandle && state.AddressableHandle.IsValid())
            {
                if (!EnsureContext(state, sceneKey, requestContext, "addressable")) return;

                if (state.LoadInFlight != null)
                {
                    AddProgressCallback(state.LoadInFlight, onProgress);
                    await state.LoadInFlight.CompletionSource.Task;
                    if (state.AddressableHandle.IsValid() && state.AddressableHandle.IsDone && state.AddressableHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        onCompleted?.Invoke();
                    }
                    return;
                }

                while (!state.AddressableHandle.IsDone)
                {
                    onProgress?.Invoke(state.AddressableHandle.PercentComplete);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                onProgress?.Invoke(1f);
                if (!activateOnLoad && state.PendingActivateRequest)
                {
                    await ActivateSceneAsync(sceneKey, null);
                }
                onCompleted?.Invoke();
                return;
            }

            // 如果之前有已完成的 CTS，清理它（不 Cancel，因为已完成）
            state.LoadCts?.Dispose();
            state.LoadCts = TaskManager.Instance.CreateLinkedCTS(cancellationToken);
            var loadToken = state.LoadCts.Token;

            var handle = _assetManager.LoadSceneAsync(sceneKey, mode, activateOnLoad);
            state.IsAddressable = handle.IsAddressable;
            state.Context = requestContext;
            state.HasContext = true;

            if (handle.IsAddressable)
            {
                state.HasAddressableHandle = true;
                state.AddressableHandle = handle.AddressableHandle;

                var inflight = new LoadInFlight { Context = requestContext };
                AddProgressCallback(inflight, onProgress);
                state.LoadInFlight = inflight;

                try
                {
                    while (!handle.AddressableHandle.IsDone)
                    {
                        if (loadToken.IsCancellationRequested)
                        {
                            CancelInFlight(inflight);
                            return;
                        }

                        InvokeProgress(inflight, handle.AddressableHandle.PercentComplete);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    if (loadToken.IsCancellationRequested)
                    {
                        CancelInFlight(inflight);
                        return;
                    }

                    await AddressablesAsyncExtensions.ToUniTask(handle.AddressableHandle);
                    if (handle.AddressableHandle.Status != AsyncOperationStatus.Succeeded)
                    {
                        Debug.LogError($"[SceneManager] Failed to load addressable scene: {sceneKey}");
                        inflight.CompletionSource.TrySetResult(false);
                        ResetState(sceneKey, state, true, true);
                        return;
                    }
                    else
                    {
                        if (!activateOnLoad)
                        {
                            // 保留 handle，等待手动激活
                            inflight.CompletionSource.TrySetResult(true);
                            if (state.PendingActivateRequest)
                            {
                                await ActivateSceneAsync(sceneKey, null);
                            }
                            onCompleted?.Invoke();
                            return;
                        }
                        InvokeProgress(inflight, 1f);
                        inflight.CompletionSource.TrySetResult(true);
                        onCompleted?.Invoke();
                    }
                }
                catch (global::System.Exception e)
                {
                    Debug.LogError($"[SceneManager] Exception loading addressable scene {sceneKey}: {e.Message}");
                    inflight.CompletionSource.TrySetResult(false);
                    ResetState(sceneKey, state, true, true);
                }
                finally
                {
                    state.LoadInFlight = null;
                    state.LoadCts?.Dispose();
                    state.LoadCts = null;
                }
                return;
            }

            if (state.PendingActivationAvailable)
            {
                if (!EnsureContext(state, sceneKey, requestContext, "builtin")) return;
                onProgress?.Invoke(1f);
                if (!activateOnLoad && state.PendingActivateRequest)
                {
                    await ActivateSceneAsync(sceneKey, null);
                }
                onCompleted?.Invoke();
                return;
            }

            if (state.LoadInFlight != null)
            {
                if (!EnsureContext(state, sceneKey, requestContext, "builtin")) return;
                AddProgressCallback(state.LoadInFlight, onProgress);
                await state.LoadInFlight.CompletionSource.Task;
                if (IsSceneLoaded(sceneKey))
                {
                    onCompleted?.Invoke();
                }
                return;
            }

            var op = handle.BuiltinLoadOp;
            if (op != null)
            {
                state.IsAddressable = false;
                state.BuiltinLoadOp = op;
                state.Context = requestContext;
                state.HasContext = true;

                var inflight = new LoadInFlight { Context = requestContext };
                AddProgressCallback(inflight, onProgress);
                state.LoadInFlight = inflight;
                op.allowSceneActivation = activateOnLoad;
                try
                {
                    while (!op.isDone)
                    {
                        if (loadToken.IsCancellationRequested)
                        {
                            CancelInFlight(inflight);
                            return;
                        }

                        if (activateOnLoad)
                        {
                            InvokeProgress(inflight, op.progress);
                        }
                        else
                        {
                            InvokeProgress(inflight, Mathf.Clamp01(op.progress / 0.9f));
                            if (op.progress >= 0.9f)
                            {
                                state.PendingActivationAvailable = true;
                                InvokeProgress(inflight, 1f);
                                if (state.PendingActivateRequest)
                                {
                                    await ActivateSceneAsync(sceneKey, null);
                                }
                                return;
                            }
                        }

                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    if (loadToken.IsCancellationRequested)
                    {
                        CancelInFlight(inflight);
                        return;
                    }

                    var loadedScene = UnitySceneManager.GetSceneByName(sceneKey);
                    if (!loadedScene.isLoaded)
                    {
                        Debug.LogError($"[SceneManager] Builtin scene load failed: {sceneKey}");
                        inflight.CompletionSource.TrySetResult(false);
                        ResetState(sceneKey, state, true, false);
                        return;
                    }

                    InvokeProgress(inflight, 1f);
                    inflight.CompletionSource.TrySetResult(true);
                    onCompleted?.Invoke();
                }
                finally
                {
                    state.LoadInFlight = null;
                    state.LoadCts?.Dispose();
                    state.LoadCts = null;
                }
            }
            else
            {
                Debug.LogError($"[SceneManager] Builtin scene load failed (null AsyncOperation): {sceneKey}");
                ResetState(sceneKey, state, true, false);
            }
        }

        public async UniTask UnloadSceneAsync(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey)) return;

            if (_sceneStates.TryGetValue(sceneKey, out var state))
            {
                state.IsUnloading = true;
                if (state.LoadCts != null)
                {
                    state.LoadCts.Cancel();
                }

                if (state.ActivateCts != null)
                {
                    state.ActivateCts.Cancel();
                }

                CancelInFlight(state.LoadInFlight);
                CancelInFlight(state.ActivateInFlight);
            }

            if (_sceneStates.TryGetValue(sceneKey, out var addressableState) && addressableState.HasAddressableHandle && addressableState.AddressableHandle.IsValid())
            {
                await _assetManager.UnloadSceneAsync(CreateSceneHandle(sceneKey, addressableState));
                ResetState(sceneKey, addressableState, true, false);
                return;
            }

            await _assetManager.UnloadSceneAsync(CreateSceneHandle(sceneKey, null));

            if (_sceneStates.TryGetValue(sceneKey, out var cleanupState))
            {
                ResetState(sceneKey, cleanupState, true, false);
            }
        }

        public bool IsSceneLoaded(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey)) return false;

            if (_sceneStates.TryGetValue(sceneKey, out var state) && state.HasAddressableHandle && state.AddressableHandle.IsValid())
            {
                return state.AddressableHandle.IsDone && state.AddressableHandle.Status == AsyncOperationStatus.Succeeded;
            }

            var scene = UnitySceneManager.GetSceneByName(sceneKey);
            return scene.isLoaded;
        }

        public async UniTask ActivateSceneAsync(string sceneKey, global::System.Action<float> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneKey)) return;

            var state = GetOrCreateState(sceneKey);
            if (state.IsUnloading)
            {
                Debug.LogWarning($"[SceneManager] ActivateSceneAsync rejected (unloading). SceneKey={sceneKey}");
                return;
            }
            state.PendingActivateRequest = true;

            if (state.ActivateInFlight != null)
            {
                AddProgressCallback(state.ActivateInFlight, onProgress);
                await state.ActivateInFlight.CompletionSource.Task;
                return;
            }

            if (state.HasAddressableHandle && state.AddressableHandle.IsValid())
            {
                // 如果之前有已完成的 CTS，直接清理
                state.ActivateCts?.Dispose();
                state.ActivateCts = TaskManager.Instance.CreateLinkedCTS(cancellationToken);
                var activateToken = state.ActivateCts.Token;

                var activateInFlight = new LoadInFlight { Context = state.Context };
                AddProgressCallback(activateInFlight, onProgress);
                state.ActivateInFlight = activateInFlight;

                try
                {
                    var activateHandle = state.AddressableHandle.Result.ActivateAsync();
                    while (!activateHandle.isDone)
                    {
                        if (activateToken.IsCancellationRequested)
                        {
                            CancelInFlight(activateInFlight);
                            return;
                        }

                        InvokeProgress(activateInFlight, activateHandle.progress);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    if (activateToken.IsCancellationRequested)
                    {
                        CancelInFlight(activateInFlight);
                        return;
                    }

                    InvokeProgress(activateInFlight, 1f);
                }
                catch (global::System.Exception e)
                {
                    Debug.LogError($"[SceneManager] Exception activating addressable scene {sceneKey}: {e.Message}");
                }
                finally
                {
                    activateInFlight.CompletionSource.TrySetResult(true);
                    state.ActivateInFlight = null;
                    state.ActivateCts?.Dispose();
                    state.ActivateCts = null;
                }
                return;
            }

            if (state.BuiltinLoadOp != null)
            {
                if (state.BuiltinLoadOp.isDone && !UnitySceneManager.GetSceneByName(sceneKey).isLoaded)
                {
                    Debug.LogError($"[SceneManager] Builtin scene activate failed (not loaded): {sceneKey}");
                    ResetState(sceneKey, state, true, false);
                    return;
                }

                if (!state.PendingActivationAvailable && state.BuiltinLoadOp.progress < 0.9f)
                {
                    return;
                }

                // 如果之前有已完成的 CTS，直接清理
                state.ActivateCts?.Dispose();
                state.ActivateCts = TaskManager.Instance.CreateLinkedCTS(cancellationToken);
                var activateToken = state.ActivateCts.Token;

                var activateInFlight = new LoadInFlight { Context = state.Context };
                AddProgressCallback(activateInFlight, onProgress);
                state.ActivateInFlight = activateInFlight;

                try
                {
                    state.BuiltinLoadOp.allowSceneActivation = true;
                    while (!state.BuiltinLoadOp.isDone)
                    {
                        if (activateToken.IsCancellationRequested)
                        {
                            CancelInFlight(activateInFlight);
                            return;
                        }

                        InvokeProgress(activateInFlight, state.BuiltinLoadOp.progress);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    if (activateToken.IsCancellationRequested)
                    {
                        CancelInFlight(activateInFlight);
                        return;
                    }

                    InvokeProgress(activateInFlight, 1f);
                    state.PendingActivationAvailable = false;
                    state.PendingActivateRequest = false;
                }
                finally
                {
                    activateInFlight.CompletionSource.TrySetResult(true);
                    state.ActivateInFlight = null;
                    state.ActivateCts?.Dispose();
                    state.ActivateCts = null;
                }
            }
        }

        private static SceneHandle CreateSceneHandle(string sceneKey, SceneLoadState state)
        {
            return new SceneHandle
            {
                SceneKey = sceneKey,
                IsAddressable = state != null && state.HasAddressableHandle && state.AddressableHandle.IsValid(),
                AddressableHandle = state != null ? state.AddressableHandle : default,
                BuiltinLoadOp = state != null ? state.BuiltinLoadOp : null
            };
        }
    }
}
