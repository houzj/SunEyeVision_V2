using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal event Action? ResultWorkspaceChanged;
    internal event Action<ResultSelection?>? ResultWorkspaceSelectionChanged;
    internal event Action? ResultWorkspaceGraphicsCleared;
    internal event Action<SunEyeVision.UI.Shared.ImageInputSource,SourceDisplayPreservationScope>? SourceDisplayPreservationPreparing;
    internal event Action<string>? ResultWorkspaceRootSettled;
    private void ConfigureFormalImageSourcePresentation()
    {
        ImageSourceCanNavigate=()=>CanAcceptExternalActivation&&!IsDebugWindowOpen&&DocumentManager.CurrentSession is not null;
        ImageSourceActivate=ActivateFormalImageSourceAsync;
        ImageSourceEditAccepted=receipt=>
        {
            if(!ReferenceEquals(DocumentManager.CurrentSession,receipt.Session))return;
            RefreshProductionPublication(receipt.Session,receipt);
            ResultWorkspaceChanged?.Invoke();OnPropertyChanged(nameof(ActiveInputSource));
        };
        ImageSourcePresentationError=(stage,error)=>AddLog(stage+"："+error.Message);
        ImageSourcePrepareDisplay=_=>
        {
            var session=DocumentManager.CurrentSession;var node=SelectedNode;var tab=WorkflowTabViewModel.SelectedTab;
            var binding=ActiveImageSourceBinding;var clearEpoch=_nativeExplicitClearEpoch;
            if(session is null||node is null||tab is null||binding is null)return null;
            var source=binding.Source;var version=source.EditVersion;
            bool Current()=>ReferenceEquals(DocumentManager.CurrentSession,session)&&ReferenceEquals(SelectedNode,node)
                &&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)&&ReferenceEquals(ActiveImageSourceBinding,binding)
                &&source.EditVersion==version&&clearEpoch==_nativeExplicitClearEpoch&&OwnsSelectedDocumentNode(node);
            var scope=new SourceDisplayPreservationScope(Current);
            try{SourceDisplayPreservationPreparing?.Invoke(source,scope);return scope;}
            catch{scope.Dispose();throw;}
        };
    }
    private long _nativeExplicitClearEpoch;
    private void NotifyResultWorkspaceRootSettled(string workflowId)
    {
        try{ResultWorkspaceRootSettled?.Invoke(workflowId);}
        catch(Exception error){AddLog("运行结束后的图像清理失败："+error.Message);}
    }
    private string? _nativePreviewPath;
    private bool _nativeImagesSuppressed;
    internal string? NativePreviewPath=>_nativePreviewPath;
    internal void RefreshNativeResultImages()
    {
        _nativePreviewPath=null;_nativeImagesSuppressed=false;
        ResultWorkspaceChanged?.Invoke();
    }
    private void ClearNativeImagePresentation()
    {
        _nativePreviewPath=null;_nativeImagesSuppressed=true;
        ResultWorkspaceChanged?.Invoke();
    }
    public void ClearResultGraphics()=>ResultWorkspaceGraphicsCleared?.Invoke();
    public void ClearResultHighlight()=>ResultWorkspaceSelectionChanged?.Invoke(null);

    internal ResultWorkspaceImagePlan CaptureNativeImageSources(Func<bool> consumerCurrent)
    {
        var plan=new ResultWorkspaceImagePlan();
        var session=DocumentManager.CurrentSession;var selected=SelectedNode;
        var tab=WorkflowTabViewModel.SelectedTab;var scope=tab?.CurrentWorkflowId;
        if(session is null||selected is null||scope is null||!session.GetNodes(scope).Any(node=>ReferenceEquals(node,selected)))return plan;
        bool Owner()=>!plan.IsDisposed&&consumerCurrent()&&ReferenceEquals(DocumentManager.CurrentSession,session)
            &&ReferenceEquals(SelectedNode,selected)&&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)&&OwnsSelectedDocumentNode(selected);
        var nodes=session.GetNodes(scope);var edges=session.GetConnections(scope);
        var ids=new HashSet<string>(StringComparer.Ordinal){selected.Id};var queue=new Queue<string>();queue.Enqueue(selected.Id);
        while(queue.TryDequeue(out var id))foreach(var edge in edges.Where(edge=>edge.TargetNodeId==id))if(ids.Add(edge.SourceNodeId))queue.Enqueue(edge.SourceNodeId);
        try
        {
            foreach(var node in nodes.Where(node=>ids.Contains(node.Id)).OrderBy(node=>node.Id==selected.Id?0:1))
            {
                using var acquired=session.AcquirePublishedResult(scope,node.Id);
                var snapshot=acquired?.Snapshot;
                bool SnapshotCurrent()
                {
                    if(!Owner()||snapshot is null)return false;
                    using var latest=session.AcquirePublishedResult(scope,node.Id);
                    return latest is not null&&ReferenceEquals(latest.Snapshot,snapshot)&&Owner();
                }
                var schema=snapshot?.Schema??session.GetNodeSchema(scope,node.Id);
                if(schema is null){plan.Diagnostics.Add(node.Name+"：输出声明尚未就绪。");continue;}
                IResultPublication? publication=null;
                if(!_nativeImagesSuppressed&&snapshot is {IsComplete:true}&&SnapshotCurrent())
                {publication=ToolResultViews.FromSnapshot(snapshot,SnapshotCurrent);plan.Own(publication);}
                using var values=publication?.Borrow();
                string? fileName=null;
                foreach(var port in schema.Outputs)
                {
                    var label=schema.FindOutputDisplay(port.Id)?.DisplayName
                        ??(snapshot?.Fields.TryGetValue(port.Id,out var metadata)==true?metadata.DisplayName:port.Id);
                    Walk(port.Type,values?.Values?.Read(port.Id),Array.Empty<string>(),null,node.Name+" · "+label);
                    void Walk(TypeRef type,DeclaredValueReader? value,string[] path,int? index,string name)
                    {
                        if(plan.Sources.Count>=4096)throw new InvalidOperationException("图像来源超过4096项，拒绝整批而非截断列表。");
                        if(type==TypeRef.ResourceOf(ResourceKind.Image))
                        {
                            var reference=new OutputReference(node.Id,port.Id,port.Type,path,
                                selection:index is {} item?new BindingSelection.AtConstant(item):null,scopeId:scope,storage:port.Storage);
                            plan.Sources.Add(new(name,reference,publication,SnapshotCurrent,node.Id!=selected.Id)
                                {FileName=port.Id is "output.image" or "output.gray-image"?fileName:null});
                            return;
                        }
                        if(type.Shape==ValueShape.Record)
                            foreach(var field in type.Fields)
                            {
                                // A record with one image keeps its declared group label in the image selector.
                                var onlyImage=field.Type==TypeRef.ResourceOf(ResourceKind.Image)
                                    &&type.Fields.Count(item=>item.Type==TypeRef.ResourceOf(ResourceKind.Image))==1;
                                var memberPath=path.Append(field.Id).ToArray();
                                var memberName=schema.FindOutputDisplay(port.Id,memberPath)?.DisplayName??field.Id;
                                Walk(field.Type,value is {IsMissing:false,IsNull:false}?value.GetField(field.Id):null,
                                    memberPath,index,onlyImage?name:name+" / "+memberName);
                            }
                        else if(type.Shape==ValueShape.Sequence&&ContainsImage(type.Element!))
                        {
                            if(index is not null||path.Length>0){plan.Diagnostics.Add(name+"：组合内或嵌套序列不具备正式索引合同，当前不可显示。");return;}
                            if(value is null||value.IsMissing||value.IsNull)
                            {plan.Diagnostics.Add(name+"：序列尚无实际元素，不能推断首项图像。");return;}
                            if(value.Count>4096)throw new InvalidOperationException(name+"图像序列超过来源预算。");
                            for(var i=0;i<value.Count;i++)Walk(type.Element!,value.GetElement(i),path,i,name+" ["+i+"]");
                        }
                    }
                }
                if(!Owner())throw new InvalidOperationException("建立图像来源期间工作区已被替换。");
            }
            if(session.GetNodeSchema(scope,selected.Id) is {} inputSchema)
            {
                var inputKeys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var input in inputSchema.Inputs.Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)))
                {
                    if(selected.Configuration.Fields.Read(input.Id) is not ReferenceSetting setting)continue;
                    var reference=setting.Reference;
                    if(!string.IsNullOrEmpty(setting.RebindingReason)||reference.ScopeId is {} explicitScope&&explicitScope!=scope)continue;
                    var scoped=new OutputReference(reference.NodeId,reference.PortId,reference.Type,reference.MemberFieldPath,
                        selection:reference.Selection,scopeId:scope,storage:reference.Storage);
                    var key=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(scoped);
                    var source=plan.Sources.FirstOrDefault(item=>item.Key==key);
                    if(source is not null&&inputKeys.Add(key))plan.InputSources.Add(source.CreateInputItem());
                }
            }

            return plan;
        }
        catch{plan.Dispose();throw;}
    }
    private static bool ContainsImage(TypeRef type)=>type==TypeRef.ResourceOf(ResourceKind.Image)
        ||type.Shape==ValueShape.Sequence&&ContainsImage(type.Element!)
        ||type.Shape==ValueShape.Record&&type.Fields.Any(field=>ContainsImage(field.Type));
}
