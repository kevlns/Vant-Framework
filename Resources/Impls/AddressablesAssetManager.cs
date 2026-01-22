using System;
using System.Collections.Generic;
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
        private class HandleInfo
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        private readonly Dictionary<string, HandleInfo> _loadedHandles = new Dictionary<string, HandleInfo>();

        public override async UniTask<T> LoadAssetAsync<T>(string key, string packageName = null)
        {
            if (_loadedHandles.TryGetValue(key, out var info))
            {
                info.RefCount++;
                if (info.Handle.IsDone)
                {
                    return info.Handle.Result as T;
                }
                // UniTask 可以直接 await Handle
                await AddressablesAsyncExtensions.ToUniTask(info.Handle);
                return info.Handle.Result as T;
            }

            // 开启新加载
            var handle = Addressables.LoadAssetAsync<T>(key);
            var newInfo = new HandleInfo { Handle = handle, RefCount = 1 };
            _loadedHandles.Add(key, newInfo);

            try
            {
                // UniTask 可以直接 await Handle
                T result = await AddressablesAsyncExtensions.ToUniTask(handle);
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return result;
                }
                else
                {
                    Debug.LogError($"[AddressablesManager] Failed to load asset: {key}");
                    _loadedHandles.Remove(key);
                    Addressables.Release(handle);
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesManager] Exception loading asset {key}: {e.Message}");
                _loadedHandles.Remove(key);
                // 异常情况下也要尝试释放 handle
                Addressables.Release(handle);
                return null;
            }
        }

        public override void ReleaseAsset(string key, string packageName = null)
        {
            if (_loadedHandles.TryGetValue(key, out var info))
            {
                info.RefCount--;
                if (info.RefCount <= 0)
                {
                    if (info.Handle.IsValid())
                    {
                        Addressables.Release(info.Handle);
                    }
                    _loadedHandles.Remove(key);
                }
            }
        }

        public override void ClearUnused()
        {
            // 清理对象池中的对象，确保引用计数正确下降
            ClearPool();

            // Addressables 的引用计数机制通常能自动处理，
            // 这里可以用来强制清理那些 RefCount <= 0 但可能因为逻辑漏洞没被移除的 Handle
            var toRemove = new List<string>();
            foreach (var kvp in _loadedHandles)
            {
                if (kvp.Value.RefCount <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                ReleaseAsset(key);
            }
        }
    }
}
