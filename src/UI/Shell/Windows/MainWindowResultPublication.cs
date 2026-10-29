using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Monitoring;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private IResultPublication? _resultPublication;
    private DeclaredOutputSnapshot? _resultPublicationSnapshot;
    private long _resultPublicationEpoch;
    private IReadOnlyDictionary<string,ResultBindingInfo>? _resultPublicationBindings;
    internal IResultPublication? CurrentResultPublication=>_resultPublication;

    private Func<bool> CaptureResultViewOwner(long epoch)
    {
        var session=DocumentManager.Session;var node=SelectedNode;
        var tab=WorkflowTabViewModel?.SelectedTab;var context=CurrentWorkflowContext;
        var selection=_outputContractSelectionGeneration;
        return ()=>epoch==_resultPublicationEpoch&&selection==_outputContractSelectionGeneration
            &&ReferenceEquals(DocumentManager.Session,session)&&ReferenceEquals(SelectedNode,node)
            &&ReferenceEquals(WorkflowTabViewModel?.SelectedTab,tab)&&ReferenceEquals(CurrentWorkflowContext,context);
    }
    private long RetireResultPublication()
    {
        var epoch=++_resultPublicationEpoch;
        var publication=_resultPublication;_resultPublication=null;_resultPublicationSnapshot=null;_resultPublicationBindings=null;
        _declaredResultActive=false;
        var failures=new List<Exception>();
        void Cleanup(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        Cleanup(()=>publication?.Dispose());
        if(epoch==_resultPublicationEpoch)Cleanup(()=>_declaredResultPresenter?.Clear());
        if(epoch==_resultPublicationEpoch)Cleanup(()=>_activeResultView?.Clear());
        if(epoch==_resultPublicationEpoch)Cleanup(()=>ModuleResultTree.Clear());
        if(epoch==_resultPublicationEpoch)Cleanup(()=>_nodeResultManager?.ClearHighlight());
        if(epoch==_resultPublicationEpoch)Cleanup(ClearNativeImagePresentation);
        if(failures.Count>0)throw new AggregateException("结果视图清理失败，所有可执行清理均已尝试。",failures);
        return epoch;
    }
    private void PresentResultPublication(IResultPublication publication,DeclaredOutputSnapshot? snapshot,
        string? nodeName,IReadOnlyDictionary<string,ResultBindingInfo>? bindings)
    {
        var epoch=_resultPublicationEpoch;
        bool Owns()=>epoch==_resultPublicationEpoch&&ReferenceEquals(_resultPublication,publication);
        bool Current()=>Owns()&&publication.IsCurrent()&&Owns();
        bool EnsureCurrent(){if(Current())return true;if(Owns())RetireResultPublication();return false;}
        try
        {
            if(!publication.IsCurrent()){publication.Dispose();return;}
            _resultPublication=publication;_resultPublicationSnapshot=snapshot;_resultPublicationBindings=bindings;_declaredResultActive=true;
            DeclaredResultPresenter.Present(publication,bindings);
            if(!EnsureCurrent())return;
            if(FlatResultRows.FirstOrDefault() is {} root)root.DisplayName=nodeName??"结果";
            if(!EnsureCurrent())return;
            _activeResultView?.Present(publication);
            if(EnsureCurrent())RefreshNativeResultImages();
            EnsureCurrent();
        }
        catch
        {
            if(ReferenceEquals(_resultPublication,publication))RetireResultPublication();
            else publication.Dispose();
            throw;
        }
    }
    private void PresentSnapshot(DeclaredOutputSnapshot snapshot,string? nodeName,Func<bool>? sourceIsCurrent=null)
    {
        var session=DocumentManager.Session;
        var node=SelectedNode;
        var workflowId=WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId;
        bool ExecutionOwnerMatches()
        {
            if(session is null)return false;
            if(snapshot.Identity.DocumentSessionId==session.Document.ExecutionOwner.Id)return true;
            if(_productionEditors is null)return false;
            foreach(var editor in _productionEditors)
                if(editor.DebugOwner.Id==snapshot.Identity.DocumentSessionId&&editor.IsValid
                    &&editor.Identity.WorkflowId==workflowId&&editor.Identity.NodeId==snapshot.Identity.NodeId)return true;
            return false;
        }
        bool SourceMatches()
        {
            if(session is null||!ReferenceEquals(DocumentManager.Session,session)
                ||!ReferenceEquals(SelectedNode,node)||node?.Id!=snapshot.Identity.NodeId||!ExecutionOwnerMatches()
                ||string.IsNullOrEmpty(workflowId)||snapshot.SourceWorkflowId!=workflowId
                ||WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId!=workflowId)return false;
            // This proof runs for each lazy page entry. Re-check membership without
            // allocating nested captured predicates or caching an earlier authorization.
            var flows=session.Document.Workflows;var scopeCount=0;var ownsNode=false;
            for(var i=0;i<flows.Count;i++)
            {
                var flow=flows[i];if(flow.Id!=workflowId)continue;
                scopeCount++;var localCount=0;
                for(var j=0;j<flow.Nodes.Count;j++)
                    if(flow.Nodes[j].Id==snapshot.Identity.NodeId)
                    {localCount++;ownsNode=ReferenceEquals(flow.Nodes[j],node);}
                if(localCount!=1)ownsNode=false;
            }
            return scopeCount==1&&ownsNode;
        }
        if(!SourceMatches())
        {
            var current=_resultPublication;
            if(current is not null&&!current.IsCurrent()&&ReferenceEquals(_resultPublication,current))
            {
                var retired=RetireResultPublication();
                if(retired==_resultPublicationEpoch&&_resultPublication is null)RefreshNativeResultImages();
            }
            return;
        }
        if(ReferenceEquals(_resultPublicationSnapshot,snapshot)&&_resultPublication?.IsCurrent()==true)
        {
            PresentResultPublication(_resultPublication,snapshot,nodeName,_resultPublicationBindings);
            return;
        }
        var epoch=RetireResultPublication();
        var viewOwner=CaptureResultViewOwner(epoch);
        var historical=_declaredHistoricalResult;
        bool SameLiveResult()=>historical||snapshot.Identity.DocumentSessionId!=session!.Document.ExecutionOwner.Id
            ||ReferenceEquals(CurrentWorkflowContext?.GetNodeResult(snapshot.Identity.NodeId)?.Result,snapshot);
        bool Owner()=>viewOwner()&&SourceMatches()&&_declaredHistoricalResult==historical&&SameLiveResult()
            &&(sourceIsCurrent?.Invoke()!=false)&&viewOwner()&&SourceMatches()&&SameLiveResult();
        if(!Owner())return;
        var publication=ToolResultViews.FromSnapshot(snapshot,Owner);
        var handedToPresenter=false;
        try
        {
            var bindings=DeclaredBindings(publication.Schema,snapshot.Identity.NodeId,snapshot.SourceWorkflowId);
            if(!Owner())return;
            // From this point the presenter owns every acceptance/rejection/exception path.
            handedToPresenter=true;
            PresentResultPublication(publication,snapshot,nodeName,bindings);
        }
        finally{if(!handedToPresenter)publication.Dispose();}
    }
    private void RefreshCurrentResultBindings()
    {
        var publication=_resultPublication;var epoch=_resultPublicationEpoch;
        if(publication is null)return;
        bool Owns()=>epoch==_resultPublicationEpoch&&ReferenceEquals(_resultPublication,publication);
        bool EnsureCurrent(){if(Owns()&&publication.IsCurrent()&&Owns())return true;if(Owns())RetireResultPublication();return false;}
        if(!EnsureCurrent())return;
        var nodeId=publication.Stamp.Invocation?.NodeId??SelectedNode?.Id;
        var bindings=DeclaredBindings(publication.Schema,nodeId,_resultPublicationSnapshot?.SourceWorkflowId);
        if(!EnsureCurrent())return;
        _resultPublicationBindings=bindings;
        DeclaredResultPresenter.Present(publication,bindings);
        EnsureCurrent();
    }
    private void PresentDeclaration(ToolSchema schema,string? nodeName,
        IReadOnlyDictionary<string,string> names,IReadOnlyDictionary<string,ResultBindingInfo>? bindings,
        IReadOnlySet<string>? displayedPorts=null)
    {
        var epoch=RetireResultPublication();var owner=CaptureResultViewOwner(epoch);
        if(!owner())return;
        PresentResultPublication(ToolResultViews.Declaration(schema,owner,names,displayedPorts),null,nodeName,bindings);
    }
}
