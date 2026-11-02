using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SunEyeVision.UI.Shell.Controls.Common
{
    /// <summary>
    /// 可折叠分栏控件 - 带折叠/展开按钮的 GridSplitter。
    /// 折叠逻辑内聚在控件内部（对比 SplitterWithToggle 由外部处理折叠），
    /// 支持向指定方向折叠相邻面板。折叠状态经 IsCollapsed 暴露，供布局持久化。
    /// </summary>
    public class CollapsibleSplitterBox : GridSplitter
    {
        private Button? _toggleButton;
        private TextBlock? _arrowTextBlock;

        public static readonly DependencyProperty CollapseDirectionProperty =
            DependencyProperty.Register(nameof(CollapseDirection), typeof(CollapseDirectionType), typeof(CollapsibleSplitterBox),
                new PropertyMetadata(CollapseDirectionType.Right, OnDirectionChanged));

        /// <summary>
        /// 折叠方向 - 分隔条折叠时面板收缩到哪一侧
        /// </summary>
        public CollapseDirectionType CollapseDirection
        {
            get => (CollapseDirectionType)GetValue(CollapseDirectionProperty);
            set => SetValue(CollapseDirectionProperty, value);
        }

        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(CollapsibleSplitterBox),
                new PropertyMetadata(false));

        /// <summary>
        /// 是否已折叠（供布局持久化）
        /// </summary>
        public bool IsCollapsed
        {
            get => (bool)GetValue(IsCollapsedProperty);
            set => SetValue(IsCollapsedProperty, value);
        }

        /// <summary>
        /// 折叠后分隔条相邻面板的宽度/高度（双折叠档 - 收缩档）
        /// </summary>
        public static readonly DependencyProperty CollapsedSizeProperty =
            DependencyProperty.Register(nameof(CollapsedSize), typeof(double), typeof(CollapsibleSplitterBox),
                new PropertyMetadata(0.0));

        /// <summary>
        /// 折叠后相邻面板尺寸（像素）
        /// </summary>
        public double CollapsedSize
        {
            get => (double)GetValue(CollapsedSizeProperty);
            set => SetValue(CollapsedSizeProperty, value);
        }

        public CollapsibleSplitterBox()
        {
            DefaultStyleKey = typeof(CollapsibleSplitterBox);
        }

        private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CollapsibleSplitterBox box)
            {
                box.UpdateArrowText();
            }
        }

        private void UpdateArrowText()
        {
            if (_arrowTextBlock == null)
            {
                return;
            }

            // 折叠时箭头指向"展开方向"，展开时指向"折叠方向"
            var arrow = (IsCollapsed, CollapseDirection) switch
            {
                (true, CollapseDirectionType.Left) => "▶",
                (true, CollapseDirectionType.Right) => "◀",
                (true, CollapseDirectionType.Top) => "▼",
                (true, CollapseDirectionType.Bottom) => "▲",
                (false, CollapseDirectionType.Left) => "◀",
                (false, CollapseDirectionType.Right) => "▶",
                (false, CollapseDirectionType.Top) => "▲",
                (false, CollapseDirectionType.Bottom) => "▼",
                _ => ""
            };
            _arrowTextBlock.Text = arrow;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_toggleButton != null)
            {
                _toggleButton.Click -= OnToggleButtonClick;
            }

            _toggleButton = GetTemplateChild("PART_ToggleButton") as Button;
            if (_toggleButton != null)
            {
                _toggleButton.Click += OnToggleButtonClick;
                _arrowTextBlock = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush?)TryFindResource(SunEyeVision.UI.Themes.Base.ThemeKeys.TextSecondaryBrush) ?? Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _toggleButton.Content = _arrowTextBlock;
                UpdateArrowText();
            }
        }

        private void OnToggleButtonClick(object? sender, RoutedEventArgs e)
        {
            ToggleCollapsed();
        }

        /// <summary>
        /// 折叠/展开相邻面板
        /// </summary>
        public void ToggleCollapsed()
        {
            if (Parent is not Grid grid) return;
            if (ResizeDirection == GridResizeDirection.Columns)
            {
                var index = Grid.GetColumn(this) + (CollapseDirection == CollapseDirectionType.Left ? -1 : 1);
                if (index < 0 || index >= grid.ColumnDefinitions.Count) return;
                var target = grid.ColumnDefinitions[index];
                if (!IsCollapsed)
                {
                    _savedLength = target.Width;
                    _savedMinimum = target.MinWidth;
                    ExpandedPixelSize = target.Width.IsAbsolute ? target.Width.Value : target.ActualWidth;
                    target.MinWidth = 0;
                    target.Width = new GridLength(CollapsedSize);
                }
                else
                {
                    target.MinWidth = _savedMinimum;
                    target.Width = _savedLength;
                }
            }
            else
            {
                var index = Grid.GetRow(this) + (CollapseDirection == CollapseDirectionType.Top ? -1 : 1);
                if (index < 0 || index >= grid.RowDefinitions.Count) return;
                var target = grid.RowDefinitions[index];
                if (!IsCollapsed)
                {
                    _savedLength = target.Height;
                    _savedMinimum = target.MinHeight;
                    ExpandedPixelSize = target.Height.IsAbsolute ? target.Height.Value : target.ActualHeight;
                    target.MinHeight = 0;
                    target.Height = new GridLength(CollapsedSize);
                }
                else
                {
                    target.MinHeight = _savedMinimum;
                    target.Height = _savedLength;
                }
            }
            IsCollapsed = !IsCollapsed;
            UpdateArrowText();
        }

        /// <summary>折叠前的实际宽度或高度，用于保存可恢复的用户布局。</summary>
        public double ExpandedPixelSize { get; private set; }
        private GridLength _savedLength = new GridLength(1, GridUnitType.Star);
        private double _savedMinimum;
    }

    /// <summary>
    /// 折叠方向
    /// </summary>
    public enum CollapseDirectionType
    {
        Left,
        Right,
        Top,
        Bottom
    }
}
