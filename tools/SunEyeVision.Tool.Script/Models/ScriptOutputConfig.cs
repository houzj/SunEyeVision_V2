using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

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
        public string CsTypeName => TypeName switch
        {
            "Image"       => "IVisionImage",
            "Integer"     => "int",
            "Double"      => "double",
            "Float"       => "double",
            "String"      => "string",
            "Boolean"     => "bool",
            "Byte"        => "byte",
            "Point"       => "System.Windows.Point",
            "Rectangle"   => "System.Windows.Rect",
            "Size"        => "System.Windows.Size",
            "Region"      => "IVisionRegion",
            "Circle"      => "Circle2d",
            "Line"        => "Line2d",
            "Annulus"     => "Annulus2d",
            "RotatedRect" => "RotatedRect",
            "Polygon"     => "Polygon2d",
            _             => "object"
        };

        /// <summary>生成字段声明代码行</summary>
        public string GenerateFieldCode()
        {
            var defaultExpr = TypeName switch
            {
                "Image"       => "= null!",
                "String"      => "= string.Empty",
                "Integer"     => "= 0",
                "Double"      => "= 0.0",
                "Float"       => "= 0.0",
                "Boolean"     => "= false",
                "Byte"        => "= 0",
                "Point"       => "= default",
                "Rectangle"   => "= default",
                "Size"        => "= default",
                "Region"      => "= null!",
                "Circle"      => "= default",
                "Line"        => "= default",
                "Annulus"     => "= default",
                "RotatedRect" => "= default",
                "Polygon"     => "= null!",
                _             => "= default!"
            };
            return $"public {CsTypeName} {Name} {defaultExpr};";
        }
    }
}
