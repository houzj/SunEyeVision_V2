using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private VariableSetting? _activeRoiImageVariable;
    private Guid? _activeRoiImageScope;
    internal event Action? ResultWorkspaceChanged;
    internal event Action<ResultSelection?>? ResultWorkspaceSelectionChanged;
    internal event Action? ResultWorkspaceGraphicsCleared;
    internal event Action<SunEyeVision.UI.Shared.ImageInputSource,SourceDisplayPreservationScope>? SourceDisplayPreservationPreparing;
    internal event Action<string>? ResultWorkspaceRootSettled;
    internal bool IsResultImageChoiceLocked=>IsRunning||_editorDebugControl?.IsDebugBusy==true;
    private void ConfigureFormalImageSourcePresentation()
    {
        // VM keeps the image-source strip navigable while a module debug window
        // is open. Navigation changes the displayed input cursor and does not
        // commit the debug draft or start a formal run.
        ImageSourceCanNavigate=()=>CanAcceptExternalActivation&&!IsExecutionStopping&&DocumentManager.CurrentSession is not null;
        ImageSourceActivate=ActivateFormalImageSourceAsync;
        ImageSourceEditAccepted=receipt=>
        {
            if(!ReferenceEquals(DocumentManager.CurrentSession,receipt.Session))return;
            // VM keeps the current result/display when only the image-source run
            // policy changes. The projection binding has already accepted the
            // one-shot display retention lease for this edit.
            if(ActiveImageSourceBinding?.LastAcceptedPreservedDisplay==true)
            {
                RefreshRootTabReadiness();
                BuildFlatRows();
                OnPropertyChanged(nameof(ActiveInputSource));
                return;
            }
            RefreshProductionPublication(receipt.Session,receipt);
            ResultWorkspaceChanged?.Invoke();OnPropertyChanged(nameof(ActiveInputSource));
        };
        ImageSourcePresentationError=(stage,error)=>AddLog(stage+"："+error.Message);
        ImageSourcePrepareDisplay=_=>
        {
            var session=DocumentManager.CurrentSession;var node=SelectedNode;var tab=WorkflowTabViewModel.SelectedTab;
            var binding=ActiveImageSourceBinding;var clearEpoch=_nativeExplicitClearEpoch;
            if(session is null||node is null||tab is null||binding is null)return null;
            // The document transaction retires execution domains before the
            // image-source policy acceptance callback runs. Keep the last
            // completed result owned by the display layer so a policy-only edit
            // cannot turn a visible result into an empty workspace.
            RetainCurrentResultForEdit();
            var source=binding.Source;var version=source.EditVersion;
            bool Current()=>ReferenceEquals(DocumentManager.CurrentSession,session)&&ReferenceEquals(SelectedNode,node)
                &&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)&&ReferenceEquals(ActiveImageSourceBinding,binding)
                // The acceptance callback runs after the source projection commits
                // and increments EditVersion once. Keep the retention lease valid
                // across that single transition, but reject any later edit.
                &&source.EditVersion>=version&&source.EditVersion<=version+1
                &&clearEpoch==_nativeExplicitClearEpoch&&OwnsSelectedDocumentNode(node);
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
    internal bool ShowLocalImageName
    {
        get
        {
            var node=SelectedNode;
            if(node?.ToolType!="ImageLoad")return false;
            var preview=_displayPreview;
            var fields=(preview?.Current==true?preview.Context.PreparedDraft.Configuration:node.Configuration).Fields;
            return fields.Read("source.kind") is ConstantSetting { Value:"local" } &&
                fields.Read("display.image-name") is ConstantSetting { Value:true };
        }
    }
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
        var session=DocumentManager.CurrentSession;var selected=SelectedNode;
        // Keep the live debug draft as the source of truth during a synchronous
        // node rebind caused by a parameter selection. The public selected
        // context briefly reports invalid while the same editor is being
        // rebound, but discarding its pending image variable would detach ROI.
        var editor=SelectedEditorContext??(_editorDebugContext is {} candidate
            &&ReferenceEquals(candidate.Session,session)&&selected is not null&&candidate.Identity.NodeId==selected.Id?candidate:null);
        var plan=new ResultWorkspaceImagePlan {EditorScopeId=editor?.ScopeId};
        var tab=WorkflowTabViewModel.SelectedTab;var scope=tab?.CurrentWorkflowId;
        if(session is null||selected is null||scope is null||!session.GetNodes(scope).Any(node=>ReferenceEquals(node,selected)))return plan;
        // 原件 ModuleResultView 语义：正式结果显示不随参数窗口开合变化（证据
        // docs/collaboration/evidence/VM调试窗口结果视口更新路径-20261112.json）。
        // 因此正式发布来源的当前性不绑定编辑上下文；私有调试快照的上下文校验
        // 由 CurrentEditorDebugSnapshot 内部（IsValid/会话/节点一致）保证，上下文
        // 失效或更换都会使其返回 null/false，无需在此重复绑定。
        bool Owner()=>!plan.IsDisposed&&consumerCurrent()&&ReferenceEquals(DocumentManager.CurrentSession,session)
            &&ReferenceEquals(SelectedNode,selected)&&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)
            &&OwnsSelectedDocumentNode(selected);
        var nodes=session.GetNodes(scope);var edges=session.GetConnections(scope);
        var ids=new HashSet<string>(StringComparer.Ordinal){selected.Id};var queue=new Queue<string>();queue.Enqueue(selected.Id);
        while(queue.TryDequeue(out var id))foreach(var edge in edges.Where(edge=>edge.TargetNodeId==id))if(ids.Add(edge.SourceNodeId))queue.Enqueue(edge.SourceNodeId);
        try
        {
            // 原件显示来源下拉先列当前模块自己的图像结果，再列上游来源。
            // 当前 Blob 的实机证据为“显示 Blob/显示二值化 → 图像源输出图像/灰度 → 输出掩膜”。
            // 节点序号仍只用于同组内稳定排序，不能把上游图像源整体移到当前模块之前。
            foreach(var node in nodes.Where(node=>ids.Contains(node.Id))
                .OrderBy(node=>node.Id==selected.Id?0:1).ThenBy(node=>node.GlobalIndex))
            {
                var privateNode=editor is not null&&ReferenceEquals(node,selected);
                // 调试窗口打开但尚未执行时没有私有快照，此时节点仍以正式发布呈现图像与叠加
                // （原件 ModuleResultView 编辑态同样显示模块结果）；不得因此抽走正式图像。
                var debugSnapshot=privateNode?CurrentEditorDebugSnapshot:null;
                if(debugSnapshot is null&&ReferenceEquals(node,selected)&&_retainedEditorDebugSnapshot is {} retained
                    &&IsRetainedEditorDebugSnapshot(retained))debugSnapshot=retained;
                // 原件编辑态同样显示该模块的正式结果；用户显式改选显示来源后，
                // UpdateVMResultShow 立即读取所选图像对象当前值显示（RenderControlBase
                // .xaml.cs.pending:1127-1154，由 imageListCombobox_SelectionChanged 调用），
                // 与参数窗口开合无关。因此不再按私有编辑作用域压制正式像素
                // （此前“有持久化选择即压制”导致调试窗口打开期间显式改选后视口空白）；
                // 调试执行产生私有快照后自然切换到私有结果。
                var retainFormalImage=true;
                using var acquired=debugSnapshot is null?session.AcquirePublishedResult(scope,node.Id):null;
                var snapshot=debugSnapshot??acquired?.Snapshot;
                bool SnapshotCurrent()
                {
                    if(!Owner()||snapshot is null)return false;
                    if(ReferenceEquals(debugSnapshot,snapshot))return (ReferenceEquals(CurrentEditorDebugSnapshot,snapshot)||IsRetainedEditorDebugSnapshot(snapshot))&&Owner();
                    return session.IsPublishedResultCurrent(scope,node.Id,snapshot)&&Owner();
                }
                var schema=snapshot?.Schema??session.GetNodeSchema(scope,node.Id);
                if(schema is null){plan.Diagnostics.Add(node.Name+"：输出声明尚未就绪。");continue;}
                IResultPublication? publication=null;
                if(!_nativeImagesSuppressed&&snapshot is {IsComplete:true}&&SnapshotCurrent())
                {publication=ToolResultViews.FromSnapshot(snapshot,SnapshotCurrent);plan.Own(publication);}
                using var values=publication?.Borrow();
                var fileName=node.ToolType=="ImageLoad"&&values?.Values?.Read("output.file-name") is {IsMissing:false,IsNull:false} name
                    ?name.GetScalar<string>():null;
                foreach(var port in schema.Outputs)
                {
                    var caption=schema.FindOutputDisplay(port.Id);
                    var label=caption?.DisplayName
                        ??(snapshot?.Fields.TryGetValue(port.Id,out var metadata)==true?metadata.DisplayName:port.Id);
                            Walk(port.Type,values?.Values?.Read(port.Id),Array.Empty<string>(),null,
                                node.Name+"."+WorkspaceImageLabel(node.ToolType,label));
                    void Walk(TypeRef type,DeclaredValueReader? value,string[] path,int? index,string name)
                    {
                        if(plan.Sources.Count>=4096)throw new InvalidOperationException("图像来源超过4096项，拒绝整批而非截断列表。");
                        if(type==TypeRef.ResourceOf(ResourceKind.Image))
                        {
                            var reference=new OutputReference(node.Id,port.Id,port.Type,path,
                                selection:index is {} item?new BindingSelection.AtConstant(item):null,scopeId:scope,storage:port.Storage);
                            plan.Sources.Add(new(name,reference,publication,SnapshotCurrent,node.Id!=selected.Id,
                                retainFormalImage&&value is {IsMissing:false,IsNull:false})
                                {
                                    FileName=port.Id is "output.image" or "output.gray-image"?fileName:null,
                                    IsAuxiliaryImage=caption?.IsAuxiliaryImage==true
                                });
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
            // 原件掩膜等辅助图不挪队尾，保持所属模块组内的端口顺序（用户实机截图：
            // "2 Blob分析1 · 输出掩膜"紧跟"二值化图像数据"）。
            string? primaryInputKey=null;
            if((editor?.PreparedDraft.InputSchema??session.GetNodeSchema(scope,selected.Id)) is {} inputSchema)
            {
                var configuration=editor?.PreparedDraft.Configuration??selected.Configuration;
                var inputKeys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var input in inputSchema.Inputs.Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)))
                {
                    var setting=configuration.Fields.Read(input.Id);
                    if(setting is not VariableSetting&&_activeRoiImageVariable is {} pendingVariable)
                        setting=pendingVariable;
                    var key=GetWorkspaceImageSourceKey(setting,scope);
                    if(key is null)continue;
                    if(inputKeys.Add(key)&&setting is VariableSetting variable&&!_nativeImagesSuppressed)
                    {
                        try
                        {
                            if(session.CaptureVariableImageForDisplay(variable.Reference) is {} capture)
                            {
                                plan.Own(capture.Image);
                                if(!Owner())throw new InvalidOperationException("建立图像来源期间工作区已被替换。");
                                plan.Sources.Add(new("变量 · "+capture.Name,key,capture.Image,()=>Owner()&&capture.Current()&&Owner()));
                            }
                            else plan.Diagnostics.Add("图像变量不可用："+variable.Reference.VariableId);
                        }
                        catch(Exception error) when(error is ArgumentException or InvalidOperationException or OverflowException)
                        {
                            if(!Owner())throw;
                            plan.Diagnostics.Add("图像变量不可用："+error.Message);
                        }
                    }
                    if(input.Id==inputSchema.WorkflowImageInputId)primaryInputKey=key;
                }
                plan.InputSources.AddRange(plan.Sources.Where(source=>inputKeys.Contains(source.Key)).Select(source=>source.CreateInputItem()));
            }
            var ownImage=plan.Sources.FirstOrDefault(source=>!source.IsUpstream&&!source.IsAuxiliaryImage
                &&(!plan.IsPrivateDebug||source.HasImageValue));
            plan.PrimaryInputKey=primaryInputKey;
            var primaryImage=primaryInputKey is null?null:plan.Sources.FirstOrDefault(source=>source.Key==primaryInputKey);
            plan.DefaultOutput=ownImage??primaryImage
                ??plan.Sources.FirstOrDefault();

            return plan;
        }
        catch{plan.Dispose();throw;}
    }
    private static string WorkspaceImageLabel(string toolType,string label)
        =>toolType switch
        {
            "ImageLoad"=>label switch
            {
                "图像"=>"输出图像",
                "灰度图像"=>"灰度图像数据",
                _=>label
            },
            "BlobAnalysis"=>label switch
            {
                "Blob图像数据"=>"显示Blob图像",
                "二值化图像数据"=>"显示二值化图像",
                _=>label
            },
            _=>label
        };
    internal (string Name, FrozenImageResource Image, Func<bool> Current)? CaptureVariableImageForRoi(string key)
    {
        try
        {
            var reference=ParameterSettingCanonical.DeserializeVariable(key);
            return DocumentManager.CurrentSession?.CaptureVariableImageForDisplay(reference);
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return null; }
    }
    private static string? GetWorkspaceImageSourceKey(ParameterSetting setting,string scope) => setting switch
    {
        ReferenceSetting {RebindingReason:null or ""} value when value.Reference.ScopeId is null || value.Reference.ScopeId==scope
            => ParameterSettingCanonical.Serialize(new OutputReference(value.Reference.NodeId,value.Reference.PortId,value.Reference.Type,
                value.Reference.MemberFieldPath,selection:value.Reference.Selection,scopeId:scope,storage:value.Reference.Storage)),
        VariableSetting {RebindingReason:null or ""} value when value.Reference.Type==TypeRef.ResourceOf(ResourceKind.Image)
            => ParameterSettingCanonical.Serialize(value.Reference),
        _ => null
    };
    private static bool ContainsImage(TypeRef type)=>type==TypeRef.ResourceOf(ResourceKind.Image)
        ||type.Shape==ValueShape.Sequence&&ContainsImage(type.Element!)
        ||type.Shape==ValueShape.Record&&type.Fields.Any(field=>ContainsImage(field.Type));
}
