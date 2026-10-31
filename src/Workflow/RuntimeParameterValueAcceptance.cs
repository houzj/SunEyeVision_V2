using System;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

/// <summary>Host capability captured with a published configuration, not a design-node
/// reference or a value store. Only the owning host can install an admitted parameter write.</summary>
public sealed class RuntimeParameterValueAcceptance
{
    private readonly Action<string,string,ToolExecutionIdentity,RuntimeToolConfiguration,string,object?> _accept;
    internal RuntimeParameterValueAcceptance(Action<string,string,ToolExecutionIdentity,RuntimeToolConfiguration,string,object?> accept)
        =>_accept=accept??throw new ArgumentNullException(nameof(accept));
    internal void Accept(string workflowId,string nodeId,ToolExecutionIdentity identity,RuntimeToolConfiguration configuration,
        string fieldId,object? value)=>_accept(workflowId,nodeId,identity,configuration,fieldId,value);
}
