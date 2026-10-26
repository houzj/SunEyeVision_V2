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
        public override SunEyeVision.Plugin.SDK.Validation.ValidationResult Validate()
        {
            var result = base.Validate();
            if (RunParameterGroups is null) { result.AddError("运行参数分组缺失。"); return result; }
            var groups = RunParameterGroups.ToArray();
            if (groups.Any(g => g is null || g.Id == Guid.Empty || string.IsNullOrWhiteSpace(g.Name) || g.Name.Length > SubroutineRunParameterGroup.MaximumNameLength || g.Parameters is null))
            { result.AddError("运行参数分组身份或名称无效。"); return result; }
            if (groups.Select(g => g.Id).Distinct().Count() != groups.Length || groups.Select(g => g.Name).Distinct(StringComparer.Ordinal).Count() != groups.Length)
                result.AddError("运行参数分组身份或名称重复。");
            var items = groups.SelectMany(g => g.Parameters).ToArray();
            if (items.Any(i => i is null || i.Id == Guid.Empty || string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > SubroutineRunParameterGroup.MaximumNameLength || i.Targets is null))
            { result.AddError("运行参数身份或名称无效。"); return result; }
            if (items.Select(i => i.Id).Distinct().Count() != items.Length || items.Select(i => i.Name).Distinct(StringComparer.Ordinal).Count() != items.Length)
                result.AddError("运行参数身份或名称重复。");
            foreach (var item in items)
            {
                if(item.SourceToolType is null||item.SourceParameterName is null||string.IsNullOrEmpty(item.SourceToolType)!=string.IsNullOrEmpty(item.SourceParameterName)||!Enum.IsDefined(item.SourceEditorKind))
                    result.AddError("运行参数原模块声明无效。");
                if (item.Targets.Any(t => t is null || string.IsNullOrWhiteSpace(t.WorkflowId) || string.IsNullOrWhiteSpace(t.NodeId) ||
                    (t.ExposedParameterId is { } id ? id == Guid.Empty || !string.IsNullOrEmpty(t.PropertyName) : string.IsNullOrWhiteSpace(t.PropertyName))))
                { result.AddError("运行参数关联身份无效。"); continue; }
                if (item.Targets.Select(t => (t.WorkflowId, t.NodeId, t.PropertyName, t.ExposedParameterId)).Distinct().Count() != item.Targets.Count)
                    result.AddError("运行参数关联重复。");
            }
            return result;
        }

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
        public ObservableCollection<SubroutineRunParameterGroup> RunParameterGroups { get; set; } = new();
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
