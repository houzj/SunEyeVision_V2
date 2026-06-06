using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.Tool.Subroutine.Models
{
    public class SubroutineInputConfig : ObservableObject
    {
        private string _name = string.Empty;
        private string _typeName = "Image";
        private AvailableDataSource? _source;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string TypeName
        {
            get => _typeName;
            set
            {
                if (SetProperty(ref _typeName, value))
                    OnPropertyChanged(nameof(ParamType));
            }
        }

        /// <summary>参数类型（供 ParamBinding.DataType 类型筛选）</summary>
        public Type? ParamType => GlobalVariableTypeMapper.ToSystemType(TypeName);

        public AvailableDataSource? Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value))
                {
                    if (value != null && !string.IsNullOrEmpty(value.TypeName))
                        TypeName = value.TypeName;
                }
            }
        }

        public string SourceDisplayText => Source?.DropdownDisplayText ?? string.Empty;

        /// <summary>绑定源路径（格式：节点ID.属性名），对接 ParamBinding.BindingSource</summary>
        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        /// <summary>绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }
    }
}
