using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
        UniTask<T> LoadAssetAsync<T>(string path, string packageName = null) where T : Object;

        /// <summary>
        /// 预加载资源（只加载不实例化），用于提前占用引用计数，避免进入界面时卡顿。
        /// 调用后需要在合适时机使用 ReleaseAsset/ReleaseInstance 释放引用。
        /// </summary>
        UniTask PreloadAssetAsync<T>(string path, string packageName = null) where T : Object;

        /// <summary>
        /// 预加载资源（只加载不实例化），用于提前占用引用计数，避免进入界面时卡顿。
        /// 调用后需要在合适时机使用 ReleaseAsset/ReleaseInstance 释放引用。
        /// </summary>
        UniTask PreloadAssetsAsync<T>(IEnumerable<string> paths, string packageName = null) where T : Object;

        /// <summary>
        /// 异步加载并实例化 GameObject
        /// </summary>
        UniTask<GameObject> InstantiateAsync(string path, Transform parent = null, bool worldPositionStays = false, string packageName = null);

        /// <summary>
        /// 从对象池获取或实例化对象
        /// </summary>
        UniTask<GameObject> SpawnAsync(string path, Transform parent = null, string packageName = null);

        /// <summary>
        /// 将对象回收至对象池
        /// </summary>
        void Despawn(GameObject instance);

        /// <summary>
        /// 释放资源引用
        /// </summary>
        void ReleaseAsset(string path, string packageName = null);

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
        protected struct AssetRef
        {
            public string Path;
            public string PackageName;
        }

        protected readonly Dictionary<int, AssetRef> _instancePathMap = new Dictionary<int, AssetRef>();
        protected readonly Dictionary<string, Stack<GameObject>> _pools = new Dictionary<string, Stack<GameObject>>();
        protected Transform _poolRoot;

        public AssetManagerBase()
        {
            GameObject go = new GameObject("AssetPoolRoot");
            Object.DontDestroyOnLoad(go);
            _poolRoot = go.transform;
        }

        public abstract UniTask<T> LoadAssetAsync<T>(string path, string packageName = null) where T : Object;
        public abstract void ReleaseAsset(string path, string packageName = null);
        public abstract void ClearUnused();

        public virtual async UniTask PreloadAssetAsync<T>(string path, string packageName = null) where T : Object
        {
            await LoadAssetAsync<T>(path, packageName);
        }

        public virtual async UniTask PreloadAssetsAsync<T>(IEnumerable<string> paths, string packageName = null) where T : Object
        {
            if (paths == null) return;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                await PreloadAssetAsync<T>(path, packageName);
            }
        }

        public virtual async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null, bool worldPositionStays = false, string packageName = null)
        {
            GameObject prefab = await LoadAssetAsync<GameObject>(path, packageName);
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
            _instancePathMap[instance.GetInstanceID()] = new AssetRef { Path = path, PackageName = packageName };
            return instance;
        }

        public virtual void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (_instancePathMap.TryGetValue(id, out AssetRef refData))
            {
                ReleaseAsset(refData.Path, refData.PackageName);
                _instancePathMap.Remove(id);
            }
            Object.Destroy(instance);
        }

        public async UniTask<GameObject> SpawnAsync(string path, Transform parent = null, string packageName = null)
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
            return await InstantiateAsync(path, parent, false, packageName);
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (!_instancePathMap.TryGetValue(id, out AssetRef refData))
            {
                Debug.LogWarning($"[AssetManager] Despawn failed: Instance {instance.name} not managed by AssetManager.");
                Object.Destroy(instance);
                return;
            }

            if (!_pools.TryGetValue(refData.Path, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[refData.Path] = stack;
            }

            instance.SetActive(false);
            instance.transform.SetParent(_poolRoot);
            stack.Push(instance);
        }

        public void ClearPool()
        {
            foreach (var kvp in _pools)
            {
                while (kvp.Value.Count > 0)
                {
                    var go = kvp.Value.Pop();
                    if (go != null)
                    {
                        // 需要正确释放实例以减少引用计数
                        ReleaseInstance(go);
                    }
                }
            }
            _pools.Clear();
        }
    }
}
