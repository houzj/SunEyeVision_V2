using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Controls.Parameters;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>One configuration root's presentation lifetime. It borrows field leases and a UI-only preference.</summary>
public sealed class ReferenceWindowOwner : INotifyPropertyChanged, IDisposable
{
    private readonly FrameworkElement _root;
    private readonly Func<bool> _isCurrent;
    private readonly ReferenceExpansionPreference _preference;
    private ReferenceExpansionRequest? _request;
    private ParamSubscribeWindow? _window;
    private Action? _closeModal;
    private bool _expanded, _disposed, _wasLoaded;
    private long _generation;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal ReferenceExpansionPreference Preference => _preference;
    public bool IsCurrent => !_disposed && _isCurrent() && !_disposed;
    public bool IsExpanded => IsCurrent && _expanded;
    public ReferenceExpansionRequest? CurrentRequest => IsCurrent && _request is {IsValid:true} request ? request : null;
    public bool HasSelection => CurrentRequest?.HasSelection == true;
    public bool IsIndex => CurrentRequest?.IsIndex == true;
    public IEnumerable SourceNodes => CurrentRequest?.SourceNodes ?? Array.Empty<object>();
    public IEnumerable IndexNodes => CurrentRequest?.IndexNodes ?? Array.Empty<object>();
    public object? SelectedSourceNode {get=>CurrentRequest?.SelectedSourceNode;set{if(CurrentRequest is {} request)request.SelectedSourceNode=value;}}
    public object? SelectedIndexNode {get=>CurrentRequest?.SelectedIndexNode;set{if(CurrentRequest is {} request)request.SelectedIndexNode=value;}}
    public bool PreferExpanded {get=>_preference.PreferExpanded;set{if(IsCurrent)_preference.PreferExpanded=value;}}
    public string PreferenceScopeDescription => _preference.ScopeDescription;
    public ICommand ClickCommand {get;}
    public ICommand IndexClickCommand {get;}
    public ICommand RefreshCommand {get;}
    public ReferenceWindowOwner(FrameworkElement root, Func<bool> isCurrent, ReferenceExpansionPreference preference, bool initiallyExpanded)
    {
        _root=root??throw new ArgumentNullException(nameof(root));_isCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));
        _preference=preference??throw new ArgumentNullException(nameof(preference));_expanded=initiallyExpanded;_wasLoaded=root.IsLoaded;
        ClickCommand=new ForwardCommand(()=>CurrentRequest?.ClickCommand);
        IndexClickCommand=new ForwardCommand(()=>CurrentRequest?.IndexClickCommand);
        RefreshCommand=new ForwardCommand(()=>CurrentRequest?.RefreshCommand);
        root.Loaded+=OnLoaded;root.Unloaded+=OnUnloaded;root.IsVisibleChanged+=OnVisible;root.IsEnabledChanged+=OnVisible;
        preference.PropertyChanged+=OnPreference;
        try{if(root.IsLoaded&&root.IsVisible)EnsureWindow();}
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private void NotifyProjection()=>ParameterCleanup.Run(new[]{nameof(CurrentRequest),nameof(HasSelection),nameof(IsIndex),nameof(SourceNodes),nameof(IndexNodes),nameof(SelectedSourceNode),nameof(SelectedIndexNode)}
        .Select(name=>(Action)(()=>PropertyChanged?.Invoke(this,new(name)))));
    private void OnPreference(object? sender,PropertyChangedEventArgs args){if(IsCurrent)PropertyChanged?.Invoke(this,new(nameof(PreferExpanded)));}
    private void OnLoaded(object sender,RoutedEventArgs args){if(!ReferenceEquals(args.OriginalSource,_root))return;_wasLoaded=true;EnsureWindow();}
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_root))Close();}
    private void OnVisible(object sender,DependencyPropertyChangedEventArgs args){if(!_wasLoaded)return;if(!_root.IsVisible||!_root.IsEnabled)Close();else EnsureWindow();}
    private void EnsureWindow()
    {
        _root.Dispatcher.VerifyAccess();
        if(!IsCurrent||!_expanded||!_root.IsLoaded||!_root.IsVisible||!_root.IsEnabled||_window is not null||Window.GetWindow(_root) is not {} owner)return;
        var generation=_generation;ParamSubscribeWindow? window=null;
        try
        {
            window=new ParamSubscribeWindow(owner);_window=window;window.Closed+=OnWindowClosed;window.DataContext=this;
            if(!IsCurrent||!_expanded||!ReferenceEquals(_window,window)){RetireWindow(window);return;}
            window.Show();
        }
        catch(Exception failure){try{if(generation==_generation)Close();if(window is not null&&!ReferenceEquals(_window,window))RetireWindow(window);}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    internal void Open(ReferenceExpansionRequest request)
    {
        _root.Dispatcher.VerifyAccess();
        if(!IsCurrent||!request.IsValid||!_root.IsLoaded||!_root.IsVisible||!_root.IsEnabled){request.Dispose();return;}
        var generation=++_generation;
        try
        {
            CloseModal();if(!IsCurrent||generation!=_generation){request.Dispose();return;}ClearTarget();
            if(!IsCurrent||generation!=_generation||!request.IsValid){request.Dispose();return;}
            _request=request;request.Invalidated+=OnRequestInvalidated;request.PropertyChanged+=OnRequestChanged;
            _expanded=true;NotifyProjection();
            if(IsCurrent&&generation==_generation&&request.IsValid)EnsureWindow();
        }
        catch(Exception failure){try{ParameterCleanup.Run(new Action[]{request.Dispose,()=>{if(generation==_generation)Close();}});}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    internal void Release(ReferenceExpansionRequest request){if(ReferenceEquals(_request,request))ClearTarget();}
    public void ClearSelection(){if(CurrentRequest is {} request)request.ClearSelection();}
    private void OnRequestInvalidated(){if(_request is {IsValid:false})ClearTarget();}
    private void OnRequestChanged(object? sender,PropertyChangedEventArgs args){if(ReferenceEquals(sender,_request)&&IsCurrent)NotifyProjection();}
    private void ClearTarget() {var prior=_request;_request=null;RetireTarget(prior);}
    private void RetireTarget(ReferenceExpansionRequest? prior)
    {
        ParameterCleanup.Run(new Action[]{()=>{if(prior is not null){prior.Invalidated-=OnRequestInvalidated;prior.PropertyChanged-=OnRequestChanged;}},
            ()=>prior?.Dispose(),NotifyProjection});
    }
    private void OnWindowClosed(object? sender,EventArgs args)
    {
        if(!ReferenceEquals(sender,_window))return;
        var window=_window;_window=null;_expanded=false;_generation++;var request=_request;_request=null;
        ParameterCleanup.Run(new Action[]{()=>RetireTarget(request),()=>{if(window is not null){window.Closed-=OnWindowClosed;window.DetachOwner();window.DataContext=null;}},
            ()=>PropertyChanged?.Invoke(this,new(nameof(IsExpanded)))});
    }
    private void RetireWindow(ParamSubscribeWindow window)
        =>ParameterCleanup.Run(new Action[]{()=>window.Closed-=OnWindowClosed,window.DetachOwner,()=>window.DataContext=null,
            ()=>{if(window.IsVisible)window.Hide();},window.Close});
    internal bool InstallModal(Action close)
    {
        var generation=_generation+1;Close();if(!IsCurrent||_generation!=generation)return false;_closeModal=close;return true;
    }
    internal void ReleaseModal(Action close){if(ReferenceEquals(_closeModal,close))_closeModal=null;}
    private void CloseModal(){var close=_closeModal;_closeModal=null;close?.Invoke();}
    public void Close()
    {
        _root.Dispatcher.VerifyAccess();_expanded=false;_generation++;var window=_window;_window=null;var request=_request;_request=null;var modal=_closeModal;_closeModal=null;
        ParameterCleanup.Run(new Action[]{()=>modal?.Invoke(),()=>RetireTarget(request),()=>{if(window is not null)RetireWindow(window);},
            ()=>PropertyChanged?.Invoke(this,new(nameof(IsExpanded)))});
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{()=>{_root.Loaded-=OnLoaded;_root.Unloaded-=OnUnloaded;_root.IsVisibleChanged-=OnVisible;_root.IsEnabledChanged-=OnVisible;},
            ()=>_preference.PropertyChanged-=OnPreference,Close,()=>PropertyChanged=null});
    }
    private sealed class ForwardCommand(Func<ICommand?> read):ICommand
    {
        public bool CanExecute(object? value)=>read()?.CanExecute(value)==true;
        public void Execute(object? value){var command=read();if(command?.CanExecute(value)==true)command.Execute(value);}
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}