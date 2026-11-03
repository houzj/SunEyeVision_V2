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
        var session=DocumentManager.CurrentSession;var node=SelectedNode;var tab=WorkflowTabViewModel.SelectedTab;
        var selection=_outputContractSelectionGeneration;
        return ()=>epoch==_resultPublicationEpoch && selection==_outputContractSelectionGeneration && ReferenceEquals(DocumentManager.CurrentSession,session)
            &&ReferenceEquals(SelectedNode,node)&&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)
            &&(node is null||OwnsSelectedDocumentNode(node));
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
        if(epoch==_resultPublicationEpoch)Cleanup(ClearResultHighlight);
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
            DeclaredResultPresenter.Present(publication,bindings,true,snapshot?.ExecutionTimeMs);
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
        var session=DocumentManager.CurrentSession;var node=SelectedNode;var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
        if(session is null||node is null||workflowId is null||snapshot.Identity.NodeId!=node.Id||snapshot.SourceWorkflowId!=workflowId)return;
        if(snapshot.Identity.DocumentSessionId!=session.ResultOwnerId&&!ReferenceEquals(CurrentEditorDebugSnapshot,snapshot))return;
        var epoch=RetireResultPublication();var owner=CaptureResultViewOwner(epoch);
        bool Current()
        {
            if(!owner()||sourceIsCurrent?.Invoke()==false)return false;
            return IsCurrentPresentedSnapshot(session,workflowId,node,snapshot)&&owner();
        }
        if(!Current())return;
        var publication=ToolResultViews.FromSnapshot(snapshot,Current);
        var transferred=false;
        try{var bindings=DeclaredBindings(publication.Schema,node.Id,workflowId);if(!Current())return;transferred=true;PresentResultPublication(publication,snapshot,nodeName,bindings);}
        finally{if(!transferred)publication.Dispose();}
    }
    private void RefreshFormalPublishedResult()
    {
        var session=DocumentManager.CurrentSession;var node=SelectedNode;var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
        if(session is null||node is null||workflowId is null){ClearResultDisplay();return;}
        if(!OwnsSelectedDocumentNode(node)){SelectedNode=null;return;}
        if(CurrentEditorDebugSnapshot is {} debug){PresentSnapshot(debug,node.Name);return;}
        using var reader=session.AcquirePublishedResult(workflowId,node.Id);
        if(reader is not null){PresentSnapshot(reader.Snapshot,node.Name);return;}
        var schema=session.GetNodeSchema(workflowId,node.Id);
        if(schema is null){ClearResultDisplay();return;}
        PresentDeclaration(schema,node.Name,schema.Outputs.ToDictionary(port=>port.Id,port=>schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id,StringComparer.Ordinal),DeclaredBindings(schema,node.Id,workflowId));
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
        DeclaredResultPresenter.Present(publication,bindings,true,_resultPublicationSnapshot?.ExecutionTimeMs);
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
