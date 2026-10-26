using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    public class SubroutineParameters : ToolParameters, ISubWorkflowExecutor, IOutputContractProvider
    {
        public override ToolParameters CreateExecutionSnapshot()
        {
            var copy = (SubroutineParameters)ToolExecutionPreparation.CopyParameters(this);
            // The workflow owner injects this invocation service after configuration freezing.
            // It is deliberately excluded from persistence, but must reach the execution copy.
            copy.ExecuteSubWorkflowAsync = ExecuteSubWorkflowAsync;
            return copy;
        }
        public string SubroutineId { get; set; } = string.Empty;
        public string SubroutineName { get; set; } = string.Empty;

        [JsonIgnore]
        public Func<string, IVisionImage?, Dictionary<string, object>?, Task<ExecutionResult>>? ExecuteSubWorkflowAsync { get; set; }

        public ObservableCollection<SubroutineInputConfig> Inputs { get; set; } = new();
        public ObservableCollection<SubroutineOutputConfig> Outputs { get; set; } = new();
        public ObservableCollection<SubroutineDisplayItem> DisplayConfigs { get; set; } = new();
        private SubroutineLoopConfig _loopConfig = new();
        public SubroutineLoopConfig LoopConfig { get => _loopConfig; set => SetProperty(ref _loopConfig, value); }

        [JsonIgnore]
        public int CurrentIteration { get; set; }

        [JsonIgnore]
        public bool IsLoopEnabled => LoopConfig?.EnableLoop ?? false;

        IReadOnlyList<OutputVariable> IOutputContractProvider.GetOutputContract()
        {
            var prototype = new SubroutineResults { OutputConfigs = Outputs.ToList() };
            var variables = prototype.GetOutputVariables();
            foreach (var variable in variables) variable.RuntimeValue = null;
            return variables;
        }
    }
}
