using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Script.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具参数 — 包含脚本代码、DLL 引用列表、输入/输出变量定义
    /// </summary>
    [JsonDerivedType(typeof(ScriptParameters), "Script")]
    public class ScriptParameters : ToolParameters
    {
        /// <summary>
        /// 默认脚本代码模板（三段式生命周期，含常用 using）
        /// </summary>
        public const string DefaultScriptCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using HalconDotNet;

/************************************
 * 脚本工具 - 用户自定义脚本
 * 继承 Script，实现 Init/Process/Release 生命周期
 * 自动注入 using：System.*, HalconDotNet, System.Drawing 等
 ************************************/
public class UserScript : Script
{
    private int _processCount;
    private DateTime _startTime;

    public override void Init()
    {
        _processCount = 0;
        _startTime = DateTime.Now;
        LogInfo($""[{_startTime:HH:mm:ss}] 脚本初始化完成"");
    }

    public override void Process()
    {
        _processCount++;
        var elapsedMs = (DateTime.Now - _startTime).TotalMilliseconds;

        SetOutput(""ProcessCount"", _processCount);
        SetOutput(""ElapsedMs"", elapsedMs);

        LogInfo($""第 {_processCount} 次处理, 耗时 {elapsedMs:F0}ms"");
    }

    public override void Release()
    {
        LogInfo($""共处理 {_processCount} 次, 总耗时 {(DateTime.Now - _startTime).TotalSeconds:F1}s"");
    }
}
";

        /// <summary>
        /// 新建工具时预填充的默认程序集引用列表（按名称装载）
        /// </summary>
        private static readonly List<string> DefaultAssemblyReferences =
        [
            "System.Drawing",
            "System.Data",
            "System.Xml",
            "System.Xml.Linq",
            "System.Net.Http",
            "System.Numerics",
            "System.IO.Compression"
        ];

        private string _scriptCode = DefaultScriptCode;
        private List<string> _assemblyReferences = new(DefaultAssemblyReferences);
        private List<ScriptParamItem> _inputVariables = [];
        private List<ScriptParamItem> _outputVariables = [];

        /// <summary>
        /// 脚本代码（C# 源码文本）
        /// </summary>
        [JsonPropertyOrder(1)]
        public string ScriptCode
        {
            get => _scriptCode;
            set => SetProperty(ref _scriptCode, value, "脚本代码");
        }

        /// <summary>
        /// 引用的 DLL 程序集路径列表
        /// </summary>
        [JsonPropertyOrder(2)]
        public List<string> AssemblyReferences
        {
            get => _assemblyReferences;
            set => SetProperty(ref _assemblyReferences, value, "程序集引用");
        }

        /// <summary>
        /// 输入变量列表（调试控件中 DataGrid 绑定显示）
        /// </summary>
        [JsonPropertyOrder(3)]
        public List<ScriptParamItem> InputVariables
        {
            get => _inputVariables;
            set => SetProperty(ref _inputVariables, value, "输入变量");
        }

        /// <summary>
        /// 输出变量列表（调试控件中 DataGrid 绑定显示，实时监视）
        /// </summary>
        [JsonPropertyOrder(4)]
        public List<ScriptParamItem> OutputVariables
        {
            get => _outputVariables;
            set => SetProperty(ref _outputVariables, value, "输出变量");
        }

        public new string GetParameterSummary()
        {
            return $"脚本行数: {ScriptCode.Split('\n').Length}, 引用: {AssemblyReferences.Count}, 输入: {InputVariables.Count}, 输出: {OutputVariables.Count}";
        }
    }
}