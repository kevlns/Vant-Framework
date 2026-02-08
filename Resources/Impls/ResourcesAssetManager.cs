using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vant.Resources
{
    /// <summary>
    /// 基于 Resources 的资源管理器
    /// 适用于：开发阶段、小型项目、必须放在 Resources 目录下的配置/预制体
    /// </summary>
    public class ResourcesAssetManager : AssetManagerBase
    {
        private readonly object _lock = new object();

        private class AssetInfo
        {
            public Object Asset;
            public int RefCount;
            public UniTaskCompletionSource<Object> LoadingTcs;
            public bool ReleaseWhenLoaded;
            public List<Action<float>> ProgressCallbacks;
        }

        private readonly Dictionary<string, AssetInfo> _loadedAssets = new Dictionary<string, AssetInfo>();

        public override async UniTask<T> LoadAssetAsync<T>(string path, string packageName = null, Action<float> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path)) return default;

            AssetInfo info;
            lock (_lock)
            {
                _loadedAssets.TryGetValue(path, out info);
                if (info != null)
                {
                    info.RefCount++;
                }
            }

            if (info != null)
            {
                if (info.Asset != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return info.Asset as T;
                }

                // 资源仍在加载中：等待同一个 in-flight 任务，避免重复 LoadAsync 导致重复 Add key 或引用计数错乱
                if (info.LoadingTcs != null)
                {
                    AddProgressCallback(info, onProgress);
                    using (var registration = cancellationToken.Register(() =>
                    {
                        RemoveProgressCallback(info, onProgress);
                        ReleaseAsset(path);
                    }))
                    {
                        var asset = await info.LoadingTcs.Task.AttachExternalCancellation(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        return asset as T;
                    }
                }

                return default;
            }

            // 首次请求：使用 double-check 防止竞态
            AssetInfo newInfo = null;
            AssetInfo existingInfo = null;
            bool joinExisting = false;

            lock (_lock)
            {
                // Double-check: 可能其他协程已经创建了条目
                if (_loadedAssets.TryGetValue(path, out existingInfo))
                {
                    existingInfo.RefCount++;
                    if (existingInfo.Asset != null)
                    {
                        // 已加载完成，直接返回
                    }
                    else if (existingInfo.LoadingTcs != null)
                    {
                        AddProgressCallback(existingInfo, onProgress);
                        joinExisting = true;
                    }
                    // else: Asset == null && LoadingTcs == null，返回 default
                }
                else
                {
                    newInfo = new AssetInfo
                    {
                        Asset = null,
                        RefCount = 1,
                        LoadingTcs = new UniTaskCompletionSource<Object>(),
                        ReleaseWhenLoaded = false,
                        ProgressCallbacks = new List<Action<float>>()
                    };
                    _loadedAssets[path] = newInfo;
                }
            }

            // 处理 double-check 发现已有条目的情况
            if (existingInfo != null && newInfo == null)
            {
                if (existingInfo.Asset != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return existingInfo.Asset as T;
                }

                if (joinExisting)
                {
                    using (var registration = cancellationToken.Register(() =>
                    {
                        RemoveProgressCallback(existingInfo, onProgress);
                        ReleaseAsset(path);
                    }))
                    {
                        var asset = await existingInfo.LoadingTcs.Task.AttachExternalCancellation(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        return asset as T;
                    }
                }

                return default;
            }

            AddProgressCallback(newInfo, onProgress);

            RunResourcesLoad(path, newInfo).Forget();

            using (var registration = cancellationToken.Register(() =>
            {
                RemoveProgressCallback(newInfo, onProgress);
                ReleaseAsset(path);
            }))
            {
                var asset = await newInfo.LoadingTcs.Task.AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return asset as T;
            }

        }

        private async UniTask RunResourcesLoad(string path, AssetInfo info)
        {
            try
            {
                ResourceRequest request = UnityEngine.Resources.LoadAsync<Object>(path);
                while (!request.isDone)
                {
                    InvokeProgress(info, request.progress);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                await UnityAsyncExtensions.ToUniTask(request);

                if (request.asset == null)
                {
                    Debug.LogError($"[ResourcesAssetManager] Failed to load asset: {path}");
                    lock (_lock)
                    {
                        _loadedAssets.Remove(path);
                    }
                    info.LoadingTcs.TrySetResult(null);
                    return;
                }

                info.Asset = request.asset;
                info.LoadingTcs.TrySetResult(info.Asset);
                info.LoadingTcs = null;

                // 如果加载期间所有引用都释放了，则加载完成后立刻清理
                bool releaseNow = false;
                lock (_lock)
                {
                    if (info.RefCount <= 0 || info.ReleaseWhenLoaded)
                    {
                        releaseNow = true;
                        _loadedAssets.Remove(path);
                    }
                }

                // 成功时先通知进度完成，再释放资源
                InvokeProgress(info, 1f);

                if (releaseNow && info.Asset != null && !(info.Asset is GameObject))
                {
                    UnityEngine.Resources.UnloadAsset(info.Asset);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourcesAssetManager] Exception loading asset {path}: {e.Message}");
                lock (_lock)
                {
                    _loadedAssets.Remove(path);
                }
                info.LoadingTcs.TrySetResult(null);
            }
            finally
            {
                if (info.ProgressCallbacks != null)
                {
                    lock (info.ProgressCallbacks)
                    {
                        info.ProgressCallbacks.Clear();
                    }
                }
            }
        }

        private static void AddProgressCallback(AssetInfo info, Action<float> onProgress)
        {
            if (info == null || onProgress == null) return;
            if (info.ProgressCallbacks == null)
            {
                info.ProgressCallbacks = new List<Action<float>>();
            }
            lock (info.ProgressCallbacks)
            {
                info.ProgressCallbacks.Add(onProgress);
            }
        }

        private static void InvokeProgress(AssetInfo info, float value)
        {
            if (info == null || info.ProgressCallbacks == null || info.ProgressCallbacks.Count == 0) return;
            Action<float>[] snapshot;
            lock (info.ProgressCallbacks)
            {
                if (info.ProgressCallbacks.Count == 0) return;
                snapshot = info.ProgressCallbacks.ToArray();
            }
            foreach (var cb in snapshot)
            {
                cb?.Invoke(value);
            }
        }

        private static void RemoveProgressCallback(AssetInfo info, Action<float> onProgress)
        {
            if (info == null || onProgress == null || info.ProgressCallbacks == null) return;
            lock (info.ProgressCallbacks)
            {
                info.ProgressCallbacks.Remove(onProgress);
            }
        }

        public override void ReleaseAsset(string path, string packageName = null)
        {
            AssetInfo info;
            bool releaseNow = false;
            lock (_lock)
            {
                _loadedAssets.TryGetValue(path, out info);
                if (info != null)
                {
                    info.RefCount--;
                    if (info.RefCount <= 0)
                    {
                        // 仍在加载：无法取消 Resources.LoadAsync，只能标记加载完后释放
                        if (info.Asset == null)
                        {
                            info.RefCount = 0;
                            info.ReleaseWhenLoaded = true;
                        }
                        else
                        {
                            releaseNow = true;
                            _loadedAssets.Remove(path);
                        }
                    }
                }
            }

            if (releaseNow && info != null)
            {
                // Resources.UnloadAsset 只能卸载非 GameObject 资源 (如 Texture, Mesh)
                // 对于 GameObject，通常依赖 Resources.UnloadUnusedAssets()
                if (!(info.Asset is GameObject))
                {
                    UnityEngine.Resources.UnloadAsset(info.Asset);
                }
            }
        }

        public override void ClearUnused()
        {
            // 清理对象池中的对象，确保引用计数正确下降
            ClearPool();

            // 移除引用计数 <= 0 的资源
            var toRemove = new List<string>();
            lock (_lock)
            {
                foreach (var kvp in _loadedAssets)
                {
                    var info = kvp.Value;
                    if (info == null) continue;

                    if (info.RefCount <= 0)
                    {
                        // 正在加载中的资源不要直接移除：保留条目，加载完成后可正确 Unload
                        if (info.Asset == null && info.LoadingTcs != null)
                        {
                            info.ReleaseWhenLoaded = true;
                            continue;
                        }

                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in toRemove)
            {
                lock (_lock)
                {
                    _loadedAssets.Remove(key);
                }
            }

            UnityEngine.Resources.UnloadUnusedAssets();
        }

        public override SceneHandle LoadSceneAsync(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
        {
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneKey, mode);
            if (op != null)
            {
                op.allowSceneActivation = activateOnLoad;
            }

            return new SceneHandle
            {
                SceneKey = sceneKey,
                IsAddressable = false,
                AddressableHandle = default,
                BuiltinLoadOp = op
            };
        }

        public override async UniTask UnloadSceneAsync(SceneHandle handle)
        {
            if (!string.IsNullOrEmpty(handle.SceneKey))
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(handle.SceneKey);
                if (scene.isLoaded)
                {
                    var op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
                    if (op != null)
                    {
                        await op.ToUniTask();
                    }
                }
            }
        }
    }
}
