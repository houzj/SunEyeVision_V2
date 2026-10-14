using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本输入配置 — 定义脚本的一个输入变量及其上游绑定关系。
    /// 对应子程序 SubroutineInputConfig 的设计模式。
    /// </summary>
    public class ScriptInputConfig : ObservableObject
    {
        public Guid FieldId { get; set; } = Guid.NewGuid();
        private string _name = string.Empty;
        private string _displayName = string.Empty;
        private string _typeName = "Image";
        private AvailableDataSource? _source;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        /// <summary>变量名称（同时也是 C# 字段名，用户可编辑但需符合标识符规范）</summary>
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
        [JsonIgnore]
        public Type? ParamType => SdkTypeVocabulary.ToSystemType(TypeName);

        public AvailableDataSource? Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value))
                {
                    if (value != null && SdkTypeVocabulary.ToTypeVocabulary(value.PropertyType) is { } vocabulary)
                        TypeName = vocabulary;
                }
            }
        }

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

        [JsonIgnore]
        public string SourceDisplayText => Source?.DropdownDisplayText ?? string.Empty;

        /// <summary>映射为 C# 类型名（供生成字段声明和自动补全）</summary>
        [JsonIgnore]
        public string CsTypeName => ScriptTypeMapper.ToCsTypeName(TypeName);

        /// <summary>生成字段声明代码行</summary>
        public string GenerateFieldCode()
        {
            return $"public {CsTypeName} {Name} {ScriptTypeMapper.GetFieldDefault(TypeName)};";
        }
    }
}
