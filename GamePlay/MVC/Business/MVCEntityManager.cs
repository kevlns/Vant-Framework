using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;

namespace Vant.MVC
{
    /// <summary>
    /// MVC Entity 管理器
    /// 负责实体创建、资源加载与实例回收
    /// </summary>
    public class MVCEntityManager
    {
        public static MVCEntityManager Instance { get; private set; }
        private readonly AppCore _appCore;

        /// <summary>
        /// Max cached entities per type. Set to 0 or less to disable caching.
        /// </summary>
        public int PoolCapacityPerType { get; set; } = 32;

        private readonly Dictionary<Type, Stack<object>> _entityPool = new Dictionary<Type, Stack<object>>();
        private readonly Dictionary<IMVCEntity, EntityRecord> _activeEntities = new Dictionary<IMVCEntity, EntityRecord>();

        private sealed class EntityRecord
        {
            public GameObject ViewObject;
            public string PrefabPath;
            public bool UseFallback;
        }

        public MVCEntityManager(AppCore appCore)
        {
            Instance = this;
            _appCore = appCore;
        }

        /// <summary>
        /// 创建一个 MVC 实体实例
        /// </summary>
        public async UniTask<TEntity> CreateEntityAsync<TEntity>(
            bool showOnInit = true,
            Transform parent = null,
            CancellationToken token = default)
            where TEntity : class, IMVCEntity
        {
            if (token.IsCancellationRequested) return default;

            if (!TryGetEntityGenericArgs(typeof(TEntity), out var controllerType, out var viewType, out var viewModelType))
            {
                throw new InvalidOperationException($"[MVCEntityManager] {typeof(TEntity).Name} must inherit AbstractMVCEntityBase<TController, TView, TViewModel>.");
            }

            // 1) 先创建 View 以获取配置
            var view = Activator.CreateInstance(viewType) as AbstractGeneralViewBase;
            if (view == null) throw new InvalidOperationException("[MVCEntityManager] View instance creation failed.");
            var config = view.RegisterConfig;

            // 2) 加载并实例化视图预制体
            GameObject viewObject = null;
            bool useFallback = false;
            if (config != null && !string.IsNullOrEmpty(config.PrefabPath))
            {
                viewObject = await _appCore.MainAssetManager.SpawnAsync(config.PrefabPath, parent);
                if (viewObject == null && _appCore.FallbackAssetManager != null)
                {
                    viewObject = await _appCore.FallbackAssetManager.SpawnAsync(config.PrefabPath, parent);
                    useFallback = viewObject != null;
                }
            }

            if (token.IsCancellationRequested)
            {
                if (viewObject != null)
                {
                    if (useFallback && _appCore.FallbackAssetManager != null)
                    {
                        _appCore.FallbackAssetManager.Despawn(viewObject);
                    }
                    else
                    {
                        _appCore.MainAssetManager.Despawn(viewObject);
                    }
                }
                return default;
            }

            // 3) 绑定视图对象
            view.BindViewObject(viewObject);

            // 4) 创建 Controller / ViewModel
            var controller = Activator.CreateInstance(controllerType);
            var viewModel = Activator.CreateInstance(viewModelType);

            // 5) 创建/复用 Entity
            var entity = GetFromPool<TEntity>();
            if (entity == null)
            {
                entity = CreateEntityInstance<TEntity>(controller, view, viewModel, showOnInit, token);
            }
            else
            {
                await entity.Reset(controller, view, viewModel, showOnInit, token);
            }

            if (entity != null)
            {
                _activeEntities[entity] = new EntityRecord
                {
                    ViewObject = viewObject,
                    PrefabPath = config?.PrefabPath,
                    UseFallback = useFallback
                };
            }

            return entity;
        }

        /// <summary>
        /// 回收实体实例（对象池缓存）
        /// </summary>
        public void RecycleEntity<TController, TView, TViewModel>(
            AbstractMVCEntityBase<TController, TView, TViewModel> entity,
            bool cache = true)
            where TController : AbstractControllerBase<TView, TViewModel>
            where TView : AbstractGeneralViewBase
            where TViewModel : class, IViewModel
        {
            if (entity == null) return;

            RecycleEntityInternal(entity, cache);
        }

        /// <summary>
        /// 一次性创建多个相同类型实体
        /// </summary>
        public async UniTask<List<TEntity>> CreateEntitiesAsync<TEntity>(
            int count,
            bool showOnInit = true,
            Transform parent = null,
            CancellationToken token = default)
            where TEntity : class, IMVCEntity
        {
            var result = new List<TEntity>();
            if (count <= 0 || token.IsCancellationRequested) return result;

            for (int i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) break;
                var entity = await CreateEntityAsync<TEntity>(showOnInit, parent, token);
                if (entity != null) result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// 预加载指定 View 的预制体资源
        /// </summary>
        public async UniTask PreloadViewAsync<TView>() where TView : AbstractGeneralViewBase, new()
        {
            var view = new TView();
            var config = view.RegisterConfig;
            if (config == null || string.IsNullOrEmpty(config.PrefabPath)) return;

            await _appCore.MainAssetManager.PreloadAssetAsync<GameObject>(config.PrefabPath);
            if (_appCore.FallbackAssetManager != null)
            {
                await _appCore.FallbackAssetManager.PreloadAssetAsync<GameObject>(config.PrefabPath);
            }
        }

        /// <summary>
        /// 回收所有活动实体
        /// </summary>
        public void RecycleAll(bool cache = true)
        {
            if (_activeEntities.Count == 0) return;

            var entities = new List<IMVCEntity>(_activeEntities.Keys);
            foreach (var entity in entities)
            {
                RecycleEntityInternal(entity, cache);
            }
        }

        /// <summary>
        /// 清理对象池
        /// </summary>
        public void ClearPool()
        {
            _entityPool.Clear();
        }

        private TEntity GetFromPool<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);
            if (_entityPool.TryGetValue(type, out var stack) && stack.Count > 0)
            {
                return stack.Pop() as TEntity;
            }

            return null;
        }

        private void RecycleEntityInternal(IMVCEntity entity, bool cache)
        {
            if (entity == null) return;

            if (_activeEntities.TryGetValue(entity, out var record))
            {
                _activeEntities.Remove(entity);
            }

            var entityType = entity.GetType();
            bool shouldCache = cache && PoolCapacityPerType > 0 && GetPoolCount(entityType) < PoolCapacityPerType;

            if (record?.ViewObject != null)
            {
                if (shouldCache)
                {
                    if (record.UseFallback && _appCore.FallbackAssetManager != null)
                    {
                        _appCore.FallbackAssetManager.Despawn(record.ViewObject);
                    }
                    else
                    {
                        _appCore.MainAssetManager.Despawn(record.ViewObject);
                    }
                }
                else
                {
                    if (record.UseFallback && _appCore.FallbackAssetManager != null)
                    {
                        _appCore.FallbackAssetManager.ReleaseInstance(record.ViewObject);
                    }
                    else
                    {
                        _appCore.MainAssetManager.ReleaseInstance(record.ViewObject);
                    }
                }
            }

            entity.Destroy();

            if (shouldCache)
            {
                if (!_entityPool.TryGetValue(entityType, out var stack))
                {
                    stack = new Stack<object>();
                    _entityPool[entityType] = stack;
                }
                stack.Push(entity);
            }
        }

        private int GetPoolCount(Type entityType)
        {
            if (_entityPool.TryGetValue(entityType, out var stack))
            {
                return stack.Count;
            }

            return 0;
        }

        private static bool TryGetEntityGenericArgs(Type entityType, out Type controllerType, out Type viewType, out Type viewModelType)
        {
            controllerType = null;
            viewType = null;
            viewModelType = null;

            var current = entityType;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractMVCEntityBase<,,>))
                {
                    var args = current.GetGenericArguments();
                    controllerType = args[0];
                    viewType = args[1];
                    viewModelType = args[2];
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        private static TEntity CreateEntityInstance<TEntity>(object controller, AbstractGeneralViewBase view, object viewModel, bool showOnInit, CancellationToken token)
            where TEntity : class, IMVCEntity
        {
            var entityType = typeof(TEntity);

            var ctorWithToken = entityType.GetConstructor(new[]
            {
                controller.GetType(),
                view.GetType(),
                viewModel.GetType(),
                typeof(bool),
                typeof(CancellationToken)
            });

            if (ctorWithToken != null)
            {
                return (TEntity)ctorWithToken.Invoke(new object[] { controller, view, viewModel, showOnInit, token });
            }

            var ctor = entityType.GetConstructor(new[]
            {
                controller.GetType(),
                view.GetType(),
                viewModel.GetType(),
                typeof(bool)
            });

            if (ctor != null)
            {
                return (TEntity)ctor.Invoke(new object[] { controller, view, viewModel, showOnInit });
            }

            throw new MissingMethodException(entityType.Name, ".ctor");
        }

    }
}
