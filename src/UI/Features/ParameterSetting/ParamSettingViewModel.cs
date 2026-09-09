using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Shared.Events;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.ParameterSetting
{
    /// <summary>
    /// 参数绑定ViewModel
    /// </summary>
    /// <remarks>
    /// 管理单个参数的绑定配置，支持常量值和动态绑定两种模式。
    ///
    /// 核心功能：
    /// 1. 切换绑定类型（常量/动态）
    /// 2. 管理常量值
    /// 3. 选择数据源
    /// 4. 验证绑定配置
    /// </remarks>
    public class ParamSettingViewModel : ParamSettingViewModelBase
    {
        #region 字段

        private readonly IDataSourceQueryService? _dataSourceQueryService;
        private ParamSetting _setting;
        private BindingType _selectedBindingType;
        private object? _constantValue;
        private AvailableDataSource? _selectedDataSource;
        private bool _isValid = true;
        private string _validationMessage = string.Empty;

        #endregion

        #region 属性

        /// <summary>
        /// 参数名称
        /// </summary>
        public override string ParameterName => _setting.ParameterName;

        /// <summary>
        /// 参数显示名称
        /// </summary>
        public override string DisplayName { get; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public Type ParameterType { get; }

        /// <summary>
        /// 参数类型名称
        /// </summary>
        public string TypeName => ParameterType.Name;

        /// <summary>
        /// 参数描述
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// 参数默认值
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// 最小值
        /// </summary>
        public double? Min { get; }

        /// <summary>
        /// 最大值
        /// </summary>
        public double? Max { get; }

        /// <summary>
        /// 步进值
        /// </summary>
        public double Step { get; }

        /// <summary>
        /// 单位
        /// </summary>
        public string? Unit { get; }

        /// <summary>
        /// 是否有范围限制
        /// </summary>
        public bool HasRange { get; }

        /// <summary>
        /// 是否为数值类型
        /// </summary>
        public bool IsNumericType =>
            ParameterType == typeof(int) ||
            ParameterType == typeof(double) ||
            ParameterType == typeof(float) ||
            ParameterType == typeof(long) ||
            ParameterType == typeof(decimal) ||
            ParameterType == typeof(short) ||
            ParameterType == typeof(byte);

        /// <summary>
        /// 绑定类型选项
        /// </summary>
        public ObservableCollection<BindingTypeOption> BindingTypeOptions { get; }

        /// <summary>
        /// 选中的绑定类型
        /// </summary>
        public BindingType SelectedBindingType
        {
            get => _selectedBindingType;
            set
            {
                if (_selectedBindingType != value)
                {
                    var oldType = _selectedBindingType.ToString();
                    if (SetProperty(ref _selectedBindingType, value))
                    {
                        _setting.BindingType = value;
                        UpdateBindingMode();
                        Validate();

                        // 触发参数变更事件
                        OnParameterChanged(ParameterChangeEventArgs.BindingTypeChanged(
                            ParameterName, DisplayName, oldType, value.ToString()));
                    }
                }
            }
        }

        /// <summary>
        /// 是否为常量绑定模式
        /// </summary>
        public bool IsConstantMode => SelectedBindingType == BindingType.Constant;

        /// <summary>
        /// 是否为动态绑定模式
        /// </summary>
        public bool IsDynamicMode => SelectedBindingType == BindingType.Binding;

        /// <summary>
        /// 常量值
        /// </summary>
        public object? ConstantValue
        {
            get => _constantValue;
            set
            {
                var oldValue = _constantValue;
                if (SetProperty(ref _constantValue, value))
                {
                    _setting.ConstantValue = value;
                    Validate();

                    // 触发 NumericValue 更新
                    OnPropertyChanged(nameof(NumericValue));

                    // 触发参数变更事件（仅当值确实改变时）
                    if (!Equals(oldValue, value))
                    {
                        OnParameterChanged(ParameterChangeEventArgs.ConstantValueChanged(
                            ParameterName, DisplayName, oldValue, value));
                    }
                }
            }
        }

        /// <summary>
        /// 常量值字符串表示
        /// </summary>
        public string? ConstantValueString
        {
            get => ConstantValue?.ToString();
            set
            {
                if (TryParseValue(value, out var parsedValue))
                {
                    ConstantValue = parsedValue;
                }
            }
        }

        /// <summary>
        /// 数值类型值（用于Slider绑定）
        /// </summary>
        public double NumericValue
        {
            get
            {
                if (_constantValue == null) return Min ?? 0;
                try
                {
                    return Convert.ToDouble(_constantValue);
                }
                catch
                {
                    return Min ?? 0;
                }
            }
            set
            {
                if (!IsNumericType) return;

                object? convertedValue = ParameterType switch
                {
                    Type t when t == typeof(int) => (int)value,
                    Type t when t == typeof(double) => value,
                    Type t when t == typeof(float) => (float)value,
                    Type t when t == typeof(long) => (long)value,
                    Type t when t == typeof(decimal) => (decimal)value,
                    Type t when t == typeof(short) => (short)value,
                    Type t when t == typeof(byte) => (byte)value,
                    _ => value
                };

                ConstantValue = convertedValue;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 可用数据源列表
        /// </summary>
        public ObservableCollection<AvailableDataSource> AvailableDataSources { get; private set; }

        /// <summary>
        /// 树形结构的数据源节点
        /// </summary>
        public ObservableCollection<TreeNodeData> TreeNodes { get; }

        /// <summary>
        /// 选中的数据源
        /// </summary>
        public AvailableDataSource? SelectedDataSource
        {
            get => _selectedDataSource;
            set
            {
                if (SetProperty(ref _selectedDataSource, value))
                {
                    if (value != null)
                    {
                        _setting.SourceNodeId = value.SourceNodeId;
                        _setting.SourceProperty = value.PropertyName;

                        // 触发参数变更事件
                        OnParameterChanged(ParameterChangeEventArgs.DynamicBindingConfigured(
                            ParameterName, DisplayName, value.SourceNodeName, value.PropertyName));
                    }
                    else
                    {
                        _setting.SourceNodeId = null;
                        _setting.SourceProperty = null;
                    }
                    OnPropertyChanged(nameof(SelectedDataSourceDisplay));
                    Validate();
                }
            }
        }

        /// <summary>
        /// 选中数据源的显示文本
        /// </summary>
        public string SelectedDataSourceDisplay => SelectedDataSource?.GetDisplayText() ?? "选择数据源...";

        /// <summary>
        /// 是否有效
        /// </summary>
        public new bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        /// <summary>
        /// 验证消息
        /// </summary>
        public new string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        /// <summary>
        /// 当前绑定配置
        /// </summary>
        public ParamSetting Binding => _setting;

        #endregion

        #region 命令

        /// <summary>
        /// 选择数据源命令
        /// </summary>
        public ICommand SelectDataSourceCommand { get; }

        /// <summary>
        /// 清除数据源命令
        /// </summary>
        public ICommand ClearDataSourceCommand { get; }

        /// <summary>
        /// 重置为默认值命令
        /// </summary>
        public ICommand ResetToDefaultCommand { get; }

        /// <summary>
        /// 应用绑定命令
        /// </summary>
        public ICommand ApplySettingCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 数据源选择请求事件
        /// </summary>
        public event EventHandler<DataSourceSelectionRequestEventArgs>? DataSourceSelectionRequested;

        /// <summary>
        /// 参数变更事件
        /// </summary>
        public event EventHandler<ParameterChangeEventArgs>? ParameterChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建参数绑定ViewModel
        /// </summary>
        public ParamSettingViewModel(
            string parameterName,
            string displayName,
            Type parameterType,
            object? defaultValue = null,
            string? description = null,
            IDataSourceQueryService? dataSourceQueryService = null,
            double? min = null,
            double? max = null,
            double step = 1.0,
            string? unit = null)
        {
            _setting = new ParamSetting
            {
                ParameterName = parameterName,
                BindingType = BindingType.Constant,
                TargetType = parameterType
            };

            DisplayName = displayName;
            ParameterType = parameterType;
            DefaultValue = defaultValue;
            Description = description;
            _dataSourceQueryService = dataSourceQueryService;

            // 范围属性
            Min = min;
            Max = max;
            Step = step;
            Unit = unit;
            HasRange = min.HasValue && max.HasValue;

            // 初始化绑定类型选项
            BindingTypeOptions = new ObservableCollection<BindingTypeOption>
            {
                new BindingTypeOption(BindingType.Constant, "常量值", "使用固定的常量值"),
                new BindingTypeOption(BindingType.Binding, "动态绑定", "从父节点输出获取值")
            };

            // 初始化数据源列表
            AvailableDataSources = new ObservableCollection<AvailableDataSource>();
            TreeNodes = new ObservableCollection<TreeNodeData>();

            // 初始化命令
            SelectDataSourceCommand = new RelayCommand(ExecuteSelectDataSource);
            ClearDataSourceCommand = new RelayCommand(ExecuteClearDataSource, CanClearDataSource);
            ResetToDefaultCommand = new RelayCommand(ExecuteResetToDefault, CanResetToDefault);
            ApplySettingCommand = new RelayCommand(ExecuteApplySetting, CanApplySetting);

            // 设置默认值
            if (defaultValue != null)
            {
                ConstantValue = defaultValue;
            }

            _selectedBindingType = BindingType.Constant;
        }

        /// <summary>
        /// 从现有绑定创建ViewModel
        /// </summary>
        public static ParamSettingViewModel FromSetting(
            ParamSetting binding,
            string displayName,
            Type parameterType,
            object? defaultValue = null,
            string? description = null,
            IDataSourceQueryService? dataSourceQueryService = null,
            double? min = null,
            double? max = null,
            double step = 1.0,
            string? unit = null)
        {
            var viewModel = new ParamSettingViewModel(
                binding.ParameterName,
                displayName,
                parameterType,
                defaultValue,
                description,
                dataSourceQueryService,
                min,
                max,
                step,
                unit);

            // 恢复绑定配置
            viewModel._setting = binding.Clone();
            viewModel._selectedBindingType = binding.BindingType;
            viewModel._constantValue = binding.ConstantValue;

            // 如果是动态绑定，尝试恢复数据源选择
            if (binding.BindingType == BindingType.Binding &&
                !string.IsNullOrEmpty(binding.SourceNodeId) &&
                dataSourceQueryService != null)
            {
                var dataSources = dataSourceQueryService.GetAvailableDataSources(binding.SourceNodeId, parameterType);
                viewModel._selectedDataSource = dataSources.FirstOrDefault(ds => ds.PropertyName == binding.SourceProperty);
            }

            viewModel.Validate();
            return viewModel;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新可用数据源
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        public void RefreshAvailableDataSources(string nodeId)
        {
            try
            {
                AvailableDataSources.Clear();
                TreeNodes.Clear();

                if (_dataSourceQueryService == null)
                {
                    return;
                }

                // 获取所有可用数据源（包括类型过滤）
                var dataSources = _dataSourceQueryService.GetAvailableDataSources(nodeId, ParameterType);

                // 批量添加数据源，避免逐个触发 CollectionChanged 导致重复构建树形结构
                AvailableDataSources = new ObservableCollection<AvailableDataSource>(dataSources);

                // 构建树形结构（传 ParameterType：RoiShape[] 子树按目标词目定型）
                var treeNodes = ConfigSetting.BuildTreeStructure(dataSources, ParameterType);
                foreach (var node in treeNodes)
                {
                    TreeNodes.Add(node);
                }

                // 如果当前选中的数据源不在列表中，清除选择
                if (SelectedDataSource != null &&
                    !AvailableDataSources.Contains(SelectedDataSource))
                {
                    SelectedDataSource = null;
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ 刷新数据源失败: {ex.Message}", exception: ex);
            }
        }

        /// <summary>
        /// 获取当前绑定配置
        /// </summary>
        public override ParamSetting GetSetting()
        {
            return _setting.Clone();
        }

        /// <summary>
        /// 应用绑定配置
        /// </summary>
        public void ApplySetting()
        {
            RaiseSettingChanged(_setting.Clone());
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 触发参数变更事件
        /// </summary>
        protected virtual void OnParameterChanged(ParameterChangeEventArgs e)
        {
            ParameterChanged?.Invoke(this, e);
        }

        private void UpdateBindingMode()
        {
            OnPropertyChanged(nameof(IsConstantMode));
            OnPropertyChanged(nameof(IsDynamicMode));
        }

        private void ExecuteSelectDataSource()
        {
            try
            {
                // 触发数据源选择请求事件
                var args = new DataSourceSelectionRequestEventArgs(ParameterName, ParameterType);
                DataSourceSelectionRequested?.Invoke(this, args);

                if (args.SelectedDataSource != null)
                {
                    SelectedDataSource = args.SelectedDataSource;
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ 选择数据源失败: {ex.Message}", exception: ex);
            }
        }

        private bool CanClearDataSource()
        {
            return SelectedDataSource != null;
        }

        private void ExecuteClearDataSource()
        {
            try
            {
                SelectedDataSource = null;
            }
            catch (Exception ex)
            {
                LogError($"❌ 清除数据源失败: {ex.Message}", exception: ex);
            }
        }

        private bool CanResetToDefault()
        {
            return DefaultValue != null;
        }

        private void ExecuteResetToDefault()
        {
            var oldValue = ConstantValue;

            SelectedBindingType = BindingType.Constant;
            ConstantValue = DefaultValue;
            SelectedDataSource = null;

            // 触发重置事件
            OnParameterChanged(ParameterChangeEventArgs.ResetToDefault(
                ParameterName, DisplayName, DefaultValue));
        }

        private bool CanApplySetting()
        {
            return IsValid;
        }

        private void ExecuteApplySetting()
        {
            ApplySetting();
        }

        private void Validate()
        {
            var result = _setting.Validate();
            IsValid = result.IsValid;

            if (result.IsValid)
            {
                ValidationMessage = string.Empty;
            }
            else
            {
                ValidationMessage = string.Join("\n", result.Errors);
            }
        }

        private bool TryParseValue(string? valueString, out object? parsedValue)
        {
            parsedValue = null;

            if (string.IsNullOrWhiteSpace(valueString))
            {
                return true; // 允许空值
            }

            try
            {
                if (ParameterType == typeof(string))
                {
                    parsedValue = valueString;
                }
                else if (ParameterType == typeof(int))
                {
                    parsedValue = int.Parse(valueString);
                }
                else if (ParameterType == typeof(double))
                {
                    parsedValue = double.Parse(valueString);
                }
                else if (ParameterType == typeof(float))
                {
                    parsedValue = float.Parse(valueString);
                }
                else if (ParameterType == typeof(bool))
                {
                    parsedValue = bool.Parse(valueString);
                }
                else if (ParameterType == typeof(long))
                {
                    parsedValue = long.Parse(valueString);
                }
                else
                {
                    parsedValue = Convert.ChangeType(valueString, ParameterType);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// 绑定类型选项
    /// </summary>
    public class BindingTypeOption
    {
        public BindingType Type { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public BindingTypeOption(BindingType type, string displayName, string description)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
        }
    }

    /// <summary>
    /// 数据源选择请求事件参数
    /// </summary>
    public class DataSourceSelectionRequestEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public Type ParameterType { get; }
        public AvailableDataSource? SelectedDataSource { get; set; }

        public DataSourceSelectionRequestEventArgs(string parameterName, Type parameterType)
        {
            ParameterName = parameterName;
            ParameterType = parameterType;
        }
    }
}
