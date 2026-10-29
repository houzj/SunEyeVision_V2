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
    internal ImageSourceRunInput? SourceRunInput { get; set; }

    private async Task<DeclaredOutputSnapshot> ExecuteRuntimeNodeAsync(IDictionary<string,DeclaredOutputSnapshot>? results,CancellationToken cancellation)
    {
        var identity=AdmittedIdentity??throw new InvalidOperationException("Runtime node has no admitted workflow identity.");
        using var admission=identity.Owner.EnterRun(identity);
        cancellation.ThrowIfCancellationRequested();
        if(RuntimeInputError is not null)throw new InvalidOperationException(RuntimeInputError);
        var configuration=RuntimeConfiguration!;
        var schema=configuration.Schema;
        var settingFields=configuration.SettingsSchema?.Inputs.ToDictionary(p=>p.Id,StringComparer.Ordinal)??new();
        var targets=configuration.BindingTargets.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var values=new Dictionary<string,object?>(StringComparer.Ordinal);
        var settingValues=new Dictionary<string,object?>(StringComparer.Ordinal);
        var leases=new List<IDisposable>();
        var captureResult=BindingContext!.PrepareResult(this);
        var resources=new FrozenOutputResources();
        bool transferred=false;
        ToolOutcome? outcome=null;
        DeclaredOutputSnapshot? prepared=null;
        Exception? failure=null;
        var timer=Stopwatch.StartNew();
        try
        {
            foreach(var dependency in CompiledBindings.Values.SelectMany(plan=>plan.Dependencies).Distinct())
                if(results?.TryGetValue(dependency.NodeId,out var source)==true&&source is { } published)
                    leases.Add(published.AcquireReader());
            var snapshot=BindingContext!.Capture(this,results);
            foreach(var (fieldId,setting) in configuration.Fields.Capture())
            {
                if(!targets.TryGetValue(fieldId,out var port))
                    throw new InvalidOperationException("Unknown Runtime input or setting: "+fieldId);
                var destination=settingFields.ContainsKey(port.Id)?settingValues:values;
                if(setting is ConstantSetting constant)
                {
                    if(port.EditModes!=FieldEditModes.None&&(port.EditModes&FieldEditModes.Constant)==0)throw new InvalidOperationException("Runtime input cannot use a persisted constant.");
                    if(!settingFields.ContainsKey(port.Id))destination.Add(port.Id,RuntimeInputBindings.Constant(port,constant.Value));
                }
                else
                {
                    if(!CompiledBindings.TryGetValue(port.Id,out var plan))throw new InvalidOperationException("Runtime input has no admitted binding plan: "+port.Id);
                    var resolution=new ParameterBindingService().ResolveBinding(plan,snapshot);
                    if(!resolution.Success)throw new InvalidOperationException(resolution.Diagnostic?.Message??"Runtime binding resolution failed.");
                    destination.Add(port.Id,resolution.Value);
                }
            }
            var settings=configuration.MaterializeSettings(settingValues);
            if(ToolType=="ImageLoad")
            {
                var sourceInputs=await ImageSourceInvocationInputs.CaptureAsync(this,
                    SourceRunInput??throw new InvalidOperationException("图像源缺少本轮冻结输入。"),
                    identity.ToInvocationIdentity(Id),CameraCaptureService,CameraFrame,cancellation).ConfigureAwait(false);
                leases.Add(sourceInputs);
                foreach(var value in sourceInputs.Values)
                    if(!values.TryAdd(value.Key,value.Value))throw new InvalidOperationException("图像源冻结输入与持久绑定冲突："+value.Key);
            }
            if(schema.WorkflowImageInputId is { } imagePort&&!values.ContainsKey(imagePort)&&FrozenWorkflowImage is { } image)
                values.Add(imagePort,image);
            var inputs=new InputValues(schema.Inputs,values);
            outcome=await ExecuteRuntimeAsync(configuration,inputs,new RuntimeContext(identity.ToInvocationIdentity(Id),cancellation,resources),OutputRequests,settings).ConfigureAwait(false);
            transferred=true; // The envelope also disposes both owners when its constructor fails.
            prepared=captureResult(outcome,resources,timer.Elapsed.TotalMilliseconds);
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
                if(prepared is not null)try{prepared.Dispose();}catch(Exception error){cleanup.Add(error);}
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
