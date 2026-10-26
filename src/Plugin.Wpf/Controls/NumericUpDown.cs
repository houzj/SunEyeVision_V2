// 从旧SDK提取的纯WPF基础交互；不持有来源、执行或工具状态。
using System.Windows.Media;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;



namespace SunEyeVision.Plugin.Wpf.Controls
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
        public TextBox? TextEditor => _textBox;
        public TypeCode IntegerType { get; set; } = TypeCode.Empty;
        private RepeatButton _increaseButton;
        private RepeatButton _decreaseButton;
        private Slider _slider;
        private Popup _sliderPopup;
        private bool _isUpdatingText;
        private bool _hasPendingText;
        private bool _isSyncingValues; // 防止 Value 和 IntValue 循环更新

        static NumericUpDown()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDown),
                new FrameworkPropertyMetadata(typeof(NumericUpDown)));
            BackgroundProperty.OverrideMetadata(typeof(NumericUpDown),
                new FrameworkPropertyMetadata(Brushes.White));
            BorderBrushProperty.OverrideMetadata(typeof(NumericUpDown),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(204,199,198))));
            ForegroundProperty.OverrideMetadata(typeof(NumericUpDown),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(102,102,102))));
        }

        public NumericUpDown()
        {
            SetResourceReference(StyleProperty, typeof(NumericUpDown));
            Unloaded += (_, _) => { if (_sliderPopup is not null) _sliderPopup.IsOpen=false; };
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
                _textBox.TextChanged -= OnPendingTextChanged;
                _textBox.PreviewKeyDown -= OnTextBoxKeyDown;
                _textBox.PreviewMouseWheel -= OnTextBoxMouseWheel;
                _textBox.LostFocus -= OnTextBoxLostFocus;
                _textBox.GotFocus -= OnTextBoxGotFocus;
            }
            if (_slider != null)
            {
                _slider.GotFocus -= OnSliderGotFocus;
                _slider.LostFocus -= OnSliderLostFocus;
                _slider.PreviewKeyDown -= OnSliderKeyDown;
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
            

            // 订阅新事件
            if (_textBox != null)
            {
                _textBox.TextChanged += OnPendingTextChanged;
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
                _slider.PreviewKeyDown += OnSliderKeyDown;
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
                

                // 订阅Popup事件，监控打开/关闭
                _sliderPopup.Opened += (s, e) =>
                {
                    var focusedElement = Keyboard.FocusedElement;
                    
                };
                _sliderPopup.Closed += (s, e) =>
                {
                    var focusedElement = Keyboard.FocusedElement;
                    
                };
            }
            else
            {
                
            }

            // 初始化滑块状态（参考 RangeInputControl 的实现）
            UpdateSliderState();

            // ===== [Diag] 汇总 NumericUpDown 模板初始化状态 =====
            if (_slider != null)
            {
                
            }
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
            if (!IsReadOnly && (IsKeyboardFocusWithin || _slider?.IsKeyboardFocusWithin==true))
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
                    case Key.Escape:
                        if(_sliderPopup?.IsOpen==true){_sliderPopup.IsOpen=false;e.Handled=true;}
                        break;
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
                        SetCurrentValue(ValueProperty, Minimum);
                        e.Handled = true;
                        break;
                    case Key.End:
                        SetCurrentValue(ValueProperty, Maximum);
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
            SetCurrentValue(ValueProperty, Clamp(newValue));
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
                    // DecimalPlaces controls the preferred display, never the stored precision.
                    string format = Math.Round(Value,DecimalPlaces)==Value ? $"F{DecimalPlaces}" : "R";
                    _textBox.Text = Value.ToString(format,System.Globalization.CultureInfo.InvariantCulture);
                    _hasPendingText = false;
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
                
                return;
            }

            // 如果 ShowSlider=false，强制关闭 Popup
            // 如果 ShowSlider=true，Popup 由焦点事件控制（不在这里干预）
            if (!ShowSlider)
            {
                _sliderPopup.IsOpen = false;
                
            }
        }

        private void OnPendingTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;
            _hasPendingText = true;
            if (!TryReadPendingNumber(out var value, out _)) return;
            // Preview a valid typed value in the slider without reformatting the input or moving its caret.
            _isUpdatingText = true;
            try { SetCurrentValue(ValueProperty, value); }
            finally { _isUpdatingText = false; }
        }

        /// <summary>Check the actual template input without changing its text or numeric value.</summary>
        public bool HasPendingEdit => _hasPendingText && !IsReadOnly && IsEnabled;
        public bool TryValidatePendingEdit(out string error) => TryReadPendingNumber(out _, out error);
        public bool TryCommitPendingEdit(out string error)
        {
            if (!TryReadPendingNumber(out var value, out error)) return false;
            if (_hasPendingText && !IsReadOnly && IsEnabled)
            {
                SetCurrentValue(ValueProperty, value);
                UpdateText();
            }
            return true;
        }
        public void DiscardPendingEdit() => UpdateText();
        internal void AcceptInitialDisplayText()
        {
            if(TryReadPendingNumber(out var value,out _)&&value==Value)_hasPendingText=false;
        }
        private bool TryReadPendingNumber(out double value, out string error)
        {
            Dispatcher.VerifyAccess(); value = Value; error = "";
            if (!_hasPendingText || _textBox is null || IsReadOnly || !IsEnabled) return true;
            var integerType = System.Windows.Data.BindingOperations.IsDataBound(this, IntValueProperty) ? TypeCode.Int32 :
                IntegerType;
            double entered;
            if (integerType is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64)
            {
                // Parse the integer before any double conversion: 2^53+1 must never round into 2^53.
                if (!System.Numerics.BigInteger.TryParse(_textBox.Text,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var exact))
                { error = "此参数需要格式正确的精确整数。"; return false; }
                var (minimum, maximum) = integerType switch
                {
                    TypeCode.SByte => ((System.Numerics.BigInteger)sbyte.MinValue, (System.Numerics.BigInteger)sbyte.MaxValue),
                    TypeCode.Byte => ((System.Numerics.BigInteger)byte.MinValue, (System.Numerics.BigInteger)byte.MaxValue),
                    TypeCode.Int16 => ((System.Numerics.BigInteger)short.MinValue, (System.Numerics.BigInteger)short.MaxValue),
                    TypeCode.UInt16 => ((System.Numerics.BigInteger)ushort.MinValue, (System.Numerics.BigInteger)ushort.MaxValue),
                    TypeCode.Int32 => ((System.Numerics.BigInteger)int.MinValue, (System.Numerics.BigInteger)int.MaxValue),
                    TypeCode.UInt32 => ((System.Numerics.BigInteger)uint.MinValue, (System.Numerics.BigInteger)uint.MaxValue),
                    TypeCode.Int64 => ((System.Numerics.BigInteger)long.MinValue, (System.Numerics.BigInteger)long.MaxValue),
                    _ => ((System.Numerics.BigInteger)ulong.MinValue, (System.Numerics.BigInteger)ulong.MaxValue)
                };
                if (exact < minimum || exact > maximum)
                { error = "整数超出参数声明的 CLR 范围。"; return false; }
                entered = (double)exact;
                if (!double.IsFinite(entered) || new System.Numerics.BigInteger(entered) != exact)
                { error = "此整数无法无损通过当前数值控件，请保留精确输入并使用支持该范围的编辑器。"; return false; }
            }
            else if (!double.TryParse(_textBox.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture, out entered) || !double.IsFinite(entered))
            { error = "数值输入无效，请输入有限数值。"; return false; }
            if (entered < Minimum || entered > Maximum)
            { error = "数值超出允许范围。"; return false; }
            value = entered;
            if (!double.IsFinite(value) || value < Minimum || value > Maximum)
            { error = "数值精度或范围无效。"; return false; }
            return true;
        }
        private void ParseAndApplyText() => TryCommitPendingEdit(out _);
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
                    control.SetCurrentValue(IntValueProperty, (int)Math.Round((double)e.NewValue));
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
                    control.SetCurrentValue(ValueProperty, (double)(int)e.NewValue);
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
                e.Handled = true;
                if (!TryCommitPendingEdit(out _)) return;
                if (_sliderPopup != null) _sliderPopup.IsOpen = false;
                if (_textBox != null) FocusManager.SetFocusedElement(FocusManager.GetFocusScope(_textBox), null);
                Keyboard.ClearFocus();
            }
        }

        private void OnTextBoxMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsReadOnly && (IsKeyboardFocusWithin || _slider?.IsKeyboardFocusWithin==true))
            {
                ChangeValue(e.Delta > 0 ? SmallChange : -SmallChange);
                e.Handled = true;
            }
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            var focusedElement = Keyboard.FocusedElement;
            
            
            // TextBox 获得焦点时，直接打开 Popup
            if (ShowSlider && _sliderPopup != null)
            {
                
                
                _sliderPopup.IsOpen = true;
                
                
            }

            // ===== [Diag] Slider Popup 打开后汇总 =====
            if (_slider != null)
            {
                
            }
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var focusedElement = Keyboard.FocusedElement;
            
            
            // 立即解析 TextBox 中的文本（同步执行，确保获取最新输入值）
            ParseAndApplyText();
            
            // 延迟检查焦点状态（给焦点转移时间）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var currentFocused = Keyboard.FocusedElement;
                
                
                // 检查焦点是否在控件内部（包括 TextBox 和 Slider）
                if (IsKeyboardFocusWithin || _slider?.IsKeyboardFocusWithin==true)
                {
                    
                    return;
                }

                // 焦点已离开控件，直接关闭 Popup
                if (_sliderPopup != null && _sliderPopup.IsOpen)
                {
                    
                    _sliderPopup.IsOpen = false;
                    
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnSliderGotFocus(object sender, RoutedEventArgs e)
        {
            
            // Slider 获得焦点，Popup 已经打开，无需操作
        }

        private void OnSliderLostFocus(object sender, RoutedEventArgs e)
        {
            
            
            // 延迟检查
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 检查焦点是否在控件内部（包括 TextBox 和 Slider）
                if (IsKeyboardFocusWithin || _slider?.IsKeyboardFocusWithin==true)
                {
                    
                    return;
                }

                // 焦点已离开控件，直接关闭 Popup
                if (_sliderPopup != null && _sliderPopup.IsOpen)
                {
                    _sliderPopup.IsOpen = false;
                    
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnSliderKeyDown(object sender,KeyEventArgs e)
        {if(e.Key==Key.Escape){if(_sliderPopup is not null)_sliderPopup.IsOpen=false;e.Handled=true;}}
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
