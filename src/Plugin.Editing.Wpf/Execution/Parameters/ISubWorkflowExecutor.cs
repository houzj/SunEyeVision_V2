using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;
using SunEyeVision.Plugin.Contracts.Vision;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters
{
    public interface ISubWorkflowExecutor
    {
        string SubroutineId { get; set; }
        Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }
    }
}
