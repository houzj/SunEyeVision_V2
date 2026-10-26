// 从旧SDK提取的纯WPF基础交互；不持有来源、执行或工具状态。
using System.Windows.Media;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;


namespace SunEyeVision.Plugin.Wpf.Controls
{
    /// <summary>
    /// 范围输入控件 - 包含最小值/最大值输入框和范围滑块（核心控件，不包含标签和外框）
    /// </summary>
    /// <remarks>
    /// 封装了范围选择的核心UI，包含两个数值输入框和一个双滑块。
    /// 滑块默认隐藏，当输入框获得焦点时自动展开。
    /// 
    /// 注意：此控件只包含核心功能，标签和布局由工具层UI负责。
    /// 
    /// 使用示例：
    /// <code>
    /// &lt;controls:RangeInputControl
    ///     Unit="%"
    ///     Minimum="0"
    ///     Maximum="100"
    ///     MinValue="{Binding MinRatio, Mode=TwoWay}"
    ///     MaxValue="{Binding MaxRatio, Mode=TwoWay}"
    ///     DecimalPlaces="1" /&gt;
    /// </code>
    /// </remarks>
    [TemplatePart(Name = PART_MinNumeric, Type = typeof(NumericUpDown))]
    [TemplatePart(Name = PART_MaxNumeric, Type = typeof(NumericUpDown))]
    [TemplatePart(Name = PART_RangeSlider, Type = typeof(RangeSlider))]
    [TemplatePart(Name = PART_SliderPopup, Type = typeof(Popup))]
    public class RangeInputControl : Control
    {
        private const string PART_MinNumeric = "PART_MinNumeric";
        private const string PART_MaxNumeric = "PART_MaxNumeric";
        private const string PART_RangeSlider = "PART_RangeSlider";
        private const string PART_SliderPopup = "PART_SliderPopup";

        private NumericUpDown _minNumeric;
        private NumericUpDown _maxNumeric;
        private RangeSlider _rangeSlider;
        private Popup _sliderPopup;
        private bool _isUpdating;

        static RangeInputControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RangeInputControl),
                new FrameworkPropertyMetadata(typeof(RangeInputControl)));
        }

        public RangeInputControl()
        {
            SetResourceReference(StyleProperty, typeof(RangeInputControl));
            Unloaded += (_, _) => { if (_sliderPopup is not null) _sliderPopup.IsOpen=false; };
        }

        #region 依赖属性

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(RangeInputControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(0.0, OnRangePropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(100.0, OnRangePropertyChanged));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(RangeInputControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnMinValueChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(RangeInputControl),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnMaxValueChanged));

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(RangeInputControl),
                new PropertyMetadata(0, OnDecimalPlacesChanged));

        public static readonly DependencyProperty ShowSliderProperty =
            DependencyProperty.Register(nameof(ShowSlider), typeof(bool), typeof(RangeInputControl),
                new PropertyMetadata(true, OnShowSliderChanged));

        public static readonly DependencyProperty MinGapProperty =
            DependencyProperty.Register(nameof(MinGap), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(0.0, OnMinGapChanged));

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(10.0));

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(RangeInputControl),
                new PropertyMetadata(true, OnIsVisibleChanged));

        public static readonly DependencyProperty NumericWidthProperty =
            DependencyProperty.Register(nameof(NumericWidth), typeof(double), typeof(RangeInputControl),
                new PropertyMetadata(100.0));

        #endregion

        #region 路由事件

        public static readonly RoutedEvent MinValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(MinValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(RangeInputControl));

        public static readonly RoutedEvent MaxValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(MaxValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(RangeInputControl));

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

        /// <summary>
        /// 单位文本
        /// </summary>
        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        /// <summary>
        /// 整体范围最小值
        /// </summary>
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>
        /// 整体范围最大值
        /// </summary>
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>
        /// 当前最小值
        /// </summary>
        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        /// <summary>
        /// 当前最大值
        /// </summary>
        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        /// <summary>
        /// 小数位数
        /// </summary>
        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        /// <summary>
        /// 是否显示滑块
        /// </summary>
        public bool ShowSlider
        {
            get => (bool)GetValue(ShowSliderProperty);
            set => SetValue(ShowSliderProperty, value);
        }

        /// <summary>
        /// 最小值和最大值之间的最小间隔
        /// </summary>
        public double MinGap
        {
            get => (double)GetValue(MinGapProperty);
            set => SetValue(MinGapProperty, value);
        }

        /// <summary>
        /// 小步进值
        /// </summary>
        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        /// <summary>
        /// 大步进值
        /// </summary>
        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        /// <summary>
        /// 控件是否可见（便捷属性，自动同步到 Visibility）
        /// </summary>
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        /// <summary>
        /// 数值输入框宽度（默认100px）
        /// </summary>
        public double NumericWidth
        {
            get => (double)GetValue(NumericWidthProperty);
            set => SetValue(NumericWidthProperty, value);
        }

        #endregion

        #region 重写方法

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 取消旧事件订阅
            if (_minNumeric != null)
            {
                _minNumeric.GotFocus -= OnNumericGotFocus;
                _minNumeric.LostFocus -= OnNumericLostFocus;
                _minNumeric.ValueChanged -= OnMinNumericValueChanged;
            }
            if (_maxNumeric != null)
            {
                _maxNumeric.GotFocus -= OnNumericGotFocus;
                _maxNumeric.LostFocus -= OnNumericLostFocus;
                _maxNumeric.ValueChanged -= OnMaxNumericValueChanged;
            }
            if (_rangeSlider != null)
            {
                _rangeSlider.MinValueChanged -= OnSliderMinValueChanged;
                _rangeSlider.MaxValueChanged -= OnSliderMaxValueChanged;
            }

            // 获取模板部件
            _minNumeric = GetTemplateChild(PART_MinNumeric) as NumericUpDown;
            _maxNumeric = GetTemplateChild(PART_MaxNumeric) as NumericUpDown;
            _rangeSlider = GetTemplateChild(PART_RangeSlider) as RangeSlider;
            _sliderPopup = GetTemplateChild(PART_SliderPopup) as Popup;

            // 诊断日志：检查模板部件是否获取成功
            

            // 订阅新事件
            if (_minNumeric != null)
            {
                _minNumeric.GotFocus += OnNumericGotFocus;
                _minNumeric.LostFocus += OnNumericLostFocus;
                _minNumeric.ValueChanged += OnMinNumericValueChanged;
            }
            if (_maxNumeric != null)
            {
                _maxNumeric.GotFocus += OnNumericGotFocus;
                _maxNumeric.LostFocus += OnNumericLostFocus;
                _maxNumeric.ValueChanged += OnMaxNumericValueChanged;
            }
            if (_rangeSlider != null)
            {
                _rangeSlider.MinValueChanged += OnSliderMinValueChanged;
                _rangeSlider.MaxValueChanged += OnSliderMaxValueChanged;
            }

            // 在代码中设置 PlacementTarget，确保绑定成功（方案2）
            if (_sliderPopup != null && _minNumeric != null)
            {
                _sliderPopup.PlacementTarget = _minNumeric;
                

                // 订阅Popup事件，监控打开/关闭
            }
            else
            {
                
            }

            UpdateSliderState();
        }

        #endregion

        #region 私有方法

        private void UpdateSliderState()
        {
            if (_minNumeric == null || _maxNumeric == null || _sliderPopup == null)
            {
                
                return;
            }

            // 使用 IsKeyboardFocusWithin 检查焦点（更可靠，避免事件顺序问题）
            var hasFocus = _minNumeric.IsKeyboardFocusWithin || _maxNumeric.IsKeyboardFocusWithin;
            var shouldShow = ShowSlider && hasFocus;

            // 诊断日志：详细的状态信息
            

            // 直接控制Popup的打开/关闭
            _sliderPopup.IsOpen = shouldShow;

            // 诊断日志：Popup状态变化结果
            
        }

        private double ClampMinValue(double value)
        {
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            return Math.Min(value, MaxValue - MinGap);
        }

        private double ClampMaxValue(double value)
        {
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            return Math.Max(value, MinValue + MinGap);
        }

        #endregion

        #region 回调方法

        private static void OnRangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            control.CoerceValue(MinValueProperty);
            control.CoerceValue(MaxValueProperty);
        }

        private static void OnMinValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            if (control._isUpdating) return;

            control._isUpdating = true;
            try
            {
                control.CoerceValue(MaxValueProperty);
                control.RaiseEvent(new RoutedPropertyChangedEventArgs<double>(
                    (double)e.OldValue, (double)e.NewValue, MinValueChangedEvent));
            }
            finally
            {
                control._isUpdating = false;
            }
        }

        private static void OnMaxValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            if (control._isUpdating) return;

            control._isUpdating = true;
            try
            {
                control.CoerceValue(MinValueProperty);
                control.RaiseEvent(new RoutedPropertyChangedEventArgs<double>(
                    (double)e.OldValue, (double)e.NewValue, MaxValueChangedEvent));
            }
            finally
            {
                control._isUpdating = false;
            }
        }

        private static void OnDecimalPlacesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // DecimalPlaces 通过绑定自动更新到子控件
        }

        private static void OnShowSliderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            control.UpdateSliderState();
        }

        private static void OnMinGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            control.CoerceValue(MinValueProperty);
            control.CoerceValue(MaxValueProperty);
        }

        private static void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RangeInputControl)d;
            control.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region 事件处理

        private void OnNumericGotFocus(object sender, RoutedEventArgs e)
        {
            var numeric = sender as NumericUpDown;
            
            UpdateSliderState();
        }

        private void OnNumericLostFocus(object sender, RoutedEventArgs e)
        {
            var numeric = sender as NumericUpDown;
            
            UpdateSliderState();
        }

        private void OnMinNumericValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                MinValue = ClampMinValue(e.NewValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnMaxNumericValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                MaxValue = ClampMaxValue(e.NewValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnSliderMinValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                MinValue = ClampMinValue(e.NewValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnSliderMaxValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                MaxValue = ClampMaxValue(e.NewValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        #endregion
    }
}