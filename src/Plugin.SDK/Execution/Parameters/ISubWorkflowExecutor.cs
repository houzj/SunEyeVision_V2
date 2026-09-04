using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    public interface ISubWorkflowExecutor
    {
        string SubroutineId { get; set; }
        Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }
    }
}
