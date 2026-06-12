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
    public class ScriptParameters : ToolParameters
    {
        private string _scriptCode = string.Empty;
        private List<string> _assemblyReferences = new();
        private List<ScriptParamItem> _inputVariables = new();
        private List<ScriptParamItem> _outputVariables = new();

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