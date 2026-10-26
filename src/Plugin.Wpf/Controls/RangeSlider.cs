// 从旧SDK提取的纯WPF基础交互；不持有来源、执行或工具状态。
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Controls
{
    /// <summary>
    /// 双滑块范围选择控件
    /// </summary>
    /// <remarks>
    /// 支持选择一个数值范围，包含最小值和最大值两个滑块。
    /// 
    /// 使用示例：
    /// <code>
    /// &lt;controls:RangeSlider Minimum="0" Maximum="100"
    ///     MinValue="{Binding MinThreshold, Mode=TwoWay}"
    ///     MaxValue="{Binding MaxThreshold, Mode=TwoWay}" /&gt;
    /// </code>
    /// </remarks>
    [TemplatePart(Name = PART_Track, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PART_RangeFill, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PART_MinThumb, Type = typeof(Thumb))]
    [TemplatePart(Name = PART_MaxThumb, Type = typeof(Thumb))]
    public class RangeSlider : Control
    {
        private const string PART_Track = "PART_Track";
        private const string PART_RangeFill = "PART_RangeFill";
        private const string PART_MinThumb = "PART_MinThumb";
        private const string PART_MaxThumb = "PART_MaxThumb";
        private const string PART_Canvas = "PART_Canvas";

        private FrameworkElement _track;
        private FrameworkElement _rangeFill;
        private Thumb _minThumb;
        private Thumb _maxThumb;
        private Canvas _canvas;
        private bool _isDragging;
        private double _dragStartValue;

        static RangeSlider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RangeSlider),
                new FrameworkPropertyMetadata(typeof(RangeSlider)));
        }

        #region 依赖属性

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeSlider),
                new PropertyMetadata(0.0, OnRangePropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeSlider),
                new PropertyMetadata(100.0, OnRangePropertyChanged));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(RangeSlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnMinValueChanged, CoerceMinValue));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(RangeSlider),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnMaxValueChanged, CoerceMaxValue));

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(RangeSlider),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(RangeSlider),
                new PropertyMetadata(10.0));

        public static readonly DependencyProperty MinGapProperty =
            DependencyProperty.Register(nameof(MinGap), typeof(double), typeof(RangeSlider),
                new PropertyMetadata(0.0));

        #endregion

        #region 路由事件

        public static readonly RoutedEvent MinValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(MinValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(RangeSlider));

        public static readonly RoutedEvent MaxValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(MaxValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(RangeSlider));

        public event RoutedPropertyChangedEventHandler<double> MinValueChanged
        {
            add => AddHandler(MinValueChangedEvent, value);
            remove => RemoveHandler(MinValueChangedEvent, value);
        }

        public event RoutedPropertyChangedEventHandler<double> MaxValueChanged
        {
            add => AddHandler(MaxValueChangedEvent, value);
            remove => RemoveHandler(MaxValueChangedEvent, value);
        }

        #endregion

        #region 属性

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        /// <summary>
        /// 最小值和最大值之间的最小间隔
        /// </summary>
        public double MinGap
        {
            get => (double)GetValue(MinGapProperty);
            set => SetValue(MinGapProperty, value);
        }

        #endregion

        #region 重写方法

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 取消旧事件订阅
            if (_minThumb != null)
            {
                _minThumb.DragStarted -= OnMinThumbDragStarted;
                _minThumb.DragDelta -= OnMinThumbDragDelta;
                _minThumb.DragCompleted -= OnThumbDragCompleted;
            }
            if (_maxThumb != null)
            {
                _maxThumb.DragStarted -= OnMaxThumbDragStarted;
                _maxThumb.DragDelta -= OnMaxThumbDragDelta;
                _maxThumb.DragCompleted -= OnThumbDragCompleted;
            }

            // 获取模板部件
            _canvas = GetTemplateChild(PART_Canvas) as Canvas;
            _track = GetTemplateChild(PART_Track) as FrameworkElement;
            _rangeFill = GetTemplateChild(PART_RangeFill) as FrameworkElement;
            _minThumb = GetTemplateChild(PART_MinThumb) as Thumb;
            _maxThumb = GetTemplateChild(PART_MaxThumb) as Thumb;

            // 订阅新事件
            if (_minThumb != null)
            {
                _minThumb.DragStarted += OnMinThumbDragStarted;
                _minThumb.DragDelta += OnMinThumbDragDelta;
                _minThumb.DragCompleted += OnThumbDragCompleted;
            }
            if (_maxThumb != null)
            {
                _maxThumb.DragStarted += OnMaxThumbDragStarted;
                _maxThumb.DragDelta += OnMaxThumbDragDelta;
                _maxThumb.DragCompleted += OnThumbDragCompleted;
            }

            UpdateVisualState();
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (_isDragging)
            {
                e.Handled = true;
                return;
            }

            // 根据鼠标位置决定调整哪个滑块
            var position = e.GetPosition(_track);
            var minDistance = Math.Abs(position.X - GetThumbPosition(MinValue));
            var maxDistance = Math.Abs(position.X - GetThumbPosition(MaxValue));

            if (minDistance <= maxDistance)
            {
                MinValue = ClampValue(MinValue + (e.Delta > 0 ? SmallChange : -SmallChange));
            }
            else
            {
                MaxValue = ClampValue(MaxValue + (e.Delta > 0 ? SmallChange : -SmallChange));
            }

            e.Handled = true;
            base.OnPreviewMouseWheel(e);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var result = base.ArrangeOverride(arrangeBounds);
            UpdateVisualState();
            return result;
        }

        #endregion

        #region 私有方法

        private double ClampValue(double value)
        {
            return Math.Max(Minimum, Math.Min(Maximum, value));
        }

        private double GetThumbPosition(double value)
        {
            // 使用 Canvas 的宽度，而不是 _track 的宽度
            if (_canvas == null || Maximum <= Minimum) return 0;
            var range = Maximum - Minimum;
            var ratio = (value - Minimum) / range;
            return _canvas.ActualWidth * ratio;
        }

        private double GetValueFromPosition(double position)
        {
            // 使用 Canvas 的宽度，而不是 _track 的宽度
            if (_canvas == null || Maximum <= Minimum) return Minimum;
            var ratio = position / _canvas.ActualWidth;
            ratio = Math.Max(0, Math.Min(1, ratio));
            return Minimum + (Maximum - Minimum) * ratio;
        }

        private void UpdateVisualState()
        {
            if (_track == null || _rangeFill == null || _canvas == null) return;

            var minPos = GetThumbPosition(MinValue);
            var maxPos = GetThumbPosition(MaxValue);

            // 更新填充区域位置
            Canvas.SetLeft(_rangeFill, minPos);
            _rangeFill.Width = Math.Max(0, maxPos - minPos);

            // 更新滑块位置（居中对齐）
            if (_minThumb != null)
            {
                Canvas.SetLeft(_minThumb, minPos - _minThumb.Width / 2);
            }
            if (_maxThumb != null)
            {
                Canvas.SetLeft(_maxThumb, maxPos - _maxThumb.Width / 2);
            }
        }

        #endregion

        #region 回调方法

        private static void OnRangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (RangeSlider)d;
            slider.CoerceValue(MinValueProperty);
            slider.CoerceValue(MaxValueProperty);
            slider.UpdateVisualState();
        }

        private static void OnMinValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (RangeSlider)d;
            slider.UpdateVisualState();
            slider.RaiseEvent(new RoutedPropertyChangedEventArgs<double>(
                (double)e.OldValue, (double)e.NewValue, MinValueChangedEvent));
        }

        private static void OnMaxValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (RangeSlider)d;
            slider.UpdateVisualState();
            slider.RaiseEvent(new RoutedPropertyChangedEventArgs<double>(
                (double)e.OldValue, (double)e.NewValue, MaxValueChangedEvent));
        }

        private static object CoerceMinValue(DependencyObject d, object baseValue)
        {
            var slider = (RangeSlider)d;
            var value = slider.ClampValue((double)baseValue);
            // 确保最小值不超过最大值减去最小间隔
            return Math.Min(value, slider.MaxValue - slider.MinGap);
        }

        private static object CoerceMaxValue(DependencyObject d, object baseValue)
        {
            var slider = (RangeSlider)d;
            var value = slider.ClampValue((double)baseValue);
            // 确保最大值不小于最小值加上最小间隔
            return Math.Max(value, slider.MinValue + slider.MinGap);
        }

        #endregion

        #region 事件处理

        private void OnMinThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
            _dragStartValue = MinValue;
        }

        private void OnMaxThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
            _dragStartValue = MaxValue;
        }

        private void OnMinThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_track == null) return;

            var currentPos = GetThumbPosition(MinValue);
            var newPos = currentPos + e.HorizontalChange;
            var newValue = GetValueFromPosition(newPos);

            MinValue = ClampValue(Math.Min(newValue, MaxValue - MinGap));
        }

        private void OnMaxThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_track == null) return;

            var currentPos = GetThumbPosition(MaxValue);
            var newPos = currentPos + e.HorizontalChange;
            var newValue = GetValueFromPosition(newPos);

            MaxValue = ClampValue(Math.Max(newValue, MinValue + MinGap));
        }

        private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
        }

        #endregion
    }
}