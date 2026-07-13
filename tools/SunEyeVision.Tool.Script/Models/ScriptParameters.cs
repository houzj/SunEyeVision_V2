using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
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
        /// 最简代码模板 — Init + Process 空壳骨架，无示例注释，仅含 null 检查和输出传递。
        /// 默认新建工具时显示的代码。如果代码不小心被删除，可通过"复位最简"按钮恢复到此模板。
        /// </summary>
        public static string MinimalCode => LoadEmbeddedResource("Resources.MinimalCode.txt");

        /// <summary>
        /// Demo 示例代码 — Halcon 算子风格，覆盖 5 个核心视觉处理场景。
        /// 用户可通过工具栏"Demo示例"按钮加载此代码进行学习和测试。
        /// </summary>
        /// <remarks>
        /// 场景1: 图像信息查询（GetImageSize + 通道数）
        /// 场景2: 彩色转灰度（高层封装 CreateGrayFromColor + 底层 Rgb1ToGray）
        /// 场景3: 阈值分割（Threshold + AreaCenter）
        /// 场景4: Blob 分析（Connection + SelectShape + 遍历输出）
        /// 场景5: 全局变量读写（GetGlobal/SetGlobal）
        /// 注意：input1/output1 类型为 IVisionImage，强制转换 ((HalImage)input1).Unwrap() 拿到 HImage。
        /// 场景5 演示了全局变量读写的通用接口优先原则：用 IVisionImage 拿上游图像，需 Halcon 算子时再 is 断言。
        /// </remarks>
        public static string DemoCode => LoadEmbeddedResource("Resources.DemoCode.txt");

        /// <summary>
        /// 向后兼容别名 — DefaultMethodCode 目前指向 DemoCode
        /// </summary>
        public static string DefaultMethodCode => DemoCode;

        private string _scriptCode = MinimalCode;
        private string _assemblyReferenceDir = "ScriptRefs";
        private ObservableCollection<ScriptInputConfig> _inputs =
        [
            new ScriptInputConfig { Name = "input1", TypeName = "Image" }
        ];
        private ObservableCollection<ScriptOutputConfig> _outputs =
        [
            new ScriptOutputConfig { Name = "output1", TypeName = "Image" }
        ];

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
        /// 输入绑定解析后的实际值（运行时填充，供调试监视面板显示）
        /// 相当于普通工具的 ResolvedInputImage —— 都是"绑定解析完成后得到的实际输入值"
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object?>? ResolvedInputValues { get; set; }

        /// <summary>
        /// 获取引用目录的完整路径（基于工具自身 Assembly.Location 解析，
        /// 跟随 OutDir 迁移，不依赖主程序 AppDomain.BaseDirectory）。
        /// 注：typeof(ScriptParameters).Assembly.Location = 工具 DLL 所在目录
        /// （如 dev/Debug/plugins/SunEyeVision.Tool.Script/），
        /// AssemblyReferenceDir 默认 "ScriptRefs" 拼接后指向同目录下的 ScriptRefs 子目录。
        /// </summary>
        [JsonIgnore]
        public string AssemblyReferenceFullPath
        {
            get
            {
                var assemblyDir = Path.GetDirectoryName(typeof(ScriptParameters).Assembly.Location)
                    ?? AppContext.BaseDirectory;
                return Path.GetFullPath(Path.Combine(assemblyDir, _assemblyReferenceDir));
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
            sb.AppendLine("using SunEyeVision.Plugin.SDK.Scripting;");
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

        /// <summary>
        /// 从嵌入式资源加载文本内容
        /// </summary>
        private static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullName = $"{assembly.GetName().Name}.{resourceName}";
            using var stream = assembly.GetManifestResourceStream(fullName);
            if (stream == null)
                throw new InvalidOperationException($"嵌入式资源 '{fullName}' 未找到。确认 Build Action 设置为 Embedded resource。");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}