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
using SunEyeVision.Workflow;
using ConfigurationSetting=SunEyeVision.Plugin.Contracts.ParameterSetting;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Document adapter for the actual Runtime settings view. No SDK parameter
/// model or algorithm is created. The existing document participant owns publication.</summary>
public sealed class RuntimeNodeConfigurationControl : UserControl,IToolEditorPendingState,IToolEditorCommitParticipant,IDisposable
{
    private WorkflowNodeBase _node=null!;
    private IToolSettingsView? _view;
    private DataSourceQueryService? _query;
    private readonly List<DeclaredFieldEditor> _inputs=new();
    private bool _syncing,_disposed,_capturing;
    private long _generation;
    private bool _running;
    private Func<WorkflowNodeBase,RuntimeDebugInvocation>? _captureDebug;
    public bool HasPendingEdits=>_view?.HasInvalidInput==true||_inputs.Any(editor=>editor.HasInvalidText);
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
            {window.ApplyCompactParameterLayout();window.Resources.MergedDictionaries.Add(Plugin.Wpf.EditorResources.Create());}
        };
    }
    public void SetDataProvider(DataSourceQueryService query)=>_query=query;
    public void SetDebugCapture(Func<WorkflowNodeBase,RuntimeDebugInvocation> capture)=>_captureDebug=capture;
    public async Task RunDebugAsync()
    {
        Dispatcher.VerifyAccess();
        if(_running)throw new InvalidOperationException("调试正在执行。");
        _running=true;RuntimeToolResults? result=null;var generation=_generation;
        FrameworkElement? lockedView=null;bool previousEnabled=true;
        try
        {
            if(!TryValidateEditor(out var error))throw new InvalidOperationException(error);
            using var invocation=(_captureDebug??throw new InvalidOperationException("调试宿主尚未就绪。"))(_node);
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
            try{result?.Resources?.Dispose();}
            finally
            {
                if(!_disposed&&generation==_generation&&ReferenceEquals(_view?.View,lockedView)&&lockedView is not null)
                    lockedView.SetCurrentValue(IsEnabledProperty,previousEnabled);
                _running=false;CommandManager.InvalidateRequerySuggested();
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
        var settings=configuration.SettingsSchema??throw new ArgumentException("Runtime settings metadata is required.");
        var references=node.ParamSettings.Settings.Where(s=>settings.Inputs.Any(p=>p.Id==s.ParameterName)&&s.ReferenceExpression is not null)
            .ToDictionary(s=>s.ParameterName,s=>new ReferenceSetting(BindingReferenceSerializer.Deserialize(s.ReferenceExpression!)),StringComparer.Ordinal);
        var schema=configuration.Schema;
        if(schema.Presentation is null)throw new ArgumentException("Runtime input presentation metadata is required.");
        var referenceOptions=new ReferenceFieldOptions(_=>
        {
            var candidates=ReadReferenceCandidates(generation);
            if(_disposed||generation!=_generation)throw new InvalidOperationException("来源目录读取期间参数页已失效。");
            return new(candidates,candidates.Where(candidate=>candidate.Output.Type.Shape!=ValueShape.Sequence&&candidate.SelectedType==DataType.Integer));
        },(target,proposal)=>ValidateProposal(target,proposal,generation));
        var panel=node.ToolType=="ImageLoad"?null:new DeclaredInputFieldsView(schema,port=>
        {
            var saved=node.ParamSettings.GetSetting(port.Id);
            return saved?.ReferenceExpression is { } expression?new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression)):new UnsetSetting();
        },(port,setting)=>WriteInput(port,setting,generation),referenceOptions with {EditableIndex=true},FormatReference,
            _=>ReadReferenceCandidates(generation));
        var sourceContent=node.ToolType=="ImageLoad"?new ImageSourceDraftContent(node.Id,
            ()=>!_disposed&&generation==_generation&&ReferenceEquals(_node,node),
            SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution)):null;
        FrameworkElement inputContent=(FrameworkElement?)sourceContent??panel!;
        void DisposeInput(){if(sourceContent is not null)sourceContent.Dispose();else panel?.Dispose();}
        IToolSettingsView? candidate=null;
        try
        {
        if(_disposed||generation!=_generation){DisposeInput();return;}
        candidate=ToolRegistry.CreateSettingsView(configuration.ToolId,configuration.CaptureSettings(),
            new(references,referenceOptions,FormatReference,(sourceContent is null?schema:settings).Presentation!.Fields.First().Layout!.Page,inputContent,
                (candidate,bindings)=>ValidateSettingsCandidate(candidate,bindings,generation)));
        if(_disposed||generation!=_generation){candidate.Dispose();DisposeInput();return;}
        if(panel is not null)_inputs.AddRange(panel.Fields);_view=candidate;
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
    }
    private void Changed(){if(!_syncing&&!_capturing)TryCapture(out _);}
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
            if(!input.TryCommitPending()){error=input.Error;return false;}
            if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
        }
        if(_inputs.FirstOrDefault(f=>f.HasInvalidText) is { } invalid){error=invalid.Error;return false;}
        if(view is null){error="配置尚未初始化。";return false;}
        if(!view.TryCapture(out var settings,out var references,out error))return false;
        if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
        _syncing=true;
        try
        {
            var next=node.RuntimeConfiguration!.WithSettings(settings);
            if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
            var bindings=node.ParamSettings.Clone();
            foreach(var port in next.SettingsSchema!.Inputs)
                if(references.TryGetValue(port.Id,out var reference))bindings.SetSetting(ToSetting(node,port.Id,reference.Reference));
                else bindings.RemoveSetting(port.Id);
            if(!IsCurrent()){error="参数页已重新绑定，请使用当前参数页。";return false;}
            node.RuntimeConfiguration=next;node.ParamSettings=bindings;error="";return true;
        }
        catch(Exception exception)when(exception is ArgumentException or InvalidOperationException){error=exception.Message;return false;}
        finally{_syncing=false;}
        }
        finally{_capturing=false;}
    }
    private static ParamSetting ToSetting(WorkflowNodeBase node,string id,OutputReference reference)
    {
        var expression=BindingReferenceSerializer.Serialize(reference);var prior=node.ParamSettings.GetSetting(id);
        return new(){ParameterName=id,BindingType=BindingType.Binding,ReferenceExpression=expression,SourceNodeId=reference.NodeId,
            SourceWorkflowId=reference.ScopeId,SourceProperty=reference.PortId,
            Conversion=prior?.ReferenceExpression==expression?prior.Conversion:BindingConversion.Automatic};
    }
    private void WriteInput(Port port,ConfigurationSetting setting,long generation)
    {
        if(_disposed||generation!=_generation)throw new InvalidOperationException("来源编辑已失效，请使用当前参数页。");
        if(ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            throw new InvalidOperationException("当前编辑范围尚未就绪或已经失效。");
        var node=_node;
        var bindings=node.ParamSettings.Clone();
        if(setting is ReferenceSetting reference)bindings.SetSetting(ToSetting(node,port.Id,reference.Reference));
        else if(setting is UnsetSetting)bindings.RemoveSetting(port.Id);
        else throw new ArgumentException("运行输入必须选择已声明来源。");
        node.ParamSettings=bindings;
    }
    private string FormatReference(OutputReference reference)
    {
        var source=_query?.GetAvailableDataSources(_node.Id,null,true).FirstOrDefault(s=>s.SourceNodeId==reference.NodeId);
        var output=_query?.FormalOutputContractReader?.Invoke(reference.NodeId)?.FirstOrDefault(p=>BindingClrAdapter.OutputPortId(p)==reference.PortId);
        return (source?.SourceNodeName??"已选择来源")+" · "+(output?.DisplayName??output?.Name??"已声明字段")
            +(reference.MemberFieldPath.Count==0?"":" / "+string.Join(" / ",reference.MemberFieldPath));
    }
    private IReadOnlyList<DeclaredReferenceCandidate> ReadReferenceCandidates(long generation)
    {
        if(_disposed||generation!=_generation||_query is null||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true})
            return Array.Empty<DeclaredReferenceCandidate>();
        var candidates=new List<DeclaredReferenceCandidate>();
        var names=_query.GetAvailableDataSources(_node.Id,null,true).GroupBy(s=>s.SourceNodeId).ToDictionary(g=>g.Key,g=>g.First().SourceNodeName);
        foreach(var id in _query.BindingCandidateNodeIds?.Invoke(_node.Id)??names.Keys.ToArray())
        foreach(var output in _query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
        {
            if(output.Kind!=OutputVariableKind.Data)continue;
            Port port;try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
            var label=names.GetValueOrDefault(id,"可绑定节点")+" · "+(output.DisplayName??output.Name);
            candidates.Add(new(label,id,_query.WorkflowId,port,Array.Empty<string>()));
            AddMembers(port.Type,Array.Empty<string>(),label,id,port);
        }
        void AddMembers(TypeRef type,IReadOnlyList<string> path,string label,string id,Port root)
        {
            while(type.Shape==ValueShape.Sequence)type=type.Element!;
            if(type.Shape!=ValueShape.Record)return;
            foreach(var member in type.Fields)
            {var next=path.Append(member.Id).ToArray();var name=label+" / "+member.Id;candidates.Add(new(name,id,_query.WorkflowId,root,next));AddMembers(member.Type,next,name,id,root);}
        }
        return candidates.AsReadOnly();
    }
    private void ValidateSettingsCandidate(object settings,IReadOnlyDictionary<string,ReferenceSetting> references,long generation)
    {
        var node=_node;
        if(_disposed||generation!=_generation
            ||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            throw new InvalidOperationException("参数编辑已失效，请使用当前参数页。");
        var priorBindings=node.ParamSettings;var priorConfiguration=node.RuntimeConfiguration!;
        try
        {
            var candidate=priorConfiguration.WithSettings(settings);
            if(_disposed||generation!=_generation||!ReferenceEquals(node,_node))
                throw new InvalidOperationException("参数编辑已失效，请使用当前参数页。");
            var bindings=priorBindings.Clone();
            foreach(var port in candidate.SettingsSchema!.Inputs)
                if(references.TryGetValue(port.Id,out var reference))bindings.SetSetting(ToSetting(node,port.Id,reference.Reference));
                else bindings.RemoveSetting(port.Id);
            node.RuntimeConfiguration=candidate;node.ParamSettings=bindings;
            context.Host.Validate(context.PreparedDraft);
            if(_disposed||generation!=_generation||!context.IsValid||!ReferenceEquals(_node,node)
                ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context))
                throw new InvalidOperationException("参数编辑已失效，请使用当前参数页。");
        }
        finally{node.RuntimeConfiguration=priorConfiguration;node.ParamSettings=priorBindings;}
    }
    private string? ValidateProposal(Port target,ConfigurationSetting proposal,long generation)
    {
        if(_disposed||generation!=_generation
            ||ToolEditorCommit.GetDraftContext(this) is not ProductionEditorContext {IsValid:true} context)
            return "来源编辑已失效，请使用当前参数页。";
        if(proposal is not ReferenceSetting reference)return null;
        var node=_node;var priorBindings=node.ParamSettings;var priorConfiguration=node.RuntimeConfiguration;
        try
        {
            var proposed=priorBindings.Clone();proposed.SetSetting(ToSetting(node,target.Id,reference.Reference));
            node.ParamSettings=proposed;
            context.Host.Validate(context.PreparedDraft);
            return _disposed||generation!=_generation||!context.IsValid||!ReferenceEquals(_node,node)
                ||!ReferenceEquals(ToolEditorCommit.GetDraftContext(this),context)
                ?"来源编辑已失效，请使用当前参数页。":null;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException){return error.Message;}
        finally{node.ParamSettings=priorBindings;node.RuntimeConfiguration=priorConfiguration;}
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;_generation++;_captureDebug=null;var view=_view;_view=null;_inputs.Clear();Content=null;if(view is not null){view.Changed-=Changed;view.Dispose();}}
}
