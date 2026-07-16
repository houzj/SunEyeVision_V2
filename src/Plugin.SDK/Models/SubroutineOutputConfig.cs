using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models
{
    public class SubroutineOutputConfig : ObservableObject
    {
        private string _name = string.Empty;
        private string _typeName = "Float";
        private string _displayName = string.Empty;
        private string? _group;
        private AvailableDataSource? _source;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>中文显示名（用于结果面板/绑定下拉展示，空则回退 Name）</summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>分组（用于树形分组，null 表示无分组）</summary>
        public string? Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
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

        [JsonIgnore]
        public Type? ParamType => SdkTypeVocabulary.ToSystemType(TypeName);

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

        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        public string SourceDisplayText => Source?.DropdownDisplayText ?? string.Empty;

        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }
    }
}
