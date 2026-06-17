using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具参数 — 包含脚本代码、DLL 引用目录、输入/输出变量定义
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
using System.Diagnostics;

/************************************
 * 脚本工具 - 用户自定义脚本
 * 继承 Script，实现 Init/Process/Release 生命周期
 * 可用引用：System.Runtime（BCL 核心类型）、
 *   SunEyeVision.Tool.Script（Script 基类）、
 *   SunEyeVision.Plugin.SDK（全局变量和日志 API）
 * 需要额外程序集请通过 [程序集引用管理] 对话框添加
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

        private string _scriptCode = DefaultScriptCode;
        private string _assemblyReferenceDir = "ScriptRefs";
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
            var dir = AssemblyReferenceFullPath;
            var count = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.dll").Length : 0;
            return $"脚本行数: {ScriptCode.Split('\n').Length}, 引用DLL: {count}, 输入: {InputVariables.Count}, 输出: {OutputVariables.Count}";
        }
    }
}