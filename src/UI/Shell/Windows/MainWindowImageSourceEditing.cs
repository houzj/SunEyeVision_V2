using System.Text.Json;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private bool _acceptingImageSource;
    internal SolutionSession? CaptureImageSourceSession(WorkflowNodeBase node)
    {
        var session=DocumentManager.Session;
        return session?.Document.Workflows.Any(flow=>!flow.IsSubroutine&&flow.Nodes.Any(item=>ReferenceEquals(item,node)))==true
            ?session:null;
    }
    internal bool TrySelectPreviewImage(SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel tab,
        ImageInputSource source,ImageInfo image,int index,long editVersion)
    {
        var session=DocumentManager.Session;
        var root=session?.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Id==tab.RootWorkflowId);
        var node=root?.Nodes.SingleOrDefault(item=>ReferenceEquals(item.InputSource,source));
        bool Current()=>session is not null&&root is not null&&node is not null&&
            ReferenceEquals(DocumentManager.Session,session)&&session.Document.Workflows.Contains(root)&&root.Nodes.Contains(node)&&
            ReferenceEquals(node.InputSource,source)&&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)&&
            ReferenceEquals(ActiveInputSource,source)&&CanEditRootWorkspace&&!source.IsReadOnly&&
            (node.ImageSourceConfig is null||node.ImageSourceConfig.SourceKind==SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local)&&
            index>=0&&index<source.Count&&ReferenceEquals(source.Images[index],image);
        if(source.EditVersion!=editVersion||!Current())return false;
        source.SelectImage(index);
        // Acceptance notifications may switch documents, roots, sources or execution state.
        return Current();
    }
    internal System.Threading.Tasks.Task RunPreviewImageAsync(SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel tab,
        ImageInputSource source,ImageInfo image,int index,long editVersion)
    {
        var session=DocumentManager.Session;
        var root=session?.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Id==tab.RootWorkflowId);
        var node=root?.Nodes.SingleOrDefault(item=>ReferenceEquals(item.InputSource,source));
        bool Current()=>session is not null&&root is not null&&node is not null&&
            ReferenceEquals(DocumentManager.Session,session)&&session.Document.Workflows.Contains(root)&&root.Nodes.Contains(node)&&
            ReferenceEquals(node.InputSource,source)&&ReferenceEquals(WorkflowTabViewModel.SelectedTab,tab)&&
            ReferenceEquals(ActiveInputSource,source)&&source.EditVersion==editVersion&&source.CurrentIndex==index&&
            node.ImageSourceConfig?.SourceKind==SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local&&
            node.ImageSourceConfig?.CurrentIndex==index&&index>=0&&index<source.Count&&ReferenceEquals(source.Images[index],image);
        return Current()?ExecuteRunWorkflow(advanceLocalImage:false,expectedTab:tab,isCurrent:Current):System.Threading.Tasks.Task.CompletedTask;
    }
    private void BindLocalImageSourceEditor(WorkflowNodeBase node,ImageInputSource source)
    {
        var session=DocumentManager.Session??throw new InvalidOperationException("图像源缺少当前方案编辑会话。");
        var root=session.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Nodes.Any(item=>ReferenceEquals(item,node)));
        source.AttachFormalOwner();
        // Root source editing only; Group source editing requires its deferred ownership contract.
        source.ConfigurationWriter=proposal=>
        {
            var configuration=proposal.Configuration;
            if(_acceptingImageSource||root is null||!CanEditRootWorkspace||
                !ReferenceEquals(DocumentManager.Session,session)||
                !session.Document.Workflows.Contains(root)||!root.Nodes.Any(item=>ReferenceEquals(item,node))||
                !ReferenceEquals(node.InputSource,source)||configuration.NodeId!=node.Id)
                throw new InvalidOperationException("图像源所属方案、流程或编辑状态已失效，修改未接纳。");
            var host=GetProductionEditorHost(session);
            var draft=host.BeginEdit(root.Id,node.Id);
            draft.Node.ImageSourceConfig??=new(){NodeId=node.Id};
            draft.Node.ImageSourceConfig.UpdateLocalInput(configuration);
            draft.Node.ImageSourceConfig.NodeId=node.Id;
            _acceptingImageSource=true;
            try
            {
                var publication=host.Commit(draft);
                // An explicit preview choice can equal the persisted start while differing
                // from the last runtime cursor. Reset only this source, never other sources.
                if(proposal.SelectStart)_executionManager.ResetLocalImageCursor(root.Id,node.Id);
                // Acceptance is final; observer failure cannot make the source roll back its accepted state.
                try { RefreshProductionPublication(session,publication); }
                catch(Exception error) { try { _logger?.LogError("图像源已接纳，显示刷新失败", "MainWindowViewModel",error); } catch { } }
            }
            finally { _acceptingImageSource=false; }
        };
    }
    internal bool CanEditImageSourceSettings(SolutionSession? session, WorkflowNodeBase node,
        SunEyeVision.UI.Features.WorkflowUI.ImageSourceSettingsControl owner)
    {
        if (session is null || !ReferenceEquals(DocumentManager.Session, session) ||
            !CanEditRootWorkspaceState || !owner.IsLoaded ||
            !session.Document.Workflows.Any(flow => !flow.IsSubroutine && flow.Nodes.Any(item => ReferenceEquals(item, node))))
            return false;
        if (!IsDebugWindowOpen)
            return ReferenceEquals(SelectedNodeDataAnalysisControl, owner);
        // An immediate source-settings page may edit in its own window only. An
        // unrelated debug/parameter window never grants permission to this page.
        return _auxiliaryParameterWindows.Count == 0 && _openDebugWindow is { IsLoaded: true } window &&
            ReferenceEquals(System.Windows.Window.GetWindow(owner), window);
    }
    internal bool TryEditImageSourceSettings(SolutionSession? session, WorkflowNodeBase node,
        SunEyeVision.UI.Features.WorkflowUI.ImageSourceSettingsControl owner,
        Action<ImageSourceConfig> change, out string error)
    {
        try
        {
            if (_acceptingImageSource || !CanEditImageSourceSettings(session, node, owner))
                throw new InvalidOperationException("图像源设置所属方案、节点或编辑状态已失效，修改未接纳。");
            var root = session!.Document.Workflows.Single(flow => !flow.IsSubroutine && flow.Nodes.Any(item => ReferenceEquals(item, node)));
            var host = GetProductionEditorHost(session);
            var draft = host.BeginEdit(root.Id, node.Id);
            draft.Node.ImageSourceConfig ??= new() { NodeId = node.Id };
            change(draft.Node.ImageSourceConfig);
            _acceptingImageSource = true;
            try
            {
                var publication = host.Commit(draft);
                try { RefreshProductionPublication(session, publication); }
                catch (Exception refreshError) { try { _logger?.LogError("图像源设置已接纳，显示刷新失败", "MainWindowViewModel", refreshError); } catch { } }
            }
            finally { _acceptingImageSource = false; }
        }
        catch (Exception failure) { error = failure.Message; return false; }
        // Acceptance already succeeded. A view-refresh failure must not be reported
        // as a rejected edit or trigger a retry of the same command.
        try { RefreshAcceptedImageSourceViews(); ForceRefreshImagePreview(); }
        catch (Exception failure) { try { _logger?.LogError("图像源设置已接纳，预览刷新失败", "MainWindowViewModel", failure); } catch { } }
        error = string.Empty;
        return true;
    }
    private void RefreshAcceptedImageSourceViews()
    {
        if(_acceptingImageSource||DocumentManager.Session is not {} session)return;
        var resolver=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document);
        foreach(var root in session.Document.Workflows.Where(flow=>!flow.IsSubroutine))
        foreach(var node in root.Nodes)
        {
            if(node.InputSource is not ImageInputSource source)continue;
            var formal=node.ImageSourceConfig??new ImageSourceConfig{NodeId=node.Id};
            var displayed=source.ToConfig();
            var local=new ImageSourceConfig{NodeId=node.Id};local.UpdateLocalInput(formal);
            bool addressesMatch;
            try
            {
                addressesMatch=source.Images.Select(image=>image.FilePath).SequenceEqual(
                    formal.FilePaths.Select(reference=>resolver.ResolveAddress("ImageSourceConfig.FilePaths",reference)),StringComparer.OrdinalIgnoreCase);
            }
            catch(ArgumentException){addressesMatch=false;}
            catch(System.IO.InvalidDataException){addressesMatch=false;}
            if(JsonSerializer.Serialize(displayed)!=JsonSerializer.Serialize(local)||!addressesMatch)
                source.LoadFromConfig(formal,resolver);
            BindLocalImageSourceEditor(node,source);
        }
    }
}
