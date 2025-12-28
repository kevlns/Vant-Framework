using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Vant.Resources;
using Vant.Core;
using Vant.MVC;
using Object = UnityEngine.Object;
using Vant.UI.UIComponents;

namespace Vant.UI.UIFramework
{
    /// <summary>
    /// UI 管理器
    /// 负责 UI 的堆栈管理、层级管理、资源加载与缓存
    /// </summary>
    public class UIManager
    {
        private AppCore _appCore;

        // 资源管理器引用
        private IAssetManager _assetManager;
        // 备用资源管理器
        private IAssetManager _fallbackAssetManager;

        // UI 根节点
        private Transform _uiRoot;

        // 各层级节点
        private Dictionary<UILayer, Transform> _layerRoots;

        // 缓存池 (LRU)
        private LRUCache<string, AbstractUIBase> _uiCache;

        // UI 栈 (用于管理 Back 逻辑)
        // 使用 List 代替 Stack 以支持任意位置移除 (优化 "提至栈顶" 的性能)
        private readonly List<AbstractUIBase> _uiStack = new List<AbstractUIBase>();

        // 当前所有活动的 UI (包括在栈里的和不在栈里的)
        private readonly Dictionary<string, AbstractUIBase> _activeUIs = new Dictionary<string, AbstractUIBase>();

        // 当前已创建的 UI 实例集合（包含多实例 UI；缓存中的 UI 也可能在此集合中）
        private readonly HashSet<AbstractUIBase> _uiInstances = new HashSet<AbstractUIBase>();

        // 正在加载中的 UI (防止重复加载)
        private readonly HashSet<string> _loadingSet = new HashSet<string>();

        // 全屏遮罩 (用于屏蔽操作)
        private GameObject _maskGo;

        // UI 配置注册表 (UI Name -> UIConfig)
        private readonly Dictionary<string, UIConfig> _uiConfigRegistry = new Dictionary<string, UIConfig>();

        // 缓存层 (用于存放缓存中的 UI)
        private Transform _cacheLayerRoot;

        // 记录 UI 原始所属的层级 (用于从缓存恢复时)
        private readonly Dictionary<AbstractUIBase, UILayer> _uiOriginalLayerMap = new Dictionary<AbstractUIBase, UILayer>();

        public UIManager(AppCore appCore, IAssetManager assetManager, IAssetManager fallbackAssetManager = null)
        {
            _appCore = appCore;
            _assetManager = assetManager;
            _fallbackAssetManager = fallbackAssetManager;
        }

        /// <summary>
        /// 注册 UI 配置
        /// </summary>
        public void RegisterUI(UIConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[UIManager] RegisterUI called with null config.");
                return;
            }

            if (string.IsNullOrEmpty(config.Name))
            {
                Debug.LogError("[UIManager] RegisterUI called with empty config.Name.");
                return;
            }

            if (_uiConfigRegistry.ContainsKey(config.Name))
            {
                Debug.LogWarning($"[UIManager] UI {config.Name} already registered. Overwriting.");
            }
            _uiConfigRegistry[config.Name] = config;
        }

        /// <summary>
        /// 批量注册 UI 配置
        /// </summary>
        public void RegisterUIs(IEnumerable<UIConfig> configs)
        {
            if (configs == null)
            {
                Debug.LogError("[UIManager] RegisterUIs called with null configs.");
                return;
            }

            foreach (var config in configs)
            {
                RegisterUI(config);
            }
        }

        /// <summary>
        /// 批量注册 UI 配置 (params 便捷写法)
        /// </summary>
        public void RegisterUIs(params UIConfig[] configs)
        {
            RegisterUIs((IEnumerable<UIConfig>)configs);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="uiRoot">UI 根节点 (Canvas)</param>
        public void Init(Transform uiRoot)
        {
            _uiRoot = uiRoot;

            // 注册事件监听
            _appCore.Notifier.AddListener<string, object>(UICommonEvent.OPEN_UI, OnOpenUIEvent);
            _appCore.Notifier.AddListener<string>(UICommonEvent.CLOSE_UI, OnCloseUIEvent);

            // 初始化层级
            _layerRoots = new Dictionary<UILayer, Transform>();
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                GameObject layerGo = new GameObject(layer.ToString(), typeof(RectTransform));
                layerGo.transform.SetParent(_uiRoot, false);

                // 必须添加 RectTransform 才能正确布局
                RectTransform rect = layerGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                // 添加 Canvas 以便控制层级排序
                Canvas canvas = layerGo.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = (int)layer;

                layerGo.AddComponent<GraphicRaycaster>();

                _layerRoots[layer] = layerGo.transform;
            }

            // 初始化缓存层 (隐藏的层级，用于存放缓存中的 UI)
            CreateCacheLayer();

            // 初始化全屏遮罩 (放在 System 层)
            CreateMask();

            // 初始化缓存
            _uiCache = new LRUCache<string, AbstractUIBase>(AppCore.GlobalSettings.UI_LRU_MAX_SIZE, OnCacheRemove);
        }

        private void CreateCacheLayer()
        {
            GameObject cacheGo = new GameObject("UICache", typeof(RectTransform));
            cacheGo.transform.SetParent(_uiRoot, false);

            RectTransform rect = cacheGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            cacheGo.SetActive(false); // 缓存层隐藏
            _cacheLayerRoot = cacheGo.transform;
        }

        private void CreateMask()
        {
            // 创建全屏遮罩，初始隐藏
            // 它的层级和父节点会根据当前打开的 UI 动态调整
            _maskGo = new GameObject("UIManager_Mask");
            // 初始挂在 UI Root 下，但隐藏
            _maskGo.transform.SetParent(_uiRoot, false);
            
            var rect = _maskGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = _maskGo.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 透明
            img.raycastTarget = true;

            _maskGo.SetActive(false);
        }

        /// <summary>
        /// 刷新遮罩状态
        /// 将遮罩移动到最上层且需要遮罩的活动 UI 的下方，屏蔽该层级下方的所有交互
        /// </summary>
        private void RefreshMaskState()
        {
            if (_maskGo == null) return;

            AbstractUIBase topUI = null;
            int maxLayer = int.MinValue;
            int maxSibling = int.MinValue;

            // 遍历所有活动 UI，找到层级最高、同层级中最靠上的那个，且 NeedMask = true
            foreach (var ui in _activeUIs.Values)
            {
                if (ui == null || ui.gameObject == null || !ui.gameObject.activeInHierarchy) continue;
                
                // 忽略不需要遮罩的 UI (如 Overlay 类型的 HUD、Toast 等)
                if (!ui.Config.NeedMask) continue;

                int layer = (int)ui.Config.Layer;
                int sibling = ui.transform.GetSiblingIndex();

                if (layer > maxLayer || (layer == maxLayer && sibling > maxSibling))
                {
                    maxLayer = layer;
                    maxSibling = sibling;
                    topUI = ui;
                }
            }

            if (topUI != null)
            {
                _maskGo.SetActive(true);
                // 移动到目标 UI 的同一父节点下
                if (_maskGo.transform.parent != topUI.transform.parent)
                {
                    _maskGo.transform.SetParent(topUI.transform.parent, false);
                }
                
                // 设置 sibling index 为目标 UI 的 index
                // 这样 mask 就在 topUI 的后面 (渲染顺序上)，挡住 topUI 之前的所有物体
                _maskGo.transform.SetSiblingIndex(topUI.transform.GetSiblingIndex());
            }
            else
            {
                _maskGo.SetActive(false);
            }
        }

        /// <summary>
        /// 打开 UI (泛型版本)
        /// </summary>
        private async UniTask<T> Open<T>(UIConfig config, object args = null) where T : AbstractUIBase, new()
        {
            if (config.UIClass == null) config.UIClass = typeof(T);
            var ui = await Open(config, args);
            return ui as T;
        }

        /// <summary>
        /// 打开 UI (核心逻辑)
        /// </summary>
        private async UniTask<AbstractUIBase> Open(UIConfig config, object args = null)
        {
            string assetPath = config.AssetPath;

            // 1. 防止重复加载
            if (_loadingSet.Contains(assetPath))
            {
                Debug.LogWarning($"[UIManager] UI {assetPath} is loading...");
                return null;
            }

            // 2. 检查是否已经打开 (且不支持多实例)
            if (!config.AllowMultiInstance && _activeUIs.TryGetValue(assetPath, out var activeUI))
            {
                // 如果已经在栈中，需要将其提取到栈顶
                if (_uiStack.Contains(activeUI))
                {
                    // 1. 从栈中移除 (List 优化：O(N) 且无额外 GC)
                    _uiStack.Remove(activeUI);

                    // 2. 重新压入栈顶并处理遮挡逻辑
                    ProcessStackOnOpen(activeUI);
                }

                // 3. 确保显示
                await activeUI.InternalOpen(args);

                _uiInstances.Add(activeUI);

                return activeUI;
            }

            _loadingSet.Add(assetPath);

            AbstractUIBase uiInstance = null;

            try
            {
                // 3. 获取实例 (缓存 -> 加载)
                uiInstance = _uiCache.Get(assetPath);

                if (uiInstance != null)
                {
                    // 从缓存恢复：还原到原始层级
                    RestoreFromCache(uiInstance);
                }
                else
                {
                    // 缓存未命中：通过资源管理器实例化（建立 instance->path 映射，便于正确 ReleaseInstance 释放引用计数）
                    Transform targetLayerRoot = null;
                    _layerRoots.TryGetValue(config.Layer, out targetLayerRoot);

                    GameObject go = null;

                    // 1) 主资源管理器优先
                    try
                    {
                        go = await _assetManager.InstantiateAsync(assetPath, targetLayerRoot, false);
                    }
                    catch (Exception)
                    {
                        // Ignore, try fallback
                    }

                    // 2) 备用资源管理器兜底
                    if (go == null && _fallbackAssetManager != null)
                    {
                        try
                        {
                            go = await _fallbackAssetManager.InstantiateAsync(assetPath, targetLayerRoot, false);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[UIManager] Fallback instantiate failed for {assetPath}: {ex}");
                        }
                    }

                    if (go == null)
                    {
                        Debug.LogError($"[UIManager] Failed to instantiate UI prefab: {assetPath}");
                        return null;
                    }

                    // 修复实例化后的缩放和位置问题
                    // (已移动到下方统一处理，确保缓存 UI 也能被重置)
                    /*
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.localScale = Vector3.one;
                        rt.localPosition = Vector3.zero;
                        rt.anchoredPosition3D = Vector3.zero;

                        // 如果是全屏拉伸布局，确保 offset 也归零 (防止某些情况下实例化产生的微小误差)
                        if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
                        {
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                        }
                    }
                    */

                    // 如果预制体根节点带有 Canvas 但没有 GraphicRaycaster，会导致吞掉事件但不响应点击
                    // 为了兼容旧资源，这里自动补一个 GraphicRaycaster
                    var rootCanvas = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
                    if (go.GetComponent<GraphicRaycaster>() == null)
                    {
                        go.AddComponent<GraphicRaycaster>();
                        Debug.LogWarning($"[UIManager] Prefab {assetPath} has Canvas but no GraphicRaycaster. Added GraphicRaycaster automatically.");
                    }

                    // 获取 ReferenceContainerGenerator
                    var skin = go.GetComponent<ReferenceContainerGenerator>();
                    if (skin == null)
                    {
                        Debug.LogWarning($"[UIManager] Prefab {assetPath} missing ReferenceContainerGenerator. Creating UI without skin references.");
                    }

                    // 创建纯 C# UI 实例
                    if (config.UIClass == null)
                    {
                        Debug.LogError($"[UIManager] UI Config {config.Name} missing UIClass!");
                        Object.Destroy(go);
                        return null;
                    }

                    uiInstance = Activator.CreateInstance(config.UIClass) as AbstractUIBase;

                    // 初始化 (注入 GameObject 和 AppCore)
                    uiInstance.InternalInit(config, go.GetInstanceID(), go, _appCore);
                    uiInstance.UIManager = this;
                }

                _uiInstances.Add(uiInstance);

                // 4. 记录原始层级并设置父节点 (层级)
                _uiOriginalLayerMap[uiInstance] = config.Layer;
                if (_layerRoots.TryGetValue(config.Layer, out var layerRoot))
                {
                    uiInstance.transform.SetParent(layerRoot, false);
                    uiInstance.transform.SetAsLastSibling();
                }

                // 5. 确保 RectTransform 属性正确 (修复 WebGL 实例化缩放问题，以及重置缓存 UI 的状态)
                var rt = uiInstance.transform as RectTransform;
                if (rt != null)
                {
                    rt.localScale = Vector3.one;
                    rt.localPosition = Vector3.zero;
                    rt.anchoredPosition3D = Vector3.zero;

                    if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
                    {
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                }

                // 6. 处理 UI 栈逻辑
                ProcessStackOnOpen(uiInstance);

                // 7. 记录活动 UI
                if (!config.AllowMultiInstance)
                {
                    _activeUIs[assetPath] = uiInstance;
                }

                // 7. 刷新遮罩状态 (在打开动画前，确保遮罩到位)
                RefreshMaskState();

                // 8. 执行打开流程
                await uiInstance.InternalOpen(args);

                return uiInstance;
            }
            finally
            {
                _loadingSet.Remove(assetPath);
            }
        }

        /// <summary>
        /// 关闭 UI
        /// </summary>
        private async UniTask Close(AbstractUIBase ui)
        {
            if (ui == null) return;

            // 1. 执行关闭流程
            await ui.InternalClose();

            // 2. 处理 UI 栈逻辑
            ProcessStackOnClose(ui);

            // 3. 移除活动记录
            if (!ui.Config.AllowMultiInstance)
            {
                _activeUIs.Remove(ui.Config.AssetPath);
            }

            // 刷新遮罩
            RefreshMaskState();

            // 4. 放入缓存或销毁
            // 单实例 UI：按配置参与 LRU 缓存
            // 多实例 UI：只允许一份进缓存，其余直接销毁，避免短时间连点堆积大量缓存对象
            if (AppCore.GlobalSettings.UI_LRU_MAX_SIZE > 0 && ui.Config.IsCacheable)
            {
                if (!ui.Config.AllowMultiInstance)
                {
                    // 非多实例：正常放入缓存池（LRU 满了会触发 OnCacheRemove -> DestroyUI）
                    MoveToCache(ui);
                    _uiCache.Put(ui.Config.AssetPath, ui);
                }
                else
                {
                    // 多实例：仅当缓存中不存在该 AssetPath 时才缓存一份
                    var cached = _uiCache.Get(ui.Config.AssetPath);
                    if (cached == null || cached == ui)
                    {
                        MoveToCache(ui);
                        _uiCache.Put(ui.Config.AssetPath, ui);
                    }
                    else
                    {
                        // 已经有一份缓存，当前实例直接销毁
                        DestroyUI(ui);
                        // 将之前的缓存放回去，维持缓存不变
                        _uiCache.Put(ui.Config.AssetPath, cached);
                    }
                }
            }
            else
            {
                DestroyUI(ui);
            }
        }

        // 实例级关闭，供 AbstractUIBase.CloseSelfAsync 调用
        internal UniTask CloseInstanceAsync(AbstractUIBase ui)
        {
            return Close(ui);
        }

        /// <summary>
        /// 关闭栈顶 UI (返回键逻辑)
        /// </summary>
        public async UniTask Back()
        {
            if (_uiStack.Count > 0)
            {
                var topUI = _uiStack[_uiStack.Count - 1];
                await Close(topUI);
            }
        }

        #region Internal Logic

        private void ProcessStackOnOpen(AbstractUIBase ui)
        {
            // 只有 Normal 层的 UI 才参与栈管理 (Pop 和 Top 层通常不影响底层逻辑)
            if (ui.Config.Layer != UILayer.Normal) return;

            if (ui.Config.Mode == UIMode.HideOther)
            {
                // 隐藏栈中其他 UI
                foreach (var otherUI in _uiStack)
                {
                    if (otherUI.IsVisible)
                    {
                        otherUI.gameObject.SetActive(false);
                    }
                }
            }
            else if (ui.Config.Mode == UIMode.Single)
            {
                // 关闭栈中所有其他 UI
                // 复制一份列表进行关闭，避免修改集合
                var toClose = _uiStack.ToArray();
                _uiStack.Clear();

                foreach (var otherUI in toClose)
                {
                    // 注意：这里不能 await，否则会阻塞 Open 流程。
                    Close(otherUI).Forget();
                }
            }

            _uiStack.Add(ui);
        }

        private void ProcessStackOnClose(AbstractUIBase ui)
        {
            if (ui.Config.Layer != UILayer.Normal) return;

            // 如果关闭的是栈顶 UI
            if (_uiStack.Count > 0 && _uiStack[_uiStack.Count - 1] == ui)
            {
                _uiStack.RemoveAt(_uiStack.Count - 1);

                // 恢复栈顶 UI 的显示 (如果之前被 HideOther 隐藏了)
                if (_uiStack.Count > 0)
                {
                    var nextUI = _uiStack[_uiStack.Count - 1];
                    if (!nextUI.IsVisible)
                    {
                        nextUI.gameObject.SetActive(true);
                        // 可选：调用 OnReopen 之类的钩子
                    }
                }
            }
            else
            {
                // 如果关闭的不是栈顶 UI，直接移除
                _uiStack.Remove(ui);
            }
        }

        private void OnCacheRemove(AbstractUIBase ui)
        {
            // 缓存溢出，销毁 UI
            _uiOriginalLayerMap.Remove(ui);
            DestroyUI(ui);
        }

        private void MoveToCache(AbstractUIBase ui)
        {
            // 将 UI 移动到缓存层
            if (_cacheLayerRoot != null)
            {
                ui.transform.SetParent(_cacheLayerRoot, false);
            }
        }

        private void RestoreFromCache(AbstractUIBase ui)
        {
            // 从缓存层恢复到原始层级
            if (_uiOriginalLayerMap.TryGetValue(ui, out var originalLayer))
            {
                if (_layerRoots.TryGetValue(originalLayer, out var layerRoot))
                {
                    ui.transform.SetParent(layerRoot, false);
                    ui.transform.SetAsLastSibling();
                }
            }
        }

        private void DestroyUI(AbstractUIBase ui)
        {
            if (ui != null)
            {
                _uiInstances.Remove(ui);
                var go = ui.gameObject;

                // 从原始层级映射中移除
                _uiOriginalLayerMap.Remove(ui);
                ui.InternalDestroy();

                // 尝试通过两个管理器释放实例
                // 注意：ReleaseInstance 内部通常会检查 ID 是否属于自己管理的资源
                // 如果不属于，通常会忽略或仅执行 Destroy
                // 为了安全，我们可以先尝试主管理器，再尝试备用管理器

                // 这里假设 AssetManager 的 ReleaseInstance 实现是安全的（即如果不属于它管理的实例，它不会报错）
                // 实际上我们在 AssetManagerBase 中实现了 _instancePathMap 检查，所以是安全的

                _assetManager.ReleaseInstance(go);
                if (_fallbackAssetManager != null)
                {
                    _fallbackAssetManager.ReleaseInstance(go);
                }
            }
        }

        #region Utility

        /// <summary>
        /// 获取指定 UIName 当前打开的一个实例（单实例 UI 返回唯一实例；多实例 UI 返回“最靠上”的一个实例）。
        /// 注意：缓存层（UICache）中的 UI 会被忽略。
        /// </summary>
        public AbstractUIBase GetUI(string uiName)
        {
            if (string.IsNullOrEmpty(uiName)) return null;

            if (!_uiConfigRegistry.TryGetValue(uiName, out var config) || config == null)
            {
                return null;
            }

            // 单实例：优先走 active 字典
            if (!config.AllowMultiInstance)
            {
                if (_activeUIs.TryGetValue(config.AssetPath, out var ui) && ui != null)
                {
                    if (_cacheLayerRoot != null && ui.transform != null && ui.transform.IsChildOf(_cacheLayerRoot))
                    {
                        return null;
                    }

                    return ui;
                }

                return null;
            }

            // 多实例：从实例集合里找“同层级最上”的那个
            AbstractUIBase top = null;
            int maxLayer = int.MinValue;
            int maxSibling = int.MinValue;

            foreach (var ui in _uiInstances)
            {
                if (ui == null || ui.Config == null) continue;
                if (ui.Config.AssetPath != config.AssetPath) continue;
                if (ui.transform == null) continue;

                if (_cacheLayerRoot != null && ui.transform.IsChildOf(_cacheLayerRoot))
                {
                    continue;
                }

                var layer = (int)ui.Config.Layer;
                var sibling = ui.transform.GetSiblingIndex();
                if (layer > maxLayer || (layer == maxLayer && sibling > maxSibling))
                {
                    maxLayer = layer;
                    maxSibling = sibling;
                    top = ui;
                }
            }

            return top;
        }

        /// <summary>
        /// 获取指定 UIName 当前打开的所有实例（主要用于多实例 UI）。
        /// 注意：缓存层（UICache）中的 UI 会被忽略。
        /// </summary>
        public List<AbstractUIBase> GetUIs(string uiName)
        {
            var result = new List<AbstractUIBase>();
            if (string.IsNullOrEmpty(uiName)) return result;

            if (!_uiConfigRegistry.TryGetValue(uiName, out var config) || config == null)
            {
                return result;
            }

            if (!config.AllowMultiInstance)
            {
                var single = GetUI(uiName);
                if (single != null) result.Add(single);
                return result;
            }

            foreach (var ui in _uiInstances)
            {
                if (ui == null || ui.Config == null) continue;
                if (ui.Config.AssetPath != config.AssetPath) continue;
                if (ui.transform == null) continue;

                if (_cacheLayerRoot != null && ui.transform.IsChildOf(_cacheLayerRoot))
                {
                    continue;
                }

                result.Add(ui);
            }

            return result;
        }

        public T GetUI<T>(string uiName) where T : AbstractUIBase
        {
            return GetUI(uiName) as T;
        }

        /// <summary>
        /// 关闭指定层级内的所有 UI（包含该层级的多实例 UI）。
        /// 注意：缓存层（UICache）中的 UI 不会被关闭（它们本就处于关闭态）。
        /// </summary>
        public void CloseAllInLayer(UILayer layer)
        {
            CloseAllInLayers(layer);
        }

        /// <summary>
        /// 关闭指定多个层级内的所有 UI（包含多实例 UI）。
        /// </summary>
        public void CloseAllInLayers(params UILayer[] layers)
        {
            if (layers == null || layers.Length == 0) return;

            var layerSet = new HashSet<UILayer>(layers);
            var snapshot = new List<AbstractUIBase>(_uiInstances);
            foreach (var ui in snapshot)
            {
                if (ui == null || ui.gameObject == null) continue;

                // 排除缓存层中的 UI（它们已关闭，仅用于复用）
                if (_cacheLayerRoot != null && ui.transform != null && ui.transform.IsChildOf(_cacheLayerRoot))
                {
                    continue;
                }

                if (ui.Config == null) continue;
                if (!layerSet.Contains(ui.Config.Layer)) continue;

                Close(ui).Forget();
            }
        }

        #endregion

        #endregion

        #region Event Handlers

        private void OnOpenUIEvent(string uiName, object args)
        {
            if (_uiConfigRegistry.TryGetValue(uiName, out var config))
            {
                Open(config, args).Forget();
            }
            else
            {
                Debug.LogError($"[UIManager] UI {uiName} not registered.");
            }
        }

        private void OnCloseUIEvent(string uiName)
        {
            if (_uiConfigRegistry.TryGetValue(uiName, out var config))
            {
                // 关闭所有同名 UI：包含非多实例和多实例

                // 1. 先关非多实例（在 _activeUIs 中记录的）
                if (_activeUIs.TryGetValue(config.AssetPath, out var singleUi))
                {
                    Close(singleUi).Forget();
                }

                // 2. 再遍历栈中所有匹配的 UI
                var stackSnapshot = _uiStack.ToArray();
                foreach (var ui in stackSnapshot)
                {
                    if (ui.Config == config)
                    {
                        Close(ui).Forget();
                    }
                }
            }
            else
            {
                Debug.LogError($"[UIManager] UI {uiName} not registered.");
            }
        }

        #endregion
    }
}
