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
        if(failures.Count>0)throw new AggregateException("结果视图清理失败，所有可执行清理均已尝试。",failures);
        return epoch;
    }
    private void PresentResultPublication(IResultPublication publication,DeclaredOutputSnapshot? snapshot,
        string? nodeName,IReadOnlyDictionary<string,ResultBindingInfo>? bindings)
    {
        if(!publication.IsCurrent()){publication.Dispose();return;}
        _resultPublication=publication;_resultPublicationSnapshot=snapshot;_resultPublicationBindings=bindings;_declaredResultActive=true;
        bool Current()=>ReferenceEquals(_resultPublication,publication)&&publication.IsCurrent();
        try
        {
            DeclaredResultPresenter.Present(publication,bindings);
            if(!Current())return;
            if(FlatResultRows.FirstOrDefault() is {} root)root.DisplayName=nodeName??"结果";
            if(!Current())return;
            _activeResultView?.Present(publication);
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
        if(ReferenceEquals(_resultPublicationSnapshot,snapshot)&&_resultPublication?.IsCurrent()==true)
        {
            PresentResultPublication(_resultPublication,snapshot,nodeName,_resultPublicationBindings);
            return;
        }
        var epoch=RetireResultPublication();
        var viewOwner=CaptureResultViewOwner(epoch);
        bool Owner()=>viewOwner()&&(sourceIsCurrent?.Invoke()!=false)&&viewOwner();
        if(!Owner())return;
        var publication=ToolResultViews.FromSnapshot(snapshot,Owner);
        PresentResultPublication(publication,snapshot,nodeName,DeclaredBindings(publication.Schema,snapshot.Identity.NodeId));
    }
    private void RefreshCurrentResultBindings()
    {
        var publication=_resultPublication;var epoch=_resultPublicationEpoch;
        if(publication is null||!publication.IsCurrent())return;
        var nodeId=publication.Stamp.Invocation?.NodeId??SelectedNode?.Id;
        var bindings=DeclaredBindings(publication.Schema,nodeId);
        if(epoch!=_resultPublicationEpoch||!ReferenceEquals(_resultPublication,publication)||!publication.IsCurrent())return;
        _resultPublicationBindings=bindings;
        DeclaredResultPresenter.Present(publication,bindings);
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
