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
    /// 资源管理接口
    /// </summary>
    public interface IAssetManager
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        UniTask<T> LoadAssetAsync<T>(string path) where T : Object;

        /// <summary>
        /// 预加载资源（只加载不实例化），用于提前占用引用计数，避免进入界面时卡顿。
        /// 调用后需要在合适时机使用 ReleaseAsset/ReleaseInstance 释放引用。
        /// </summary>
        UniTask PreloadAssetAsync<T>(string path) where T : Object;

        /// <summary>
        /// 预加载 UI/Prefab (GameObject)。
        /// </summary>
        UniTask PreloadGameObjectAsync(string path);

        /// <summary>
        /// 批量预加载 UI/Prefab (GameObject)。
        /// </summary>
        UniTask PreloadGameObjectsAsync(IEnumerable<string> paths);

        /// <summary>
        /// 异步加载并实例化 GameObject
        /// </summary>
        UniTask<GameObject> InstantiateAsync(string path, Transform parent = null, bool worldPositionStays = false);

        /// <summary>
        /// 从对象池获取或实例化对象
        /// </summary>
        UniTask<GameObject> SpawnAsync(string path, Transform parent = null);

        /// <summary>
        /// 将对象回收至对象池
        /// </summary>
        void Despawn(GameObject instance);

        /// <summary>
        /// 释放资源引用
        /// </summary>
        void ReleaseAsset(string path);

        /// <summary>
        /// 销毁实例并释放对应的资源引用
        /// </summary>
        void ReleaseInstance(GameObject instance);

        /// <summary>
        /// 清理所有未使用的资源
        /// </summary>
        void ClearUnused();

        /// <summary>
        /// 清理对象池
        /// </summary>
        void ClearPool();
    }

    /// <summary>
    /// 资源管理器基类，实现了通用的对象池逻辑
    /// </summary>
    public abstract class AssetManagerBase : IAssetManager
    {
        protected readonly Dictionary<int, string> _instancePathMap = new Dictionary<int, string>();
        protected readonly Dictionary<string, Stack<GameObject>> _pools = new Dictionary<string, Stack<GameObject>>();
        protected Transform _poolRoot;

        public AssetManagerBase()
        {
            GameObject go = new GameObject("AssetPoolRoot");
            Object.DontDestroyOnLoad(go);
            _poolRoot = go.transform;
        }

        public abstract UniTask<T> LoadAssetAsync<T>(string path) where T : Object;
        public abstract void ReleaseAsset(string path);
        public abstract void ClearUnused();

        public virtual async UniTask PreloadAssetAsync<T>(string path) where T : Object
        {
            await LoadAssetAsync<T>(path);
        }

        public virtual async UniTask PreloadGameObjectAsync(string path)
        {
            await LoadAssetAsync<GameObject>(path);
        }

        public virtual async UniTask PreloadGameObjectsAsync(IEnumerable<string> paths)
        {
            if (paths == null) return;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                await LoadAssetAsync<GameObject>(path);
            }
        }

        public virtual async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null, bool worldPositionStays = false)
        {
            GameObject prefab = await LoadAssetAsync<GameObject>(path);
            if (prefab == null) return null;

            GameObject instance;
            if (parent != null)
            {
                instance = Object.Instantiate(prefab, parent, worldPositionStays);
            }
            else
            {
                instance = Object.Instantiate(prefab);
            }
            _instancePathMap[instance.GetInstanceID()] = path;
            return instance;
        }

        public virtual void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (_instancePathMap.TryGetValue(id, out string path))
            {
                ReleaseAsset(path);
                _instancePathMap.Remove(id);
            }
            Object.Destroy(instance);
        }

        public async UniTask<GameObject> SpawnAsync(string path, Transform parent = null)
        {
            if (_pools.TryGetValue(path, out var stack) && stack.Count > 0)
            {
                // 查找有效对象（防止外部意外销毁）
                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    if (go != null)
                    {
                        go.transform.SetParent(parent);
                        go.SetActive(true);
                        return go;
                    }
                }
            }
            return await InstantiateAsync(path, parent);
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (!_instancePathMap.TryGetValue(id, out string path))
            {
                Debug.LogWarning($"[AssetManager] Despawn failed: Instance {instance.name} not managed by AssetManager.");
                Object.Destroy(instance);
                return;
            }

            if (!_pools.TryGetValue(path, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[path] = stack;
            }

            instance.SetActive(false);
            instance.transform.SetParent(_poolRoot);
            stack.Push(instance);
        }

        public void ClearPool()
        {
             foreach (var kvp in _pools)
             {
                 while(kvp.Value.Count > 0)
                 {
                     var go = kvp.Value.Pop();
                     if(go != null)
                     {
                         // 需要正确释放实例以减少引用计数
                         ReleaseInstance(go);
                     }
                 }
             }
             _pools.Clear();
        }
    }

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

        public override async UniTask<T> LoadAssetAsync<T>(string path)
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

        public override void ReleaseAsset(string path)
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

        public override async UniTask<T> LoadAssetAsync<T>(string key)
        {
            if (_loadedHandles.TryGetValue(key, out var info))
            {
                info.RefCount++;
                if (info.Handle.IsDone)
                {
                    return info.Handle.Result as T;
                }
                // UniTask 可以直接 await Handle
                await info.Handle;
                return info.Handle.Result as T;
            }

            // 开启新加载
            var handle = Addressables.LoadAssetAsync<T>(key);
            var newInfo = new HandleInfo { Handle = handle, RefCount = 1 };
            _loadedHandles.Add(key, newInfo);

            try
            {
                // UniTask 可以直接 await Handle
                T result = await handle;
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

        public override void ReleaseAsset(string key)
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
