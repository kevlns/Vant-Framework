using System;
using System.Collections.Generic;
using Vant.Core;

namespace Vant.MVC
{
    /// <summary>
    /// Model 管理器
    /// 负责 Model 的创建、获取和销毁
    /// </summary>
    public class ModelManager
    {
        private AppCore _appCore;

        public ModelManager(AppCore appCore)
        {
            _appCore = appCore;
        }

        private readonly Dictionary<Type, AbstractModelBase> _models = new Dictionary<Type, AbstractModelBase>();
        /// <summary>
        /// 获取已注册的 Model 实例，如果未注册则返回 null
        /// </summary>
        /// <typeparam name="T">Model 类型</typeparam>
        /// <returns>Model 实例或 null</returns>
        public T Get<T>() where T : AbstractModelBase
        {
            Type type = typeof(T);
            if (_models.TryGetValue(type, out var model))
            {
                return (T)model;
            }

            return null;
        }

        /// <summary>
        /// 注册一个 Model 实例（如果已存在会覆盖）
        /// </summary>
        /// <param name="model">Model 实例</param>
        public void Register(AbstractModelBase model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Type type = model.GetType();
            // 如果已有实例，先 Dispose
            if (_models.TryGetValue(type, out var existing))
            {
                existing.Dispose();
            }

            model.PreInit(_appCore, this);
            _models[type] = model;
        }

        /// <summary>
        /// 批量注册 Model 实例（如果已存在会覆盖）
        /// </summary>
        public void RegisterModels(IEnumerable<AbstractModelBase> models)
        {
            if (models == null) throw new ArgumentNullException(nameof(models));

            foreach (var model in models)
            {
                Register(model);
            }
        }

        /// <summary>
        /// 批量注册 Model 实例（params 便捷写法）
        /// </summary>
        public void RegisterModels(params AbstractModelBase[] models)
        {
            RegisterModels((IEnumerable<AbstractModelBase>)models);
        }

        /// <summary>
        /// 注销并销毁指定类型的 Model
        /// </summary>
        /// <typeparam name="T">Model 类型</typeparam>
        public void Unregister<T>() where T : AbstractModelBase
        {
            Type type = typeof(T);
            if (_models.TryGetValue(type, out var model))
            {
                model.Dispose();
                _models.Remove(type);
            }
        }
    }
}
