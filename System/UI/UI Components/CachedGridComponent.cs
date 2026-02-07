using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using DG.Tweening;

namespace Vant.UI.UIComponents
{
    public enum GridAlignmentMode
    {
        Standard,
        Distributed,
        DistributedCenter
    }

    [RequireComponent(typeof(GridLayoutGroup))]
    public class CachedGridComponent : CachedContainerBase
    {
        [SerializeField] private GridAlignmentMode _alignmentMode = GridAlignmentMode.Standard;
        [SerializeField] private TextAnchor _childAlignment = TextAnchor.UpperLeft;
        [SerializeField] private int _maxColumns = 0; // 0 means unlimited (auto-fill width)
        [SerializeField] private int _maxRows = 0; // 0 means unlimited
        [SerializeField] private Vector2 _cellSize = Vector2.zero; // If zero, use layout group's
        [SerializeField] private Vector2 _spacing = Vector2.zero; // If zero, use layout group's

        private GridLayoutGroup _layoutGroup;
        private Vector2 _runtimeCellSize;
        private Vector2 _runtimeSpacing;
        private RectOffset _padding;
        private int _constraintCount;

        /// <summary>
        /// 网格对齐模式
        /// </summary>
        public GridAlignmentMode alignmentMode
        {
            get { return _alignmentMode; }
            set
            {
                if (_alignmentMode != value)
                {
                    _alignmentMode = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 子物体对齐方式（仅在 Standard 模式下有效）
        /// </summary>
        public TextAnchor childAlignment
        {
            get { return _childAlignment; }
            set
            {
                if (_childAlignment != value)
                {
                    _childAlignment = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 最大列数（0 表示不限制，自动填充宽度）
        /// </summary>
        public int maxColumns
        {
            get { return _maxColumns; }
            set
            {
                if (_maxColumns != value)
                {
                    _maxColumns = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 最大行数（0 表示不限制）
        /// </summary>
        public int maxRows
        {
            get { return _maxRows; }
            set
            {
                if (_maxRows != value)
                {
                    _maxRows = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 单元格大小（如果为零，则使用 LayoutGroup 的设置）
        /// </summary>
        public Vector2 cellSize
        {
            get { return _cellSize; }
            set
            {
                if (_cellSize != value)
                {
                    _cellSize = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 间距（如果为零，则使用 LayoutGroup 的设置）
        /// </summary>
        public Vector2 spacing
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

        protected override void Awake()
        {
            base.Awake();
            _layoutGroup = GetComponent<GridLayoutGroup>();
            if (_layoutGroup != null) _padding = _layoutGroup.padding;
        }

        protected override void RefreshStandard()
        {
            RecycleAllVirtualItems();

            if (_layoutGroup != null && !_layoutGroup.enabled) _layoutGroup.enabled = true;

            int targetCount = _data != null ? _data.Count : 0;
            int currentCount = _activeItems.Count;

            if (currentCount > targetCount)
            {
                for (int i = currentCount - 1; i >= targetCount; i--)
                {
                    ReturnToPool(_activeItems[i]);
                    _activeItems.RemoveAt(i);
                }
            }

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

            for (int i = 0; i < targetCount; i++)
            {
                var item = _activeItems[i];
                var data = _data[i];

                if (OnRefreshItem != null)
                {
                    OnRefreshItem(item.skin, data);
                }

                if (item.button != null)
                {
                    if (item.clickAction != null) item.button.onClick.RemoveListener(item.clickAction);
                    item.clickAction = () => { OnClickItem?.Invoke(item.skin, data, item.go.transform.position); };
                    item.button.onClick.AddListener(item.clickAction);
                }
            }
        }

        protected override void OnScroll(Vector2 pos)
        {
            if (enableVirtualization && _data != null)
            {
                UpdateVisibleItems();
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

            if (_layoutGroup == null) _layoutGroup = GetComponent<GridLayoutGroup>();
            if (_layoutGroup != null)
            {
                _runtimeCellSize = new Vector2(
                    Mathf.Abs(_cellSize.x) > 0.001f ? _cellSize.x : _layoutGroup.cellSize.x,
                    Mathf.Abs(_cellSize.y) > 0.001f ? _cellSize.y : _layoutGroup.cellSize.y
                );

                if (_runtimeCellSize.x <= 0.001f) _runtimeCellSize.x = 100f;
                if (_runtimeCellSize.y <= 0.001f) _runtimeCellSize.y = 100f;

                _runtimeSpacing = new Vector2(
                    Mathf.Abs(_spacing.x) > 0.001f ? _spacing.x : _layoutGroup.spacing.x,
                    Mathf.Abs(_spacing.y) > 0.001f ? _spacing.y : _layoutGroup.spacing.y
                );
                _padding = _layoutGroup.padding;
                
                if (_layoutGroup.enabled) _layoutGroup.enabled = false;
            }

            if (_padding == null) _padding = new RectOffset();

            CalculateLayout();
            UpdateContentSize();
            UpdateVisibleItems(true);
        }

        private void CalculateLayout()
        {
            if (_contentRect == null) return;
            float width = _contentRect.rect.width;
            
            if (_maxColumns > 0)
            {
                _constraintCount = _maxColumns;
            }
            else if (_layoutGroup != null && _layoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                _constraintCount = _layoutGroup.constraintCount;
            }
            else
            {
                int horizontalPadding = _padding != null ? _padding.horizontal : 0;
                _constraintCount = Mathf.FloorToInt((width - horizontalPadding + _runtimeSpacing.x) / (_runtimeCellSize.x + _runtimeSpacing.x));
                if (_constraintCount < 1) _constraintCount = 1;
            }
        }

        private void UpdateContentSize()
        {
            if (_contentRect == null) return;

            if (_constraintCount <= 0) _constraintCount = 1;

            int count = _data != null ? _data.Count : 0;
            int rows = Mathf.CeilToInt((float)count / _constraintCount);
            
            if (_maxRows > 0 && rows > _maxRows) rows = _maxRows;

            int verticalPadding = _padding != null ? _padding.vertical : 0;
            float height = verticalPadding + rows * _runtimeCellSize.y + (rows - 1) * _runtimeSpacing.y;
            if (height < 0) height = 0;
            
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, height);
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
                float scrollY = _contentRect.anchoredPosition.y;
                float viewHeight = ((RectTransform)_scrollRect.transform).rect.height;
                
                float itemHeight = _runtimeCellSize.y + _runtimeSpacing.y;
                
                int startRow = Mathf.FloorToInt((scrollY - _padding.top) / itemHeight);
                int endRow = Mathf.CeilToInt((scrollY + viewHeight - _padding.top) / itemHeight);
                
                startRow -= 1;
                endRow += 1;

                if (startRow < 0) startRow = 0;
                
                int totalRows = Mathf.CeilToInt((float)_data.Count / _constraintCount);
                if (_maxRows > 0 && totalRows > _maxRows) totalRows = _maxRows;
                
                if (endRow > totalRows) endRow = totalRows;

                startIndex = startRow * _constraintCount;
                endIndex = endRow * _constraintCount;
            }
            
            int maxItemCount = _data.Count;
            if (_maxRows > 0)
            {
                int limit = _maxRows * _constraintCount;
                if (maxItemCount > limit) maxItemCount = limit;
            }
            
            if (endIndex > maxItemCount) endIndex = maxItemCount;

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
                    if (OnRefreshItem != null)
                    {
                        OnRefreshItem(item.skin, data);
                    }

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

                    if (OnRefreshItem != null)
                    {
                        OnRefreshItem(item.skin, data);
                    }

                    if (item.button != null)
                    {
                        if (item.clickAction != null) item.button.onClick.RemoveListener(item.clickAction);
                        item.clickAction = () => { OnClickItem?.Invoke(item.skin, data, item.go.transform.position); };
                        item.button.onClick.AddListener(item.clickAction);
                    }
                }
            }
        }

        private void LayoutExistingChildrenInEditor()
        {
            if (container == null) return;
            
            int childCount = container.childCount;
            if (childCount == 0) return;

            if (_constraintCount <= 0) _constraintCount = 1;

            int rows = Mathf.CeilToInt((float)childCount / _constraintCount);
            if (_maxRows > 0 && rows > _maxRows) rows = _maxRows;

            float height = _padding.vertical + rows * _runtimeCellSize.y + (rows - 1) * _runtimeSpacing.y;
            if (height < 0) height = 0;
            
            _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, height);

            for (int i = 0; i < childCount; i++)
            {
                Transform child = container.GetChild(i);
                RectTransform rt = child as RectTransform;
                if (rt != null)
                {
                    SetItemPosition(rt, i);
                }
            }
        }

        private void SetItemPosition(RectTransform rt, int index)
        {
            int row = index / _constraintCount;
            int col = index % _constraintCount;

            float currentSpacingX = _runtimeSpacing.x;
            float offsetX = 0;

            if (_alignmentMode == GridAlignmentMode.Distributed && _constraintCount > 1)
            {
                float contentWidth = _contentRect.rect.width;
                float availableWidth = contentWidth - _padding.left - _padding.right;
                float totalCellWidth = _constraintCount * _runtimeCellSize.x;
                
                if (availableWidth > totalCellWidth)
                {
                    currentSpacingX = (availableWidth - totalCellWidth) / (_constraintCount - 1);
                }
            }
            else if (_alignmentMode == GridAlignmentMode.DistributedCenter)
            {
                float contentWidth = _contentRect.rect.width;
                float availableWidth = contentWidth - _padding.left - _padding.right;
                float totalCellWidth = _constraintCount * _runtimeCellSize.x;
                
                if (availableWidth > totalCellWidth)
                {
                    currentSpacingX = (availableWidth - totalCellWidth) / _constraintCount;
                    offsetX = currentSpacingX / 2f;
                }
            }
            else
            {
                float contentWidth = _contentRect.rect.width;
                float rowWidth = 0;
                
                int itemsInThisRow = _constraintCount;
                int totalItems = _data != null ? _data.Count : (container != null ? container.childCount : 0);
                if (_maxRows > 0)
                {
                    int limit = _maxRows * _constraintCount;
                    if (totalItems > limit) totalItems = limit;
                }
                
                int lastRowIndex = Mathf.CeilToInt((float)totalItems / _constraintCount) - 1;
                if (row == lastRowIndex)
                {
                    int remainder = totalItems % _constraintCount;
                    if (remainder > 0) itemsInThisRow = remainder;
                }
                
                rowWidth = _padding.left + _padding.right + itemsInThisRow * _runtimeCellSize.x + (itemsInThisRow - 1) * _runtimeSpacing.x;

                if (_childAlignment == TextAnchor.UpperCenter || _childAlignment == TextAnchor.MiddleCenter || _childAlignment == TextAnchor.LowerCenter)
                {
                    offsetX = (contentWidth - rowWidth) / 2f;
                }
                else if (_childAlignment == TextAnchor.UpperRight || _childAlignment == TextAnchor.MiddleRight || _childAlignment == TextAnchor.LowerRight)
                {
                    offsetX = contentWidth - rowWidth;
                }
            }

            float x = _padding.left + col * (_runtimeCellSize.x + currentSpacingX) + offsetX;
            float y = -(_padding.top + row * (_runtimeCellSize.y + _runtimeSpacing.y));

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
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
            float viewHeight = ((RectTransform)_scrollRect.transform).rect.height;
            float contentHeight = _contentRect.rect.height;

            if (enableVirtualization)
            {
                int row = index / _constraintCount;
                float itemHeight = _runtimeCellSize.y + _runtimeSpacing.y;
                targetY = _padding.top + row * itemHeight;
            }
            else
            {
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
    }
}
