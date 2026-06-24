using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models
{
    public class SubroutineInputConfig : ObservableObject
    {
        private string _name = string.Empty;
        private string? _bindingKey;
        private string _typeName = "Image";
        private AvailableDataSource? _source;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? BindingKey
        {
            get => _bindingKey;
            init => _bindingKey = value;
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
