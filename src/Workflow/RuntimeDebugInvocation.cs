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
    private readonly Func<Action<string,object?>,(InputValues Inputs,object Settings)> _prepare;
    private RuntimeToolConfiguration? _acceptedParameters;
    private readonly List<IDisposable> _readers;
    private ImageSourceRunInput? _sourceInput;
    private ICameraCaptureService? _cameraService;
    private Func<bool>? _previewCurrent;
    private int _started;
    private bool _disposed;
    public ToolExecutionIdentity Identity {get;}
    public IReadOnlyList<RunIdentity> SourceIdentities {get;}
    /// <summary>Private parameter-write receipt, independent of algorithm outcome. Never a formal publication.</summary>
    public RuntimeToolConfiguration? AcceptedParameters=>Volatile.Read(ref _acceptedParameters)?.Copy();
    private RuntimeDebugInvocation(string workflowId,WorkflowNodeBase node,ToolExecutionIdentity identity,List<IDisposable> readers,
        IEnumerable<RunIdentity> sources,Func<Action<string,object?>,(InputValues Inputs,object Settings)> prepare)
    {_workflowId=workflowId;_node=node;_prepare=prepare;Identity=identity;_readers=readers;SourceIdentities=Array.AsReadOnly(sources.ToArray());}

    /// <summary>Call on the document owner before dispatching execution. The double
    /// read rejects replacement during capture; retained readers survive later replacement.</summary>
    public static RuntimeDebugInvocation Capture(Workflow workflow,WorkflowNodeBase draft,ToolExecutionOwner owner,long revision,
        Func<string,DeclaredOutputSnapshot?> readPublished,CancellationToken cancellation,IVisionImage? defaultImage=null,ToolExecutionOwner? publishedOwner=null,
        IResourcePathResolver? resourcePaths=null,ICameraCaptureService? cameraService=null,
        string? solutionId=null,IReadOnlyList<GlobalVariable>? variables=null,IReadOnlyList<Workflow>? sourceWorkflows=null,
        Func<BindingNodeId,DeclaredOutputSnapshot?>? readScopedPublished=null)
        => CaptureCore(workflow,draft,owner,revision,readPublished,cancellation,defaultImage,publishedOwner,resourcePaths,cameraService,solutionId,variables,sourceWorkflows,readScopedPublished,null);

    /// <summary>Resolve only explicitly requested active input ports from already published snapshots.
    /// No algorithm, parameter current value, settings materialization or upstream execution occurs.</summary>
    public static RuntimeInputPreviewLease CaptureInputs(Workflow workflow,WorkflowNodeBase draft,ToolExecutionOwner owner,long revision,
        Func<string,DeclaredOutputSnapshot?> readPublished,CancellationToken cancellation,IVisionImage? defaultImage=null,ToolExecutionOwner? publishedOwner=null,
        IResourcePathResolver? resourcePaths=null,ICameraCaptureService? cameraService=null,
        string? solutionId=null,IReadOnlyList<GlobalVariable>? variables=null,IReadOnlyList<Workflow>? sourceWorkflows=null,
        Func<BindingNodeId,DeclaredOutputSnapshot?>? readScopedPublished=null,
        IReadOnlyCollection<string>? inputFieldIds=null,Action<IReadOnlyCollection<string>>? observeVariables=null)
    {
        ArgumentNullException.ThrowIfNull(inputFieldIds);
        var selected=inputFieldIds.ToArray();
        if(selected.Length==0||selected.Any(string.IsNullOrWhiteSpace)||selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length)
            throw new ArgumentException("预览输入必须非空、唯一且已声明。",nameof(inputFieldIds));
        var invocation=CaptureCore(workflow,draft,owner,revision,readPublished,cancellation,defaultImage,publishedOwner,resourcePaths,cameraService,solutionId,variables,sourceWorkflows,readScopedPublished,selected.ToHashSet(StringComparer.Ordinal),observeVariables);
        try
        {
            var prepared=invocation._prepare((_,_)=>throw new InvalidOperationException("只读输入不得写参数当前值。"));
            if(invocation._previewCurrent?.Invoke()!=true)throw new InvalidOperationException("预览输入在捕获期间已变化。");
            return new(prepared.Inputs,invocation.SourceIdentities,()=>!invocation._disposed&&invocation._previewCurrent?.Invoke()==true,invocation.Dispose);
        }
        catch(Exception failure)
        {
            try{invocation.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
    }
    private static RuntimeDebugInvocation CaptureCore(Workflow workflow,WorkflowNodeBase draft,ToolExecutionOwner owner,long revision,
        Func<string,DeclaredOutputSnapshot?> readPublished,CancellationToken cancellation,IVisionImage? defaultImage=null,ToolExecutionOwner? publishedOwner=null,
        IResourcePathResolver? resourcePaths=null,ICameraCaptureService? cameraService=null,
        string? solutionId=null,IReadOnlyList<GlobalVariable>? variables=null,IReadOnlyList<Workflow>? sourceWorkflows=null,
        Func<BindingNodeId,DeclaredOutputSnapshot?>? readScopedPublished=null,IReadOnlySet<string>? previewInputs=null,Action<IReadOnlyCollection<string>>? observeVariables=null)
    {
        ArgumentNullException.ThrowIfNull(workflow);ArgumentNullException.ThrowIfNull(draft);ArgumentNullException.ThrowIfNull(owner);
        if(!workflow.Nodes.Any(n=>n.Id==draft.Id&&n.ToolType==draft.ToolType))throw new InvalidOperationException("调试节点不属于当前工作流。");
        var node=draft.Clone();var configuration=node.RuntimeConfiguration??throw new InvalidOperationException("调试节点没有Runtime配置。");
        var originalConfiguration=draft.RuntimeConfiguration;
        var originalTarget=workflow.Nodes.Single(n=>n.Id==draft.Id);
        string PreviewConfiguration(RuntimeToolConfiguration? current)
        {
            if(current is null||current.Schema.Fingerprint!=configuration.Schema.Fingerprint)return "";
            if(previewInputs is null)return "";
            if(previewInputs.Any(id=>!current.IsExecutionFieldActive(id)||!Equals(current.GetParameterReferenceBehavior(id),configuration.GetParameterReferenceBehavior(id))))return "";
            using var stream=new System.IO.MemoryStream();
            using(var writer=new System.Text.Json.Utf8JsonWriter(stream))RuntimeFieldJson.Write(writer,previewInputs.Select(id=>new KeyValuePair<string,ParameterSetting>(id,current.Fields.Read(id))).Where(pair=>pair.Value is not UnsetSetting).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal));
            return Convert.ToBase64String(stream.ToArray());
        }
        var configurationProof=previewInputs is null?"":PreviewConfiguration(configuration);
        bool ConfigurationCurrent()=>workflow.Nodes.Any(node=>ReferenceEquals(node,originalTarget))&&originalTarget.ToolType==draft.ToolType&&PreviewConfiguration(draft.RuntimeConfiguration)==configurationProof;
        Dictionary<string,ParameterSetting> settings;
        WorkflowExecutionDependencyGraph? graph=null;
        if(previewInputs is null)
        {
            var errors=RuntimeInputBindings.Validate(workflow,node,workflow.Id);
            if(errors.Count>0)throw new InvalidOperationException(errors[0].Message);
            graph=WorkflowExecutionDependencyGraph.Compile(workflow);
            settings=configuration.CaptureExecutionFields().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        }
        else
        {
            if(previewInputs.Any(id=>!configuration.Schema.Inputs.Any(port=>port.Id==id)||!configuration.IsExecutionFieldActive(id)))
                throw new ArgumentException("预览仅接受当前活动的正式输入端口。");
            settings=previewInputs.ToDictionary(id=>id,id=>configuration.Fields.Read(id),StringComparer.Ordinal);
            var missing=settings.FirstOrDefault(pair=>pair.Value is UnsetSetting);
            if(missing.Key is not null)throw new InvalidOperationException("预览来源尚未就绪："+missing.Key);
        }
        var references=settings.Where(pair=>pair.Value is ReferenceSetting).ToDictionary(pair=>pair.Key,pair=>((ReferenceSetting)pair.Value).Reference,StringComparer.Ordinal);
        var outputReferences=new List<OutputReference>();var variableReferences=new Dictionary<string,VariableReference>(StringComparer.Ordinal);
        var variableSelections=new HashSet<string>(StringComparer.Ordinal);
        void AddOutput(OutputReference reference)
        {
            outputReferences.Add(reference);
            if(reference.Selection is BindingSelection.AtField index)AddOutput(index.Reference);
            else if(reference.Selection is BindingSelection.AtVariable variable)AddVariable(variable.Reference);
        }
        void AddVariable(VariableReference reference)
        {
            if(string.IsNullOrWhiteSpace(solutionId)||reference.SolutionId!=solutionId)
                throw new InvalidOperationException("调试变量不属于已捕获方案。");
            if(!variableSelections.Add(BindingReferenceSerializer.Serialize(reference)))return;
            variableReferences.TryAdd(reference.VariableId,reference);
            if(reference.Selection is BindingSelection.AtField index)AddOutput(index.Reference);
            else if(reference.Selection is BindingSelection.AtVariable variable)AddVariable(variable.Reference);
        }
        foreach(var reference in references.Values)AddOutput(reference);
        foreach(var variable in settings.Values.OfType<VariableSetting>())AddVariable(variable.Reference);
        if(outputReferences.SelectMany(reference=>WorkflowBindingContext.ReferenceNodes(reference,workflow.Id)).Any(id=>id.ScopeId!=workflow.Id))
            throw new InvalidOperationException("调试节点的直接来源必须属于当前工作流。");
        observeVariables?.Invoke(variableReferences.Keys.ToArray());
        var originalVariableObjects=(variables??Array.Empty<GlobalVariable>()).ToArray();
        var globalDefinitions=GlobalVariable.CaptureDefinitions(originalVariableObjects).ToArray();
        var usedVariables=variableReferences.Keys.Select(id=>globalDefinitions.SingleOrDefault(variable=>variable.Id==id)
            ??throw new InvalidOperationException("调试变量声明已删除："+id)).ToArray();
        foreach(var variable in usedVariables)
            if(string.IsNullOrEmpty(variable.RebindingReason)&&string.IsNullOrEmpty(variable.BindingValidationPending)
                &&variable.ProducerBinding?.SourceReference is {} producer)
            {
                if(producer.Selection is not BindingSelection.AllItems)
                    throw new InvalidOperationException("变量生产来源必须保留正式All选择合同。");
                AddOutput(producer);
            }
        var dependencies=outputReferences.SelectMany(reference=>WorkflowBindingContext.ReferenceNodes(reference,workflow.Id)).Distinct().ToArray();
        var readers=new List<IDisposable>();ToolExecutionIdentity? identity=null;
        try
        {
            WorkflowGlobalRunValues? globals=null;
            if(variableReferences.Count>0)
            {
                var initial=globalDefinitions.Where(variable=>variable.ProducerBinding is null&&string.IsNullOrEmpty(variable.RebindingReason)
                    &&string.IsNullOrEmpty(variable.BindingValidationPending)&&GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out _,out _,out _))
                    .Select(variable=>new KeyValuePair<string,object?>(variable.Id,variable.Value));
                globals=new WorkflowGlobalRunValues(solutionId!,globalDefinitions,initial);readers.Add(globals);
            }
            DeclaredOutputSnapshot? Read(BindingNodeId source)=>source.ScopeId==workflow.Id?readPublished(source.NodeId)
                :readScopedPublished?.Invoke(source);
            var observed=new Dictionary<BindingNodeId,DeclaredOutputSnapshot>();
            var sourceConfigurations=new Dictionary<BindingNodeId,RuntimeToolConfiguration?>();
            var sources=new List<BindingSource>();var values=new List<BindingNodeResult>();
            foreach(var dependency in dependencies)
            {
                if(dependency.ScopeId==workflow.Id&&dependency.NodeId==node.Id)throw new InvalidOperationException("调试来源引用了自身。");
                var sourceWorkflow=dependency.ScopeId==workflow.Id?workflow:(sourceWorkflows??Array.Empty<Workflow>()).SingleOrDefault(flow=>flow.Id==dependency.ScopeId)
                    ??throw new InvalidOperationException("变量生产流程不属于当前方案。");
                var sourceNode=sourceWorkflow.Nodes.Single(n=>n.Id==dependency.NodeId);
                var sourceConfiguration=sourceNode.RuntimeConfiguration;sourceConfigurations.Add(dependency,sourceConfiguration);
                var result=Read(dependency)??throw new InvalidOperationException("来源尚无已发布结果，请先运行上游节点或工作流："+sourceNode.Name);
                var published=result??throw new InvalidOperationException("来源没有正式冻结输出。");
                var admitted=result.ExecutionIdentity??throw new InvalidOperationException("来源没有原始执行身份。");
                if(!result.IsSuccess||published.Status!=ExecutionStatus.Success&&published.Status!=ExecutionStatus.PartialSuccess||!published.IsComplete||
                    !ReferenceEquals(admitted.Owner,publishedOwner??workflow.ExecutionOwner)||admitted.NodeId!=dependency.ScopeId+"/"+dependency.NodeId||published.SourceWorkflowId!=dependency.ScopeId||
                    published.Identity!=admitted.ToInvocationIdentity(dependency.NodeId))
                    throw new InvalidOperationException("来源结果不属于当前工作流的完整成功发布。");
                var declaration=new ToolSchema(Array.Empty<Port>(),(new OutputBindingChangeService().ReadContract(sourceNode)??Array.Empty<OutputVariable>())
                    .Where(p=>p.Kind==OutputVariableKind.Data).Select(BindingClrAdapter.DescribeOutput));
                if(declaration.Fingerprint!=published.Schema.Fingerprint)throw new InvalidOperationException("来源声明已更改，请重新运行来源节点。");
                if(previewInputs is not null&&!ReferenceEquals(sourceConfiguration,sourceNode.RuntimeConfiguration))throw new InvalidOperationException("预览来源配置在捕获期间已变化。");
                var reader=published.AcquireReader();readers.Add(reader);
                if(reader.Values is null)throw new InvalidOperationException("来源没有可读的冻结值。");
                observed.Add(dependency,result);
                sources.Add(new(dependency,published.Identity.ScopeId,declaration,published.SchemaRevision));
                values.Add(new(dependency,published.Identity,published.SchemaRevision,declaration.Fingerprint,true,reader.Values));
            }
            foreach(var pair in observed)
                if(!ReferenceEquals(pair.Value,Read(pair.Key)))throw new InvalidOperationException("捕获期间来源结果已更新，请重新执行调试。");
            cancellation.ThrowIfCancellationRequested();
            identity=owner.Begin(workflow.Id+"/"+node.Id,Guid.NewGuid(),FoundationContract.Major+"."+FoundationContract.Minor,cancellation);
            var consumer=new BindingNodeId(workflow.Id,node.Id);
            var scope=new BindingScopeSnapshot(consumer,owner.Id,revision,sources,sources.Select(s=>s.Node),
                workflow.Nodes.Select(n=>new KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>(new(workflow.Id,n.Id),
                    (graph?.GetPredecessors(n.Id)??Array.Empty<string>()).Select(id=>new BindingNodeId(workflow.Id,id)).ToArray())),globals?.CaptureBindingSources());
            var settingsIds=configuration.SettingsSchema?.Inputs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal)??new();
            var manualIds=configuration.SettingsSchema.Inputs.Where(OutputBindingChangeService.IsOrdinaryNumericParameter).Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
            var manualScope=scope.WithReferenceQualification(new ManualReferenceQualification(workflow,workflow.Id,node.Id).Validate);
            var resolver=new ParameterBindingService();
            if(globals is not null)
            {
                foreach(var variable in usedVariables.Where(variable=>variable.ProducerBinding is not null))
                {
                    var unavailable=!string.IsNullOrEmpty(variable.RebindingReason)?variable.RebindingReason:variable.BindingValidationPending;
                    if(!string.IsNullOrEmpty(unavailable))
                    {
                        globals.Publish(new[]{new WorkflowGlobalProducedValue(variable.Id,variable.Name,null,
                            new Diagnostic("variable-unavailable",unavailable,variable.Id))},cancellation);
                        continue;
                    }
                    var source=variable.ProducerBinding!.SourceReference??throw new InvalidOperationException("变量缺少正式生产来源。");
                    if(!GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var target,out var clr,out var reason))
                        throw new InvalidOperationException(reason);
                    var producerScope=source.ScopeId??throw new InvalidOperationException("变量生产来源缺少流程身份。");
                    var operationId="debug-global-producer:"+variable.Id;
                    var producerConsumer=new BindingNodeId(producerScope,operationId);
                    var producerSources=sources.Where(item=>item.Node.ScopeId==producerScope).ToArray();
                    var producerCompilation=new BindingScopeSnapshot(producerConsumer,owner.Id,revision,producerSources,
                        producerSources.Select(item=>item.Node),Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
                    var compiled=resolver.CompileBinding(source,target,producerCompilation);
                    BindingResolution resolved=compiled.Plan is null?new(false,null,compiled.Diagnostic??new Diagnostic("variable-binding","变量生产声明不可用。"))
                        :resolver.ResolvePublishedBinding(compiled.Plan,new PublishedBindingSnapshot(producerConsumer,
                            identity.ToInvocationIdentity(operationId),revision,values.Where(item=>item.Node.ScopeId==producerScope)));
                    object? produced=null;var issue=resolved.Diagnostic;
                    if(resolved.Success)
                    {
                        try{produced=BindingClrAdapter.Materialize(target,clr,resolved.Value);}
                        catch(Exception error)when(error is ArgumentException or InvalidOperationException or OverflowException)
                        {issue=new Diagnostic("variable-value",error.Message,variable.Id);}
                    }
                    globals.Publish(new[]{new WorkflowGlobalProducedValue(variable.Id,variable.Name,produced,issue)},cancellation);
                }
            }
            var snapshot=new PublishedBindingSnapshot(consumer,identity.ToInvocationIdentity(node.Id),revision,
                values.Where(value=>value.Node.ScopeId==workflow.Id),globals?.CaptureBindingValues(identity.ToInvocationIdentity(node.Id)));
            var plans=new Dictionary<string,BindingPlan>(StringComparer.Ordinal);
            var variablePlans=new Dictionary<string,RuntimeVariableBindingPlan>(StringComparer.Ordinal);
            foreach(var (fieldId,setting) in settings)
            {
                var target=configuration.BindingTargets.Single(port=>port.Id==fieldId);
                if(setting is ReferenceSetting reference)
                {
                    var compiled=resolver.CompileBinding(reference.Reference,target,manualIds.Contains(fieldId)?manualScope:scope,reference.Conversion,configuration.GetParameterReferenceBehavior(fieldId));
                    plans.Add(fieldId,compiled.Plan??throw new InvalidOperationException(compiled.Diagnostic?.Message??"调试来源声明不可用。"));
                }
                else if(setting is VariableSetting variable)variablePlans.Add(fieldId,RuntimeVariableBindings.Compile(variable,target,globals,manualIds.Contains(fieldId)?manualScope:scope,configuration.GetParameterReferenceBehavior(fieldId)));
            }
            IReadOnlyImage? fallbackImage=null;
            if(previewInputs is null&&configuration.Schema.WorkflowImageInputId is { } imagePort&&!settings.ContainsKey(imagePort)&&defaultImage is not null)
            {
                if(defaultImage is not ITypedImageSnapshotSource typed)throw new InvalidOperationException("调试图像不支持像素及实际Domain快照。");
                var image=typed.CaptureSnapshot(new ImageFrameAddress(Guid.NewGuid(),"debug-input:"+identity.RequestId.ToString("N")));
                readers.Add(image);fallbackImage=image;
            }
            if(previewInputs is null&&configuration.Schema.WorkflowImageInputId is { } requiredImage&&!settings.ContainsKey(requiredImage)&&fallbackImage is null)
                throw new InvalidOperationException("请选择已发布的图像输入源后再调试。");
            (InputValues Inputs,object Settings) Prepare(Action<string,object?> accept)
            {
                var inputs=new Dictionary<string,object?>(StringComparer.Ordinal);var fields=new Dictionary<string,object?>(StringComparer.Ordinal);
                foreach(var (fieldId,setting) in previewInputs is null?configuration.CaptureExecutionFields():(IEnumerable<KeyValuePair<string,ParameterSetting>>)settings)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var target=configuration.BindingTargets.Single(port=>port.Id==fieldId);object? value;
                    if(setting is ConstantSetting constant)
                    {if(settingsIds.Contains(fieldId))continue;value=RuntimeInputBindings.Constant(target,constant.Value);}
                    else
                    {
                        var resolved=setting is VariableSetting?RuntimeVariableBindings.ResolvePublished(variablePlans[fieldId],globals!,snapshot)
                            :resolver.ResolvePublishedBinding(plans[fieldId],snapshot);
                        if(!resolved.Success)throw new InvalidOperationException(resolved.Diagnostic?.Message??"调试参数解析失败。");
                        value=resolved.Value;
                        if(settingsIds.Contains(fieldId))accept(fieldId,value);
                    }
                    (settingsIds.Contains(fieldId)?fields:inputs).Add(fieldId,value);
                }
                if(configuration.Schema.WorkflowImageInputId is {} fallbackPort&&fallbackImage is not null)inputs.Add(fallbackPort,fallbackImage);
                return (new InputValues(previewInputs is null?configuration.Schema.Inputs:configuration.Schema.Inputs.Where(port=>previewInputs.Contains(port.Id)),inputs),previewInputs is null?configuration.MaterializeSettings(fields):null!);
            }
            var invocation=new RuntimeDebugInvocation(workflow.Id,node,identity,readers,values.Select(v=>v.Identity),Prepare);
            if(previewInputs is not null)
            {
                var variableProofs=globalDefinitions.Where(variable=>variableReferences.ContainsKey(variable.Id)).Select(frozen=>(Variable:originalVariableObjects.Single(variable=>variable.Id==frozen.Id),Revision:frozen.ValueRevision,Type:frozen.Type,Producer:frozen.ProducerBinding?.ReferenceExpression,Reason:frozen.RebindingReason,Pending:frozen.BindingValidationPending)).ToArray();
                bool Current()
                {
                    if(!identity.IsCurrent||!ConfigurationCurrent())return false;
                    foreach(var pair in observed)
                    {
                        if(!ReferenceEquals(pair.Value,Read(pair.Key))||pair.Value.ExecutionIdentity?.IsCurrent!=true)return false;
                        var sourceFlow=pair.Key.ScopeId==workflow.Id?workflow:(sourceWorkflows??Array.Empty<Workflow>()).SingleOrDefault(flow=>flow.Id==pair.Key.ScopeId);
                        if(sourceFlow is null||!ReferenceEquals(sourceFlow.Nodes.SingleOrDefault(n=>n.Id==pair.Key.NodeId)?.RuntimeConfiguration,sourceConfigurations[pair.Key]))return false;
                    }
                    foreach(var proof in variableProofs)
                        if(!(variables??Array.Empty<GlobalVariable>()).Any(variable=>ReferenceEquals(variable,proof.Variable))||proof.Variable.ValueRevision!=proof.Revision||proof.Variable.Type!=proof.Type||proof.Variable.ProducerBinding?.ReferenceExpression!=proof.Producer||proof.Variable.RebindingReason!=proof.Reason||proof.Variable.BindingValidationPending!=proof.Pending)return false;
                    return identity.IsCurrent&&ConfigurationCurrent();
                }
                invocation._previewCurrent=Current;
            }
            if(previewInputs is null&&node.ToolType=="ImageLoad")
            {
                invocation._sourceInput=ImageSourceInvocationInputs.PrepareDebug(node,resourcePaths,owner,workflow.Id);
                invocation._cameraService=cameraService;
            }
            return invocation;
        }
        catch(Exception failure)
        {
            var cleanup=new List<Exception>();
            try{identity?.CompleteCallbacks();}catch(Exception error){cleanup.Add(error);}
            finally{cleanup.AddRange(Release(readers));}
            if(cleanup.Count>0)throw new AggregateException(new[]{failure}.Concat(cleanup));throw;
        }
    }
    public async Task<DeclaredOutputSnapshot> ExecuteAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(Interlocked.Exchange(ref _started,1)!=0)throw new InvalidOperationException("调试输入快照只能执行一次。");
        IDisposable? admission=null;
        ImageSourceInvocationInputs? sourceLease=null;
        var resultSchema=_node.RuntimeConfiguration!.Schema;var resultNodeId=_node.Id;
        var resources=new FrozenOutputResources();ToolOutcome? outcome=null;DeclaredOutputSnapshot? result=null;bool transferred=false;Exception? failure=null;
        var timer=Stopwatch.StartNew();
        try
        {
            admission=Identity.Owner.EnterRun(Identity);
            var prepared=_prepare((fieldId,value)=>
            {
                var current=Volatile.Read(ref _acceptedParameters)??_node.RuntimeConfiguration!;
                var next=current.WithAcceptedParameterValue(fieldId,value);
                if(!Identity.TryInvokeCallback(()=>Volatile.Write(ref _acceptedParameters,next)))
                    throw new InvalidOperationException("私有参数写入的调试身份已失效。");
            });
            var inputs=prepared.Inputs;
            if(_sourceInput is not null)
            {
                sourceLease=await ImageSourceInvocationInputs.CaptureAsync(_node,_sourceInput,Identity.ToInvocationIdentity(_node.Id),
                    _cameraService,null,Identity.CancellationToken).ConfigureAwait(false);
                var values=inputs.CopyValues().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
                foreach(var pair in sourceLease.Values)
                    if(!values.TryAdd(pair.Key,pair.Value))throw new InvalidOperationException("图像源冻结输入与节点绑定冲突："+pair.Key);
                inputs=new InputValues(_node.RuntimeConfiguration!.Schema.Inputs,values);
            }
            outcome=await _node.ExecuteRuntimeAsync(_node.RuntimeConfiguration!,inputs,new Context(Identity.ToInvocationIdentity(_node.Id),Identity.CancellationToken,resources,_sourceInput is null ? null : () => _sourceInput.AcceptLocalImage(Identity)),null,prepared.Settings).ConfigureAwait(false);
            transferred=true;result=DeclaredOutputSnapshot.Capture(resultSchema,0,Identity,resultNodeId,outcome,resources,timer.Elapsed.TotalMilliseconds,sourceWorkflowId:_workflowId);
            return result;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            _disposed=true;var cleanup=new List<Exception>();
            try{Identity.CompleteCallbacks();}catch(Exception error){cleanup.Add(error);}
            finally{cleanup.AddRange(Release(_readers));}
            try{sourceLease?.Dispose();}catch(Exception error){cleanup.Add(error);}
            try{_sourceInput?.FinishRead();}catch(Exception error){cleanup.Add(error);}
            try{admission?.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(!transferred){try{(outcome?.Output as IDisposable)?.Dispose();}catch(Exception error){cleanup.Add(error);}try{resources.Dispose();}catch(Exception error){cleanup.Add(error);}}
            if((failure is not null||cleanup.Count>0)&&result is not null)try{result.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(cleanup.Count>0){if(failure is not null)cleanup.Insert(0,failure);throw new AggregateException(cleanup);}
        }
    }
    public void Dispose()
    {
        if(_disposed)return;
        if(Volatile.Read(ref _started)!=0)throw new InvalidOperationException("正在执行的调试快照必须等待执行排空。");
        _disposed=true;var errors=new List<Exception>();
        try{Identity.CompleteCallbacks();}catch(Exception error){errors.Add(error);}
        finally{errors.AddRange(Release(_readers));}
        try{_sourceInput?.FinishRead();}catch(Exception error){errors.Add(error);}if(errors.Count>0)throw new AggregateException(errors);
    }
    private static List<Exception> Release(List<IDisposable> readers)
    {var errors=new List<Exception>();foreach(var reader in readers)try{reader.Dispose();}catch(Exception error){errors.Add(error);}readers.Clear();return errors;}
    private sealed record Context(RunIdentity Identity,CancellationToken Cancellation,IOutputResources Resources,Func<string>? LocalRead):IToolExecutionContext,ILocalImageReadContext
    {public string AcceptLocalImage()=>LocalRead?.Invoke()??throw new InvalidOperationException("当前调试不提供本地图像读取接纳。");
     public void Log(string message)=>SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Info,message,"Runtime调试");}
}
