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
        /// 默认代码模板 — Halcon 算子风格，覆盖 5 个核心视觉处理场景
        /// </summary>
        /// <remarks>
        /// 客户新建 Script 工具时，编辑器直接显示一份可编译运行的标准示例：
        /// 场景1: 图像信息查询（GetImageSize + 通道数）
        /// 场景2: 彩色转灰度（高层封装 CreateGrayFromColor + 底层 Rgb1ToGray）
        /// 场景3: 阈值分割（Threshold + AreaCenter）
        /// 场景4: Blob 分析（Connection + SelectShape + 遍历输出）
        /// 场景5: 全局变量读写（GetGlobal/SetGlobal）
        /// 用户在此基础上自由改写即可。
        /// 注意：input1/output1 类型为 IVisionImage，强制转换 ((HalImage)input1).Unwrap() 拿到 HImage。
        /// 场景5 演示了全局变量读写的通用接口优先原则：用 IVisionImage 拿上游图像，需 Halcon 算子时再 is 断言。
        /// </remarks>
        public const string DefaultMethodCode = @"using System;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Plugin.SDK.VisionAdapters;

public partial class UserScript : Script
{
    private int _invokeCount;
    private double _lastMean;

    public override void Init()
    {
        _invokeCount = 0;
        LogInfo(""脚本初始化完成"");
    }

    public override void Process()
    {
        _invokeCount++;

        // === 场景1: 图像信息查询 ===
        if (input1 == null || input1.IsEmpty)
        {
            LogError(""输入图像 input1 为空，请在上游绑定图像源"");
            return;
        }

        // 使用 Halcon 算子查询图像尺寸
        HOperatorSet.GetImageSize(((HalImage)input1).Unwrap(), out var imgW, out var imgH);
        LogInfo($""[{_invokeCount}] 输入图像: {imgW.I}×{imgH.I}, 通道数: {input1.Channels}"");

        // === 场景2: 彩色转灰度（高层封装 + 底层算子对比） ===
        var grayImage = HalImage.CreateGrayFromColor(input1);
        output1 = grayImage;

        // 底层算子方式（演示两种调用风格）
        HOperatorSet.Rgb1ToGray(((HalImage)input1).Unwrap(), out var grayObj);
        using var grayH = new HImage(grayObj);
        LogInfo(""灰度转换完成"");

        // === 场景3: 阈值分割 ===
        HOperatorSet.Threshold(grayImage.Unwrap(), out var thresholdRegion, 100.0, 255.0);
        HOperatorSet.AreaCenter(thresholdRegion, out var thresholdArea, out _, out _);
        LogInfo($""阈值分割区域: {thresholdArea.I} 像素"");

        // === 场景4: Blob 分析（连通域 + 面积筛选 + 遍历输出） ===
        HOperatorSet.Connection(thresholdRegion, out var connectedRegions);
        HOperatorSet.SelectShape(connectedRegions, out var bigBlobs, ""area"", ""and"", 500.0, 99999.0);

        var blobCount = bigBlobs.CountObj();
        LogInfo($""筛选后 Blob 数量: {blobCount}"");

        for (int i = 1; i <= blobCount; i++)
        {
            HOperatorSet.SelectObj(bigBlobs, out var singleBlob, i);
            HOperatorSet.AreaCenter(singleBlob, out var area, out var row, out var col);
            LogInfo($""  Blob#{i}: 面积={area.I}, 中心=({col.D:F1}, {row.D:F1})"");
        }

        // === 场景5: 全局变量读写（通用接口优先，引擎特定能力再断言） ===
        // 5a. 用通用接口拿上游图像，不受引擎类型限制
        var upstream = GetGlobal<IVisionImage>(""upstreamImage"");
        if (upstream != null)
            LogInfo($""上游图像可用: {upstream.Width}×{upstream.Height}, 通道数: {upstream.Channels}"");

        // 5b. 需要用 Halcon 算子时，显式断言引擎类型
        if (upstream is HalImage halUpstream)
        {
            HOperatorSet.GetImageSize(halUpstream.Unwrap(), out var w, out var h);
            LogInfo($""[Halcon] 解析尺寸: {w.I}×{h.I}"");
        }
        else if (upstream != null)
        {
            LogWarning($""[非 Halcon] 上游类型 {upstream.GetType().Name}, 无法用 HOperatorSet 算子"");
        }

        // 5c. 写:推荐用通用接口类型 SetGlobal
        SetGlobal(""lastImage"", upstream);
        SetGlobal(""lastBlobCount"", blobCount);
        SetGlobal(""lastInvokeCount"", _invokeCount);
    }
}";

        private string _scriptCode = DefaultMethodCode;
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