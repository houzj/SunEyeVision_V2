using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow;

/// <summary>One editor preview of frozen Runtime settings and explicitly selected
/// published inputs. Never executes upstream nodes or writes production configuration/results.</summary>
public sealed class RuntimeDebugInvocation : IDisposable
{
    private readonly WorkflowNodeBase _node;
    private readonly string _workflowId;
    private readonly InputValues _inputs;
    private readonly List<IDisposable> _readers;
    private ImageSourceRunInput? _sourceInput;
    private ICameraCaptureService? _cameraService;
    private int _started;
    private bool _disposed;
    public ToolExecutionIdentity Identity {get;}
    public IReadOnlyList<RunIdentity> SourceIdentities {get;}
    private RuntimeDebugInvocation(string workflowId,WorkflowNodeBase node,InputValues inputs,ToolExecutionIdentity identity,List<IDisposable> readers,IEnumerable<RunIdentity> sources)
    {_workflowId=workflowId;_node=node;_inputs=inputs;Identity=identity;_readers=readers;SourceIdentities=Array.AsReadOnly(sources.ToArray());}

    /// <summary>Call on the document owner before dispatching execution. The double
    /// read rejects replacement during capture; retained readers survive later replacement.</summary>
    public static RuntimeDebugInvocation Capture(Workflow workflow,WorkflowNodeBase draft,ToolExecutionOwner owner,long revision,
        Func<string,ToolResults?> readPublished,CancellationToken cancellation,IVisionImage? defaultImage=null,ToolExecutionOwner? publishedOwner=null,
        IResourcePathResolver? resourcePaths=null,ICameraCaptureService? cameraService=null)
    {
        ArgumentNullException.ThrowIfNull(workflow);ArgumentNullException.ThrowIfNull(draft);ArgumentNullException.ThrowIfNull(owner);
        if(!workflow.Nodes.Any(n=>n.Id==draft.Id&&n.ToolType==draft.ToolType))throw new InvalidOperationException("调试节点不属于当前工作流。");
        var node=draft.Clone();var configuration=node.RuntimeConfiguration??throw new InvalidOperationException("调试节点没有Runtime配置。");
        var errors=RuntimeInputBindings.Validate(workflow,node,workflow.Id);
        if(errors.Count>0)throw new InvalidOperationException(errors[0].Message);
        var graph=WorkflowExecutionDependencyGraph.Compile(workflow);
        var settings=node.ParamSettings.Settings.ToArray();
        var references=settings.Where(s=>s.BindingType==BindingType.Binding).ToDictionary(s=>s.ParameterName,
            s=>BindingReferenceSerializer.Deserialize(s.ReferenceExpression!),StringComparer.Ordinal);
        var dependencies=references.Values.SelectMany(r=>WorkflowBindingContext.ReferenceNodes(r,workflow.Id)).Distinct().ToArray();
        var readers=new List<IDisposable>();ToolExecutionIdentity? identity=null;
        try
        {
            var observed=new Dictionary<string,ToolResults>(StringComparer.Ordinal);
            var sources=new List<BindingSource>();var values=new List<BindingNodeResult>();
            foreach(var dependency in dependencies)
            {
                if(dependency.ScopeId!=workflow.Id||dependency.NodeId==node.Id)throw new InvalidOperationException("调试来源不在当前工作流或引用了自身。");
                var sourceNode=workflow.Nodes.Single(n=>n.Id==dependency.NodeId);
                var result=readPublished(dependency.NodeId)??throw new InvalidOperationException("来源尚无已发布结果，请先运行上游节点或工作流："+sourceNode.Name);
                var published=result.DeclaredOutput??throw new InvalidOperationException("来源没有正式冻结输出。");
                var admitted=result.ExecutionIdentity??throw new InvalidOperationException("来源没有原始执行身份。");
                if(!result.IsSuccess||published.Status!=ExecutionStatus.Success&&published.Status!=ExecutionStatus.PartialSuccess||!published.IsComplete||
                    !ReferenceEquals(admitted.Owner,publishedOwner??workflow.ExecutionOwner)||admitted.NodeId!=workflow.Id+"/"+dependency.NodeId||published.SourceWorkflowId!=workflow.Id||
                    published.Identity!=admitted.ToInvocationIdentity(dependency.NodeId))
                    throw new InvalidOperationException("来源结果不属于当前工作流的完整成功发布。");
                var declaration=new ToolSchema(Array.Empty<Port>(),(new OutputBindingChangeService().ReadContract(sourceNode)??Array.Empty<OutputVariable>())
                    .Where(p=>p.Kind==OutputVariableKind.Data).Select(BindingClrAdapter.DescribeOutput));
                if(declaration.Fingerprint!=published.Schema.Fingerprint)throw new InvalidOperationException("来源声明已更改，请重新运行来源节点。");
                var reader=published.AcquireReader();readers.Add(reader);
                if(reader.Values is null)throw new InvalidOperationException("来源没有可读的冻结值。");
                observed.Add(dependency.NodeId,result);
                sources.Add(new(dependency,published.Identity.ScopeId,declaration,published.SchemaRevision));
                values.Add(new(dependency,published.Identity,published.SchemaRevision,declaration.Fingerprint,true,reader.Values));
            }
            foreach(var pair in observed)
                if(!ReferenceEquals(pair.Value,readPublished(pair.Key)))throw new InvalidOperationException("捕获期间来源结果已更新，请重新执行调试。");
            cancellation.ThrowIfCancellationRequested();
            identity=owner.Begin(workflow.Id+"/"+node.Id,Guid.NewGuid(),ToolContract.Version,cancellation);
            var consumer=new BindingNodeId(workflow.Id,node.Id);
            var scope=new BindingScopeSnapshot(consumer,owner.Id,revision,sources,sources.Select(s=>s.Node),
                workflow.Nodes.Select(n=>new KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>(new(workflow.Id,n.Id),
                    graph.GetPredecessors(n.Id).Select(id=>new BindingNodeId(workflow.Id,id)).ToArray())));
            var snapshot=new PublishedBindingSnapshot(consumer,identity.ToInvocationIdentity(node.Id),revision,values);
            var inputs=new Dictionary<string,object?>(StringComparer.Ordinal);var fields=new Dictionary<string,object?>(StringComparer.Ordinal);
            var settingsIds=configuration.SettingsSchema?.Inputs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal)??new();
            var resolver=new ParameterBindingService();
            foreach(var setting in settings)
            {
                var target=configuration.BindingTargets.Single(p=>p.Id==setting.ParameterName);object? value;
                if(setting.BindingType==BindingType.Constant)value=RuntimeInputBindings.Constant(target,setting.ConstantValue);
                else
                {
                    var compiled=resolver.CompileBinding(references[setting.ParameterName],target,scope,setting.Conversion);
                    if(compiled.Plan is null)throw new InvalidOperationException(compiled.Diagnostic?.Message??"调试来源声明不可用。");
                    var resolved=resolver.ResolvePublishedBinding(compiled.Plan,snapshot);
                    if(!resolved.Success)throw new InvalidOperationException(resolved.Diagnostic?.Message??"调试输入解析失败。");
                    value=resolved.Value;
                }
                (settingsIds.Contains(target.Id)?fields:inputs).Add(target.Id,value);
            }
            if(fields.Count>0)
            {
                var effective=configuration.WithFieldValues(fields);
                if(effective.Schema.Fingerprint!=configuration.Schema.Fingerprint||!effective.Capabilities.ResourceKeys.ToHashSet(StringComparer.Ordinal).SetEquals(configuration.Capabilities.ResourceKeys))
                    throw new InvalidOperationException("调试订阅改变了已准入声明或资源要求。");
                node.RuntimeConfiguration=effective;configuration=effective;
            }
            if(configuration.Schema.WorkflowImageInputId is { } imagePort&&!inputs.ContainsKey(imagePort)&&defaultImage is not null)
            {
                if(defaultImage is not ITypedImageSnapshotSource typed)throw new InvalidOperationException("调试图像不支持像素及实际Domain快照。");
                var image=typed.CaptureSnapshot(new ImageFrameAddress(Guid.NewGuid(),"debug-input:"+identity.RequestId.ToString("N")));
                readers.Add(image);inputs.Add(imagePort,image);
            }
            if(configuration.Schema.WorkflowImageInputId is { } requiredImage&&!inputs.ContainsKey(requiredImage))
                throw new InvalidOperationException("请选择已发布的图像输入源后再调试。");
            var invocation=new RuntimeDebugInvocation(workflow.Id,node,new InputValues(configuration.Schema.Inputs,inputs),identity,readers,values.Select(v=>v.Identity));
            if(node.ToolType=="ImageLoad")
            {
                invocation._sourceInput=ImageSourceInvocationInputs.PrepareDebug(node,resourcePaths);
                invocation._cameraService=cameraService;
            }
            return invocation;
        }
        catch(Exception failure)
        {
            identity?.CompleteCallbacks();var cleanup=Release(readers);
            if(cleanup.Count>0)throw new AggregateException(new[]{failure}.Concat(cleanup));throw;
        }
    }
    public async Task<RuntimeToolResults> ExecuteAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(Interlocked.Exchange(ref _started,1)!=0)throw new InvalidOperationException("调试输入快照只能执行一次。");
        IDisposable? admission=null;
        ImageSourceInvocationInputs? sourceLease=null;
        var resources=new FrozenOutputResources();ToolOutcome? outcome=null;RuntimeToolResults? result=null;bool transferred=false;Exception? failure=null;
        var timer=Stopwatch.StartNew();
        try
        {
            admission=Identity.Owner.EnterRun(Identity);
            var inputs=_inputs;
            if(_sourceInput is not null)
            {
                sourceLease=await ImageSourceInvocationInputs.CaptureAsync(_node,_sourceInput,Identity.ToInvocationIdentity(_node.Id),
                    _cameraService,null,Identity.CancellationToken).ConfigureAwait(false);
                var values=_inputs.CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
                foreach(var pair in sourceLease.Values)
                    if(!values.TryAdd(pair.Key,pair.Value))throw new InvalidOperationException("图像源冻结输入与节点绑定冲突："+pair.Key);
                inputs=new InputValues(_node.RuntimeConfiguration!.Schema.Inputs,values);
            }
            outcome=await _node.ExecuteRuntimeAsync(inputs,new Context(Identity.ToInvocationIdentity(_node.Id),Identity.CancellationToken,resources)).ConfigureAwait(false);
            transferred=true;result=new(_node.ToolType,_node.RuntimeConfiguration!.Schema,Identity,outcome,resources,timer.Elapsed.TotalMilliseconds);
            DeclaredOutputSnapshot.Publish(result,Identity.ToInvocationIdentity(_node.Id),result.Values,sourceWorkflowId:_workflowId);
            return result;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            Identity.CompleteCallbacks();_disposed=true;var cleanup=Release(_readers);
            try{sourceLease?.Dispose();}catch(Exception error){cleanup.Add(error);}
            try{admission?.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(!transferred){try{(outcome?.Output as IDisposable)?.Dispose();}catch(Exception error){cleanup.Add(error);}try{resources.Dispose();}catch(Exception error){cleanup.Add(error);}}
            if((failure is not null||cleanup.Count>0)&&result is not null)try{result.Resources?.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(cleanup.Count>0){if(failure is not null)cleanup.Insert(0,failure);throw new AggregateException(cleanup);}
        }
    }
    public void Dispose()
    {
        if(_disposed)return;
        if(Volatile.Read(ref _started)!=0)throw new InvalidOperationException("正在执行的调试快照必须等待执行排空。");
        _disposed=true;Identity.CompleteCallbacks();var errors=Release(_readers);if(errors.Count>0)throw new AggregateException(errors);
    }
    private static List<Exception> Release(List<IDisposable> readers)
    {var errors=new List<Exception>();foreach(var reader in readers)try{reader.Dispose();}catch(Exception error){errors.Add(error);}readers.Clear();return errors;}
    private sealed record Context(RunIdentity Identity,CancellationToken Cancellation,IOutputResources Resources):IToolExecutionContext
    {public void Log(string message)=>SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,message,"Runtime调试");}
}
