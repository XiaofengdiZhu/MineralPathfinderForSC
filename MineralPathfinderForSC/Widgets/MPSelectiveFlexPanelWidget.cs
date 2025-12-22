using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game {
    public class MPSelectiveFlexPanelWidget<T> : ScrollPanelWidget {
        public List<T> m_items = [];
        public int? m_selectedItemIndex;
        public Dictionary<int, Widget> m_widgetsByIndex = [];
        public int m_firstVisibleIndex;
        public int m_lastVisibleIndex;
        public int m_columnCount;
        public bool PlayClickSound = true;
        public Vector2 m_itemSize;
        public bool m_widgetsDirty;
        public bool m_clickAllowed;
        public Vector2 lastActualSize = new(-1f);

        public Func<T, Widget> ItemWidgetFactory { get; set; }

        /// <summary>
        ///     Horizontal: 先从左到右，再从上到下，滚动方向为垂直
        ///     Vertical: 先从上到下，再从左到右，滚动方向为水平
        /// </summary>
        public override LayoutDirection Direction {
            get => base.Direction;
            set {
                if (value != Direction) {
                    base.Direction = value;
                    m_widgetsDirty = true;
                }
            }
        }

        public override float ScrollPosition {
            get => base.ScrollPosition;
            set {
                if (value != ScrollPosition) {
                    base.ScrollPosition = value;
                    m_widgetsDirty = true;
                }
            }
        }

        public Vector2 ItemSize {
            get => m_itemSize;
            set {
                if (value != m_itemSize) {
                    m_itemSize = value;
                    m_widgetsDirty = true;
                }
            }
        }

        public int? SelectedIndex {
            get => m_selectedItemIndex;
            set {
                if (value.HasValue
                    && (value.Value < 0 || value.Value >= m_items.Count)) {
                    value = null;
                }
                if (value != m_selectedItemIndex) {
                    m_selectedItemIndex = value;
                    SelectionChanged?.Invoke();
                }
            }
        }

        public T SelectedItem {
            get {
                if (!m_selectedItemIndex.HasValue) {
                    return default;
                }
                return m_items[m_selectedItemIndex.Value];
            }
            set {
                int num = m_items.IndexOf(value);
                SelectedIndex = num >= 0 ? new int?(num) : null;
            }
        }

        public ReadOnlyList<T> Items => new(m_items);

        public Color SelectionColor { get; set; }

        public virtual Action<T> ItemClicked { get; set; }

        public virtual Action SelectionChanged { get; set; }

        public MPSelectiveFlexPanelWidget() {
            SelectionColor = Color.Gray;
            ItemWidgetFactory = item => new LabelWidget {
                Text = item != null ? item.ToString() : string.Empty,
                HorizontalAlignment = WidgetAlignment.Center,
                VerticalAlignment = WidgetAlignment.Center
            };
            ItemSize = new Vector2(48f);
        }

        public void AddItem(T item) {
            m_items.Add(item);
            m_widgetsDirty = true;
        }

        public void AddItems(IEnumerable<T> items) {
            m_items.AddRange(items);
            m_widgetsDirty = true;
        }

        public void RemoveItem(T item) {
            int num = m_items.IndexOf(item);
            if (num >= 0) {
                RemoveItemAt(num);
            }
        }

        public void RemoveItemAt(int index) {
            _ = m_items[index];
            m_items.RemoveAt(index);
            m_widgetsByIndex.Clear();
            m_widgetsDirty = true;
            if (index == SelectedIndex) {
                SelectedIndex = null;
            }
        }

        public void ClearItems() {
            m_items.Clear();
            m_widgetsByIndex.Clear();
            m_widgetsDirty = true;
            SelectedIndex = null;
        }

        public override float CalculateScrollAreaLength() => MathF.Ceiling((float)Items.Count / m_columnCount)
            * (Direction == LayoutDirection.Horizontal ? ItemSize.X : ItemSize.Y);

        public void ScrollToItem(T item) {
            int num = m_items.IndexOf(item);
            if (num >= 0) {
                int row = num / m_columnCount;
                float rowLength = Direction == LayoutDirection.Horizontal ? ItemSize.X : ItemSize.Y;
                float num2 = row * rowLength;
                float num3 = Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y;
                if (num2 < ScrollPosition) {
                    ScrollPosition = num2;
                }
                else if (num2 > ScrollPosition + num3 - rowLength) {
                    ScrollPosition = num2 - num3 + rowLength;
                }
            }
        }

        public override void MeasureOverride(Vector2 parentAvailableSize) {
            IsDrawRequired = true;
            foreach (Widget child in Children) {
                if (child.IsVisible) {
                    child.Measure(ItemSize);
                }
            }
            if (m_widgetsDirty) {
                m_widgetsDirty = false;
                CreateListWidgets(ActualSize);
            }
        }

        public override void ArrangeOverride() {
            if (ActualSize != lastActualSize) {
                m_widgetsDirty = true;
                m_columnCount = Direction == LayoutDirection.Horizontal ? (int)(ActualSize.Y / ItemSize.Y) : (int)(ActualSize.X / ItemSize.X);
            }
            lastActualSize = ActualSize;
            int num = m_firstVisibleIndex;
            for (int i = 0; i < Children.Count; i++) {
                Widget child = Children[i];
                int row = num / m_columnCount;
                int column = num % m_columnCount;
                if (Direction == LayoutDirection.Horizontal) {
                    Vector2 vector = new(row * ItemSize.X - ScrollPosition, column * ItemSize.Y);
                    ArrangeChildWidgetInCell(vector, vector + ItemSize, child);
                }
                else {
                    Vector2 vector2 = new(column * ItemSize.X, row * ItemSize.Y - ScrollPosition);
                    ArrangeChildWidgetInCell(vector2, vector2 + ItemSize, child);
                }
                num++;
            }
        }

        public override void Update() {
            bool flag = ScrollSpeed != 0f;
            base.Update();
            if (Input.Tap.HasValue
                && HitTestPanel(Input.Tap.Value)) {
                m_clickAllowed = !flag;
            }
            if (Input.Click.HasValue
                && m_clickAllowed
                && HitTestPanel(Input.Click.Value.Start)
                && HitTestPanel(Input.Click.Value.End)) {
                int num = PositionToItemIndex(Input.Click.Value.End);
                if (ItemClicked != null
                    && num >= 0
                    && num < m_items.Count) {
                    ItemClicked(Items[num]);
                }
                SelectedIndex = num;
                if (SelectedIndex.HasValue && PlayClickSound) {
                    AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                }
            }
        }

        public override void Draw(DrawContext dc) {
            if (SelectedIndex.HasValue
                && SelectedIndex.Value >= m_firstVisibleIndex
                && SelectedIndex.Value <= m_lastVisibleIndex) {
                int row = SelectedIndex.Value / m_columnCount;
                int column = SelectedIndex.Value % m_columnCount;
                Vector2 vector = Direction == LayoutDirection.Horizontal
                    ? new Vector2(row * ItemSize.X - ScrollPosition, column * ItemSize.Y)
                    : new Vector2(column * ItemSize.X, row * ItemSize.Y - ScrollPosition);
                FlatBatch2D flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
                int count = flatBatch2D.TriangleVertices.Count;
                flatBatch2D.QueueQuad(vector, vector + ItemSize, 0f, SelectionColor * GlobalColorTransform);
                flatBatch2D.TransformTriangles(GlobalTransform, count);
            }
            base.Draw(dc);
        }

        public int PositionToItemIndex(Vector2 position) {
            Vector2 vector = ScreenToWidget(position);
            return Direction == LayoutDirection.Horizontal
                ? (int)((vector.X + ScrollPosition) / ItemSize.X) * m_columnCount + (int)(vector.Y / ItemSize.Y)
                : (int)((vector.Y + ScrollPosition) / ItemSize.Y) * m_columnCount + (int)(vector.X / ItemSize.X);
        }

        public void CreateListWidgets(Vector2 size) {
            Children.Clear();
            if (m_items.Count <= 0) {
                return;
            }
            int x = (int)MathF.Floor(ScrollPosition / (Direction == LayoutDirection.Horizontal ? ItemSize.X : ItemSize.Y)) * m_columnCount;
            int x2 = (int)MathF.Floor(
                    (ScrollPosition + (Direction == LayoutDirection.Horizontal ? size.X : size.Y))
                    / (Direction == LayoutDirection.Horizontal ? ItemSize.X : ItemSize.Y)
                )
                * m_columnCount
                + m_columnCount;
            m_firstVisibleIndex = MathUtils.Max(x, 0);
            m_lastVisibleIndex = MathUtils.Min(x2, m_items.Count - 1);
            for (int i = m_firstVisibleIndex; i <= m_lastVisibleIndex; i++) {
                T obj = m_items[i];
                if (!m_widgetsByIndex.TryGetValue(i, out Widget value)) {
                    value = ItemWidgetFactory(obj);
                    value.Tag = obj;
                    m_widgetsByIndex.Add(i, value);
                }
                Children.Add(value);
            }
        }
    }

    public class MPSelectiveFlexPanelWidgetInt : MPSelectiveFlexPanelWidget<int> { }
}