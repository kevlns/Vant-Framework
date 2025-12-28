using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Vant.UI.UIComponents
{
    public enum CachedListDirection
    {
        Vertical,
        Horizontal
    }

    // [RequireComponent(typeof(VerticalLayoutGroup))] // Removed to support dynamic switching
    public class CachedListComponent : CachedContainerBase
    {
        [Header("List Settings")]
        /// <summary>
        /// 虚拟化时的 Item 高度（如果为 0，则尝试使用 Template 的高度）
        /// </summary>
        public float itemHeight = 0;

        [SerializeField] private float _spacing = 0; // If zero, use layout group's

        [SerializeField] private CachedListDirection _direction = CachedListDirection.Vertical;

        /// <summary>
        /// 间距（如果为零，则使用 LayoutGroup 的设置）
        /// </summary>
        public float spacing
        {
            get { return _spacing; }
            set
            {
                if (_spacing != value)
                {
                    _spacing = value;
                    Refresh();
                }
            }
        }

        public CachedListDirection direction
        {
            get { return _direction; }
            set
            {
                if (_direction != value)
                {
                    _direction = value;
                    Refresh();
                }
            }
        }

        private HorizontalOrVerticalLayoutGroup _layoutGroup;
        private float _runtimeItemHeight;
        private float _runtimeSpacing;
        private RectOffset _padding;

        protected override void Init()
        {
            base.Init();
            _layoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (_layoutGroup != null) _padding = _layoutGroup.padding;
        }

        protected override void RefreshStandard()
        {
            RecycleAllVirtualItems();

            // Ensure correct layout group
            if (direction == CachedListDirection.Vertical)
            {
                var hGroup = GetComponent<HorizontalLayoutGroup>();
                if (hGroup != null) DestroyImmediate(hGroup);
                
                var vGroup = GetComponent<VerticalLayoutGroup>();
                if (vGroup == null) vGroup = gameObject.AddComponent<VerticalLayoutGroup>();
                _layoutGroup = vGroup;
            }
            else
            {
                var vGroup = GetComponent<VerticalLayoutGroup>();
                if (vGroup != null) DestroyImmediate(vGroup);

                var hGroup = GetComponent<HorizontalLayoutGroup>();
                if (hGroup == null) hGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
                _layoutGroup = hGroup;
            }

            if (_layoutGroup != null && !_layoutGroup.enabled) _layoutGroup.enabled = true;

            // 根据方向设置 ContentSizeFitter
            if (_contentSizeFitter != null)
            {
                if (direction == CachedListDirection.Vertical)
                {
                    _contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    _contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
                else
                {
                    _contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    _contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }

            int targetCount = _data != null ? _data.Count : 0;
            int currentCount = _activeItems.Count;

            // Remove excess
            if (currentCount > targetCount)
            {
                for (int i = currentCount - 1; i >= targetCount; i--)
                {
                    ReturnToPool(_activeItems[i]);
                    _activeItems.RemoveAt(i);
                }
            }

            // Add missing
            if (targetCount > currentCount)
            {
                for (int i = currentCount; i < targetCount; i++)
                {
                    PoolItem item = GetFromPool();
                    item.go.transform.SetParent(container, false);
                    item.go.SetActive(true);
                    item.go.transform.SetAsLastSibling();
                    _activeItems.Add(item);
                }
            }

            // Refresh data
            for (int i = 0; i < targetCount; i++)
            {
                var item = _activeItems[i];
                var data = _data[i];

                if (OnRefreshItem != null) OnRefreshItem(item.skin, data);

                if (item.button != null)
                {
                    if (item.clickAction != null) item.button.onClick.RemoveListener(item.clickAction);
                    item.clickAction = () => { OnClickItem?.Invoke(item.skin, data, item.go.transform.position); };
                    item.button.onClick.AddListener(item.clickAction);
                }
            }
            
            // Force layout rebuild
            if (container != null && container is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        protected override void RefreshVirtualization()
        {
            // Recycle standard items
            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                ReturnToPool(_activeItems[i]);
            }
            _activeItems.Clear();

            if (_contentRect == null) return;

            if (_layoutGroup == null) _layoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (_layoutGroup != null)
            {
                _padding = _layoutGroup.padding;
                
                // Disable layout group to take manual control
                if (_layoutGroup.enabled) _layoutGroup.enabled = false;
            }

            if (_padding == null) _padding = new RectOffset();

            // Determine spacing
            if (Mathf.Abs(_spacing) > 0.001f)
            {
                _runtimeSpacing = _spacing;
            }
            else if (_layoutGroup != null)
            {
                _runtimeSpacing = _layoutGroup.spacing;
            }
            else
            {
                _runtimeSpacing = 0;
            }

            // Determine item height
            if (itemHeight > 0)
            {
                _runtimeItemHeight = itemHeight;
            }
            else if (itemTemplate != null)
            {
                RectTransform rt = itemTemplate.GetComponent<RectTransform>();
                if (rt != null)
                {
                    _runtimeItemHeight = direction == CachedListDirection.Vertical ? rt.rect.height : rt.rect.width;
                }
            }

            if (_runtimeItemHeight <= 0.001f) _runtimeItemHeight = 100f; // Fallback

            UpdateContentSize();
            UpdateVisibleItems(true);
        }

        private void UpdateContentSize()
        {
            if (_contentRect == null) return;

            int count = _data != null ? _data.Count : 0;
            int verticalPadding = _padding != null ? _padding.vertical : 0;
            int horizontalPadding = _padding != null ? _padding.horizontal : 0;

            if (direction == CachedListDirection.Vertical)
            {
                float height = verticalPadding + count * _runtimeItemHeight + (count > 0 ? (count - 1) * _runtimeSpacing : 0);
                if (height < 0) height = 0;
                _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, height);
            }
            else
            {
                float width = horizontalPadding + count * _runtimeItemHeight + (count > 0 ? (count - 1) * _runtimeSpacing : 0);
                if (width < 0) width = 0;
                _contentRect.sizeDelta = new Vector2(width, _contentRect.sizeDelta.y);
            }
        }

        protected override void OnScroll(Vector2 pos)
        {
            if (enableVirtualization && _data != null)
            {
                UpdateVisibleItems();
            }
        }

        protected override void UpdateVisibleItems(bool forceRefresh = false)
        {
            if (_data == null || _data.Count == 0)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    LayoutExistingChildrenInEditor();
                    return;
                }
#endif
                RecycleAllVirtualItems();
                return;
            }

            int startIndex = 0;
            int endIndex = _data.Count;

            if (_scrollRect != null)
            {
                float scrollPos = direction == CachedListDirection.Vertical ? _contentRect.anchoredPosition.y : -_contentRect.anchoredPosition.x;
                float viewSize = direction == CachedListDirection.Vertical ? ((RectTransform)_scrollRect.transform).rect.height : ((RectTransform)_scrollRect.transform).rect.width;
                float totalItemSize = _runtimeItemHeight + _runtimeSpacing;
                float paddingStart = direction == CachedListDirection.Vertical ? _padding.top : _padding.left;

                int startRow = Mathf.FloorToInt((scrollPos - paddingStart) / totalItemSize);
                int endRow = Mathf.CeilToInt((scrollPos + viewSize - paddingStart) / totalItemSize);

                // Add buffer
                startRow -= 1;
                endRow += 1;

                if (startRow < 0) startRow = 0;
                if (endRow > _data.Count) endRow = _data.Count;

                startIndex = startRow;
                endIndex = endRow;
            }

            // Recycle items out of range
            _toRemoveCache.Clear();
            foreach (var kvp in _virtualActiveItems)
            {
                if (kvp.Key < startIndex || kvp.Key >= endIndex)
                {
                    _toRemoveCache.Add(kvp.Key);
                }
            }

            foreach (int index in _toRemoveCache)
            {
                ReturnToPool(_virtualActiveItems[index]);
                _virtualActiveItems.Remove(index);
            }

            // Add new items
            for (int i = startIndex; i < endIndex; i++)
            {
                if (!_virtualActiveItems.ContainsKey(i))
                {
                    PoolItem item = GetFromPool();
                    item.go.transform.SetParent(container, false);
                    item.go.SetActive(true);
                    
                    SetItemPosition(item.rectTransform, i);
                    
                    _virtualActiveItems.Add(i, item);
                    
                    var data = _data[i];
                    if (OnRefreshItem != null) OnRefreshItem(item.skin, data);

                    if (item.button != null)
                    {
                        if (item.clickAction != null) item.button.onClick.RemoveListener(item.clickAction);
                        item.clickAction = () => { OnClickItem?.Invoke(item.skin, data, item.go.transform.position); };
                        item.button.onClick.AddListener(item.clickAction);
                    }
                }
                else if (forceRefresh)
                {
                    SetItemPosition(_virtualActiveItems[i].rectTransform, i);
                    var item = _virtualActiveItems[i];
                    var data = _data[i];
                    if (OnRefreshItem != null) OnRefreshItem(item.skin, data);
                }
            }
        }

        private void SetItemPosition(RectTransform rt, int index)
        {
            if (direction == CachedListDirection.Vertical)
            {
                float y = -(_padding.top + index * (_runtimeItemHeight + _runtimeSpacing));

                float width = _contentRect.rect.width - _padding.horizontal;

                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1); // Stretch horizontally
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(-_padding.horizontal, _runtimeItemHeight);
                rt.anchoredPosition = new Vector2(0, y);
            }
            else
            {
                float x = _padding.left + index * (_runtimeItemHeight + _runtimeSpacing);

                float height = _contentRect.rect.height - _padding.vertical;

                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1); // Fixed left
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(_runtimeItemHeight, -_padding.vertical); // Height should be adjusted by padding
                rt.anchoredPosition = new Vector2(x, -_padding.top); // Y position should respect top padding
            }
        }

        private void RecycleAllVirtualItems()
        {
            foreach (var item in _virtualActiveItems.Values)
            {
                ReturnToPool(item);
            }
            _virtualActiveItems.Clear();
        }

        protected override Vector2 GetTargetPosition(int index)
        {
            if (_scrollRect == null) return _contentRect != null ? _contentRect.anchoredPosition : Vector2.zero;

            float targetY = 0;
            float targetX = 0;
            
            if (direction == CachedListDirection.Vertical)
            {
                float viewHeight = ((RectTransform)_scrollRect.transform).rect.height;
                float contentHeight = _contentRect.rect.height;
                
                if (enableVirtualization)
                {
                    float totalItemHeight = _runtimeItemHeight + _runtimeSpacing;
                    targetY = _padding.top + index * totalItemHeight;
                }
                else
                {
                    // Standard Mode
                    Canvas.ForceUpdateCanvases();
                    if (index >= 0 && index < _activeItems.Count)
                    {
                         RectTransform itemRect = _activeItems[index].rectTransform;
                         targetY = -itemRect.anchoredPosition.y - _padding.top;
                    }
                }

                float maxScrollY = contentHeight - viewHeight;
                if (maxScrollY < 0) maxScrollY = 0;
                targetY = Mathf.Clamp(targetY, 0, maxScrollY);
                return new Vector2(_contentRect.anchoredPosition.x, targetY);
            }
            else
            {
                float viewWidth = ((RectTransform)_scrollRect.transform).rect.width;
                float contentWidth = _contentRect.rect.width;

                if (enableVirtualization)
                {
                    float totalItemWidth = _runtimeItemHeight + _runtimeSpacing;
                    targetX = _padding.left + index * totalItemWidth;
                }
                else
                {
                    Canvas.ForceUpdateCanvases();
                    if (index >= 0 && index < _activeItems.Count)
                    {
                        RectTransform itemRect = _activeItems[index].rectTransform;
                        targetX = itemRect.anchoredPosition.x - _padding.left; // Positive X for horizontal scroll usually
                    }
                }

                float maxScrollX = contentWidth - viewWidth;
                if (maxScrollX < 0) maxScrollX = 0;
                targetX = Mathf.Clamp(targetX, 0, maxScrollX);
                // For horizontal scroll rect, content moves to negative X to show right items.
                // But ScrollRect usually handles normalized position. If we set anchoredPosition directly:
                // Leftmost is 0. Rightmost is -maxScrollX.
                return new Vector2(-targetX, _contentRect.anchoredPosition.y);
            }
        }

        private void LayoutExistingChildrenInEditor()
        {
            if (container == null) return;
            int childCount = container.childCount;
            if (childCount == 0) return;

            if (direction == CachedListDirection.Vertical)
            {
                float height = _padding.vertical + childCount * _runtimeItemHeight + (childCount - 1) * _runtimeSpacing;
                if (height < 0) height = 0;
                _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, height);
            }
            else
            {
                float width = _padding.horizontal + childCount * _runtimeItemHeight + (childCount - 1) * _runtimeSpacing;
                if (width < 0) width = 0;
                _contentRect.sizeDelta = new Vector2(width, _contentRect.sizeDelta.y);
            }

            for (int i = 0; i < childCount; i++)
            {
                Transform child = container.GetChild(i);
                RectTransform rt = child as RectTransform;
                if (rt != null) SetItemPosition(rt, i);
            }
        }
    }
}
