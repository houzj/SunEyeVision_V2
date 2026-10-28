using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.Workflow;

/// <summary>One published document generation and its actual execution/resource owners, captured before asynchronous input waits.</summary>
public sealed class WorkflowPublishedConfiguration
{
    private WorkflowPublishedConfiguration(BindingConfigurationSnapshot published,Solution document)
    {
        var configuration=published.CopyConfiguration();
        if(configuration.Id!=document.Id)throw new InvalidOperationException("正式配置快照属于其它方案。");
        var actual=document.Workflows.ToDictionary(flow=>flow.Id,StringComparer.Ordinal);
        foreach(var flow in configuration.Workflows)
        {
            if(!actual.TryGetValue(flow.Id,out var owner))throw new InvalidOperationException("正式配置快照中的流程已退出当前方案。");
            flow.AttachExecutionOwner(owner.ExecutionOwner);
        }
        Configuration=WorkflowRunConfiguration.Capture(configuration.Workflows,published.Version);
        var definitions=GlobalVariable.CaptureDefinitions(configuration.GlobalVariables).ToArray();
        var runtimeValues=GlobalVariable.CaptureValues(document.GlobalVariables);
        foreach(var variable in definitions)
        {
            // Configuration history is not the authority for live variable values. Bound sources
            // never inherit their saved/design value, and unsupported resources are not retained.
            if(variable.ProducerBinding is not null||!string.IsNullOrEmpty(variable.RebindingReason)||!string.IsNullOrEmpty(variable.BindingValidationPending)
                ||!GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out _,out _,out _))variable.Value=null;
            else variable.Value=runtimeValues.TryGetValue(variable.Id,out var value)?GlobalVariable.CopyEditableValue(value.Value)
                :throw new InvalidOperationException("运行值快照缺少正式变量身份："+variable.Id);
        }
        GlobalDefinitions=Array.AsReadOnly(definitions);
        ExecutionOwner=document.ExecutionOwner;ResourcePaths=SolutionResourceResolver.For(document);
        SolutionId=configuration.Id;SolutionName=configuration.Name;
    }
    public WorkflowRunConfiguration Configuration {get;}
    public IReadOnlyList<GlobalVariable> GlobalDefinitions {get;}
    public ToolExecutionOwner ExecutionOwner {get;}
    public SolutionResourceResolver ResourcePaths {get;}
    public string SolutionId {get;}
    public string SolutionName {get;}
    public static WorkflowPublishedConfiguration Capture(BindingConfigurationSnapshot published,Solution document)
    {ArgumentNullException.ThrowIfNull(published);ArgumentNullException.ThrowIfNull(document);return new(published,document);}
}
