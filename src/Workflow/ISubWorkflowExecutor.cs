using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 子工作流执行器接口 — 由 SubroutineParameters 实现，
    /// 引擎在 ConvertNodesForExecution 中通过此接口注入执行委托。
    /// 避免 Workflow 项目直接依赖具体工具项目。
    /// </summary>
    public interface ISubWorkflowExecutor
    {
        /// <summary>子工作流ID</summary>
        string SubroutineId { get; set; }

        /// <summary>子工作流执行委托（引擎运行时注入）</summary>
        Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }
    }
}
