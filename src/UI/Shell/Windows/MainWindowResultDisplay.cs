using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    internal sealed record ResultDisplayState(ResultDisplayDescriptor Descriptor,ResultDisplayAppearance Appearance,long Revision,Func<bool> IsCurrent,ResultGeometrySnapshot? Geometry,Func<SunEyeVision.Plugin.Runtime.ResultDisplayTextLease> BorrowText);
    private sealed class DisplayPreview : IDisposable
    {
        private sealed class Token
        {
            internal required WorkflowNodeBase Draft;
            internal required long Binding;
            internal ResultDisplayAppearance? Formal,Appearance;
            internal ResultDisplayDescriptor? FormalDescriptor,Descriptor;
            internal bool Superseded,Refreshing;
        }
        internal required MainWindowViewModel Main;
        internal required object Document;
        internal required WorkflowNodeBase Node;
        internal required ProductionEditorContext Context;
        internal required RuntimeNodeConfigurationControl Control;
        private Token? _token;
        internal ResultDisplayAppearance? Appearance=>_token?.Appearance;
        internal ResultDisplayDescriptor? Descriptor=>_token?.Descriptor;
        internal bool Disposed;
        public void Dispose()
        {
            if(Disposed)return;Disposed=true;_token=null;
            Control.ResultDisplayEdited-=Refresh;Control.ResultDisplayBound-=Bound;
            Control.IsVisibleChanged-=VisibilityChanged;Control.Loaded-=Loaded;
            if(ReferenceEquals(Main._displayPreview,this)){Main._displayPreview=null;Main.NotifyResultDisplayChanged();}
        }
        private bool Owns()=>!Disposed&&ReferenceEquals(Main._displayPreview,this)&&ReferenceEquals(DocumentManager.Session,Document)
            &&ReferenceEquals(Main.SelectedNode,Node)&&Context.IsValid;
        private bool Current(Token token)
        {
            if(token.Superseded||token.Refreshing||!ReferenceEquals(_token,token)||!Owns()||!Control.IsLoaded||!Control.IsVisible
                ||!ReferenceEquals(Context.DraftNode,token.Draft)||!Control.IsResultDisplayBinding(token.Draft,token.Binding))return false;
            var configuration=Node.RuntimeConfiguration;
            var same=ReferenceEquals(configuration?.ResultDisplay,token.FormalDescriptor)&&Equals(configuration?.CaptureResultDisplay(),token.Formal);
            return same&&ReferenceEquals(_token,token)&&Owns()&&ReferenceEquals(configuration,Node.RuntimeConfiguration)
                &&ReferenceEquals(Context.DraftNode,token.Draft)&&Control.IsResultDisplayBinding(token.Draft,token.Binding);
        }
        internal bool Current()=>_token is {} token&&Current(token);
        internal void BeginBinding()
        {if(Control.TryGetResultDisplayBinding(out var draft,out var binding))Bound(draft,binding);}
        internal void Bound(WorkflowNodeBase draft,long binding)
        {
            if(!Owns()||!ReferenceEquals(Context.DraftNode,draft)||!Control.IsResultDisplayBinding(draft,binding))return;
            try
            {
                var configuration=Node.RuntimeConfiguration;
                var formal=configuration?.CaptureResultDisplay();
                if(!Owns()||!ReferenceEquals(configuration,Node.RuntimeConfiguration)||!ReferenceEquals(Context.DraftNode,draft)
                    ||!Control.IsResultDisplayBinding(draft,binding))return;
                _token=new Token{Draft=draft,Binding=binding,Formal=formal,FormalDescriptor=configuration?.ResultDisplay};
                Refresh();
            }
            catch(Exception error){if(_token is {} token&&ReferenceEquals(token.Draft,draft)&&token.Binding==binding)token.Superseded=true;try{Main.AddLog("显示草稿绑定失败："+error.Message);}catch{}}
            if(Owns())Main.NotifyResultDisplayChanged();
        }
        internal void ObservePublished()
        {
            var token=_token;if(token is null||token.Superseded||token.Refreshing||!Owns())return;
            var configuration=Node.RuntimeConfiguration;var formal=configuration?.CaptureResultDisplay();
            if(!Owns()||!ReferenceEquals(_token,token))return;
            if(ReferenceEquals(configuration?.ResultDisplay,token.FormalDescriptor)&&Equals(formal,token.Formal))return;
            token.Refreshing=true;
            try
            {
                // A successful safe rebind creates a new token only through Bound.
                Context.TryRefreshUnmodifiedPublishedDraft();
                if(ReferenceEquals(_token,token))token.Superseded=true;
            }
            catch{token.Superseded=true;throw;}
            finally{token.Refreshing=false;}
        }
        internal void Loaded(object sender,System.Windows.RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,Control))VisibilityChanged(sender,default);}
        internal void VisibilityChanged(object sender,System.Windows.DependencyPropertyChangedEventArgs args)
        {if(!Owns())return;Refresh();if(Owns())Main.NotifyResultDisplayChanged();}
        internal void Refresh()
        {
            var token=_token;if(token is null||!Current(token))return;
            if(!Control.TryCaptureResultDisplay(out var descriptor,out var appearance)||!Current(token))return;
            if(Equals(token.Appearance,appearance)&&ReferenceEquals(token.Descriptor,descriptor))return;
            token.Descriptor=descriptor;token.Appearance=appearance;Main.NotifyResultDisplayChanged();
        }
    }
    private DisplayPreview? _displayPreview;
    private long _resultDisplayRevision;
    internal event Action? ResultDisplayChanged;
    internal void NotifyResultDisplayChanged()
    {
        try{_displayPreview?.ObservePublished();}catch(Exception error){try{AddLog("显示草稿刷新失败："+error.Message);}catch{}}
        ++_resultDisplayRevision;
        foreach(var handler in ResultDisplayChanged?.GetInvocationList()??Array.Empty<Delegate>())
            try{((Action)handler)();}catch(Exception error){try{AddLog("结果显示刷新失败："+error.Message);}catch{}}
    }
    private IDisposable InstallResultDisplayPreview(object document,WorkflowNodeBase node,ProductionEditorContext context,RuntimeNodeConfigurationControl control)
    {
        var previous=_displayPreview;var preview=new DisplayPreview{Main=this,Document=document,Node=node,Context=context,Control=control};
        _displayPreview=preview;previous?.Dispose();
        if(ReferenceEquals(_displayPreview,preview)){control.ResultDisplayEdited+=preview.Refresh;control.ResultDisplayBound+=preview.Bound;control.IsVisibleChanged+=preview.VisibilityChanged;control.Loaded+=preview.Loaded;preview.BeginBinding();}
        return preview;
    }
    internal ResultDisplayState? CaptureCurrentResultDisplay(IResultPublication expectedPublication)
    {
        var session=DocumentManager.Session;var node=SelectedNode;var configuration=node?.RuntimeConfiguration;
        var snapshot=_resultPublicationSnapshot;var workflowId=WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId;
        bool PublicationOwner()
        {
            if(session is null||node is null||snapshot is null||string.IsNullOrEmpty(workflowId)||!ReferenceEquals(_resultPublication,expectedPublication)
                ||!ReferenceEquals(_resultPublicationSnapshot,snapshot)||expectedPublication.Stamp.Invocation!=snapshot.Identity
                ||snapshot.Identity.NodeId!=node.Id||snapshot.SourceWorkflowId!=workflowId||!ReferenceEquals(session,DocumentManager.Session)
                ||!ReferenceEquals(node,SelectedNode)||workflowId!=WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId)return false;
            var documentOwner=snapshot.Identity.DocumentSessionId==session.Document.ExecutionOwner.Id;
            if(!documentOwner&&(_productionEditors is null||!_productionEditors.Any(editor=>editor.IsValid&&editor.DebugOwner.Id==snapshot.Identity.DocumentSessionId
                &&editor.Identity.WorkflowId==workflowId&&editor.Identity.NodeId==node.Id)))return false;
            return session.Document.Workflows.Count(flow=>flow.Id==workflowId&&flow.Nodes.Count(current=>current.Id==node.Id)==1
                &&flow.Nodes.Any(current=>ReferenceEquals(current,node)))==1;
        }
        if(!PublicationOwner())return null;
        if(session is null||node is null||configuration?.ResultDisplay is not {} descriptor
            ||!session.Document.Workflows.Any(flow=>flow.Nodes.Any(current=>ReferenceEquals(current,node))))return null;
        var preview=_displayPreview;var revision=_resultDisplayRevision;
        var usingPreview=preview?.Current()==true&&preview.Appearance is not null&&preview.Descriptor is not null;
        var appearance=usingPreview?preview!.Appearance:configuration.CaptureResultDisplay();
        if(appearance is null||!ReferenceEquals(session,DocumentManager.Session)||!ReferenceEquals(node,SelectedNode))return null;
        if(usingPreview)descriptor=preview!.Descriptor!;
        bool Current()
        {
            if(!PublicationOwner())return false;
            if(revision!=_resultDisplayRevision||!ReferenceEquals(session,DocumentManager.Session)||!ReferenceEquals(node,SelectedNode)
                ||!session.Document.Workflows.Any(flow=>flow.Nodes.Any(current=>ReferenceEquals(current,node))))return false;
            if(usingPreview)return preview!.Current()&&ReferenceEquals(_displayPreview,preview)&&Equals(preview.Appearance,appearance)&&PublicationOwner();
            var current=node.RuntimeConfiguration;
            return current is not null&&ReferenceEquals(current.ResultDisplay,descriptor)&&Equals(current.CaptureResultDisplay(),appearance)
                &&revision==_resultDisplayRevision&&ReferenceEquals(session,DocumentManager.Session)&&ReferenceEquals(node,SelectedNode)&&PublicationOwner();
        }
        return Current()?new(descriptor,appearance,revision,Current,(snapshot!.Output as IToolResultGeometryOutput)?.ResultGeometry,()=>
        {if(!Current())throw new InvalidOperationException("文字显示来源已失效。");var lease=new SunEyeVision.Plugin.Runtime.ResultDisplayTextLease(snapshot,appearance.Text);try{if(!Current())throw new InvalidOperationException("文字显示来源在捕获期间已变化。");return lease;}catch(Exception failure){try{lease.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}}):null;
    }
}
