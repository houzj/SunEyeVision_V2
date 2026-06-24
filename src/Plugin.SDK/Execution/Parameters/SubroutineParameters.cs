using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    public class SubroutineParameters : ToolParameters, ISubWorkflowExecutor
    {
        public string SubroutineId { get; set; } = string.Empty;
        public string SubroutineName { get; set; } = string.Empty;

        [JsonIgnore]
        public Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }

        public ObservableCollection<SubroutineInputConfig> Inputs { get; set; } = new();
        public ObservableCollection<SubroutineOutputConfig> Outputs { get; set; } = new();
        public ObservableCollection<SubroutineDisplayItem> DisplayConfigs { get; set; } = new();
        public SubroutineLoopConfig LoopConfig { get; set; } = new();

        [JsonIgnore]
        public int CurrentIteration { get; set; }

        [JsonIgnore]
        public bool IsLoopEnabled => LoopConfig?.EnableLoop ?? false;
    }
}
