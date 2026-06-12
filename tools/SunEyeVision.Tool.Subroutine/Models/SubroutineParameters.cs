using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Workflow;
using ExecutionResult = SunEyeVision.Workflow.ExecutionResult;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序参数 — 子程序本质上是一个"大号工具"，
    /// 其 Run() 内部遍历子工作流的节点进行计算。
    /// 实现 ISubWorkflowExecutor 接口供引擎注入执行委托。
    /// </summary>
    public class SubroutineParameters : ToolParameters, ISubWorkflowExecutor
    {
        /// <summary>子工作流ID</summary>
        public string SubroutineId { get; set; } = string.Empty;

        /// <summary>子程序名称</summary>
        public string SubroutineName { get; set; } = string.Empty;

        /// <summary>子工作流执行委托（引擎在运行时注入）</summary>
        [JsonIgnore]
        public Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }

        /// <summary>输入参数配置（Param 声明）</summary>
        public ObservableCollection<SubroutineInputConfig> Inputs { get; set; } = new();

        /// <summary>输出项配置（Result 声明）</summary>
        public ObservableCollection<SubroutineOutputConfig> Outputs { get; set; } = new();

        /// <summary>显示配置</summary>
        public ObservableCollection<SubroutineDisplayItem> DisplayConfigs { get; set; } = new();

        /// <summary>循环配置</summary>
        public SubroutineLoopConfig LoopConfig { get; set; } = new();

        /// <summary>当前迭代次数（运行时）</summary>
        [JsonIgnore]
        public int CurrentIteration { get; set; }

        /// <summary>是否启用循环</summary>
        [JsonIgnore]
        public bool IsLoopEnabled => LoopConfig?.EnableLoop ?? false;
    }
}
