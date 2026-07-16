using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 参数绑定项 - 用于参数面板显示
    /// </summary>
    public class ParamSettingItem : ObservableObject
    {
        private ParameterSource? _source;
        private string _displayPath = string.Empty;
        private string _bindingPath = string.Empty;
        private object? _currentValue;

        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>
        /// 参数显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 参数数据类型（CLR 类型，与 ParamBinding.DataType / AvailableDataSource.PropertyType 一致）
        /// </summary>
        public Type DataType { get; set; } = typeof(double);

        /// <summary>
        /// 参数源（可能为null表示未绑定）
        /// </summary>
        public ParameterSource? Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value))
                {
                    OnPropertyChanged(nameof(IsBound));
                    OnPropertyChanged(nameof(BindingType));
                }
            }
        }

        /// <summary>
        /// 绑定路径（作为 ParamBinding.BindingSource 的双向桥；格式：节点ID.属性名 或 __GlobalVars__.变量名）
        /// </summary>
        public string BindingPath
        {
            get => _bindingPath;
            set => SetProperty(ref _bindingPath, value);
        }

        /// <summary>
        /// 显示路径（绑定的节点输出路径）
        /// </summary>
        public string DisplayPath
        {
            get => _displayPath;
            set => SetProperty(ref _displayPath, value);
        }

        /// <summary>
        /// 当前值（实时更新）
        /// </summary>
        public object? CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        /// <summary>
        /// 是否已绑定
        /// </summary>
        public bool IsBound => Source != null;

        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindingType BindingType => Source?.BindingType ?? BindingType.Constant;
    }
}
