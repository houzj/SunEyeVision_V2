using System.Windows;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;

namespace SunEyeVision.Plugin.Wpf.Presenters.References;

/// <summary>One field composition borrows a root window owner; disposing fields never disposes a shared root.</summary>
internal sealed class ReferenceExpansionCoordinator : IDisposable
{
    private readonly FrameworkElement _root;
    private readonly ReferenceWindowOwner _owner;
    private readonly bool _ownsOwner;
    private ReferenceExpansionRequest? _request;
    private VariableLease? _variable;
    private bool _disposed;
    private long _generation;
    private sealed class VariableLease(VariableSelectionDialog window,VariableSelectionRequest request,Action close)
    {
        internal readonly VariableSelectionDialog Window=window;
        internal readonly VariableSelectionRequest Request=request;
        internal readonly Action Close=close;
        internal Action? Invalidated;
        internal EventHandler? Closed;
        internal bool Retired;
    }
    internal ReferenceExpansionCoordinator(FrameworkElement root,ReferenceWindowOwner? owner=null)
    {
        _root=root;_ownsOwner=owner is null;
        _owner=owner??new ReferenceWindowOwner(root,()=>!_disposed,new(),false);
        root.Unloaded+=OnUnloaded;root.IsVisibleChanged+=OnVisibility;
    }
    internal ReferenceExpansionPreference Preference()=>_owner.Preference;
    internal bool IsExpanded()=>_owner.IsExpanded;
    private bool CanInteract => !_disposed && _root.IsLoaded && _root.IsVisible && _root.IsEnabled && _owner.IsCurrent;
    internal static void ClearSelectionFor(ReferenceWindowOwner? owner)=>owner?.ClearSelection();
    internal void Open(ReferenceExpansionRequest request)
    {
        if(!CanInteract||!request.IsValid){request.Dispose();return;}
        _request=request;_owner.Open(request);
    }
    internal void OpenVariable(VariableSelectionRequest request)
    {
        _root.Dispatcher.VerifyAccess();
        if(!CanInteract||!request.IsValid||Window.GetWindow(_root) is not {} windowOwner)
        {request.Dispose();return;}
        var generation=++_generation;VariableLease? lease=null;
        Action close=()=>{if(lease is not null)RetireVariable(lease,true,false);else request.Dispose();};
        try
        {
            if(!_owner.InstallModal(close)){request.Dispose();return;}
            if(!CanInteract||!request.IsValid||generation!=_generation||!ReferenceEquals(Window.GetWindow(_root),windowOwner))
            {_owner.ReleaseModal(close);request.Dispose();return;}
            var dialog=new VariableSelectionDialog(request.Items){Owner=windowOwner,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            lease=new(dialog,request,close);var captured=lease;
            lease.Invalidated=()=>RetireVariable(captured,true,false);
            lease.Closed=(_,_)=>RetireVariable(captured,false,true);
            _variable=lease;request.Invalidated+=lease.Invalidated;dialog.Closed+=lease.Closed;
            if(!request.IsValid){RetireVariable(lease,true,false);return;}
            dialog.ShowDialog();if(!lease.Retired)RetireVariable(lease,true,false);
        }
        catch(Exception failure)
        {
            try{if(lease is not null)RetireVariable(lease,true,false);else{_owner.ReleaseModal(close);request.Dispose();}}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
    }
    private void RetireVariable(VariableLease lease,bool closeWindow,bool takeSelection)
    {
        if(lease.Retired)return;lease.Retired=true;
        if(ReferenceEquals(_variable,lease))_variable=null;
        _owner.ReleaseModal(lease.Close);
        var selected=takeSelection&&lease.Window.IsConfirm?lease.Window.SelectedVarItem:null;
        ParameterCleanup.Run(new Action[]{
            ()=>{if(lease.Invalidated is not null)lease.Request.Invalidated-=lease.Invalidated;},
            ()=>{if(lease.Closed is not null)lease.Window.Closed-=lease.Closed;},
            ()=>{if(closeWindow)lease.Window.Retire();},
            ()=>{if(selected is not null&&lease.Request.IsValid)lease.Request.TrySelect(selected);},lease.Request.Dispose});
    }
    internal void Close()
    {
        _generation++;var request=_request;_request=null;
        ParameterCleanup.Run(new Action[]{()=>{if(_variable is {} variable)RetireVariable(variable,true,false);},
            ()=>{if(request is not null)_owner.Release(request);}});
    }
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_root))Close();}
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!_root.IsVisible)Close();}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{()=>{_root.Unloaded-=OnUnloaded;_root.IsVisibleChanged-=OnVisibility;},Close,
            ()=>{if(_ownsOwner)_owner.Dispose();}});
    }
}
