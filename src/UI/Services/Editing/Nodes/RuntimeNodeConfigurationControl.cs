using SunEyeVision.Plugin.Wpf.Adapters.References;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.Workflow;
using ConfigurationSetting=SunEyeVision.Plugin.Contracts.ParameterSetting;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Document adapter for the actual Runtime settings view. No SDK parameter
/// model or algorithm is created. The existing document participant owns publication.</summary>
public sealed class RuntimeNodeConfigurationControl : UserControl,IToolEditorPendingState,IToolEditorCommitParticipant,IDisposable
{
    private WorkflowNodeBase _node=null!;
    private IToolSettingsView? _view;
    private ReferenceWindowOwner? _referenceWindowOwner;
    private ProductionEditorContext? _presentationContext;
    private void RetireReferencePresentation(){var owner=_referenceWindowOwner;_referenceWindowOwner=null;var context=_presentationContext;_presentationContext=null;if(context is not null)context.Deactivated-=RetireReferencePresentation;owner?.Dispose();}
    private ReferenceWindowOwner ReferencePresentation()
    {
        var context=ToolEditorCommit.GetDraftContext(this) as ProductionEditorContext;
        if(_referenceWindowOwner is not null&&ReferenceEquals(_presentationContext,context))return _referenceWindowOwner;
        RetireReferencePresentation();_presentationContext=context;
        var preference=context?.ReferencePreference??new ReferenceExpansionPreference();
        var owner=new ReferenceWindowOwner(this,()=>!_disposed&&ReferenceEquals(_presentationContext,context)&&ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)&&(context?.IsValid??true),preference,preference.PreferExpanded);
        _referenceWindowOwner=owner;if(context is not null)context.Deactivated+=RetireReferencePresentation;return owner;
    }
    private DataSourceQueryService? _query;
    private readonly List<DeclaredFieldEditor> _inputs=new();
    private bool _syncing,_disposed,_capturing;
    private long _generation;
    private long _syncedRevision;
    private string _captureError="";
    private bool _running;
    internal event Action? ResultDisplayEdited;
    internal event Action<WorkflowNodeBase,long>? ResultDisplayBound;
    internal bool IsResultDisplayBinding(WorkflowNodeBase node,long generation)
        =>!_disposed&&_view is not null&&generation==_generation&&ReferenceEquals(node,_node)&&ReferenceEquals(Content,_view.View);
    internal bool TryGetResultDisplayBinding(out WorkflowNodeBase node,out long generation)
    {node=_node;generation=_generation;return node is not null&&IsResultDisplayBinding(node,generation);}
    internal bool TryCaptureResultDisplay(out SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayDescriptor? descriptor,out SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayAppearance? appearance)
    {
        descriptor=null;appearance=null;if(_disposed||_view is null||_view.HasPendingEdits||_view.HasInvalidInput||_view.AcceptedRevision!=_syncedRevision)return false;
        var generation=_generation;var node=_node;var configuration=node.RuntimeConfiguration;
        if(configuration?.ResultDisplay is not {} declared)return false;
        var captured=configuration.CaptureResultDisplay();
        if(_disposed||generation!=_generation||!ReferenceEquals(_node,node)||!ReferenceEquals(node.RuntimeConfiguration,configuration))return false;
        descriptor=declared;appearance=captured;return captured is not null;
    }
    private void NotifyResultDisplayEdited()
    {foreach(var handler in ResultDisplayEdited?.GetInvocationList()??Array.Empty<Delegate>())try{((Action)handler)();}catch(Exception error){_captureError=error.Message;}}
    private Func<WorkflowNodeBase,RuntimeDebugInvocation>? _captureDebug;
    private Func<WorkflowNodeBase,IReadOnlyCollection<string>,RuntimeInputPreviewLease>? _captureRoiInputs;
    internal void SetRoiInputCapture(Func<WorkflowNodeBase,IReadOnlyCollection<string>,RuntimeInputPreviewLease> capture)=>_captureRoiInputs=capture;
    private Func<OutputReference,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?>? _roiFrameProvider;
    private SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameSource? _roiFrames;
    internal void SetRoiFrameProvider(Func<OutputReference,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?> provider)=>_roiFrameProvider=provider;
    internal void RefreshRoiFrame(){if(!_disposed)_roiFrames?.Refresh();}
    public bool HasPendingEdits=>_view is {} view&&(view.HasPendingEdits||view.AcceptedRevision!=_syncedRevision||_captureError.Length!=0)
        ||_inputs.Any(editor=>editor.HasPendingEdits);
    public RuntimeNodeConfigurationControl(WorkflowNodeBase node)
    {
        CommandBindings.Add(new CommandBinding(Plugin.SDK.Commands.ToolCommands.Execute,async (_,e)=>
        {
            e.Handled=true;
            try{await RunDebugAsync();}
            catch(Exception error){Plugin.SDK.Logging.PluginLogger.Error(error.Message,nameof(RuntimeNodeConfigurationControl),error);}
        },(_,e)=>{e.CanExecute=!_disposed&&!_running&&_captureDebug is not null;e.Handled=true;}));
        BindDraft(node.Clone());
        Loaded+=(_,_)=>
        {
            if(Window.GetWindow(this) is Plugin.SDK.UI.Windows.DefaultDebugWindow window)
            {window.ApplyCompactParameterLayout();
                window.Width=Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English"?680:420;
                window.Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Create());}
        };
    }
    public void SetDataProvider(DataSourceQueryService query)=>_query=query;
    public void SetDebugCapture(Func<WorkflowNodeBase,RuntimeDebugInvocation> capture)=>_captureDebug=capture;
    public async Task RunDebugAsync()
    {
        Dispatcher.VerifyAccess();
        if(_running)throw new InvalidOperationException("调试正在执行。");
        _running=true;DeclaredOutputSnapshot? result=null;var generation=_generation;
        RuntimeDebugInvocation? invocation=null;WorkflowNodeBase? capturedNode=null;RuntimeToolConfiguration? capturedConfiguration=null;
        FrameworkElement? lockedView=null;bool previousEnabled=true;
        try
        {
            if(!TryValidateEditor(out var error))throw new InvalidOperationException(error);
            capturedNode=_node;capturedConfiguration=_node.RuntimeConfiguration;
            invocation=(_captureDebug??throw new InvalidOperationException("调试宿主尚未就绪。"))(_node);
            using var ownedInvocation=invocation;
            lockedView=_view?.View;
            if(lockedView is not null){previousEnabled=lockedView.IsEnabled;lockedView.SetCurrentValue(IsEnabledProperty,false);}
            result=await Task.Run(()=>invocation.ExecuteAsync());
            if(_disposed||generation!=_generation)
            {
                if(!_disposed)RaiseEvent(new Plugin.SDK.UI.Events.ToolExecutionCompletedEventArgs("草稿已重新绑定，旧调试结果已丢弃。",ToolDebugControlBase.ToolExecutionCompletedEvent,this));
                return;
            }
            RaiseEvent(new Plugin.SDK.UI.Events.ToolExecutionCompletedEventArgs(result,(long)result.ExecutionTimeMs,ToolDebugControlBase.ToolExecutionCompletedEvent,this));
        }
        catch(Exception error)
        {
            if(!_disposed)RaiseEvent(new Plugin.SDK.UI.Events.ToolExecutionCompletedEventArgs(error.Message,ToolDebugControlBase.ToolExecutionCompletedEvent,this));
            throw;
        }
        finally
        {
            try
            {
                // The write receipt exists even when the algorithm later throws. Install only
                // into the exact still-active private candidate, never the formal node.
                if(!_disposed&&generation==_generation&&ReferenceEquals(_node,capturedNode)
                    &&ReferenceEquals(_node.RuntimeConfiguration,capturedConfiguration)
                    &&ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext {IsValid:true}
                    &&invocation?.AcceptedParameters is {} accepted)
                    _node.RuntimeConfiguration=accepted;
            }
            finally
            {
                try{result?.Dispose();}
                finally
                {
                    if(!_disposed&&generation==_generation&&ReferenceEquals(_view?.View,lockedView)&&lockedView is not null)
                        lockedView.SetCurrentValue(IsEnabledProperty,previousEnabled);
                    _running=false;CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }
    public void BindDraft(WorkflowNodeBase node)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var generation=++_generation;
        var previous=_view;_view=null;_inputs.Clear();Content=null;
        if(previous is not null){previous.Changed-=Changed;previous.Dispose();}
        if(_disposed||generation!=_generation)return;
        _node=node;
        var configuration=node.RuntimeConfiguration??throw new ArgumentException("Runtime configuration is required.");
        var settings=configuration.EditorSettingsSchema??throw new ArgumentException("Runtime settings metadata is required.");
        var schema=configuration.Schema;
        if(schema.Presentation is null)throw new ArgumentException("Runtime input presentation metadata is required.");
        var presentationOwner=ReferencePresentation();
        var referenceOptions=new ReferenceFieldOptions(target=>
        {
            var candidates=ReadReferenceCandidates(generation,manualParameters:settings.Inputs.Any(port=>port.Id==target.Id) && OutputBindingChangeService.IsOrdinaryNumericParameter(target));
            if(_disposed||generation!=_generation)throw new InvalidOperationException("来源目录读取期间参数页已失效。");
            var behavior=configuration.GetParameterReferenceBehavior(target.Id);
            return new(candidates,candidates.Where(candidate=>ParameterReferenceRules.CanUseIndexSource(candidate.SelectedType,behavior)),ReadVariableCandidates(generation),behavior);
        },(target,proposal)=>ValidateProposal(target,proposal,generation),FormatVariable:reference=>FormatVariable(reference,generation),
            ReadCurrentValue:port=>
            {
                if(_disposed||generation!=_generation||!ReferenceEquals(_node,node))
                    throw new InvalidOperationException("参数当前值的编辑会话已失效。");
                var current=node.RuntimeConfiguration!;
                if(!current.SettingsSchema.Inputs.Any(parameter=>parameter.Id==port.Id))return null;
                return ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext context
                    ?context.ReadCurrentParameter(port.Id):current.ReadCurrentParameter(port.Id);
            },WindowOwner:presentationOwner,ReadBehavior:port=>
            {
                if(_disposed||generation!=_generation||!ReferenceEquals(_node,node))throw new InvalidOperationException("引用行为所属字段页已失效。");
                return configuration.GetParameterReferenceBehavior(port.Id);
            });
        var panel=node.ToolType=="ImageLoad"?null:new DeclaredInputFieldsView(schema,port=>
        {
            return node.RuntimeConfiguration!.Fields.Read(port.Id);
        },(port,setting)=>WriteInput(port,setting,generation),referenceOptions with {EditableIndex=true},FormatReference,
            _=>ReadReferenceCandidates(generation));
        var sourceContext=ToolEditorCommit.GetDraftContext(this) as ProductionEditorContext;
        var sourceContent=node.ToolType=="ImageLoad"?new ImageSourceDraftContent(node.Id,
            ()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node)
                &&sourceContext is {IsValid:true}&&ReferenceEquals(ToolEditorCommit.GetDraftContext(this),sourceContext),
            SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution)):null;
        FrameworkElement inputContent=(FrameworkElement?)sourceContent??panel!;
        void DisposeInput(){if(sourceContent is not null)sourceContent.Dispose();else panel?.Dispose();}
        _roiFrames=new SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameSource(fieldId=>
        {
            bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node)
                &&ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext {IsValid:true};
            if(!Current()||node.RuntimeConfiguration!.Fields.Read(fieldId) is not ReferenceSetting setting)return null;
            var key=BindingReferenceSerializer.Serialize(setting.Reference);
            bool SameInput()=>Current()&&node.RuntimeConfiguration!.Fields.Read(fieldId) is ReferenceSetting now&&BindingReferenceSerializer.Serialize(now.Reference)==key;
            return _roiFrameProvider?.Invoke(setting.Reference,SameInput);
        },fields=>
        {
            if(_disposed||generation!=_generation||!ReferenceEquals(_node,node)||_running||_captureRoiInputs is null)return null;
            var lease=_captureRoiInputs(node,fields);
            bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node)
                &&ToolEditorCommit.GetDraftContext(this) is ProductionEditorContext {IsValid:true}&&lease.IsCurrent;
            try{if(!Current()){lease.Dispose();return null;}return new(lease.Inputs,Current,lease.Dispose);}
            catch(Exception failure){try{lease.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
        });
        IToolSettingsView? candidate=null;
        try
        {
        if(_disposed||generation!=_generation){DisposeInput();return;}
        candidate=SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolPresentation.CreateSettingsView(configuration.Directory,configuration.ToolId,configuration.Fields.Select(configuration.EditorSettingsSchema.Inputs),
            new(referenceOptions,FormatReference,(sourceContent is null?schema:settings).Presentation!.Fields.First().Layout!.Page,inputContent,
                candidate=>ValidateSettingsCandidate(candidate,generation),ValidateComposition:group=>ValidateSettingsChanges(group,generation),RoiFrames:_roiFrames));
        if(_disposed||generation!=_generation){candidate.Dispose();DisposeInput();return;}
        if(panel is not null)_inputs.AddRange(panel.Fields);_view=candidate;_syncedRevision=candidate.AcceptedRevision;_captureError="";
        _view.Changed+=Changed;Content=_view.View;
        }
        catch(Exception failure)
        {
            if(generation==_generation&&ReferenceEquals(_view,candidate))
            {
                _view=null;_inputs.Clear();if(candidate is not null)candidate.Changed-=Changed;Content=null;
            }
            try{try{candidate?.Dispose();}finally{DisposeInput();}}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        foreach(var callback in ResultDisplayBound?.GetInvocationList()??Array.Empty<Delegate>())
        {if(!IsResultDisplayBinding(node,generation))return;((Action<WorkflowNodeBase,long>)callback)(node,generation);}
    }
    private void Changed()
    {
        if(_syncing||_capturing||_disposed||_view is not {} view)return;
        var node=_node;var generation=_generation;var revision=view.AcceptedRevision;
        if(revision==_syncedRevision){NotifyResultDisplayEdited();return;}
        bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(node,_node)&&ReferenceEquals(view,_view)&&revision==view.AcceptedRevision;
        try
        {
            var settings=view.CaptureAcceptedFields();if(!Current())return;
            var previous=node.RuntimeConfiguration!;var next=previous.WithSettingsFields(settings,view.AcceptedFieldOrder);
            if(!Current()||!ReferenceEquals(node.RuntimeConfiguration,previous))return;
            _syncing=true;
            try
            {
                node.RuntimeConfiguration=next;
                if(Current()){_syncedRevision=revision;_captureError="";}
                else if(ReferenceEquals(node.RuntimeConfiguration,next))node.RuntimeConfiguration=previous;
            }
            finally{_syncing=false;}
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {if(Current())_captureError=error.Message;}
        NotifyResultDisplayEdited();
    }
    public bool TryValidateEditor(out string error)
    {
        if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
        {error="当前编辑会话尚未就绪或已经失效。";return false;}
        var node=_node;var generation=_generation;
        bool IsCurrent()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node)
            &&context.IsValid&&ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context);
        if(!TryCapture(out error))return false;
        if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
        try
        {
            context.Host.Validate(context.PreparedDraft);
            if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
            return true;
        }
        catch(InvalidOperationException exception){error=exception.Message;return false;}
    }
    public bool TryCommitEditor(out string error)
    {
        var context=ToolEditorCommit.GetDraftContext(this) as ProductionEditorContext;
        var generation=_generation;var node=_node;
        if(!TryValidateEditor(out error))return false;
        if(_disposed||generation!=_generation||!ReferenceEquals(node,_node)||context is not {IsValid:true}
            ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context))
        {error="参数页已重新绑定，请使用当前参数页。";return false;}
        return context.TryCommitEditor(out error);
    }
    public bool TryCapture(out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_capturing){error="参数捕获正在进行。";return false;}
        var node=_node;var view=_view;var generation=_generation;
        bool IsCurrent()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node)&&ReferenceEquals(_view,view);
        _capturing=true;
        try
        {
        foreach(var input in _inputs.ToArray())
        {
            if(node.RuntimeConfiguration?.IsExecutionFieldActive(input.FieldId)==false)continue;
            if(!input.TryCommitPending()){error=input.Error;return false;}
            if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
        }
        if(_inputs.FirstOrDefault(f=>node.RuntimeConfiguration?.IsExecutionFieldActive(f.FieldId)!=false&&f.HasInvalidText) is { } invalid){error=invalid.Error;return false;}
        if(view is null){error="配置尚未初始化。";return false;}
        if(!view.TryCapture(out var settings,out error))return false;
        if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
        var revision=view.AcceptedRevision;
        _syncing=true;
        var previous=node.RuntimeConfiguration!;RuntimeToolConfiguration? next=null;var installed=false;
        try
        {
            next=previous.WithSettingsFields(settings,view.AcceptedFieldOrder);
            if(!IsCurrent()||revision!=view.AcceptedRevision||view.HasPendingEdits||_inputs.Any(editor=>node.RuntimeConfiguration?.IsExecutionFieldActive(editor.FieldId)!=false&&editor.HasPendingEdits)
                ||!ReferenceEquals(node.RuntimeConfiguration,previous))
            {error="参数页已重新绑定或发生新的编辑。";return false;}
            node.RuntimeConfiguration=next;
            if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
                throw new InvalidOperationException("当前编辑会话尚未就绪或已经失效。");
            context.Host.Validate(context.PreparedDraft);
            if(!IsCurrent()||revision!=view.AcceptedRevision||view.HasPendingEdits||_inputs.Any(editor=>node.RuntimeConfiguration?.IsExecutionFieldActive(editor.FieldId)!=false&&editor.HasPendingEdits)
                ||!ReferenceEquals(node.RuntimeConfiguration,next)||!context.IsValid
                ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context))
            {error="参数页已重新绑定或发生新的编辑。";return false;}
            _syncedRevision=revision;_captureError="";installed=true;error="";return true;
        }
        catch(Exception exception)when(exception is ArgumentException or InvalidOperationException)
        {error=exception.Message;if(IsCurrent()&&revision==view.AcceptedRevision)_captureError=error;return false;}
        finally
        {
            if(!installed&&next is not null&&ReferenceEquals(node.RuntimeConfiguration,next))node.RuntimeConfiguration=previous;
            _syncing=false;
        }
        }
        finally{_capturing=false;}
    }
    private void WriteInput(Port port,ConfigurationSetting setting,long generation)
    {
        if(_disposed||generation!=_generation)throw new InvalidOperationException("来源编辑已失效，请使用当前参数页。");
        if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            throw new InvalidOperationException("当前编辑范围尚未就绪或已经失效。");
        var node=_node;
        node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ConfigurationSetting>{[port.Id]=setting});
    }
    private string FormatReference(OutputReference reference)
    {
        var sourceName=_query?.GetBindingCandidateNodeNames(_node.Id).GetValueOrDefault(reference.NodeId);
        var output=_query?.FormalOutputContractReader?.Invoke(reference.NodeId)?.FirstOrDefault(p=>BindingClrAdapter.OutputPortId(p)==reference.PortId);
        return (sourceName??"已选择来源")+" · "+(output?.DisplayName??output?.Name??"已声明字段")
            +(reference.MemberFieldPath.Count==0?"":" / "+string.Join(" / ",DeclaredReferenceCandidateBuilder.MemberNames(output,reference.MemberFieldPath)));
    }
    private IReadOnlyList<DeclaredReferenceCandidate> ReadReferenceCandidates(long generation,bool manualParameters=false)
    {
        if(_disposed||generation!=_generation||_query is null||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true})
            return Array.Empty<DeclaredReferenceCandidate>();
        var candidates=new List<DeclaredReferenceCandidate>();
        var names=_query.GetBindingCandidateNodeNames(_node.Id);
        foreach(var id in names.Keys)
        foreach(var output in _query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
        {
            if(output.Kind!=OutputVariableKind.Data)continue;
            try{candidates.AddRange(DeclaredReferenceCandidateBuilder.Build(output,id,_query.WorkflowId,names.GetValueOrDefault(id,"可绑定节点")));}
            catch(ArgumentException){continue;}
        }
        if(manualParameters)
        {
            var workflow=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution?.Workflows.SingleOrDefault(flow=>flow.Id==_query.WorkflowId);
            if(workflow is null)return Array.Empty<DeclaredReferenceCandidate>();
            var eligible=OutputBindingChangeService.CaptureManualParameterEligibility(workflow,_node.Id);
            candidates=candidates.Where(candidate=>eligible(candidate.Reference(BindingSelection.All))).ToList();
        }
        return candidates.AsReadOnly();
    }
    private IReadOnlyList<VariableSelectionItem> ReadVariableCandidates(long generation)
    {
        if(_disposed||generation!=_generation||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            return Array.Empty<VariableSelectionItem>();
        var snapshot=context.Host.CaptureVariablesForSelection();
        var result=new List<VariableSelectionItem>();
        foreach(var variable in snapshot.Variables)
        {
            if(!GlobalProducerBindingRules.TryDescribeTarget(variable.Type,out var port,out _,out _))continue;
            result.Add(new(variable.Name,variable.Group,variable.Index,variable.Description,VariableDisplayValue(variable.Value),
                new VariableReference(snapshot.SolutionId,variable.Id,port.Type,storage:port.Storage)));
        }
        return !_disposed&&generation==_generation&&context.IsValid&&ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)
            ?result.AsReadOnly():Array.Empty<VariableSelectionItem>();
    }
    private string FormatVariable(VariableReference reference,long generation)
    {
        if(_disposed||generation!=_generation||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            return "已选择变量";
        var names=context.Host.GetVariableNames(reference.SolutionId);
        return names.TryGetValue(reference.VariableId,out var name)?"全局变量."+name:"已选择变量";
    }
    private static string VariableDisplayValue(object? value)
    {
        if(value is Array array)value=array.Rank==1&&array.Length>0?array.GetValue(0):null;
        return value switch {
            string text=>text,bool flag=>flag.ToString(),
            sbyte or byte or short or ushort or int or uint or long or ulong or decimal=>Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"",
            float number when float.IsFinite(number)=>number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double number when double.IsFinite(number)=>number.ToString(System.Globalization.CultureInfo.InvariantCulture),_=>""};
    }
    private void ValidateSettingsCandidate(FieldSettings settings,long generation)
    {
        var node=_node;
        if(_disposed||generation!=_generation
            ||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            throw new InvalidOperationException("参数编辑已失效，请使用当前参数页。");
        if(!ReferenceEquals(context.DraftNode,node)||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context))
            throw new InvalidOperationException("参数编辑已失效，请使用当前参数页。");
    }
    private string? ValidateSettingsChanges(FieldGroupCandidate candidate,long generation)
    {
        var node=_node;
        if(_disposed||generation!=_generation
            ||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context
            ||!ReferenceEquals(context.DraftNode,node))return "参数编辑已失效，请使用当前参数页。";
        try
        {
            context.Host.ValidateRuntimeFieldProposals(context.PreparedDraft,candidate.Changes);
            return _disposed||generation!=_generation||!context.IsValid||!ReferenceEquals(_node,node)
                ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)?"参数编辑已失效，请使用当前参数页。":null;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return error.Message;}
    }
    private string? ValidateProposal(Port target,ConfigurationSetting proposal,long generation)
    {
        if(_disposed||generation!=_generation
            ||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            return "来源编辑已失效，请使用当前参数页。";
        if(proposal is not (ReferenceSetting or VariableSetting))return null;
        var node=_node;
        try
        {
            context.Host.ValidateRuntimeFieldProposals(context.PreparedDraft,new Dictionary<string,ConfigurationSetting>{[target.Id]=proposal});
            return _disposed||generation!=_generation||!context.IsValid||!ReferenceEquals(_node,node)
                ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)
                ?"来源编辑已失效，请使用当前参数页。":null;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return error.Message;}
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;_generation++;_captureDebug=null;_captureRoiInputs=null;_roiFrameProvider=null;_roiFrames=null;var view=_view;_view=null;_inputs.Clear();
        var errors=new List<Exception>();try{RetireReferencePresentation();}catch(Exception error){errors.Add(error);}
        try{Content=null;}catch(Exception error){errors.Add(error);}try{if(view is not null){view.Changed-=Changed;view.Dispose();}}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);}
}
