using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal event Action? ResultWorkspaceChanged;
    internal event Action<ResultSelection?>? ResultWorkspaceSelectionChanged;
    internal event Action? ResultWorkspaceGraphicsCleared;
    internal event Action<SunEyeVision.UI.Shared.ImageInputSource,SourceDisplayPreservationScope>? SourceDisplayPreservationPreparing;
    internal event Action<string>? ResultWorkspaceRootSettled;
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
        var session=DocumentManager.Session;var selected=SelectedNode;
        var tab=WorkflowTabViewModel?.SelectedTab;var context=CurrentWorkflowContext;
        var historical=_declaredHistoricalResult;
        var scope=tab?.CurrentWorkflowId;
        if(session is null||selected is null||scope is null)return plan;
        var flows=session.Document.Workflows.Where(flow=>flow.Id==scope).ToArray();
        if(flows.Length!=1||!flows[0].Nodes.Contains(selected))return plan;
        var flow=flows[0];
        bool OwnsUniqueNode(WorkflowNodeBase node)
        {
            var count=0;var owns=false;
            foreach(var item in flow.Nodes)
                if(item.Id==node.Id){count++;owns=ReferenceEquals(item,node);}
            return count==1&&owns;
        }
        bool Owner()
        {
            if(plan.IsDisposed||!consumerCurrent()||!ReferenceEquals(DocumentManager.Session,session)
                ||!ReferenceEquals(SelectedNode,selected)||!ReferenceEquals(WorkflowTabViewModel?.SelectedTab,tab)
                ||tab.CurrentWorkflowId!=scope||!ReferenceEquals(CurrentWorkflowContext,context)
                ||_declaredHistoricalResult!=historical)return false;
            var count=0;var owns=false;
            foreach(var item in session.Document.Workflows)
                if(item.Id==scope){count++;owns=ReferenceEquals(item,flow);}
            return count==1&&owns&&OwnsUniqueNode(selected);
        }
        // Only the selected node and its actual dependency ancestors are image candidates.
        var dependencies=WorkflowExecutionDependencyGraph.Compile(flow);
        var ids=new HashSet<string>(StringComparer.Ordinal){selected.Id};var queue=new Queue<string>();queue.Enqueue(selected.Id);
        while(queue.TryDequeue(out var id))
            foreach(var predecessor in dependencies.GetPredecessors(id))
                if(ids.Add(predecessor))queue.Enqueue(predecessor);
        using var captured=context?.CaptureNodeResults();
        try
        {
            foreach(var node in flow.Nodes.Where(node=>ids.Contains(node.Id)).OrderBy(node=>node.Id==selected.Id?0:1))
            {
                if(historical&&node.Id!=selected.Id)continue;
                DeclaredOutputSnapshot? snapshot=null;
                if(node.Id==selected.Id&&_resultPublicationSnapshot is {} shown&&_resultPublication?.IsCurrent()==true)
                    snapshot=shown;
                else if(!historical&&captured?.Results.TryGetValue(node.Id,out var execution)==true)snapshot=execution.Result;
                bool SnapshotCurrent()
                {
                    if(!Owner()||!OwnsUniqueNode(node)||snapshot is null||snapshot.SourceWorkflowId!=scope
                        ||snapshot.Identity.NodeId!=node.Id)return false;
                    var owner=snapshot.Identity.DocumentSessionId;
                    if(owner!=session.Document.ExecutionOwner.Id&&_productionEditors?.Any(editor=>editor.DebugOwner.Id==owner
                        &&editor.IsValid&&editor.Identity.WorkflowId==scope&&editor.Identity.NodeId==node.Id)!=true)return false;
                    if(ReferenceEquals(_resultPublicationSnapshot,snapshot)&&_resultPublication?.IsCurrent()==true)return Owner();
                    var live=context?.GetNodeResult(node.Id)?.Result;
                    return !historical&&ReferenceEquals(live,snapshot)
                        &&live?.ExecutionIdentity?.IsCurrent==true&&Owner();
                }
                ToolSchema schema;
                try
                {
                    schema=snapshot?.Schema??node.RuntimeConfiguration?.Schema??new ToolSchema(Array.Empty<Port>(),
                        (node.Parameters is IOutputContractProvider provider?provider.GetOutputContract():Array.Empty<OutputVariable>())
                        .Where(output=>output.Kind==OutputVariableKind.Data).Select(BindingClrAdapter.DescribeOutput));
                }
                catch(Exception error)when(error is ArgumentException or InvalidOperationException)
                {plan.Diagnostics.Add(node.Name+"：输出声明尚未就绪，"+error.Message);continue;}
                IResultPublication? publication=null;
                if(!_nativeImagesSuppressed&&snapshot is {IsComplete:true}&&SnapshotCurrent())
                {
                    if(ReferenceEquals(_resultPublicationSnapshot,snapshot)&&_resultPublication?.IsCurrent()==true)
                        publication=_resultPublication;
                    else
                    {publication=ToolResultViews.FromSnapshot(snapshot,SnapshotCurrent);plan.Own(publication);}
                }
                using var values=publication?.Borrow();
                var projection=node.RuntimeConfiguration?.ResultProjection;
                if(projection is not null && (new ToolSchema(Array.Empty<Port>(),node.RuntimeConfiguration!.Schema.Outputs).Fingerprint!=new ToolSchema(Array.Empty<Port>(),schema.Outputs).Fingerprint
                    ||projection.Validate(schema).Count!=0))projection=null;
                string? fileName=null;
                if(ImageSourcePolicy.Read(node) is not null && values?.Values is {} frozen
                    && schema.Outputs.Any(port=>port.Id=="output.file"&&port.Type==TypeRef.ScalarOf(ScalarKind.Text)))
                {
                    var file=frozen.Get<string?>("output.file");
                    if(!string.IsNullOrWhiteSpace(file))fileName=System.IO.Path.GetFileName(file);
                }
                foreach(var port in schema.Outputs)
                {
                    var label=snapshot?.Fields.TryGetValue(port.Id,out var metadata)==true?metadata.DisplayName:port.Id;
                    var declaredNames=projection?.Columns.Where(column=>column.Value.PortId==port.Id
                        &&column.Value.MemberFieldPath.Count==0&&!string.IsNullOrWhiteSpace(column.DisplayName))
                        .Select(column=>column.DisplayName).Distinct(StringComparer.Ordinal).ToArray();
                    if(declaredNames?.Length==1)label=declaredNames[0];
                    Walk(port.Type,values?.Values?.Read(port.Id),Array.Empty<string>(),null,node.Name+" · "+label);
                    void Walk(TypeRef type,DeclaredValueReader? value,string[] path,int? index,string name)
                    {
                        if(plan.Sources.Count>=4096)throw new InvalidOperationException("图像来源超过4096项，拒绝整批而非截断列表。");
                        if(type==TypeRef.ResourceOf(ResourceKind.Image))
                        {
                            var reference=new OutputReference(node.Id,port.Id,port.Type,path,
                                selection:index is {} item?new BindingSelection.AtConstant(item):null,scopeId:scope,storage:port.Storage);
                            var available=DisplayImageSources.FirstOrDefault(source=>source.SourceNodeId==node.Id
                                &&(snapshot?.Fields.TryGetValue(port.Id,out var field)==true
                                    ?source.SourceFieldId is {} fieldId?field.FieldId==fieldId:field.Name==source.PropertyName
                                    :BindingClrAdapter.OutputPortId(new OutputVariable{Name=source.PropertyName,FieldId=source.SourceFieldId})==port.Id));
                            plan.Sources.Add(new(name,reference,publication,SnapshotCurrent,DisplayImageSources.IndexOf(available!),node.Id!=selected.Id)
                                {FileName=port.Id is "output.image" or "output.gray-image"?fileName:null});
                            return;
                        }
                        if(type.Shape==ValueShape.Record)
                            foreach(var field in type.Fields)
                                Walk(field.Type,value is {IsMissing:false,IsNull:false}?value.GetField(field.Id):null,
                                    path.Append(field.Id).ToArray(),index,name+" / "+field.Id);
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
            if(!historical&&selected.RuntimeConfiguration is {} inputConfiguration)
            {
                var inputKeys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var input in inputConfiguration.Schema.Inputs.Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)))
                {
                    if(inputConfiguration.Fields.Read(input.Id) is not ReferenceSetting setting)continue;
                    var reference=setting.Reference;
                    if(!string.IsNullOrEmpty(setting.RebindingReason)||reference.ScopeId is {} explicitScope&&explicitScope!=scope)continue;
                    var scoped=new OutputReference(reference.NodeId,reference.PortId,reference.Type,reference.MemberFieldPath,
                        selection:reference.Selection,scopeId:scope,storage:reference.Storage);
                    var key=BindingReferenceSerializer.Serialize(scoped);
                    var source=plan.Sources.FirstOrDefault(item=>item.Key==key);
                    if(source is not null&&inputKeys.Add(key))plan.InputSources.Add(source.CreateInputItem());
                }
            }
            if(historical)plan.Diagnostics.Add("历史结果尚无冻结的整次运行来源上下文，未混用当前上游图像。");
            return plan;
        }
        catch{plan.Dispose();throw;}
    }
    private static bool ContainsImage(TypeRef type)=>type==TypeRef.ResourceOf(ResourceKind.Image)
        ||type.Shape==ValueShape.Sequence&&ContainsImage(type.Element!)
        ||type.Shape==ValueShape.Record&&type.Fields.Any(field=>ContainsImage(field.Type));
}
