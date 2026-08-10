using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本输出配置 — 定义脚本的一个输出变量及其绑定关系。
    /// 对应子程序 SubroutineOutputConfig 的设计模式。
    /// </summary>
    public class ScriptOutputConfig : ObservableObject
    {
        private string _name = string.Empty;
        private string _typeName = "Double";
        private string? _group;
        private AvailableDataSource? _source;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;

        /// <summary>变量名称（同时也是 C# 字段名）</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
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
                    if (value != null && !string.IsNullOrEmpty(value.TypeName))
                        TypeName = value.TypeName;
                }
            }
        }

        /// <summary>是否为累计输出（累计输出只显示最后一次值）</summary>
        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        /// <summary>绑定源路径</summary>
        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        /// <summary>绑定显示文本</summary>
        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }

        [JsonIgnore]
        public string SourceDisplayText => Source?.DropdownDisplayText ?? string.Empty;

        /// <summary>映射为 C# 类型名</summary>
        [JsonIgnore]
        public string CsTypeName => ScriptTypeMapper.ToCsTypeName(TypeName);

        /// <summary>
        /// 生成字段声明代码行（输出字段只定义不赋值，CLR 零初始化后 = 类型默认值）
        /// 监视面板显示时跳过类型默认值，显示"无结果"
        /// </summary>
        public string GenerateFieldCode()
        {
            return $"public {CsTypeName} {Name};";
        }
    }
}
