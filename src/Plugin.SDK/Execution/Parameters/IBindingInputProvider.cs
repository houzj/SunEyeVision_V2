using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public sealed record BindingInputReference(string Key, string Name, Func<string> Read, Action<string> Write,
    IReadOnlyList<Type> AllowedTypes, bool AllowsTypeChange = false, string InterpretationKey = "",
    Func<string, string?>? ValidateLiteral = null, Func<string?>? ReadWorkflowId = null,
    bool ReferenceOnly = false, bool Active = true, bool InternalProjection = false, Func<Guid?>? ReadFieldId = null);
public interface IBindingInputProvider
{
    IReadOnlyList<BindingInputReference> GetBindingInputs();
}

/// <summary>Model-level mode selection; independent of control visibility.</summary>
public interface IBindingInputActivityProvider
{
    bool IsBindingInputActive(string key);
}

public sealed record BindingSourceIdentity(string Expression, string WorkflowId, string SourceNodeId, Guid? FieldId = null, string? VariableId = null);
