using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>UI opening preference, independent of tool parameters and accepted references.</summary>
public sealed class ReferenceExpansionPreference : INotifyPropertyChanged
{
    private bool _expanded;
    public bool PreferExpanded {get=>_expanded;set{if(_expanded==value)return;_expanded=value;PropertyChanged?.Invoke(this,new(nameof(PreferExpanded)));}}
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A generation-bound forwarding lease over one session, with no second selection or accepted value.</summary>
public sealed class ReferenceExpansionRequest : INotifyPropertyChanged,IDisposable
{
    private ReferenceFieldSession? _session;
    private readonly ReferenceExpansionPreference _preference;
    private readonly long _generation;
    private bool _index;
    private bool _hasSelection=true;
    public Port Target {get;}
    public bool IsValid=>_session?.IsExpansionCurrent(this,_generation)==true;
    public bool IsIndex=>_index;
    public bool HasSelection=>_hasSelection;
    public bool PreferExpanded {get=>_preference.PreferExpanded;set{if(IsValid)_preference.PreferExpanded=value;}}
    public IEnumerable SourceNodes=>IsValid&&HasSelection?_session!.SourceNodes:Array.Empty<object>();
    public IEnumerable IndexNodes=>IsValid&&HasSelection?_session!.IndexNodes:Array.Empty<object>();
    public object? SelectedSourceNode
    {
        get=>IsValid?_session!.SelectedSourceNode:null;
        set{if(IsValid&&HasSelection&&!IsIndex&&_session!.ContainsSourceNode(value))_session.SelectedSourceNode=(ReferenceTreeNode?)value;}
    }
    public object? SelectedIndexNode
    {
        get=>IsValid?_session!.SelectedIndexNode:null;
        set{if(IsValid&&HasSelection&&IsIndex&&_session!.ContainsIndexNode(value))_session.SelectedIndexNode=(ReferenceTreeNode?)value;}
    }
    public ICommand ClickCommand {get;}
    public ICommand IndexClickCommand {get;}
    public ICommand RefreshCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Invalidated;
    internal ReferenceExpansionRequest(ReferenceFieldSession session,long generation,ReferenceExpansionPreference preference,bool index)
    {
        _session=session;_generation=generation;_preference=preference;_index=index;Target=session.Target;
        ClickCommand=new RequestCommand(value=>IsValid&&HasSelection&&!IsIndex&&_session!.ContainsSourceNode(ReferenceFieldSession.Unwrap(value)),
            value=>_session!.ClickCommand.Execute(value));
        IndexClickCommand=new RequestCommand(value=>IsValid&&HasSelection&&IsIndex&&_session!.ContainsIndexNode(ReferenceFieldSession.Unwrap(value)),
            value=>_session!.IndexClickCommand.Execute(value));
        RefreshCommand=new RequestCommand(_=>IsValid,_=>_session!.Refresh());
        session.PropertyChanged+=OnSessionChanged;preference.PropertyChanged+=OnPreferenceChanged;
    }
    internal void SetIndex(bool index,bool hasSelection=true)
    {
        if(!IsValid)return;var changed=_hasSelection!=hasSelection;_hasSelection=hasSelection;
        if(_index!=index){_index=index;PropertyChanged?.Invoke(this,new(nameof(IsIndex)));}
        if(changed){PropertyChanged?.Invoke(this,new(nameof(HasSelection)));PropertyChanged?.Invoke(this,new(nameof(SourceNodes)));PropertyChanged?.Invoke(this,new(nameof(IndexNodes)));}
    }
    private void OnSessionChanged(object? sender,PropertyChangedEventArgs args)
    {
        if(!IsValid)return;
        if(args.PropertyName is nameof(SourceNodes) or nameof(IndexNodes) or nameof(SelectedSourceNode) or nameof(SelectedIndexNode))
            PropertyChanged?.Invoke(this,args);
    }
    private void OnPreferenceChanged(object? sender,PropertyChangedEventArgs args)
    {if(IsValid)PropertyChanged?.Invoke(this,new(nameof(PreferExpanded)));}
    internal void Invalidate()
    {
        if(_session is not {} session)return;
        _session=null;session.PropertyChanged-=OnSessionChanged;_preference.PropertyChanged-=OnPreferenceChanged;
        try{PropertyChanged?.Invoke(this,new(nameof(IsValid)));}
        finally
        {
            try{Invalidated?.Invoke();}
            finally{PropertyChanged=null;Invalidated=null;}
        }
    }
    public void Dispose()
    {
        if(_session is not {} session)return;
        session.ReleaseExpansion(this,_generation);Invalidate();
    }
    private sealed class RequestCommand(Func<object?,bool> canExecute,Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter)=>canExecute(parameter);
        public void Execute(object? parameter){if(canExecute(parameter))execute(parameter);}
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}
