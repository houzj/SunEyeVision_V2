using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>Common host publication envelope. It owns the original Runtime output,
/// never a tool-specific copy, and exposes only formally declared canonical values.</summary>
public sealed class RuntimeToolResults : ToolResults
{
    private readonly IReadOnlyDictionary<string,object?> _values;
    public IToolOutput? Output { get; }

    public RuntimeToolResults(string toolId, ToolSchema schema, ToolExecutionIdentity identity,
        ToolOutcome outcome, IDisposable resources, double elapsed)
    {
        Output=outcome.Output;
        Resources=new ResultResourceOwner(()=>Release(Output as IDisposable,resources));
        try
        {
            ToolId=toolId;ToolName=toolId;ExecutionIdentity=identity;
            BindingOutputSchema=new ToolSchema(Array.Empty<Port>(),schema.Outputs);
            ExecutionTimeMs=elapsed;
            Status=outcome.Success?ExecutionStatus.Success:
                identity.CancellationToken.IsCancellationRequested?ExecutionStatus.Cancelled:ExecutionStatus.Failed;
            IsPass=outcome.Success&&outcome.Judgment!=BusinessJudgment.Fail;
            ErrorMessage=outcome.Success?null:string.Join("; ",outcome.Diagnostics.Select(d=>d.Message));
            foreach(var diagnostic in outcome.Diagnostics)AddWarning(diagnostic.Code+": "+diagnostic.Message);
            _values=outcome.Success?new InputValues(schema.Outputs,
                outcome.Output?.ToValues()??throw new ArgumentException("Successful Runtime has no output.")).CopyValues()
                :new Dictionary<string,object?>();
        }
        catch(Exception failure)
        {
            try{Resources.Dispose();}
            catch(Exception cleanup){throw new AggregateException("Runtime result capture and cleanup failed.",failure,cleanup);}
            throw;
        }
    }

    private static void Release(IDisposable? output,IDisposable resources)
    {
        Exception? failure=null;
        try{output?.Dispose();}catch(Exception error){failure=error;}
        try{resources.Dispose();}
        catch(Exception cleanup)
        {if(failure is not null)throw new AggregateException("Runtime output and resource cleanup failed.",failure,cleanup);throw;}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public IReadOnlyDictionary<string,object?> Values=>_values;
    public override bool TryGetDeclaredOutputValue(string portId,out object? value)=>_values.TryGetValue(portId,out value);
    public static IReadOnlyList<OutputVariable> Describe(ToolSchema schema)=>schema.Outputs.Select(port=>new OutputVariable
    {
        PortId=port.Id,Name=port.Id,DisplayName=port.Id,ValueDeclaration=port.Type,AllowsNull=port.Nullable,
        IsCollection=port.Type.Shape==ValueShape.Sequence,Kind=OutputVariableKind.Data
    }).ToArray();
    public override IReadOnlyList<OutputVariable> GetOutputVariables()=>Describe(BindingOutputSchema!).Select(declaration=>
    {
        declaration.RuntimeValue=_values.GetValueOrDefault(declaration.PortId!);
        return declaration;
    }).ToArray();
}
