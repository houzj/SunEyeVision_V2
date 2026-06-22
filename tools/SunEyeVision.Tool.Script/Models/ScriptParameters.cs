using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具参数 — 包含脚本代码、DLL 引用目录、输入/输出变量定义
    /// </summary>
    [JsonDerivedType(typeof(ScriptParameters), "Script")]
    public class ScriptParameters : ToolParameters
    {
        /// <summary>
        /// 默认代码模板（完整的 partial class，用户可自由写 using/class/方法/字段等）
        /// </summary>
        public const string DefaultMethodCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Plugin.SDK.VisionAdapters;

public partial class UserScript : Script
{
    public override void Init()
    {
        // 在此处初始化代码
    }

    public override void Process()
    {
        // 在此处编写算法逻辑
        // 可直接访问输入变量（如 image1, threshold）
        // 可直接赋值输出变量（如 result = xxx）
    }
}";

        private string _scriptCode = DefaultMethodCode;
        private string _assemblyReferenceDir = "ScriptRefs";
        private ObservableCollection<ScriptInputConfig> _inputs = [];
        private ObservableCollection<ScriptOutputConfig> _outputs = [];

        /// <summary>
        /// 脚本方法体代码（不含 class 声明，由 BuildFullCode 自动包裹）
        /// </summary>
        [JsonPropertyOrder(1)]
        public string ScriptCode
        {
            get => _scriptCode;
            set => SetProperty(ref _scriptCode, value, "脚本代码");
        }

        /// <summary>
        /// 程序集引用目录（相对路径，相对于插件输出目录）。目录下的所有 *.dll 均为程序集引用。
        /// </summary>
        [JsonPropertyOrder(2)]
        public string AssemblyReferenceDir
        {
            get => _assemblyReferenceDir;
            set => SetProperty(ref _assemblyReferenceDir, value, "程序集引用目录");
        }

        /// <summary>
        /// 获取引用目录的完整路径（基于应用程序根目录解析，不依赖插件 DLL 路径）
        /// </summary>
        [JsonIgnore]
        public string AssemblyReferenceFullPath
        {
            get
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.GetFullPath(Path.Combine(baseDir, "data", _assemblyReferenceDir));
            }
        }

        /// <summary>
        /// 输入变量配置列表（含 Name/TypeName/绑定关系）
        /// </summary>
        [JsonPropertyOrder(3)]
        public ObservableCollection<ScriptInputConfig> Inputs
        {
            get => _inputs;
            set => SetProperty(ref _inputs, value, "输入");
        }

        /// <summary>
        /// 输出变量配置列表（含 Name/TypeName/绑定关系）
        /// </summary>
        [JsonPropertyOrder(4)]
        public ObservableCollection<ScriptOutputConfig> Outputs
        {
            get => _outputs;
            set => SetProperty(ref _outputs, value, "输出");
        }

        /// <summary>
        /// 系统生成的字段声明 partial class（不含用户代码，由 Inputs/Outputs 字段声明生成）
        /// </summary>
        [JsonIgnore]
        public string GeneratedCode => BuildGeneratedCode();

        private string BuildGeneratedCode()
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using SunEyeVision.Tool.Script.ScriptEngine;");
            sb.AppendLine("using SunEyeVision.Plugin.SDK.VisionAdapters;");
            sb.AppendLine();
            sb.AppendLine("public partial class UserScript : Script");
            sb.AppendLine("{");

            // 输入字段
            foreach (var input in Inputs)
                sb.AppendLine($"    {input.GenerateFieldCode()}");

            if (Inputs.Count > 0)
                sb.AppendLine();

            // 输出字段
            foreach (var output in Outputs)
                sb.AppendLine($"    {output.GenerateFieldCode()}");

            sb.AppendLine("}");
            return sb.ToString();
        }

        public new string GetParameterSummary()
        {
            var dir = AssemblyReferenceFullPath;
            var count = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.dll").Length : 0;
            return $"脚本行数: {ScriptCode.Split('\n').Length}, 引用DLL: {count}, 输入: {Inputs.Count}, 输出: {Outputs.Count}";
        }
    }
}