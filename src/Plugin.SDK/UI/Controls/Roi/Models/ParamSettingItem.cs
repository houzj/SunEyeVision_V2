using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models
{
    /// <summary>
    /// 参数绑定项 - 用于参数面板显示。
    /// 承载几何参数拼装中"一个扁平参数"的订阅设置（值源统一为 <see cref="ParamSetting"/>）。
    /// 键 = <see cref="RoiShape"/> 扁平参数名（CenterX/CenterY/Width/Height/Radius/Angle/StartX/...）。
    /// </summary>
    public class ParamSettingItem : ObservableObject
    {
        private ParamSetting? _setting;
        private string _displayPath = string.Empty;
        private string _bindingPath = string.Empty;
        private object? _currentValue;

        /// <summary>
        /// 参数名称（RoiShape 扁平参数名）
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
        /// 参数订阅设置（null=未绑定）
        /// </summary>
        public ParamSetting? Setting
        {
            get => _setting;
            set
            {
                if (SetProperty(ref _setting, value))
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
        public bool IsBound => Setting != null;

        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindingType BindingType => Setting?.BindingType ?? BindingType.Constant;
    }
}
