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
            if(Inputs is null||Outputs is null||DisplayConfigs is null){result.AddError("Group输入、输出或显示集合缺失。");return result;}
            if(Inputs.Any(i=>i is null)||Outputs.Any(o=>o is null)){result.AddError("Group输入输出项缺失。");return result;}
            if(Inputs.Count>SubroutineIoRules.MaximumItems||Outputs.Count>SubroutineIoRules.MaximumItems)result.AddError("Group输入或输出最多1024项。");
            var ioNames=Inputs.Select(i=>i.Name).Concat(Outputs.Select(o=>o.Name)).ToArray();
            if(ioNames.Any(n=>!SubroutineIoRules.IsValidName(n)))result.AddError("Group输入输出名称不能为空、超过31字符或包含非法字符。");
            if(ioNames.Distinct(StringComparer.Ordinal).Count()!=ioNames.Length)result.AddError("Group输入和输出名称不能重复。");
            var fieldIds=Inputs.Select(i=>i.FieldId).Concat(Outputs.Select(o=>o.FieldId)).ToArray();
            if(fieldIds.Any(id=>id==Guid.Empty)||fieldIds.Distinct().Count()!=fieldIds.Length)result.AddError("Group输入输出字段身份无效或重复。");
            if(Inputs.Any(i=>!SubroutineIoRules.HasConsistentType(i.TypeName,i.ParamType))||Outputs.Any(o=>!SubroutineIoRules.HasConsistentType(o.TypeName,o.ParamType)))result.AddError("Group输入输出类型无效。");

            if(LoopConfig is null)result.AddError("循环配置缺失。");
            else
            {
                if(LoopConfig.LoopInterval is <0 or >10000)result.AddError("循环间隔应为0到10000毫秒。");
                if(LoopConfig.EnableLoop&&LoopConfig.EnableBreak)
                {
                    if(LoopConfig.DataType is not ("int" or "float" or "string"))result.AddError("循环比较类型必须为int、float或string。");
                    if(LoopConfig.ComparisonOperator is not (">" or "<" or "==" or "!=" or ">=" or "<="))result.AddError("循环比较运算符无效。");
                }
            }
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
