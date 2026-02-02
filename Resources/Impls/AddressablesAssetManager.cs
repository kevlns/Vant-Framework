using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Vant.Resources
{
    /// <summary>
    /// 基于 Addressables 的资源管理器
    /// 适用于：生产环境、热更新资源、内存管理要求高的场景
    /// </summary>
    public class AddressablesManager : AssetManagerBase
    {
        private readonly object _lock = new object();

        private class HandleInfo
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
            public LoadInFlight InFlight;
            public bool ReleaseWhenLoaded;
        }

        private class LoadInFlight
        {
            public readonly List<Action<float>> ProgressCallbacks = new List<Action<float>>();
            public readonly UniTaskCompletionSource<Object> CompletionSource = new UniTaskCompletionSource<Object>();
            public bool IsCanceled;
        }

        private readonly Dictionary<string, HandleInfo> _loadedHandles = new Dictionary<string, HandleInfo>();

        public override async UniTask<T> LoadAssetAsync<T>(string key, string packageName = null, Action<float> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) return default;

            HandleInfo info;
            lock (_lock)
            {
                _loadedHandles.TryGetValue(key, out info);
                if (info != null)
                {
                    info.RefCount++;
                }
            }

            if (info != null)
            {
                if (info.Handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return info.Handle.Result as T;
                }

                if (info.InFlight != null)
                {
                    AddProgressCallback(info.InFlight, onProgress);
                    using (var registration = cancellationToken.Register(() =>
                    {
                        RemoveProgressCallback(info.InFlight, onProgress);
                        ReleaseAsset(key);
                    }))
                    {
                        var loaded = await info.InFlight.CompletionSource.Task.AttachExternalCancellation(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        return loaded as T;
                    }
                }

                // 非预期情况：没有 in-flight 但未完成，直接等待 Handle
                await AddressablesAsyncExtensions.ToUniTask(info.Handle);
                cancellationToken.ThrowIfCancellationRequested();
                return info.Handle.Result as T;
            }

            // 开启新加载 - 使用 double-check 防止竞态
            HandleInfo newInfo = null;
            HandleInfo existingInfo = null;
            LoadInFlight inflight = null;
            bool joinExisting = false;
            bool waitHandle = false;

            lock (_lock)
            {
                // Double-check: 可能其他线程已经创建了条目
                if (_loadedHandles.TryGetValue(key, out existingInfo))
                {
                    existingInfo.RefCount++;
                    if (existingInfo.Handle.IsDone)
                    {
                        // 已完成，直接返回
                    }
                    else if (existingInfo.InFlight != null)
                    {
                        AddProgressCallback(existingInfo.InFlight, onProgress);
                        inflight = existingInfo.InFlight;
                        joinExisting = true;
                    }
                    else
                    {
                        // 非预期：需要等待 Handle
                        waitHandle = true;
                    }
                }
                else
                {
                    var handle = Addressables.LoadAssetAsync<T>(key);
                    inflight = new LoadInFlight();
                    AddProgressCallback(inflight, onProgress);
                    newInfo = new HandleInfo { Handle = handle, RefCount = 1, InFlight = inflight, ReleaseWhenLoaded = false };
                    _loadedHandles[key] = newInfo;
                }
            }

            // 处理 double-check 发现已有条目的情况
            if (existingInfo != null && newInfo == null)
            {
                if (existingInfo.Handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return existingInfo.Handle.Result as T;
                }

                if (waitHandle)
                {
                    await AddressablesAsyncExtensions.ToUniTask(existingInfo.Handle);
                    cancellationToken.ThrowIfCancellationRequested();
                    return existingInfo.Handle.Result as T;
                }

                if (joinExisting)
                {
                    using (var registration = cancellationToken.Register(() =>
                    {
                        RemoveProgressCallback(inflight, onProgress);
                        ReleaseAsset(key);
                    }))
                    {
                        var loaded = await inflight.CompletionSource.Task.AttachExternalCancellation(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        return loaded as T;
                    }
                }
            }

            // 启动新加载任务
            RunAddressablesLoad(key, newInfo, inflight).Forget();

            using (var registration = cancellationToken.Register(() =>
            {
                RemoveProgressCallback(inflight, onProgress);
                ReleaseAsset(key);
            }))
            {
                var loaded = await inflight.CompletionSource.Task.AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return loaded as T;
            }
        }

        private async UniTask RunAddressablesLoad(string key, HandleInfo info, LoadInFlight inflight)
        {
            try
            {
                while (!info.Handle.IsDone)
                {
                    InvokeProgress(inflight, info.Handle.PercentComplete);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                await AddressablesAsyncExtensions.ToUniTask(info.Handle);
                if (info.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[AddressablesManager] Failed to load asset: {key}");
                    inflight.CompletionSource.TrySetResult(null);
                    lock (_lock)
                    {
                        _loadedHandles.Remove(key);
                    }
                    Addressables.Release(info.Handle);
                    return;
                }

                inflight.CompletionSource.TrySetResult(info.Handle.Result as Object);
                InvokeProgress(inflight, 1f);

                bool releaseNow = false;
                lock (_lock)
                {
                    if (info.ReleaseWhenLoaded || info.RefCount <= 0)
                    {
                        releaseNow = true;
                        _loadedHandles.Remove(key);
                    }
                }

                if (releaseNow && info.Handle.IsValid())
                {
                    Addressables.Release(info.Handle);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesManager] Exception loading asset {key}: {e.Message}");
                inflight.CompletionSource.TrySetResult(null);
                lock (_lock)
                {
                    _loadedHandles.Remove(key);
                }
                if (info.Handle.IsValid())
                {
                    Addressables.Release(info.Handle);
                }
            }
            finally
            {
                info.InFlight = null;
                lock (inflight.ProgressCallbacks)
                {
                    inflight.ProgressCallbacks.Clear();
                }
            }
        }

        private static void AddProgressCallback(LoadInFlight inflight, Action<float> onProgress)
        {
            if (inflight == null || onProgress == null) return;
            lock (inflight.ProgressCallbacks)
            {
                inflight.ProgressCallbacks.Add(onProgress);
            }
        }

        private static void InvokeProgress(LoadInFlight inflight, float value)
        {
            if (inflight == null || inflight.IsCanceled || inflight.ProgressCallbacks.Count == 0) return;
            Action<float>[] snapshot;
            lock (inflight.ProgressCallbacks)
            {
                if (inflight.ProgressCallbacks.Count == 0) return;
                snapshot = inflight.ProgressCallbacks.ToArray();
            }
            foreach (var cb in snapshot)
            {
                cb?.Invoke(value);
            }
        }

        private static void RemoveProgressCallback(LoadInFlight inflight, Action<float> onProgress)
        {
            if (inflight == null || onProgress == null) return;
            lock (inflight.ProgressCallbacks)
            {
                inflight.ProgressCallbacks.Remove(onProgress);
            }
        }

        public override void ReleaseAsset(string key, string packageName = null)
        {
            HandleInfo info;
            bool releaseNow = false;
            lock (_lock)
            {
                _loadedHandles.TryGetValue(key, out info);
                if (info != null)
                {
                    info.RefCount--;
                    if (info.RefCount <= 0)
                    {
                        if (info.Handle.IsDone)
                        {
                            releaseNow = true;
                            _loadedHandles.Remove(key);
                        }
                        else
                        {
                            info.RefCount = 0;
                            info.ReleaseWhenLoaded = true;
                        }
                    }
                }
            }

            if (releaseNow && info != null && info.Handle.IsValid())
            {
                Addressables.Release(info.Handle);
            }
        }

        public override void ClearUnused()
        {
            // 清理对象池中的对象，确保引用计数正确下降
            ClearPool();

            // Addressables 的引用计数机制通常能自动处理，
            // 这里可以用来强制清理那些 RefCount <= 0 但可能因为逻辑漏洞没被移除的 Handle
            var toRemove = new List<string>();
            lock (_lock)
            {
                foreach (var kvp in _loadedHandles)
                {
                    if (kvp.Value.RefCount <= 0)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in toRemove)
            {
                ReleaseAsset(key);
            }
        }
    }
}
