using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vant.Resources
{
    /// <summary>
    /// 基于 Resources 的资源管理器
    /// 适用于：开发阶段、小型项目、必须放在 Resources 目录下的配置/预制体
    /// </summary>
    public class ResourcesAssetManager : AssetManagerBase
    {
        private class AssetInfo
        {
            public Object Asset;
            public int RefCount;
            public UniTaskCompletionSource<Object> LoadingTcs;
            public bool ReleaseWhenLoaded;
        }

        private readonly Dictionary<string, AssetInfo> _loadedAssets = new Dictionary<string, AssetInfo>();

        public override async UniTask<T> LoadAssetAsync<T>(string path, string packageName = null)
        {
            if (_loadedAssets.TryGetValue(path, out var info))
            {
                info.RefCount++;

                if (info.Asset != null)
                {
                    return info.Asset as T;
                }

                // 资源仍在加载中：等待同一个 in-flight 任务，避免重复 LoadAsync 导致重复 Add key 或引用计数错乱
                if (info.LoadingTcs != null)
                {
                    var asset = await info.LoadingTcs.Task;
                    return asset as T;
                }

                return null;
            }

            // 首次请求：创建占位条目，用于合并并发加载
            var newInfo = new AssetInfo
            {
                Asset = null,
                RefCount = 1,
                LoadingTcs = new UniTaskCompletionSource<Object>(),
                ReleaseWhenLoaded = false
            };
            _loadedAssets.Add(path, newInfo);

            // UniTask 可以直接 await ResourceRequest
            try
            {
                ResourceRequest request = UnityEngine.Resources.LoadAsync<T>(path);
                await request;

                if (request.asset == null)
                {
                    Debug.LogError($"[ResourcesAssetManager] Failed to load asset: {path}");
                    _loadedAssets.Remove(path);
                    newInfo.LoadingTcs.TrySetResult(null);
                    return null;
                }

                newInfo.Asset = request.asset;
                newInfo.LoadingTcs.TrySetResult(newInfo.Asset);
                newInfo.LoadingTcs = null;

                // 如果加载期间所有引用都释放了，则加载完成后立刻清理
                if (newInfo.RefCount <= 0 || newInfo.ReleaseWhenLoaded)
                {
                    if (newInfo.Asset != null && !(newInfo.Asset is GameObject))
                    {
                        UnityEngine.Resources.UnloadAsset(newInfo.Asset);
                    }
                    _loadedAssets.Remove(path);
                }

                return newInfo.Asset as T;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourcesAssetManager] Exception loading asset {path}: {e.Message}");
                _loadedAssets.Remove(path);
                newInfo.LoadingTcs.TrySetResult(null);
                return null;
            }
        }

        public override void ReleaseAsset(string path, string packageName = null)
        {
            if (_loadedAssets.TryGetValue(path, out var info))
            {
                info.RefCount--;
                if (info.RefCount <= 0)
                {
                    // 仍在加载：无法取消 Resources.LoadAsync，只能标记加载完后释放
                    if (info.Asset == null)
                    {
                        info.RefCount = 0;
                        info.ReleaseWhenLoaded = true;
                        return;
                    }

                    // Resources.UnloadAsset 只能卸载非 GameObject 资源 (如 Texture, Mesh)
                    // 对于 GameObject，通常依赖 Resources.UnloadUnusedAssets()
                    // 这里我们只做简单的引用计数管理，真正的内存释放可能需要手动调用 UnloadUnusedAssets
                    if (!(info.Asset is GameObject))
                    {
                        UnityEngine.Resources.UnloadAsset(info.Asset);
                    }
                    _loadedAssets.Remove(path);
                    // 注意：Resources 模式下，GameObject 的卸载比较被动
                }
            }
        }

        public override void ClearUnused()
        {
            // 清理对象池中的对象，确保引用计数正确下降
            ClearPool();

            // 移除引用计数 <= 0 的资源
            var toRemove = new List<string>();
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

            foreach (var key in toRemove)
            {
                _loadedAssets.Remove(key);
            }

            UnityEngine.Resources.UnloadUnusedAssets();
        }
    }
}
