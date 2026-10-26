using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow;

public partial class AlgorithmNode
{
    internal IReadOnlyImage? FrozenWorkflowImage { get; set; }
    internal string? RuntimeInputError { get; set; }

    private async Task<ToolResults> ExecuteRuntimeNodeAsync(IDictionary<string,ToolResults>? results,CancellationToken cancellation)
    {
        var identity=AdmittedIdentity??throw new InvalidOperationException("Runtime node has no admitted workflow identity.");
        using var admission=identity.Owner.EnterRun(identity);
        cancellation.ThrowIfCancellationRequested();
        if(RuntimeInputError is not null)throw new InvalidOperationException(RuntimeInputError);
        var configuration=RuntimeConfiguration!;
        var schema=configuration.Schema;
        var values=new Dictionary<string,object?>(StringComparer.Ordinal);
        var leases=new List<IDisposable>();
        var resources=new FrozenOutputResources();
        bool transferred=false;
        ToolOutcome? outcome=null;
        RuntimeToolResults? prepared=null;
        Exception? failure=null;
        var timer=Stopwatch.StartNew();
        try
        {
            foreach(var dependency in CompiledBindings.Values.SelectMany(plan=>plan.Dependencies).Distinct())
                if(results?.TryGetValue(dependency.NodeId,out var source)==true&&source.DeclaredOutput is { } published)
                    leases.Add(published.AcquireReader());
            var snapshot=BindingContext!.Capture(this,results);
            foreach(var setting in ParamSettings.Settings)
            {
                var port=schema.Inputs.SingleOrDefault(p=>p.Id==setting.ParameterName)
                    ??throw new InvalidOperationException("Unknown Runtime input: "+setting.ParameterName);
                if(setting.BindingType==BindingType.Constant)
                {
                    if((port.EditModes&FieldEditModes.Constant)==0)throw new InvalidOperationException("Runtime input cannot use a persisted constant.");
                    values.Add(port.Id,RuntimeInputBindings.Constant(port,setting.ConstantValue));
                }
                else
                {
                    if(!CompiledBindings.TryGetValue(port.Id,out var plan))throw new InvalidOperationException("Runtime input has no admitted binding plan: "+port.Id);
                    var resolution=new ParameterBindingService().ResolveBinding(plan,snapshot);
                    if(!resolution.Success)throw new InvalidOperationException(resolution.Diagnostic?.Message??"Runtime binding resolution failed.");
                    values.Add(port.Id,resolution.Value);
                }
            }
            if(schema.WorkflowImageInputId is { } imagePort&&!values.ContainsKey(imagePort)&&FrozenWorkflowImage is { } image)
                values.Add(imagePort,image);
            var inputs=new InputValues(schema.Inputs,values);
            outcome=await ExecuteRuntimeAsync(inputs,new RuntimeContext(identity.ToInvocationIdentity(Id),cancellation,resources),OutputRequests).ConfigureAwait(false);
            transferred=true; // The envelope also disposes both owners when its constructor fails.
            prepared=new RuntimeToolResults(ToolType,schema,identity,outcome,resources,timer.Elapsed.TotalMilliseconds);
            return prepared;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            var cleanup=new List<Exception>();
            if(!transferred)
            {
                try{(outcome?.Output as IDisposable)?.Dispose();}catch(Exception error){cleanup.Add(error);}
                try{resources.Dispose();}catch(Exception error){cleanup.Add(error);}
            }
            foreach(var lease in leases)try{lease.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(cleanup.Count>0)
            {
                if(prepared is not null)try{prepared.Resources?.Dispose();}catch(Exception error){cleanup.Add(error);}
                if(failure is not null)cleanup.Insert(0,failure);
                throw new AggregateException("Runtime invocation resource cleanup failed.",cleanup);
            }
        }
    }
    private sealed class RuntimeContext(RunIdentity identity,CancellationToken cancellation,IOutputResources resources):IToolExecutionContext
    {
        public RunIdentity Identity=>identity;
        public CancellationToken Cancellation=>cancellation;
        public IOutputResources Resources=>resources;
        public void Log(string message)=>SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(
            SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,message,"Runtime:"+identity.NodeId);
    }
}
