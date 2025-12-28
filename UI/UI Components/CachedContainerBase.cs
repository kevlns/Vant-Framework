using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using DG.Tweening;

namespace Vant.UI.UIComponents
{
    public class PoolItem
    {
        public GameObject go;
        public object skin;
        public Button button;
        public UnityEngine.Events.UnityAction clickAction;
        public RectTransform rectTransform;
    }

    [RequireComponent(typeof(ContentSizeFitter))]
    public abstract class CachedContainerBase : MonoBehaviour
    {
        [Header("Base Settings")]
        /// <summary>
        /// Item 的预制体模板
        /// </summary>
        public GameObject itemTemplate;

        /// <summary>
        /// Item 的父容器
        /// </summary>
        public Transform container;

        /// <summary>
        /// 对象池大小
        /// </summary>
        public int poolSize = 10;

        /// <summary>
        /// 是否启用虚拟化
        /// </summary>
        public bool enableVirtualization = false;

        /// <summary>
        /// 是否启用滚动效果
        /// </summary>
        public bool enableScrollEffect = false;

        /// <summary>
        /// 滚动到目标位置的持续时间
        /// </summary>
        public float scrollToTargetDuration = 0.3f;

        /// <summary>
        /// 滚动缓动类型
        /// </summary>
        public DG.Tweening.Ease scrollEaseType = DG.Tweening.Ease.OutQuad;

        [HideInInspector]
        public string skinTypeName;

        protected Action<object, object> _onRefreshItem;

        /// <summary>
        /// 刷新 Item 时的回调函数
        /// 参数1：Item 的 Skin 对象
        /// 参数2：Item 对应的数据
        /// </summary>
        public Action<object, object> OnRefreshItem
        {
            get { return _onRefreshItem; }
            set
            {
                _onRefreshItem = value;
                if (_data != null && _onRefreshItem != null)
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Item 点击时的回调函数
        /// 参数1：Item 的 Skin 对象
        /// 参数2：Item 对应的数据
        /// 参数3：点击位置的世界坐标
        /// </summary>
        public Action<object, object, Vector3> OnClickItem;

        protected Type _skinType;
        protected List<PoolItem> _pool = new List<PoolItem>();
        protected List<PoolItem> _activeItems = new List<PoolItem>(); // For Standard Mode
        protected Dictionary<int, PoolItem> _virtualActiveItems = new Dictionary<int, PoolItem>(); // For Virtualization Mode

        protected ScrollRect _scrollRect;
        protected RectTransform _contentRect;
        protected ContentSizeFitter _contentSizeFitter;
        protected IList _data;
        protected Tween _scrollTween;
        protected List<int> _toRemoveCache = new List<int>();

        /// <summary>
        /// Item 的 Skin 类型
        /// </summary>
        public Type SkinType
        {
            get { return _skinType; }
            set { _skinType = value; }
        }

        /// <summary>
        /// 数据列表
        /// </summary>
        public IList Data
        {
            get { return _data; }
            set
            {
                _data = value;
                Refresh();
            }
        }

        private bool _isInitialized = false;

        protected virtual void Awake()
        {
            Init();
        }

        protected virtual void OnEnable()
        {
            // 确保在组件启用时完成初始化与绑定（例如挂载到 ScrollRect 之后）。
            Init();

            // 已有数据时立即刷新，避免由于启用顺序导致虚拟列表未激活的情况。
            if (_data != null)
            {
                Refresh();
            }
        }

        protected virtual void Init()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (container == null) container = transform;
            _contentSizeFitter = GetComponent<ContentSizeFitter>();
            if (itemTemplate != null && itemTemplate.activeSelf) itemTemplate.SetActive(false);

            if (!string.IsNullOrEmpty(skinTypeName) && _skinType == null)
            {
                _skinType = Type.GetType(skinTypeName);
                if (_skinType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _skinType = assembly.GetType(skinTypeName);
                        if (_skinType != null) break;
                    }
                }
            }

            _scrollRect = GetComponentInParent<ScrollRect>();
            if (_scrollRect != null)
            {
                _contentRect = _scrollRect.content;
                if (enableVirtualization)
                {
                    _scrollRect.onValueChanged.AddListener(OnScroll);
                }
            }

            // Fallback if content rect is not found via ScrollRect
            if (_contentRect == null && container is RectTransform)
            {
                _contentRect = container as RectTransform;
            }

            if (Application.isPlaying)
            {
                InitializePool();
            }
        }

        protected virtual void InitializePool()
        {
            if (itemTemplate == null) return;
            for (int i = 0; i < poolSize; i++)
            {
                PoolItem item = CreateNewItem();
                ReturnToPool(item);
            }
        }

        protected virtual PoolItem CreateNewItem()
        {
            GameObject obj = Instantiate(itemTemplate);
            obj.transform.SetParent(container, false);

            object skin = null;
            if (SkinType != null)
            {
                try
                {
                    skin = Activator.CreateInstance(SkinType, (object)obj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create Skin instance: {ex.Message}");
                }
            }

            Button btn = obj.GetComponent<Button>();
            if (btn == null)
            {
                btn = obj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }

            return new PoolItem { go = obj, skin = skin, button = btn, rectTransform = obj.GetComponent<RectTransform>() };
        }

        public virtual void Refresh()
        {
            Init();

            if (container == null) container = transform;

            if (SkinType == null && Application.isPlaying)
            {
                Debug.LogWarning("SkinType is not set!");
                return;
            }

            if (itemTemplate == null && Application.isPlaying)
            {
                Debug.LogError("Template is not assigned!");
                return;
            }

            if (_contentSizeFitter == null) _contentSizeFitter = GetComponent<ContentSizeFitter>();

            // Ensure ScrollRect listener is updated
            if (_scrollRect == null) _scrollRect = GetComponentInParent<ScrollRect>();
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScroll);
                if (enableVirtualization)
                {
                    _scrollRect.onValueChanged.AddListener(OnScroll);
                }
            }

            if (enableVirtualization)
            {
                if (_contentSizeFitter != null && _contentSizeFitter.enabled) _contentSizeFitter.enabled = false;

                // Ensure references are correct
                if (_scrollRect == null) _scrollRect = GetComponentInParent<ScrollRect>();
                if (_scrollRect != null) _contentRect = _scrollRect.content;
                if (_contentRect == null && container is RectTransform) _contentRect = container as RectTransform;

                RefreshVirtualization();
            }
            else
            {
                if (_contentSizeFitter != null && !_contentSizeFitter.enabled) _contentSizeFitter.enabled = true;
                RefreshStandard();
            }
        }

        protected abstract void RefreshVirtualization();
        protected abstract void RefreshStandard();
        protected abstract void OnScroll(Vector2 pos);

        /// <summary>
        /// 清除数据
        /// </summary>
        public void Clear()
        {
            Data = null;
        }

        protected PoolItem GetFromPool()
        {
            if (_pool.Count > 0)
            {
                PoolItem item = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return item;
            }
            return CreateNewItem();
        }

        protected void ReturnToPool(PoolItem item)
        {
            item.go.SetActive(false);
            if (_pool.Count >= poolSize)
            {
                if (item.go != null) Destroy(item.go);
            }
            else
            {
                _pool.Add(item);
            }
        }

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (!gameObject.scene.IsValid()) return;
                Refresh();
            };
#endif
        }

        /// <summary>
        /// 滚动到指定索引的 Item
        /// </summary>
        public virtual void ScrollTo(int index, float duration = -1)
        {
            if (_data == null || index < 0 || index >= _data.Count) return;
            if (_scrollRect == null) return;

            if (duration < 0)
            {
                duration = enableScrollEffect ? scrollToTargetDuration : 0;
            }

            Vector2 targetPos = GetTargetPosition(index);

            if (_scrollTween != null && _scrollTween.IsActive()) _scrollTween.Kill();

            if (duration <= 0)
            {
                _scrollRect.StopMovement();
                _contentRect.anchoredPosition = targetPos;
                if (enableVirtualization) UpdateVisibleItems(true);
            }
            else
            {
                _scrollRect.StopMovement();
                _scrollTween = _contentRect.DOAnchorPos(targetPos, duration)
                    .SetEase(scrollEaseType)
                    .OnUpdate(() =>
                    {
                        if (enableVirtualization) UpdateVisibleItems();
                    })
                    .OnComplete(() =>
                    {
                        if (enableVirtualization) UpdateVisibleItems(true);
                    });
            }
        }

        protected abstract Vector2 GetTargetPosition(int index);
        protected abstract void UpdateVisibleItems(bool forceRefresh = false);

        protected virtual void OnDestroy()
        {
            if (_scrollTween != null && _scrollTween.IsActive()) _scrollTween.Kill();
        }
    }
}
