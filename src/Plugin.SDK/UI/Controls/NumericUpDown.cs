using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 数值增减控件 - 类似WinForms NumericUpDown
    /// </summary>
    /// <remarks>
    /// 支持鼠标滚轮连续增减、键盘导航、按钮点击增减。
    /// 
    /// 使用示例：
    /// <code>
    /// &lt;!-- 绑定 double 类型 --&gt;
    /// &lt;controls:NumericUpDown Value="{Binding Price, Mode=TwoWay}"
    ///     DecimalPlaces="2" Minimum="0" Maximum="100" /&gt;
    /// 
    /// &lt;!-- 绑定 int 类型 --&gt;
    /// &lt;controls:NumericUpDown IntValue="{Binding Count, Mode=TwoWay}"
    ///     Minimum="0" Maximum="100" /&gt;
    /// </code>
    /// 
    /// 注意：只绑定 Value 或 IntValue 其中一个属性，不要同时绑定两个。
    /// </remarks>
    [TemplatePart(Name = PART_TextBox, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_IncreaseButton, Type = typeof(RepeatButton))]
    [TemplatePart(Name = PART_DecreaseButton, Type = typeof(RepeatButton))]
    [TemplatePart(Name = PART_Slider, Type = typeof(Slider))]
    [TemplatePart(Name = PART_SliderPopup, Type = typeof(Popup))]
    public class NumericUpDown : Control
    {
        private const string PART_TextBox = "PART_TextBox";
        private const string PART_IncreaseButton = "PART_IncreaseButton";
        private const string PART_DecreaseButton = "PART_DecreaseButton";
        private const string PART_Slider = "PART_Slider";
        private const string PART_SliderPopup = "PART_SliderPopup";

        private TextBox _textBox;
        private RepeatButton _increaseButton;
        private RepeatButton _decreaseButton;
        private Slider _slider;
        private Popup _sliderPopup;
        private bool _isUpdatingText;
        private bool _isSyncingValues; // 防止 Value 和 IntValue 循环更新

        static NumericUpDown()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDown),
                new FrameworkPropertyMetadata(typeof(NumericUpDown)));
        }

        #region 依赖属性

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericUpDown),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged, CoerceValue));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericUpDown),
                new PropertyMetadata(0.0, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericUpDown),
                new PropertyMetadata(100.0, OnRangeChanged));

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(NumericUpDown),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(NumericUpDown),
                new PropertyMetadata(10.0));

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(NumericUpDown),
                new PropertyMetadata(0, OnDecimalPlacesChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NumericUpDown),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowUpDownButtonsProperty =
            DependencyProperty.Register(nameof(ShowUpDownButtons), typeof(bool), typeof(NumericUpDown),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IntValueProperty =
            DependencyProperty.Register(nameof(IntValue), typeof(int), typeof(NumericUpDown),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIntValueChanged, CoerceIntValue));

        public static readonly DependencyProperty ShowSliderProperty =
            DependencyProperty.Register(nameof(ShowSlider), typeof(bool), typeof(NumericUpDown),
                new PropertyMetadata(true, OnShowSliderChanged));

        #endregion

        #region 路由事件

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(NumericUpDown));

        public event RoutedPropertyChangedEventHandler<double> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        #endregion

        #region 属性

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

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

        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public bool ShowUpDownButtons
        {
            get => (bool)GetValue(ShowUpDownButtonsProperty);
            set => SetValue(ShowUpDownButtonsProperty, value);
        }

        /// <summary>
        /// 整数值属性，方便绑定 int 类型。
        /// 注意：只绑定 Value 或 IntValue 其中一个，不要同时绑定两个。
        /// </summary>
        public int IntValue
        {
            get => (int)GetValue(IntValueProperty);
            set => SetValue(IntValueProperty, value);
        }

        /// <summary>
        /// 是否显示滑块（按焦点显示）
        /// </summary>
        /// <remarks>
        /// - ShowSlider=false：滑块功能关闭
        /// - ShowSlider=true：滑块按焦点显示（获得焦点时自动打开）
        /// </remarks>
        public bool ShowSlider
        {
            get => (bool)GetValue(ShowSliderProperty);
            set => SetValue(ShowSliderProperty, value);
        }

        #endregion

        #region 重写方法

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 取消旧事件订阅
            if (_textBox != null)
            {
                _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
                _textBox.PreviewMouseWheel -= OnTextBoxMouseWheel;
                _textBox.LostFocus -= OnTextBoxLostFocus;
                _textBox.GotFocus -= OnTextBoxGotFocus;
            }
            if (_slider != null)
            {
                _slider.GotFocus -= OnSliderGotFocus;
                _slider.LostFocus -= OnSliderLostFocus;
            }
            if (_increaseButton != null)
                _increaseButton.Click -= OnIncreaseButtonClick;
            if (_decreaseButton != null)
                _decreaseButton.Click -= OnDecreaseButtonClick;

            // 获取模板部件 (may be null before template is applied)
            _textBox = GetTemplateChild(PART_TextBox) as TextBox;
            _increaseButton = GetTemplateChild(PART_IncreaseButton) as RepeatButton;
            _decreaseButton = GetTemplateChild(PART_DecreaseButton) as RepeatButton;
            _slider = GetTemplateChild(PART_Slider) as Slider;
            _sliderPopup = GetTemplateChild(PART_SliderPopup) as Popup;

            // 诊断日志：检查模板部件是否获取成功
            VisionLogger.Instance.Log(LogLevel.Info, $"[OnApplyTemplate] 模板部件获取状态: TextBox={_textBox != null}, SliderPopup={_sliderPopup != null}, Slider={_slider != null}", "NumericUpDown");

            // 订阅新事件
            if (_textBox != null)
            {
                _textBox.PreviewKeyDown += OnTextBoxKeyDown;
                _textBox.PreviewMouseWheel += OnTextBoxMouseWheel;
                _textBox.LostFocus += OnTextBoxLostFocus;
                _textBox.GotFocus += OnTextBoxGotFocus;
                UpdateText();
            }
            if (_slider != null)
            {
                _slider.GotFocus += OnSliderGotFocus;
                _slider.LostFocus += OnSliderLostFocus;
            }
            if (_increaseButton != null)
                _increaseButton.Click += OnIncreaseButtonClick;
            if (_decreaseButton != null)
                _decreaseButton.Click += OnDecreaseButtonClick;

            // 在代码中设置 PlacementTarget，确保绑定成功
            if (_sliderPopup != null)
            {
                // ✅ 关键修改：将PlacementTarget设置为整个控件，而不是TextBox
                _sliderPopup.PlacementTarget = this;
                VisionLogger.Instance.Log(LogLevel.Success, 
                    $"[OnApplyTemplate] PlacementTarget设置成功: NumericUpDown控件", 
                    "NumericUpDown");

                // 订阅Popup事件，监控打开/关闭
                _sliderPopup.Opened += (s, e) =>
                {
                    var focusedElement = Keyboard.FocusedElement;
                    VisionLogger.Instance.Log(LogLevel.Success, 
                        $"[Popup.Opened] Popup已打开 | 当前焦点: {focusedElement?.GetType().Name ?? "null"} | IsKeyboardFocusWithin: {IsKeyboardFocusWithin} | Placement: {_sliderPopup.Placement}", 
                        "NumericUpDown");
                };
                _sliderPopup.Closed += (s, e) =>
                {
                    var focusedElement = Keyboard.FocusedElement;
                    VisionLogger.Instance.Log(LogLevel.Info, 
                        $"[Popup.Closed] Popup已关闭 | 当前焦点: {focusedElement?.GetType().Name ?? "null"} | IsKeyboardFocusWithin: {IsKeyboardFocusWithin} | StaysOpen: {_sliderPopup.StaysOpen}", 
                        "NumericUpDown");
                };
            }
            else
            {
                VisionLogger.Instance.Log(LogLevel.Warning, 
                    $"[OnApplyTemplate] PlacementTarget设置失败: SliderPopup={_sliderPopup != null}", 
                    "NumericUpDown");
            }

            // 初始化滑块状态（参考 RangeInputControl 的实现）
            UpdateSliderState();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            
            // 不在这里调用 UpdateSliderState()，由 IsKeyboardFocusWithinChanged 统一处理
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            
            // 不在这里调用 UpdateSliderState()，由 IsKeyboardFocusWithinChanged 统一处理
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (!IsReadOnly && IsFocused)
            {
                ChangeValue(e.Delta > 0 ? SmallChange : -SmallChange);
                e.Handled = true;
            }
            base.OnPreviewMouseWheel(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!IsReadOnly)
            {
                switch (e.Key)
                {
                    case Key.Up:
                        ChangeValue(SmallChange);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        ChangeValue(-SmallChange);
                        e.Handled = true;
                        break;
                    case Key.PageUp:
                        ChangeValue(LargeChange);
                        e.Handled = true;
                        break;
                    case Key.PageDown:
                        ChangeValue(-LargeChange);
                        e.Handled = true;
                        break;
                    case Key.Home:
                        Value = Minimum;
                        e.Handled = true;
                        break;
                    case Key.End:
                        Value = Maximum;
                        e.Handled = true;
                        break;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        #endregion

        #region 私有方法

        private void ChangeValue(double delta)
        {
            var newValue = Math.Round(Value + delta, DecimalPlaces);
            Value = Clamp(newValue);
        }

        private double Clamp(double value)
        {
            return Math.Max(Minimum, Math.Min(Maximum, value));
        }

        private void UpdateText()
        {
            if (_textBox != null && !_isUpdatingText)
            {
                _isUpdatingText = true;
                try
                {
                    string format = DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0";
                    _textBox.Text = Value.ToString(format);
                }
                finally
                {
                    _isUpdatingText = false;
                }
            }
        }

        private void UpdateSliderState()
        {
            // 检查模板是否已应用
            if (_sliderPopup == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, "[UpdateSliderState] Popup为null，无法更新状态", "NumericUpDown");
                return;
            }

            // 如果 ShowSlider=false，强制关闭 Popup
            // 如果 ShowSlider=true，Popup 由焦点事件控制（不在这里干预）
            if (!ShowSlider)
            {
                _sliderPopup.IsOpen = false;
                VisionLogger.Instance.Log(LogLevel.Info, "[UpdateSliderState] ShowSlider=false，强制关闭Popup", "NumericUpDown");
            }
        }

        private void ParseAndApplyText()
        {
            if (_textBox == null || _isUpdatingText) return;

            if (double.TryParse(_textBox.Text, out var newValue))
            {
                newValue = Math.Round(newValue, DecimalPlaces);
                Value = Clamp(newValue);
            }
            UpdateText();
        }

        #endregion

        #region 回调方法

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            control.UpdateText();
            control.RaiseEvent(new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue, ValueChangedEvent));

            // 同步 IntValue
            if (!control._isSyncingValues)
            {
                control._isSyncingValues = true;
                try
                {
                    control.IntValue = (int)Math.Round((double)e.NewValue);
                }
                finally
                {
                    control._isSyncingValues = false;
                }
            }
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            control.CoerceValue(ValueProperty);
        }

        private static void OnDecimalPlacesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            control.UpdateText();
        }

        private static void OnShowSliderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;
            control.UpdateSliderState();
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var control = (NumericUpDown)d;
            var value = (double)baseValue;
            return control.Clamp(value);
        }

        private static void OnIntValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericUpDown)d;

            // 同步 Value
            if (!control._isSyncingValues)
            {
                control._isSyncingValues = true;
                try
                {
                    control.Value = (int)e.NewValue;
                }
                finally
                {
                    control._isSyncingValues = false;
                }
            }
        }

        private static object CoerceIntValue(DependencyObject d, object baseValue)
        {
            var control = (NumericUpDown)d;
            var value = (int)baseValue;
            var clamped = control.Clamp(value);
            return (int)Math.Round(clamped);
        }

        #endregion

        #region 事件处理

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ParseAndApplyText();
                e.Handled = true;
            }
        }

        private void OnTextBoxMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsReadOnly)
            {
                ChangeValue(e.Delta > 0 ? SmallChange : -SmallChange);
                e.Handled = true;
            }
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            var focusedElement = Keyboard.FocusedElement;
            VisionLogger.Instance.Log(LogLevel.Info, 
                $"[OnTextBoxGotFocus] TextBox获得焦点 | 当前焦点元素: {focusedElement?.GetType().Name ?? "null"} | IsKeyboardFocusWithin: {IsKeyboardFocusWithin}", 
                "NumericUpDown");
            
            // TextBox 获得焦点时，直接打开 Popup
            if (ShowSlider && _sliderPopup != null)
            {
                VisionLogger.Instance.Log(LogLevel.Info, 
                    $"[OnTextBoxGotFocus] 准备打开Popup | ShowSlider: {ShowSlider} | Popup.IsOpen: {_sliderPopup.IsOpen} | StaysOpen: {_sliderPopup.StaysOpen}", 
                    "NumericUpDown");
                
                _sliderPopup.IsOpen = true;
                
                VisionLogger.Instance.Log(LogLevel.Success, 
                    $"[OnTextBoxGotFocus] Popup已打开 | IsOpen: {_sliderPopup.IsOpen}", 
                    "NumericUpDown");
            }
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var focusedElement = Keyboard.FocusedElement;
            VisionLogger.Instance.Log(LogLevel.Info, 
                $"[OnTextBoxLostFocus] TextBox失去焦点 | 新焦点元素: {focusedElement?.GetType().Name ?? "null"} | IsKeyboardFocusWithin: {IsKeyboardFocusWithin}", 
                "NumericUpDown");
            
            // 立即解析 TextBox 中的文本（同步执行，确保获取最新输入值）
            ParseAndApplyText();
            
            // 延迟检查焦点状态（给焦点转移时间）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var currentFocused = Keyboard.FocusedElement;
                VisionLogger.Instance.Log(LogLevel.Info, 
                    $"[OnTextBoxLostFocus-延迟检查] 当前焦点: {currentFocused?.GetType().Name ?? "null"} | IsKeyboardFocusWithin: {IsKeyboardFocusWithin} | Popup.IsOpen: {_sliderPopup?.IsOpen}", 
                    "NumericUpDown");
                
                // 检查焦点是否在控件内部（包括 TextBox 和 Slider）
                if (IsKeyboardFocusWithin)
                {
                    VisionLogger.Instance.Log(LogLevel.Info, "[OnTextBoxLostFocus] 焦点仍在控件内部，保持Popup打开", "NumericUpDown");
                    return;
                }

                // 焦点已离开控件，直接关闭 Popup
                if (_sliderPopup != null && _sliderPopup.IsOpen)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning, 
                        $"[OnTextBoxLostFocus] 焦点已离开控件，准备关闭Popup | StaysOpen: {_sliderPopup.StaysOpen}", 
                        "NumericUpDown");
                    _sliderPopup.IsOpen = false;
                    VisionLogger.Instance.Log(LogLevel.Info, "[OnTextBoxLostFocus] Popup已关闭", "NumericUpDown");
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnSliderGotFocus(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Info, $"[OnSliderGotFocus] Slider获得焦点", "NumericUpDown");
            // Slider 获得焦点，Popup 已经打开，无需操作
        }

        private void OnSliderLostFocus(object sender, RoutedEventArgs e)
        {
            VisionLogger.Instance.Log(LogLevel.Info, $"[OnSliderLostFocus] Slider失去焦点", "NumericUpDown");
            
            // 延迟检查
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 检查焦点是否在控件内部（包括 TextBox 和 Slider）
                if (IsKeyboardFocusWithin)
                {
                    VisionLogger.Instance.Log(LogLevel.Info, "[OnSliderLostFocus] 焦点仍在控件内部，保持Popup打开", "NumericUpDown");
                    return;
                }

                // 焦点已离开控件，直接关闭 Popup
                if (_sliderPopup != null && _sliderPopup.IsOpen)
                {
                    _sliderPopup.IsOpen = false;
                    VisionLogger.Instance.Log(LogLevel.Info, "[OnSliderLostFocus] 焦点已离开控件，Popup已关闭", "NumericUpDown");
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnIncreaseButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsReadOnly)
            {
                ChangeValue(SmallChange);
            }
        }

        private void OnDecreaseButtonClick(object sender, RoutedEventArgs e)
        {
            if (!IsReadOnly)
            {
                ChangeValue(-SmallChange);
            }
        }

        #endregion
    }
}
