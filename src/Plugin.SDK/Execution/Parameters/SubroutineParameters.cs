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

        IReadOnlyList<OutputVariable> IOutputContractProvider.GetOutputContract()
        {
            var variables = new List<OutputVariable>();

            // 元信息：执行信息分组
            variables.Add(new OutputVariable { Name = "Status", VariableType = "String", DisplayName = "运行状态", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "ExecutionTimeMs", VariableType = "Integer", DisplayName = "执行耗时(ms)", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "SubWorkflowExecutionTimeMs", VariableType = "Integer", DisplayName = "子流程耗时(ms)", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "ExecutedNodeCount", VariableType = "Integer", DisplayName = "执行节点数", Group = "执行信息", Kind = OutputVariableKind.Data });

            // 用户配置的输出
            foreach (var cfg in Outputs)
            {
                variables.Add(new OutputVariable
                {
                    Name = cfg.Name,
                    VariableType = cfg.TypeName,
                    DisplayName = cfg.Name,
                    Group = string.IsNullOrEmpty(cfg.Group) ? "输出结果" : cfg.Group,
                    Kind = OutputVariableKind.Data,
                });
            }

            return variables;
        }
    }
}
