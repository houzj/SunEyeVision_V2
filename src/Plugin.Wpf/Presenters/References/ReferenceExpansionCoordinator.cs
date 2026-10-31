using SunEyeVision.Plugin.Wpf.Controls.Parameters;
using System.Runtime.CompilerServices;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;

namespace SunEyeVision.Plugin.Wpf.Presenters.References;

/// <summary>Hosts the original subscription window; the request borrows the field session.</summary>
internal sealed class ReferenceExpansionCoordinator : IDisposable
{
    private sealed class OwnerScope
    {
        internal readonly ReferenceExpansionPreference Preference=new();
        internal ReferenceExpansionCoordinator? Active;
        internal long Generation;
    }
    private static readonly ConditionalWeakTable<Window,OwnerScope> Owners=new();
    private readonly FrameworkElement _root;
    private readonly ReferenceExpansionPreference _unownedPreference=new();
    private sealed class WindowLease(ParamSubscribeWindow window,ReferenceExpansionRequest request,OwnerScope scope)
    {
        internal readonly ParamSubscribeWindow Window=window;
        internal readonly ReferenceExpansionRequest Request=request;
        internal readonly OwnerScope Scope=scope;
        internal Action? Invalidated;
        internal EventHandler? Closed;
        internal bool Retired;
    }
    private WindowLease? _current;
    private sealed class VariableLease(VariableSelectionDialog window,VariableSelectionRequest request,OwnerScope scope)
    {
        internal readonly VariableSelectionDialog Window=window;
        internal readonly VariableSelectionRequest Request=request;
        internal readonly OwnerScope Scope=scope;
        internal Action? Invalidated;
        internal EventHandler? Closed;
        internal bool Retired;
    }
    private VariableLease? _variable;
    private bool _disposed;

    internal ReferenceExpansionCoordinator(FrameworkElement root)
    {
        _root=root;root.Unloaded+=OnUnloaded;root.IsVisibleChanged+=OnVisibility;
    }
    internal ReferenceExpansionPreference Preference()=>Window.GetWindow(_root) is {} owner
        ?Owners.GetValue(owner,_=>new()).Preference:_unownedPreference;

    internal static void ClearSelectionFor(Window owner)
    {if(Owners.TryGetValue(owner,out var scope))scope.Active?._current?.Request.ClearSelection();}

    internal void Open(ReferenceExpansionRequest request)
    {
        _root.Dispatcher.VerifyAccess();
        if(_disposed||!request.IsValid||!_root.IsLoaded||!_root.IsVisible||Window.GetWindow(_root) is not {} owner)
        {request.Dispose();return;}
        var scope=Owners.GetValue(owner,_=>new());var generation=++scope.Generation;
        ParamSubscribeWindow? window=null;WindowLease? lease=null;
        try
        {
            scope.Active?.Close();
            if(_disposed||!request.IsValid||generation!=scope.Generation||!_root.IsLoaded||!_root.IsVisible||!ReferenceEquals(Window.GetWindow(_root),owner))
            {request.Dispose();return;}
            window=new ParamSubscribeWindow(owner);
            lease=new(window,request,scope);
            var captured=lease;
            lease.Invalidated=()=>Retire(captured,true);
            lease.Closed=(_,_)=>Retire(captured,false);
            _current=lease;scope.Active=this;
            request.Invalidated+=lease.Invalidated;window.Closed+=lease.Closed;
            window.DataContext=request;
            if(!ReferenceEquals(_current,lease)||!request.IsValid){Retire(lease,true);return;}
            window.Show();
        }
        catch(Exception failure)
        {
            try
            {
                if(lease is not null)Retire(lease,true);
                else ParameterCleanup.Run(new Action[]{request.Dispose,()=>window?.DetachOwner(),()=>window?.Close()});
            }
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    internal void OpenVariable(VariableSelectionRequest request)
    {
        _root.Dispatcher.VerifyAccess();
        if(_disposed||!request.IsValid||!_root.IsLoaded||!_root.IsVisible||Window.GetWindow(_root) is not {} owner)
        {request.Dispose();return;}
        var scope=Owners.GetValue(owner,_=>new());var generation=++scope.Generation;
        VariableLease? lease=null;
        try
        {
            scope.Active?.Close();
            if(_disposed||!request.IsValid||generation!=scope.Generation||!_root.IsLoaded||!_root.IsVisible||!ReferenceEquals(Window.GetWindow(_root),owner))
            {request.Dispose();return;}
            var dialog=new VariableSelectionDialog(request.Items){Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            lease=new(dialog,request,scope);var captured=lease;
            lease.Invalidated=()=>RetireVariable(captured,true,false);
            lease.Closed=(_,_)=>RetireVariable(captured,false,true);
            _variable=lease;scope.Active=this;
            request.Invalidated+=lease.Invalidated;dialog.Closed+=lease.Closed;
            if(!request.IsValid){RetireVariable(lease,true,false);return;}
            dialog.ShowDialog();
            if(!lease.Retired)RetireVariable(lease,true,false);
        }
        catch(Exception failure)
        {
            try{if(lease is not null)RetireVariable(lease,true,false);else request.Dispose();}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void RetireVariable(VariableLease lease,bool closeWindow,bool takeSelection)
    {
        if(lease.Retired)return;lease.Retired=true;
        if(ReferenceEquals(_variable,lease))
        {_variable=null;if(ReferenceEquals(lease.Scope.Active,this))lease.Scope.Active=null;}
        var selected=takeSelection&&lease.Window.IsConfirm?lease.Window.SelectedVarItem:null;
        ParameterCleanup.Run(new Action[]{
            ()=>{if(lease.Invalidated is not null)lease.Request.Invalidated-=lease.Invalidated;},
            ()=>{if(lease.Closed is not null)lease.Window.Closed-=lease.Closed;},
            ()=>{if(closeWindow)lease.Window.Retire();},
            ()=>{if(selected is not null&&lease.Request.IsValid)lease.Request.TrySelect(selected);},
            lease.Request.Dispose});
    }
    private void Retire(WindowLease lease,bool closeWindow)
    {
        if(lease.Retired)return;lease.Retired=true;
        if(ReferenceEquals(_current,lease))
        {
            _current=null;
            if(ReferenceEquals(lease.Scope.Active,this))lease.Scope.Active=null;
        }
        ParameterCleanup.Run(new Action[]{
            ()=>{if(lease.Closed is not null)lease.Window.Closed-=lease.Closed;},
            ()=>{if(lease.Invalidated is not null)lease.Request.Invalidated-=lease.Invalidated;},
            lease.Window.DetachOwner,lease.Request.Dispose,()=>lease.Window.DataContext=null,
            ()=>{if(closeWindow&&lease.Window.IsVisible)lease.Window.Hide();},
            ()=>{if(closeWindow)lease.Window.Close();}});
    }
    internal void Close()
    {
        ParameterCleanup.Run(new Action[]{()=>{if(_variable is {} variable)RetireVariable(variable,true,false);},
            ()=>{if(_current is {} lease)Retire(lease,true);}});
    }
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_root))Close();}
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)
    {if(!_root.IsVisible)Close();}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _root.Unloaded-=OnUnloaded;_root.IsVisibleChanged-=OnVisibility;Close();
    }
}
