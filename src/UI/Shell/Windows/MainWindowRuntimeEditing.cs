using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Workflow;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal Func<SunEyeVision.Plugin.Contracts.OutputReference,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?>? RuntimeRoiFrameBorrow {get;set;}
    internal event Action? RoiFrameAvailabilityChanged;
    internal void NotifyRoiFrameAvailabilityChanged()
    {
        foreach(var handler in RoiFrameAvailabilityChanged?.GetInvocationList()??Array.Empty<Delegate>())
            try{((Action)handler)();}catch(Exception error){try{AddLog("ROI视口通知失败："+error.Message);}catch{}}
    }

    private void TryAutoBindRuntimeImageSource(SunEyeVision.Workflow.WorkflowNodeBase node,SunEyeVision.Workflow.WorkflowConnection? connection)
    {
        if(node.RuntimeConfiguration is not { } runtime||runtime.Schema.WorkflowImageInputId is not { } target||
            runtime.Fields.Read(target) is not Plugin.Contracts.UnsetSetting||
            node is not Shared.WorkflowNode uiNode||GetDataProviderForNode(uiNode) is not Plugin.SDK.Execution.Parameters.DataSourceQueryService query)return;
        var candidates=(query.BindingCandidateNodeIds?.Invoke(node.Id)??query.GetAvailableDataSources(node.Id).Select(source=>source.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray()).ToHashSet(StringComparer.Ordinal);
        var incoming=connection is not null?new[]{connection}:_workflowEngine.GetWorkflow(query.WorkflowId!)!.Connections.Where(edge=>edge.TargetNodeId==node.Id).ToArray();
        static System.Collections.Generic.IEnumerable<string[]> ImageMembers(Plugin.Contracts.TypeRef type,string[] path)
        {
            if(type==Plugin.Contracts.TypeRef.ResourceOf(Plugin.Contracts.ResourceKind.Image)){yield return path;yield break;}
            // A sequence has no unambiguous single image without an explicit selection.
            if(type.Shape!=Plugin.Contracts.ValueShape.Record)yield break;
            foreach(var field in type.Fields)
                foreach(var child in ImageMembers(field.Type,path.Append(field.Id).ToArray()))
                    yield return child;
        }
        foreach(var edge in incoming.Where(edge=>candidates.Contains(edge.SourceNodeId)))
        foreach(var id in new[]{edge.SourceNodeId})
        foreach(var schema in new[]{query.GetSchema(id)}.OfType<Plugin.Contracts.ToolSchema>())
        foreach(var port in schema.Outputs)
        {
            var members=ImageMembers(port.Type,Array.Empty<string>()).FirstOrDefault();
            if(members is null)continue;
            var reference=new Plugin.Contracts.OutputReference(id,port.Id,port.Type,members,scopeId:query.WorkflowId,storage:port.Storage);
            // The enclosing graph command owns snapshot, rollback and undo/redo.
            // A present setting (including a broken automatic reference) is never silently replaced.
            node.RuntimeConfiguration=runtime.WithFieldSettings(new System.Collections.Generic.Dictionary<string,Plugin.Contracts.ParameterSetting>{
                [target]=new Plugin.Contracts.ReferenceSetting(reference,OriginId:"connection:"+edge.Id)});
            AddLog($"自动绑定图像源: {node.Name} → {schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id}");return;
        }
    }
    private bool InitializeRuntimeEditor(object target,Shared.WorkflowNode node)
    {
        if(node.RuntimeConfiguration is null)return false;
        if(target is not RuntimeToolEditorWindow window)
            throw new InvalidOperationException("Runtime参数窗口缺少中立配置视图。");
        var control=window.Editor;
        var session=DocumentManager.Session??throw new InvalidOperationException("当前没有方案编辑会话。");
        var flow=session.Document.Workflows.Single(w=>w.Nodes.Any(n=>ReferenceEquals(n,node)));
        var host=GetProductionEditorHost(session);var query=CreateDataSourceQueryService(flow.Id);
        control.SetDataProvider(query);
        var context=new ProductionEditorContext(host,flow.Id,node,
            ()=>ReferenceEquals(_openDebugWindow,window)&&ReferenceEquals(DocumentManager.Session,session)
                &&session.Document.Workflows.Any(w=>ReferenceEquals(w,flow)&&w.Nodes.Any(n=>ReferenceEquals(n,node))),
            draft=>control.BindDraft(draft),publication=>RefreshProductionPublication(session,publication,query),session.Editing);
        context.PrepareView=()=>control.TryCapture(out var error)?null:error;
        context.HasPendingViewEdits=()=>control.HasPendingEdits;
        ToolEditorCommit.AttachDraftContext(control,context);
        window.SetRunAvailability(()=>context.IsValid&&!IsRunning);
        void AvailabilityChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
        { if(args.PropertyName==nameof(IsRunning))window.RefreshRunAvailability(); }
        PropertyChanged+=AvailabilityChanged;
        context.Deactivated+=()=>PropertyChanged-=AvailabilityChanged;
        context.Deactivated+=control.Dispose;
        try
        {
            control.SetRoiFrameProvider((reference,current)=>context.IsValid?RuntimeRoiFrameBorrow?.Invoke(reference,()=>context.IsValid&&current()):null);
            void RefreshRoi()=>control.Dispatcher.BeginInvoke(new Action(()=>{if(context.IsValid)try{control.RefreshRoiFrame();}catch(Exception error){AddLog("ROI预览刷新失败："+error.Message);}}));
            ResultWorkspaceChanged+=RefreshRoi;RoiFrameAvailabilityChanged+=RefreshRoi;context.Deactivated+=()=>{ResultWorkspaceChanged-=RefreshRoi;RoiFrameAvailabilityChanged-=RefreshRoi;};
            var watchedVariables=new List<GlobalVariable>();var requestedVariables=new HashSet<string>(StringComparer.Ordinal);
            void VariableChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)=>RefreshRoi();
            bool WatchVariables(IReadOnlyCollection<string> ids)
            {
                requestedVariables=ids.ToHashSet(StringComparer.Ordinal);var changed=false;
                foreach(var old in watchedVariables.Where(variable=>!session.Document.GlobalVariables.Any(current=>ReferenceEquals(current,variable))||!requestedVariables.Contains(variable.Id)).ToArray())
                {old.PropertyChanged-=VariableChanged;watchedVariables.Remove(old);changed=true;}
                foreach(var variable in session.Document.GlobalVariables.Where(variable=>requestedVariables.Contains(variable.Id)&&!watchedVariables.Any(current=>ReferenceEquals(current,variable))))
                {variable.PropertyChanged+=VariableChanged;watchedVariables.Add(variable);changed=true;}
                return changed;
            }
            void HistoryChanged(object? sender,EventArgs args){if(context.IsValid&&WatchVariables(requestedVariables))RefreshRoi();}
            session.History.Changed+=HistoryChanged;
            context.Deactivated+=()=>{session.History.Changed-=HistoryChanged;foreach(var variable in watchedVariables)variable.PropertyChanged-=VariableChanged;watchedVariables.Clear();};
            control.SetRoiInputCapture((draft,fields)=>
            {
                if(!context.IsValid||IsRunning)throw new InvalidOperationException("请在当前方案停止运行后预览继承区域。");
                return RuntimeDebugInvocation.CaptureInputs(flow,session.Document.RuntimeDirectory ?? throw new InvalidOperationException("方案缺少已准入运行目录。"),draft,context.DebugOwner,host.Capture().Version,
                    id=>query.GetNodeContext(id)?.Result,context.DebugCancellation,publishedOwner:session.Document.ExecutionOwner,
                    solutionId:session.Document.Id,variables:session.Document.GlobalVariables,sourceWorkflows:session.Document.Workflows,
                    readScopedPublished:source=>CreateDataSourceQueryService(source.ScopeId).GetNodeContext(source.NodeId)?.Result,inputFieldIds:fields,observeVariables:ids=>WatchVariables(ids));
            });
            control.BindDraft(context.DraftNode);
            var displayPreview=InstallResultDisplayPreview(session,node,context,control);
            context.Deactivated+=displayPreview.Dispose;
            control.SetDebugCapture(draft=>
            {
                if(!context.IsValid||IsRunning)throw new InvalidOperationException("请在当前方案停止工作流后调试此节点。");
                var paths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document);
                var camera=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime;
                var preparer=RuntimeHostInputAdapters.CreateDebug(draft,flow.Id,context.DebugOwner,paths,camera);
                try
                {
                    return RuntimeDebugInvocation.Capture(flow,session.Document.RuntimeDirectory ?? throw new InvalidOperationException("方案缺少已准入运行目录。"),draft,context.DebugOwner,host.Capture().Version,
                        id=>query.GetNodeContext(id)?.Result,context.DebugCancellation,publishedOwner:session.Document.ExecutionOwner,
                        resourcePaths:paths,cameraService:camera,
                        solutionId:session.Document.Id,variables:session.Document.GlobalVariables,sourceWorkflows:session.Document.Workflows,
                        readScopedPublished:source=>CreateDataSourceQueryService(source.ScopeId).GetNodeContext(source.NodeId)?.Result,
                        readDisplayCandidate:id=>query.GetNodeContext(id)?.Result,hostInputPreparer:preparer);
                }
                catch(Exception failure)
                {
                    try { preparer?.Dispose(); }
                    catch(Exception cleanup) { throw new AggregateException(failure,cleanup); }
                    throw;
                }
            });
            SubscribeRuntimeExecutionCompleted(window,node,context);
            TrackProductionEditor(_productionEditors,context,window,ClearProductionEditorHost);
            _nodeDebugWindows[node.Id]=window;
            window.Closed+=(_,_)=>_nodeDebugWindows.Remove(node.Id);
            window.Show();return true;
        }
        catch{context.Discard();throw;}
    }
    private void SubscribeRuntimeExecutionCompleted(RuntimeToolEditorWindow window, Shared.WorkflowNode node, ProductionEditorContext context)
    {
        var resultContext=CurrentWorkflowContext;var session=DocumentManager.Session;var tab=WorkflowTabViewModel.SelectedTab;var closed=false;
        bool OwnerCurrent()=>!closed&&window.IsLoaded&&ReferenceEquals(DocumentManager.Session,session)&&context.IsValid;
        bool DisplayCurrent()=>OwnerCurrent()&&ReferenceEquals(tab,WorkflowTabViewModel.SelectedTab)
            &&ReferenceEquals(resultContext,CurrentWorkflowContext)&&ReferenceEquals(SelectedNode,node)&&!_declaredHistoricalResult;
        void Handler(object? sender,RuntimeToolExecutionEventArgs args)
        {
            var result=args.Result;
            try
            {
                using var retained=result?.Retain();
                void Apply()
                {
                    if(!OwnerCurrent())return;
                    if(result is null)
                    {
                        if(args.Error is not null){context.DebugResults.RemoveNodeResult(node.Id);if(DisplayCurrent()){ClearResultDisplay();ClearNativeImagePresentation();}}
                        return;
                    }
                    if(result.ExecutionIdentity is {} identity&&(!identity.IsCurrent||!ReferenceEquals(identity.Owner,context.DebugOwner)))return;
                    if(!context.TryAcceptDebugResult(result)||!OwnerCurrent())return;
                    if(DisplayCurrent())_nodeResultManager.RefreshDebugPreview(context.DraftNode,result);
                    if(OwnerCurrent())AddLog($"调试窗口执行完成: {node.Name}");
                }
                if(window.Dispatcher.CheckAccess())Apply();
                else if(result is null)window.Dispatcher.BeginInvoke(new Action(Apply));
                else
                {
                    var pending=new ResultDispatchLease(result);
                    try
                    {
                        var operation=window.Dispatcher.BeginInvoke(new Action(()=>{using var lease=pending.Claim();if(lease is not null)Apply();}));
                        operation.Aborted+=(_,_)=>pending.Dispose();
                        if(operation.Status==System.Windows.Threading.DispatcherOperationStatus.Aborted)pending.Dispose();
                    }
                    catch{pending.Dispose();throw;}
                }
            }
            catch(Exception error){AddLog($"处理执行结果失败: {error.Message}");}
        }
        void RestoreProductionDisplay()
        {
            context.Deactivated-=RestoreProductionDisplay;
            if(!ReferenceEquals(resultContext,CurrentWorkflowContext)||!ReferenceEquals(SelectedNode,node))return;
            if(resultContext?.GetNodeResult(node.Id)?.Result is { } production)_nodeResultManager.RefreshResultDisplay(node,production);
            else{_nodeResultManager.ClearResultDisplay();ClearNativeImagePresentation();}
            _=UpdateDataAnalysisControl(node);
        }
        window.ToolExecutionCompleted+=Handler;
        context.Deactivated+=RestoreProductionDisplay;
        window.Closed+=(_,_)=>{closed=true;window.ToolExecutionCompleted-=Handler;};
    }
}
