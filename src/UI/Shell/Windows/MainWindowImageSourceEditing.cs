using System.Text.Json;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private bool _acceptingImageSource;
    private void OnExplicitImageSelectionAccepted(object? sender,ImageSourceSelectionEventArgs selection)
    {
        if(sender is not ImageInputSource source||!ReferenceEquals(ActiveInputSource,source)
            ||source.EditVersion!=selection.Version||!ReferenceEquals(source.CurrentImage,selection.Image)
            ||source.CurrentIndex!=selection.Index||!CanEditRootWorkspace||DocumentManager.Session is not {} session
            ||WorkflowTabViewModel.SelectedTab is not {} tab)return;
        var roots=session.Document.Workflows.Where(flow=>!flow.IsSubroutine&&flow.Id==tab.RootWorkflowId).ToArray();
        if(roots.Length!=1)return;
        var nodes=roots[0].Nodes.Where(node=>node.Id==source.NodeId&&ReferenceEquals(node.InputSource,source)).ToArray();
        if(nodes.Length!=1||ImageSourcePolicy.Read(nodes[0])?.SourceKind!=SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local)return;
        _nativePreviewPath=selection.Image.FilePath;_nativeImagesSuppressed=false;
        ResultWorkspaceChanged?.Invoke();
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
            ImageSourcePolicy.Read(node)?.SourceKind==SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local&&
            index>=0&&index<source.Count&&ReferenceEquals(source.Images[index],image);
        if(source.EditVersion!=editVersion||!Current()||!source.CanSelectImage(index))return false;
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
            ReferenceEquals(ActiveInputSource,source)&&source.EditVersion==editVersion&&source.CurrentIndex==index&&source.CanSelectImage(index)&&
            ImageSourcePolicy.Read(node) is {SourceKind:SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Local} policy&&
            policy.CurrentIndex==index&&index>=0&&index<source.Count&&ReferenceEquals(source.Images[index],image);
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
            var policy=ImageSourcePolicy.Read(draft.Node)??throw new InvalidOperationException("图像源缺少正式Runtime设置。");
            policy.UpdateLocalInput(configuration);
            policy.ApplyTo(draft.Node);
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
    private void RefreshAcceptedImageSourceViews()
    {
        if(_acceptingImageSource||DocumentManager.Session is not {} session)return;
        var resolver=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(session.Document);
        foreach(var root in session.Document.Workflows.Where(flow=>!flow.IsSubroutine))
        foreach(var node in root.Nodes)
        {
            if(node.InputSource is not ImageInputSource source)continue;
            var formal=ImageSourcePolicy.Read(node)??throw new InvalidOperationException("图像源缺少正式Runtime设置。");
            RefreshAcceptedImageSourceView(node,source,formal,resolver);
        }
    }
    private void RefreshAcceptedImageSourceView(WorkflowNodeBase node,ImageInputSource source,ImageSourcePolicy formal,
        SunEyeVision.Workflow.Persistence.SolutionResourceResolver resolver)
    {
            var displayed=source.CapturePolicy();
            var local=new ImageSourcePolicy{NodeId=node.Id};local.UpdateLocalInput(formal);
            bool addressesMatch;
            try
            {
                addressesMatch=source.Images.Select(image=>image.FilePath).SequenceEqual(
                    formal.FilePaths.Select(reference=>resolver.ResolveAddress("ImageSourceSettings.FileReferences",reference)),StringComparer.OrdinalIgnoreCase);
            }
            catch(ArgumentException){addressesMatch=false;}
            catch(System.IO.InvalidDataException){addressesMatch=false;}
            if(JsonSerializer.Serialize(displayed)!=JsonSerializer.Serialize(local)||!addressesMatch)
                source.LoadFromPolicy(formal,resolver);
            BindLocalImageSourceEditor(node,source);
    }
}
