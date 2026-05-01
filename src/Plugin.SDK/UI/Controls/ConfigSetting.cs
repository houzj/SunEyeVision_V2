using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using System.Collections.Specialized;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 支持常量/绑定模式切换的多类型参数控件（核心控件，不包含标签和外框）
    /// </summary>
    /// <remarks>
    /// 核心概念：
    /// 1. 绑定类型（BindingType）：回答"值从哪里来" - Constant 或 Binding
    /// 2. 数据类型（DataType）：回答"值是什么类型" - Int, Double, String, Bool 等
    /// 3. 绑定类型与数据类型正交，任何数据类型都可以是常量或绑定
    ///
    /// V2.0 重构要点：
    /// 1. 继承 ParamBindingBase，专注于参数值编辑功能
    /// 2. 单一值属性 Value (object) - 统一入口，支持任意类型
    /// 3. 内部计算属性 InternalNumericValue (只读double) - 用于模板绑定，自动类型转换
    /// 4. 统一范围属性 Minimum/Maximum (object) - 支持任意数值类型
    /// 5. 类型适配器模式 - 自动处理 int ↔ double 转换
    ///
    /// 注意：此控件只包含核心功能，标签和布局由工具层UI负责。
    /// </remarks>
    public class ConfigSetting : ParamBindingBase
    {
        #region 依赖属性

        // ===== 参数名（用于参数绑定） =====
        public static readonly DependencyProperty ParameterNameProperty =
            DependencyProperty.Register(nameof(ParameterName), typeof(string), typeof(ConfigSetting),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BindingTypeProperty =
            DependencyProperty.Register(nameof(BindingType), typeof(BindingType), typeof(ConfigSetting),
                new FrameworkPropertyMetadata(BindingType.Constant, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindingTypeChanged));

        // ===== 绑定源 =====
        public static readonly DependencyProperty BindingSourceProperty =
            DependencyProperty.Register(nameof(BindingSource), typeof(string), typeof(ConfigSetting),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindingSourceChanged));

        // ===== 绑定显示文本（只读，用于UI显示） =====
        private static readonly DependencyPropertyKey BindingDisplayTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BindingDisplayText), typeof(string), typeof(ConfigSetting),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BindingDisplayTextProperty = BindingDisplayTextPropertyKey.DependencyProperty;

        // ===== 单一值属性（统一入口） =====
        /// <summary>
        /// 值属性：统一的值入口，支持任意类型
        /// </summary>
        /// <remarks>
        /// - DataType = Int: Value 存储 int 类型
        /// - DataType = Double: Value 存储 double 类型
        /// - DataType = String: Value 存储 string 类型
        /// - DataType = Bool: Value 存储 bool 类型
        /// </remarks>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(object), typeof(ConfigSetting),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        // ===== 内部数值值（只读，用于模板绑定） =====
        /// <summary>
        /// 内部数值值：只读属性，由 Value 自动计算
        /// </summary>
        /// <remarks>
        /// 用途：NumericUpDown 绑定此属性，自动进行类型转换
        /// - DataType = Int: Value (int) → InternalNumericValue (double)
        /// - DataType = Double: Value (double) → InternalNumericValue (double)
        /// </remarks>
        private static readonly DependencyPropertyKey InternalNumericValuePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(InternalNumericValue), typeof(double), typeof(ConfigSetting),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty InternalNumericValueProperty = InternalNumericValuePropertyKey.DependencyProperty;

        // ===== 统一的范围属性 =====
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(object), typeof(ConfigSetting),
                new PropertyMetadata(null, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(object), typeof(ConfigSetting),
                new PropertyMetadata(null, OnRangeChanged));

        // ===== 内部数值范围（只读，用于模板绑定） =====
        private static readonly DependencyPropertyKey InternalNumericMinimumPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(InternalNumericMinimum), typeof(double), typeof(ConfigSetting),
                new PropertyMetadata(double.MinValue));

        public static readonly DependencyProperty InternalNumericMinimumProperty = InternalNumericMinimumPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey InternalNumericMaximumPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(InternalNumericMaximum), typeof(double), typeof(ConfigSetting),
                new PropertyMetadata(double.MaxValue));

        public static readonly DependencyProperty InternalNumericMaximumProperty = InternalNumericMaximumPropertyKey.DependencyProperty;

        // ===== 其他配置属性 =====
        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(ConfigSetting),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(ConfigSetting),
                new PropertyMetadata(10.0));

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(ConfigSetting),
                new PropertyMetadata(2));

        public static readonly DependencyProperty ShowSliderProperty =
            DependencyProperty.Register(nameof(ShowSlider), typeof(bool), typeof(ConfigSetting),
                new PropertyMetadata(true, OnShowSliderPropertyChanged));

        // ===== 可用绑定源（传递给ParamBinding） =====
        /// <summary>
        /// 可用数据源列表（输入：平面列表）
        /// </summary>
        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources), typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>), typeof(ConfigSetting),
                new PropertyMetadata(null));

        public static readonly DependencyProperty AvailableBindingsProperty =
            DependencyProperty.Register(nameof(AvailableBindings), typeof(System.Collections.Generic.List<string>), typeof(ConfigSetting),
                new PropertyMetadata(null));

        // ===== 编辑器可见性控制 =====
        /// <summary>
        /// 数值编辑器可见性：常量模式且数据类型为 Int/Double 时可见
        /// </summary>
        private static readonly DependencyPropertyKey NumericEditorVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(NumericEditorVisibility), typeof(Visibility), typeof(ConfigSetting),
                new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty NumericEditorVisibilityProperty = NumericEditorVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// 字符串编辑器可见性：常量模式且数据类型为 String 时可见
        /// </summary>
        private static readonly DependencyPropertyKey StringTextBoxVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(StringTextBoxVisibility), typeof(Visibility), typeof(ConfigSetting),
                new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty StringTextBoxVisibilityProperty = StringTextBoxVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// 布尔编辑器可见性：常量模式且数据类型为 Bool 时可见
        /// </summary>
        private static readonly DependencyPropertyKey BoolCheckBoxVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BoolCheckBoxVisibility), typeof(Visibility), typeof(ConfigSetting),
                new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty BoolCheckBoxVisibilityProperty = BoolCheckBoxVisibilityPropertyKey.DependencyProperty;

        /// <summary>
        /// 参数绑定控件可见性：绑定模式时可见
        /// </summary>
        private static readonly DependencyPropertyKey ParamBindingVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ParamBindingVisibility), typeof(Visibility), typeof(ConfigSetting),
                new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty ParamBindingVisibilityProperty = ParamBindingVisibilityPropertyKey.DependencyProperty;

        #endregion

        #region 属性封装

        /// <summary>
        /// 参数名（用于参数绑定，对应 ToolParameters 中的属性名）
        /// </summary>
        public string ParameterName
        {
            get => (string)GetValue(ParameterNameProperty);
            set => SetValue(ParameterNameProperty, value);
        }

        public BindingType BindingType
        {
            get => (BindingType)GetValue(BindingTypeProperty);
            set => SetValue(BindingTypeProperty, value);
        }

        public string BindingSource
        {
            get => (string)GetValue(BindingSourceProperty);
            set => SetValue(BindingSourceProperty, value);
        }

        /// <summary>
        /// 绑定显示文本：用于UI显示的中文格式
        /// </summary>
        public string BindingDisplayText
        {
            get => (string)GetValue(BindingDisplayTextProperty);
            private set => SetValue(BindingDisplayTextPropertyKey, value);
        }

        /// <summary>
        /// 值属性：统一的值入口
        /// </summary>
        public object? Value
        {
            get => GetValue(ValueProperty);
            set
            {
                // 验证并修正值类型
                var correctedValue = ValidateAndCorrectValueType(value);
                SetValue(ValueProperty, correctedValue);
            }
        }

        /// <summary>
        /// 内部数值值：只读，由 Value 自动计算
        /// </summary>
        public double InternalNumericValue
        {
            get => (double)GetValue(InternalNumericValueProperty);
            private set => SetValue(InternalNumericValuePropertyKey, value);
        }

        /// <summary>
        /// 最小值：支持任意数值类型
        /// </summary>
        public object? Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>
        /// 最大值：支持任意数值类型
        /// </summary>
        public object? Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>
        /// 内部数值最小值：只读，由 Minimum 自动计算
        /// </summary>
        public double InternalNumericMinimum
        {
            get => (double)GetValue(InternalNumericMinimumProperty);
            private set => SetValue(InternalNumericMinimumPropertyKey, value);
        }

        /// <summary>
        /// 内部数值最大值：只读，由 Maximum 自动计算
        /// </summary>
        public double InternalNumericMaximum
        {
            get => (double)GetValue(InternalNumericMaximumProperty);
            private set => SetValue(InternalNumericMaximumPropertyKey, value);
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

        public bool ShowSlider
        {
            get => (bool)GetValue(ShowSliderProperty);
            set => SetValue(ShowSliderProperty, value);
        }

        /// <summary>
        /// 可用数据源列表（输入）
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        public System.Collections.Generic.List<string> AvailableBindings
        {
            get => (System.Collections.Generic.List<string>)GetValue(AvailableBindingsProperty);
            set => SetValue(AvailableBindingsProperty, value);
        }

        public Visibility NumericEditorVisibility
        {
            get => (Visibility)GetValue(NumericEditorVisibilityProperty);
            private set => SetValue(NumericEditorVisibilityPropertyKey, value);
        }

        public Visibility StringTextBoxVisibility
        {
            get => (Visibility)GetValue(StringTextBoxVisibilityProperty);
            private set => SetValue(StringTextBoxVisibilityPropertyKey, value);
        }

        public Visibility BoolCheckBoxVisibility
        {
            get => (Visibility)GetValue(BoolCheckBoxVisibilityProperty);
            private set => SetValue(BoolCheckBoxVisibilityPropertyKey, value);
        }

        public Visibility ParamBindingVisibility
        {
            get => (Visibility)GetValue(ParamBindingVisibilityProperty);
            private set => SetValue(ParamBindingVisibilityPropertyKey, value);
        }

        #endregion

        #region 控件引用

        private NumericUpDown? _numericEditor;
        private TextBox? _stringTextBox;
        private Button? _bindingModeButton = null!;
        private CheckBox? _boolCheckBox;
        private ParamBinding? _paramBinding;

        /// <summary>
        /// 切到绑定模式前保存的常量值，用于切回常量模式时恢复
        /// </summary>
        private object? _constantValueBeforeBinding;

        /// <summary>
        /// 是否已从 ParamValue.BindingConfig 恢复过绑定状态（防止重复恢复）
        /// </summary>
        private bool _hasRestoredBindingState;

        /// <summary>
        /// 是否正在恢复绑定状态（用于阻止 SyncBindingConfigToParamValue 覆盖已加载的配置）
        /// </summary>
        private bool _isRestoringBindingState;

        #endregion

        #region 事件

        /// <summary>
        /// 值变更事件
        /// </summary>
        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ConfigSetting));

        public event RoutedEventHandler ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        /// <summary>
        /// 绑定类型变更事件
        /// </summary>
        public static readonly RoutedEvent BindingTypeChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(BindingTypeChanged), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ConfigSetting));

        public event RoutedEventHandler BindingTypeChanged
        {
            add => AddHandler(BindingTypeChangedEvent, value);
            remove => RemoveHandler(BindingTypeChangedEvent, value);
        }

        /// <summary>
        /// 绑定源选择事件
        /// </summary>
        public static readonly RoutedEvent BindingSourceSelectedEvent =
            EventManager.RegisterRoutedEvent(nameof(BindingSourceSelected), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ConfigSetting));

        public event RoutedEventHandler BindingSourceSelected
        {
            add => AddHandler(BindingSourceSelectedEvent, value);
            remove => RemoveHandler(BindingSourceSelectedEvent, value);
        }

        #endregion

        static ConfigSetting()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ConfigSetting),
                new FrameworkPropertyMetadata(typeof(ConfigSetting)));
        }

        public ConfigSetting()
        {
            AvailableBindings = new System.Collections.Generic.List<string>();
        }

        #region 回调方法

        private static void OnBindingTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ConfigSetting control)
            {
                var oldType = (BindingType)e.OldValue;
                var newType = (BindingType)e.NewValue;

                // 切到绑定模式前，保存当前常量值
                if (oldType == BindingType.Constant && newType == BindingType.Binding)
                {
                    control._constantValueBeforeBinding = control.Value;
                }

                // 切回常量模式时，恢复原始常量值（所见即所得）
                if (oldType == BindingType.Binding && newType == BindingType.Constant
                    && control._constantValueBeforeBinding != null)
                {
                    control.Value = control._constantValueBeforeBinding;

                    // 同步到 ParamValue.Value，确保执行时使用恢复的常量值
                    var paramValue = control.GetBoundParamValue();
                    if (paramValue != null)
                    {
                        paramValue.ObjectValue = control.UnwrapParamValue(control._constantValueBeforeBinding);
                    }
                }

                control.UpdateEditorVisibility();

                // 方案B：当绑定类型变化时，同步到 ParamValue.BindingConfig
                control.SyncBindingConfigToParamValue();
            }
        }

        private static void OnBindingSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ConfigSetting control)
            {
                // 当绑定源变化时，同步到 ParamValue.BindingConfig
                control.SyncBindingConfigToParamValue();
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ConfigSetting)d;

            // 尝试从 ParamValue.BindingConfig 恢复绑定状态（反序列化后首次触发时）
            control.TryRestoreBindingFromParamValue();

            // 更新内部数值值（类型适配器）
            control.UpdateInternalNumericValue(e.NewValue);

            // 更新 UI 控件的显示
            control.UpdateUIFromValue(e.NewValue);
        }

        /// <summary>
        /// DataType 变更时更新 DecimalPlaces、数值范围、编辑器可见性和视觉状态
        /// </summary>
        protected override void OnDataTypeChangedCore(DependencyPropertyChangedEventArgs e)
        {
            // 根据 Type.Code 自动设置 DecimalPlaces
            if (e.NewValue is Type dataType)
            {
                var typeCode = Type.GetTypeCode(dataType);

                if (typeCode == TypeCode.Int32 || typeCode == TypeCode.Int64)
                {
                    // 整数类型：强制设置为 0 位小数
                    DecimalPlaces = 0;
                }
                // 浮点类型：保持用户设置的 DecimalPlaces（默认为 2）
            }

            // 重新计算内部数值值和范围
            UpdateInternalNumericValue(Value);
            UpdateInternalNumericRange();

            UpdateEditorVisibility();
            UpdateVisualState();
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ConfigSetting control)
            {
                control.UpdateInternalNumericRange();
            }
        }

        private static void OnShowSliderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ConfigSetting control)
            {
                // ShowSlider 属性变化处理（目前无需特殊操作）
            }
        }

        #endregion

        #region 类型适配器

        /// <summary>
        /// 验证并修正值的类型
        /// </summary>
        /// <param name="value">输入值</param>
        /// <returns>符合 DataType 的值</returns>
        private object? ValidateAndCorrectValueType(object? value)
        {
            if (value == null || DataType == null)
                return value;

            var typeCode = Type.GetTypeCode(DataType);

            // 如果类型已匹配，直接返回
            if (DataType.IsAssignableFrom(value.GetType()))
                return value;

            // 尝试转换
            try
            {
                object? correctedValue = typeCode switch
                {
                    TypeCode.Int32 => Convert.ToInt32(value),
                    TypeCode.Int64 => Convert.ToInt64(value),
                    TypeCode.Double => Convert.ToDouble(value),
                    TypeCode.String => value.ToString() ?? string.Empty,
                    TypeCode.Boolean => Convert.ToBoolean(value),
                    _ => value
                };

                // 使用标准日志系统记录类型修正
                PluginLogger.Warning(
                    $"值类型自动修正: {value.GetType().Name} → {DataType.Name}, 参数: {ParameterName}",
                    "ConfigSetting");

                return correctedValue;
            }
            catch (Exception ex)
            {
                PluginLogger.Error(
                    $"值类型转换失败: {value.GetType().Name} → {DataType.Name}, 参数: {ParameterName}, 错误: {ex.Message}",
                    "ConfigSetting");

                // 返回默认值
                return typeCode switch
                {
                    TypeCode.Int32 => 0,
                    TypeCode.Int64 => 0L,
                    TypeCode.Double => 0.0,
                    TypeCode.String => string.Empty,
                    TypeCode.Boolean => false,
                    _ => value
                };
            }
        }

        #region 类型适配器

        /// <summary>
        /// 解包 ParamValue&lt;T&gt; 类型（读取值）
        /// </summary>
        private object? UnwrapParamValue(object? value)
        {
            if (value == null)
                return null;

            // 检测是否是 ParamValue<> 类型
            var valueType = value.GetType();
            if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(ParamValue<>))
            {
                // 通过反射获取 Value 属性
                var valueProperty = valueType.GetProperty("Value");
                if (valueProperty != null)
                {
                    return valueProperty.GetValue(value);
                }
            }

            return value;
        }

        /// <summary>
        /// 包装值到 ParamValue&lt;T&gt; 类型（设置值）
        /// </summary>
        private object? WrapToParamValue(object? value)
        {
            if (value == null)
                return null;

            // 检测当前绑定的值是否是 ParamValue<>
            var currentValue = Value;
            if (currentValue == null)
                return value;

            var currentType = currentValue.GetType();
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(ParamValue<>))
            {
                // 通过反射设置 Value 属性
                var valueProperty = currentType.GetProperty("Value");
                if (valueProperty != null)
                {
                    valueProperty.SetValue(currentValue, value);
                    return currentValue;  // 返回原 ParamValue<> 实例
                }
            }

            return value;
        }

        #endregion

        /// <summary>
        /// 更新内部数值值（类型适配器：object → double）
        /// </summary>
        private void UpdateInternalNumericValue(object? value)
        {
            if (value == null || DataType == null)
            {
                InternalNumericValue = 0.0;
                return;
            }

            // 解包 ParamValue<> 类型
            var unwrappedValue = UnwrapParamValue(value);
            if (unwrappedValue == null)
            {
                InternalNumericValue = 0.0;
                return;
            }

            var typeCode = Type.GetTypeCode(DataType);

            // 只处理数值类型
            if (typeCode == TypeCode.Int32 || typeCode == TypeCode.Int64 || typeCode == TypeCode.Double)
            {
                InternalNumericValue = Convert.ToDouble(unwrappedValue);
            }
            else
            {
                InternalNumericValue = 0.0;
            }
        }

        /// <summary>
        /// 从内部数值值设置值（类型适配器：double → object）
        /// </summary>
        private void SetFromInternalNumericValue(double numericValue)
        {
            if (DataType == null)
            {
                Value = numericValue;
                return;
            }

            var typeCode = Type.GetTypeCode(DataType);

            // 修复：显式装箱确保正确的类型推断
            // C# switch 表达式的类型推断会选择"最佳公共类型"
            // 如果不加 (object) 强制转换，编译器会推断为 double 类型
            object? newValue = typeCode switch
            {
                TypeCode.Int32 => (object)(int)Math.Round(numericValue),  // 显式装箱为 object
                TypeCode.Int64 => (object)(long)Math.Round(numericValue), // 显式装箱为 object
                TypeCode.Double => numericValue,
                _ => numericValue
            };

            // 如果当前绑定到 ParamValue<>，则设置到包装器内部
            Value = WrapToParamValue(newValue);
        }

        /// <summary>
        /// 更新内部数值范围（类型适配器：object → double）
        /// </summary>
        private void UpdateInternalNumericRange()
        {
            if (DataType == null)
            {
                InternalNumericMinimum = double.MinValue;
                InternalNumericMaximum = double.MaxValue;
                return;
            }

            var typeCode = Type.GetTypeCode(DataType);
            bool isInteger = typeCode == TypeCode.Int32 || typeCode == TypeCode.Int64;

            // 更新最小值
            if (Minimum == null)
            {
                InternalNumericMinimum = isInteger ? int.MinValue : double.MinValue;
            }
            else
            {
                InternalNumericMinimum = Convert.ToDouble(Minimum);
            }

            // 更新最大值
            if (Maximum == null)
            {
                InternalNumericMaximum = isInteger ? int.MaxValue : double.MaxValue;
            }
            else
            {
                InternalNumericMaximum = Convert.ToDouble(Maximum);
            }

            // ===== [Diag] 范围更新摘要 =====
            PluginLogger.Info(
                $"[DispText-Diag] 范围更新 | " +
                $"ParameterName={ParameterName} | " +
                $"DataSource.Minimum={Minimum} | DataSource.Maximum={Maximum} | " +
                $"InternalMin={InternalNumericMinimum:F1} | InternalMax={InternalNumericMaximum:F1} | " +
                $"DataType={DataType?.Name ?? "null"}",
                "ConfigSetting");
        }

        /// <summary>
        /// 从 Value 更新 UI 控件的显示
        /// </summary>
        private void UpdateUIFromValue(object? value)
        {
            if (DataType == null)
                return;

            var typeCode = Type.GetTypeCode(DataType);

            // Numeric 控件通过绑定 InternalNumericValue 自动更新，无需手动处理

            // String 控件
            if (_stringTextBox != null && typeCode == TypeCode.String)
            {
                var text = value?.ToString() ?? string.Empty;
                if (_stringTextBox.Text != text)
                {
                    _stringTextBox.Text = text;
                }
            }

            // Bool 控件
            if (_boolCheckBox != null && typeCode == TypeCode.Boolean)
            {
                var isChecked = value is bool b && b;
                if (_boolCheckBox.IsChecked != isChecked)
                {
                    _boolCheckBox.IsChecked = isChecked;
                }
            }
        }

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的控件
            _numericEditor = GetTemplateChild("PART_NumericEditor") as NumericUpDown;
            _stringTextBox = GetTemplateChild("PART_StringTextBox") as TextBox;
            _bindingModeButton = GetTemplateChild("PART_BindingModeButton") as Button ??
                throw new InvalidOperationException("PART_BindingModeButton not found");
            _boolCheckBox = GetTemplateChild("PART_BoolCheckBox") as CheckBox;
            _paramBinding = GetTemplateChild("PART_ParamBinding") as ParamBinding;

            // 绑定事件
            if (_numericEditor != null)
            {
                _numericEditor.ValueChanged += (s, e) =>
                {
                    // 从 NumericUpDown 的 Value 属性更新到 ConfigSetting 的 Value
                    SetFromInternalNumericValue(e.NewValue);
                    RaiseValueChanged();
                };
            }

            if (_stringTextBox != null)
            {
                _stringTextBox.TextChanged += (s, e) =>
                {
                    if (BindingType == BindingType.Constant)
                    {
                        Value = _stringTextBox.Text;
                        RaiseValueChanged();
                    }
                };
            }

            if (_boolCheckBox != null)
            {
                _boolCheckBox.Checked += (s, e) =>
                {
                    Value = true;
                    RaiseValueChanged();
                };
                _boolCheckBox.Unchecked += (s, e) =>
                {
                    Value = false;
                    RaiseValueChanged();
                };
            }

            // 绑定模式切换按钮
            _bindingModeButton.Click += OnBindingModeButtonClick;

            // 绑定源选择事件（从ParamBinding转发）
            if (_paramBinding != null)
            {
                _paramBinding.BindingSourceSelected += (s, e) =>
                {
                    // 从 ParamBinding 获取绑定源和显示文本
                    BindingSource = _paramBinding.BindingSource;
                    BindingDisplayText = _paramBinding.BindingDisplayText;

                    RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
                };
            }

            // 初始化状态
            UpdateEditorVisibility();
            UpdateVisualState();

            // ===== [Diag] 汇总 ConfigSetting 模板初始化状态 =====
            if (_numericEditor != null)
            {
                PluginLogger.Info(
                    $"[DispText-Diag] ConfigSetting模板初始化 | " +
                    $"ParameterName={ParameterName} | " +
                    $"DataType={DataType?.Name ?? "null"} | " +
                    $"Value={Value} | " +
                    $"InternalNumericValue={InternalNumericValue:F1} | " +
                    $"InternalNumericMinimum={InternalNumericMinimum:F1} | " +
                    $"InternalNumericMaximum={InternalNumericMaximum:F1} | " +
                    $"Minimum={Minimum} | Maximum={Maximum} | " +
                    $"NumericEditor.Value={_numericEditor.Value:F1} | " +
                    $"NumericEditor.Minimum={_numericEditor.Minimum:F1} | " +
                    $"NumericEditor.Maximum={_numericEditor.Maximum:F1} | " +
                    $"NumericEditor.ShowSlider={_numericEditor.ShowSlider}",
                    "ConfigSetting");
            }
        }

        private void RaiseValueChanged()
        {
            RaiseEvent(new RoutedEventArgs(ValueChangedEvent));
        }

        private void OnBindingModeButtonClick(object sender, RoutedEventArgs e)
        {
            if (BindingType == BindingType.Constant)
            {
                // 切换到绑定模式
                BindingType = BindingType.Binding;
            }
            else
            {
                // 切换到常量模式 - 解除绑定
                BindingType = BindingType.Constant;
                BindingSource = string.Empty;
                BindingDisplayText = string.Empty;

                // 清空 ParamBinding 的绑定状态
                if (_paramBinding != null)
                {
                    _paramBinding.IsBound = false;
                    _paramBinding.BindingDisplayText = string.Empty;
                }
            }

            UpdateVisualState();
            RaiseEvent(new RoutedEventArgs(BindingTypeChangedEvent));
        }

        private void UpdateEditorVisibility()
        {
            if (BindingType == BindingType.Binding)
            {
                // 绑定模式：只显示 ParamBinding
                NumericEditorVisibility = Visibility.Collapsed;
                StringTextBoxVisibility = Visibility.Collapsed;
                BoolCheckBoxVisibility = Visibility.Collapsed;
                ParamBindingVisibility = Visibility.Visible;
            }
            else
            {
                // 常量模式：根据 Type.Code 显示对应的编辑器
                ParamBindingVisibility = Visibility.Collapsed;

                if (DataType == null)
                {
                    NumericEditorVisibility = Visibility.Collapsed;
                    StringTextBoxVisibility = Visibility.Collapsed;
                    BoolCheckBoxVisibility = Visibility.Collapsed;
                    return;
                }

                var typeCode = Type.GetTypeCode(DataType);

                switch (typeCode)
                {
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Double:
                    case TypeCode.Single:
                    case TypeCode.Decimal:
                        NumericEditorVisibility = Visibility.Visible;
                        StringTextBoxVisibility = Visibility.Collapsed;
                        BoolCheckBoxVisibility = Visibility.Collapsed;
                        break;
                    case TypeCode.String:
                    case TypeCode.Char:
                        NumericEditorVisibility = Visibility.Collapsed;
                        StringTextBoxVisibility = Visibility.Visible;
                        BoolCheckBoxVisibility = Visibility.Collapsed;
                        break;
                    case TypeCode.Boolean:
                        NumericEditorVisibility = Visibility.Collapsed;
                        StringTextBoxVisibility = Visibility.Collapsed;
                        BoolCheckBoxVisibility = Visibility.Visible;
                        break;
                    default:
                        NumericEditorVisibility = Visibility.Collapsed;
                        StringTextBoxVisibility = Visibility.Collapsed;
                        BoolCheckBoxVisibility = Visibility.Collapsed;
                        break;
                }
            }
        }

        private void UpdateVisualState()
        {
            if (_bindingModeButton == null || _paramBinding == null)
                return;

            if (BindingType == BindingType.Binding)
            {
                // 绑定模式：禁用值编辑控件
                if (_numericEditor != null)
                {
                    _numericEditor.IsEnabled = false;
                }
                if (_stringTextBox != null)
                {
                    _stringTextBox.IsEnabled = false;
                }
                if (_boolCheckBox != null) _boolCheckBox.IsEnabled = false;

                // 启用ParamBinding
                _paramBinding.IsEnabled = true;
                _paramBinding.AvailableDataSources = AvailableDataSources;
                _paramBinding.DataType = DataType;

                // 设置绑定状态
                _paramBinding.IsBound = true;
                // 注意：不要覆盖 ParamBinding 生成的 BindingDisplayText
                // ParamBinding 会通过 TreeView 节点层级动态生成显示文本
                // 这里只在 BindingDisplayText 为空时才设置（从 ParamSetting 加载时）
                if (string.IsNullOrEmpty(BindingDisplayText))
                {
                    _paramBinding.BindingDisplayText = _paramBinding.GenerateBindingDisplayText(BindingSource);
                    BindingDisplayText = _paramBinding.BindingDisplayText;
                }
                else
                {
                    _paramBinding.BindingDisplayText = BindingDisplayText;
                }

                // 更新按钮图标
                _bindingModeButton.Content = "⚡";
                _bindingModeButton.ToolTip = $"绑定源: {BindingSource}\n点击切换到常量模式";
            }
            else
            {
                // 常量模式：启用值编辑控件
                if (_numericEditor != null)
                {
                    _numericEditor.IsEnabled = true;
                }
                if (_stringTextBox != null)
                {
                    _stringTextBox.IsEnabled = true;
                }
                if (_boolCheckBox != null) _boolCheckBox.IsEnabled = true;

                // 禁用ParamBinding
                _paramBinding.IsEnabled = false;
                _paramBinding.IsBound = false;
                _paramBinding.BindingDisplayText = string.Empty;

                // 更新按钮图标
                _bindingModeButton.Content = "🔗";
                _bindingModeButton.ToolTip = "点击切换到绑定模式";
            }
        }

        /// <summary>
        /// 获取当前值（根据数据类型）
        /// </summary>
        public object? GetValue()
        {
            return Value;
        }

        /// <summary>
        /// 设置当前值（根据数据类型）
        /// </summary>
        public void SetValue(object? value)
        {
            Value = value;
        }

        /// <summary>
        /// 转换为 ParamSetting 对象
        /// </summary>
        public ParamSetting ToParamSetting()
        {
            if (BindingType == BindingType.Constant)
            {
                return ParamSetting.CreateConstant(ParameterName, GetValue());
            }
            else
            {
                var parts = BindingSource.Split('.');
                var nodeId = parts.Length > 0 ? parts[0] : string.Empty;
                var property = parts.Length > 1 ? parts[1] : BindingSource;
                return ParamSetting.CreateBinding(ParameterName, nodeId, property);
            }
        }

        /// <summary>
        /// 绑定配置变更事件 - 当绑定类型或绑定源发生变化时触发
        /// </summary>
        /// <remarks>
        /// 宿主控件（如 DispTextColorControl）可订阅此事件，在非 ParamValue 绑定场景下
        /// 主动保存 ConfigSetting 的绑定状态到自有持久化存储（如 TextStyle）。
        /// </remarks>
        public event EventHandler? BindingConfigChanged;

        /// <summary>
        /// 同步当前配置到绑定的 ParamValue 对象（方案B核心方法）
        /// </summary>
        /// <remarks>
        /// 当用户在 UI 中切换绑定模式或选择绑定源时，将配置写入绑定的 ParamValue.BindingConfig。
        /// 这样执行引擎在运行时可以直接从 parameters 内部读取绑定信息，无需依赖外部的 ParamSettings。
        /// </remarks>
        private void SyncBindingConfigToParamValue()
        {
            // 恢复绑定状态期间跳过，避免覆盖已从 JSON 加载的配置
            if (_isRestoringBindingState)
                return;

            // 获取当前绑定的 ParamValue<T> 对象
            var paramValue = GetBoundParamValue();
            if (paramValue != null)
            {
                if (BindingType == BindingType.Constant)
                {
                    paramValue.BindingConfig = null;
                }
                else if (!string.IsNullOrEmpty(BindingSource))
                {
                    var parts = BindingSource.Split('.');
                    var nodeId = parts.Length > 0 ? parts[0] : string.Empty;
                    var property = parts.Length > 1 ? parts[1] : BindingSource;

                    var setting = ParamSetting.CreateBinding(
                        ParameterName ?? string.Empty,
                        nodeId,
                        property);

                    var matchedSource = AvailableDataSources?.FirstOrDefault(ds =>
                        ds.SourceNodeId == nodeId && ds.PropertyName == property);
                    if (matchedSource != null)
                    {
                        setting.SourceNodeName = matchedSource.SourceNodeName;
                        setting.SourceDisplayName = matchedSource.DisplayName;
                    }

                    paramValue.BindingConfig = setting;
                }
            }

            // 通知外部绑定配置已变更（即使没有 ParamValue，宿主控件也可通过此事件主动保存状态）
            BindingConfigChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取当前绑定的 ParamValue 对象
        /// </summary>
        /// <returns>绑定的 ParamValue&lt;T&gt;，如果不是绑定到 ParamValue 则返回 null</returns>
        private ParamValueBase? GetBoundParamValue()
        {
            // 通过 Value 属性的绑定表达式获取绑定的源对象
            var bindingExpression = BindingOperations.GetBindingExpression(this, ValueProperty);
            if (bindingExpression?.DataItem == null)
                return null;

            // DataItem 是 ToolParameters 对象，获取绑定属性的值
            // 注意：XAML 绑定路径可能是 "Threshold.Value"（嵌套属性），
            // 需要提取第一段作为属性名来获取 ParamValue<T> 对象
            var fullPath = bindingExpression.ParentBinding?.Path?.Path;
            if (string.IsNullOrEmpty(fullPath))
                return null;

            // 处理嵌套路径："Threshold.Value" → "Threshold"
            var propertyName = fullPath.Contains('.')
                ? fullPath.Split('.')[0]
                : fullPath;

            var paramValueObj = bindingExpression.DataItem.GetType().GetProperty(propertyName)?.GetValue(bindingExpression.DataItem);
            if (paramValueObj is ParamValueBase paramValue)
                return paramValue;

            return null;
        }

        /// <summary>
        /// 从 ParamSetting 对象加载
        /// </summary>
        public void FromParamSetting(ParamSetting setting)
        {
            if (setting == null)
                return;

            ParameterName = setting.ParameterName;
            BindingType = setting.BindingType;

            if (setting.BindingType == BindingType.Constant)
            {
                SetValue(setting.ConstantValue);
                BindingDisplayText = string.Empty;
                BindingSource = string.Empty;
            }
            else
            {
                // 设置绑定源路径
                BindingSource = string.IsNullOrEmpty(setting.SourceProperty)
                    ? setting.SourceNodeId ?? string.Empty
                    : $"{setting.SourceNodeId}.{setting.SourceProperty}";

                // 优先使用 ParamSetting 中保存的展示名称
                if (!string.IsNullOrEmpty(setting.SourceNodeName) && !string.IsNullOrEmpty(setting.SourceDisplayName))
                {
                    BindingDisplayText = $"{setting.SourceNodeName}.{setting.SourceDisplayName}";
                }
                else
                {
                    // 回退：尝试从 ParamBinding 的方法生成
                    BindingDisplayText = _paramBinding?.GenerateBindingDisplayText(BindingSource) ?? BindingSource;
                }
            }
        }

        /// <summary>
        /// 尝试从 ParamValue.BindingConfig 恢复绑定状态（解决反序列化后绑定模式丢失问题）
        /// </summary>
        /// <remarks>
        /// 数据流：
        /// 1. 保存时：SyncBindingConfigToParamValue() 将绑定配置写入 ParamValue.BindingConfig → JSON序列化
        /// 2. 加载时：JSON反序列化恢复 ParamValue.BindingConfig，但 ConfigSetting.BindingType 仍为默认值 Constant
        /// 3. 本方法在 OnValueChanged 首次触发时检测并恢复绑定状态
        ///
        /// 调用时机：OnValueChanged 回调中（Value 属性首次被设置时）
        /// 防重复：_hasRestoredBindingState 标志位确保只执行一次
        /// 防覆盖：_isRestoringBindingState 标志位阻止 SyncBindingConfigToParamValue 在恢复期间执行
        /// </remarks>
        private void TryRestoreBindingFromParamValue()
        {
            // 已恢复过则跳过
            if (_hasRestoredBindingState)
                return;

            var paramValue = GetBoundParamValue();
            if (paramValue?.BindingConfig == null)
            {
                // 无绑定配置或未绑定到 ParamValue，标记为已处理
                _hasRestoredBindingState = true;
                return;
            }

            var bindingConfig = paramValue.BindingConfig;

            // 只有绑定类型为 Binding 时才需要恢复
            if (bindingConfig.BindingType != BindingType.Binding)
            {
                _hasRestoredBindingState = true;
                return;
            }

            // 当前 UI 显示的是常量模式（默认值），但实际应该是绑定模式 → 需要恢复
            if (BindingType != BindingType.Binding)
            {
                _isRestoringBindingState = true;
                try
                {
                    // 使用 FromParamSetting 从已加载的 BindingConfig 恢复
                    FromParamSetting(bindingConfig);

                    // 确保编辑器可见性和视觉状态正确更新
                    UpdateEditorVisibility();
                    UpdateVisualState();
                }
                finally
                {
                    _isRestoringBindingState = false;
                }
            }

            _hasRestoredBindingState = true;
        }

        #region 基类方法实现

        /// <summary>
        /// 获取可用的数据源（从 AvailableDataSources 属性）
        /// </summary>
        /// <returns>数据源集合</returns>
        public override List<AvailableDataSource> GetAvailableDataSources()
        {
            return AvailableDataSources?.ToList() ?? new List<AvailableDataSource>();
        }

        #endregion
    }
}
