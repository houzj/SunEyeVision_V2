using System.Runtime.CompilerServices;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

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
    private ParamSubscribeWindow? _window;
    private ReferenceExpansionRequest? _request;
    private OwnerScope? _scope;
    private bool _disposed;

    internal ReferenceExpansionCoordinator(FrameworkElement root)
    {
        _root=root;root.Unloaded+=OnUnloaded;root.IsVisibleChanged+=OnVisibility;
    }
    internal ReferenceExpansionPreference Preference()=>Window.GetWindow(_root) is {} owner
        ?Owners.GetValue(owner,_=>new()).Preference:_unownedPreference;

    internal void Open(ReferenceExpansionRequest request)
    {
        _root.Dispatcher.VerifyAccess();
        if(_disposed||!request.IsValid||!_root.IsLoaded||!_root.IsVisible||Window.GetWindow(_root) is not {} owner)
        {request.Dispose();return;}
        var scope=Owners.GetValue(owner,_=>new());var generation=++scope.Generation;
        scope.Active?.Close();
        if(_disposed||!request.IsValid||generation!=scope.Generation||!_root.IsLoaded||!_root.IsVisible||!ReferenceEquals(Window.GetWindow(_root),owner))
        {request.Dispose();return;}
        ParamSubscribeWindow? window=null;
        try
        {
            window=new ParamSubscribeWindow(owner){DataContext=request};
            _window=window;_request=request;_scope=scope;scope.Active=this;
            request.Invalidated+=OnInvalidated;window.Closed+=OnClosed;
            window.Show();
        }
        catch
        {
            if(ReferenceEquals(_window,window))Close();
            else{window?.Close();request.Dispose();}
            throw;
        }
    }
    private void OnInvalidated()=>Close();
    private void OnClosed(object? sender,EventArgs args)
    {if(ReferenceEquals(sender,_window))Release();}
    private void Release()
    {
        var window=_window;var request=_request;var scope=_scope;
        _window=null;_request=null;_scope=null;
        if(scope is not null&&ReferenceEquals(scope.Active,this))scope.Active=null;
        if(window is not null){window.Closed-=OnClosed;window.DataContext=null;}
        if(request is not null){request.Invalidated-=OnInvalidated;request.Dispose();}
    }
    private void Close()
    {
        var window=_window;
        try{Release();}
        finally{window?.Close();}
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)=>Close();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)
    {if(!_root.IsVisible)Close();}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _root.Unloaded-=OnUnloaded;_root.IsVisibleChanged-=OnVisibility;Close();
    }
}
