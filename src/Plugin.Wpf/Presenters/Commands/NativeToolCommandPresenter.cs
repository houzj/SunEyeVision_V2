using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Presenters.Commands;

public readonly record struct NativeToolCommandState(bool IsContinuous, bool IsRunning, bool RunVisible, bool ConfirmVisible);
public sealed record NativeToolCommandLabels(string Continuous="Lang_MutiRun", string Stop="Lang_StopRun", string ExecuteOnce="Execute", string Confirm="Confirm");

/// <summary>Borrowed host operations. The view never runs an execution loop or owns the host.</summary>
public interface INativeToolCommands
{
    NativeToolCommandState State { get; }
    bool IsCurrent { get; }
    ICommand StartContinuous { get; }
    ICommand Stop { get; }
    ICommand ExecuteOnce { get; }
    ICommand Confirm { get; }
    event EventHandler Changed;
}

internal sealed class NativeToolCommandPresenter : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly INativeToolCommands _source;
    private readonly Func<bool> _ownsSlot;
    private readonly Action<IReadOnlyList<NativeToolCommandItem>> _present;
    private readonly GuardedCommand[] _commands;
    private readonly IReadOnlyList<NativeToolCommandItem> _items;
    private bool _installed;
    private long _refresh;
    private volatile bool _disposed;
    private bool _adding, _subscribed;
    private List<CommandObservation> _observedCommands=new();
    private ICommand[] _hostCommands=Array.Empty<ICommand>();
    private bool _refreshing;

    internal NativeToolCommandPresenter(Dispatcher dispatcher, INativeToolCommands source, Func<bool> ownsSlot,
        Action<IReadOnlyList<NativeToolCommandItem>> present, NativeToolCommandLabels labels)
    {
        _dispatcher=dispatcher;_source=source;_ownsSlot=ownsSlot;_present=present;
        _commands=Enumerable.Range(0,4).Select(index=>new GuardedCommand(this,index)).ToArray();
        _items=Array.AsReadOnly(new[]
        {
            new NativeToolCommandItem("ContinuousExecuteBottomCommand",labels.Continuous,_commands[0]),
            new NativeToolCommandItem("StopExecuteBottomCommand",labels.Stop,_commands[1]),
            new NativeToolCommandItem("ExecuteOnceBottomCommand",labels.ExecuteOnce,_commands[2]),
            new NativeToolCommandItem("BottomCommand",labels.Confirm,_commands[3])
        });
    }
    private bool LocalCurrent=>!_disposed&&_ownsSlot();
    private bool Current(long request)=>LocalCurrent&&request==Interlocked.Read(ref _refresh);
    internal void Initialize()
    {
        _adding=true;_subscribed=true;
        try{_source.Changed+=OnChanged;}
        finally{_adding=false;if(!LocalCurrent)Detach();}
        if(LocalCurrent)OnChanged(this,EventArgs.Empty);
    }
    private void OnChanged(object? sender,EventArgs args)
    {
        var request=Interlocked.Increment(ref _refresh);
        if(_dispatcher.CheckAccess()&&!_refreshing)Refresh(request);
        else if(!_dispatcher.HasShutdownStarted)
            _dispatcher.BeginInvoke(DispatcherPriority.DataBind,new Action(()=>Refresh(request)));
    }
    private bool ReadState(long request,out NativeToolCommandState state)
    {
        state=default;
        if(!Current(request))return false;
        var valid=_source.IsCurrent;
        if(!Current(request)||!valid)return false;
        state=_source.State;
        if(!Current(request))return false;
        valid=_source.IsCurrent;
        return Current(request)&&valid;
    }
    private void Refresh(long request)
    {
        if(!Current(request))return;
        _refreshing=true;
        try
        {
        var valid=ReadState(request,out var state);
        if(!Current(request))return;
        if(valid&&!SyncCommands(request))return;
        for(var index=0;index<_items.Count;index++)
        {
            if(!Current(request))return;
            _items[index].Project(valid&&(index!=3||state.ConfirmVisible),state);
        }
        if(!Current(request))return;
        if(!_installed){_installed=true;_present(_items);}
        foreach(var command in _commands){if(!Current(request))break;command.Notify();}
        }
        finally{_refreshing=false;}
    }
    private bool SyncCommands(long request)
    {
        var commands=new ICommand[4];
        for(var index=0;index<4;index++)
        {
            commands[index]=ReadCommand(index);
            if(!Current(request))return false;
            ArgumentNullException.ThrowIfNull(commands[index]);
        }
        if(commands.SequenceEqual(_hostCommands,ReferenceEqualityComparer.Instance))return true;
        var next=new List<CommandObservation>();var installed=false;Exception? primary=null;
        try
        {
            foreach(var command in commands.Distinct(ReferenceEqualityComparer.Instance).Cast<ICommand>())
            {
                var observer=new CommandObservation(command,OnChanged);next.Add(observer);observer.Attach();
                if(!Current(request))return false;
            }
            var previous=_observedCommands;_observedCommands=next;_hostCommands=commands;installed=true;
            SunEyeVision.Plugin.Wpf.Adapters.Fields.ParameterCleanup.Run(previous.Select(observer=>(Action)observer.Dispose));
            return Current(request);
        }
        catch(Exception failure){primary=failure;throw;}
        finally
        {
            if(!installed)
            {
                try{SunEyeVision.Plugin.Wpf.Adapters.Fields.ParameterCleanup.Run(next.Select(observer=>(Action)observer.Dispose));}
                catch(Exception cleanup){if(primary is not null)throw new AggregateException(primary,cleanup);throw;}
            }
        }
    }
    private static bool Allowed(int kind,NativeToolCommandState state)=>kind switch
    {
        0 or 2=>!state.IsContinuous&&!state.IsRunning&&state.RunVisible,
        1=>state.IsContinuous,
        3=>state.ConfirmVisible,
        _=>false
    };
    private ICommand ReadCommand(int kind)=>kind switch
    {0=>_source.StartContinuous,1=>_source.Stop,2=>_source.ExecuteOnce,3=>_source.Confirm,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
    private bool Prepare(int kind,object? parameter,out ICommand? command)
    {
        command=null;
        if(!_dispatcher.CheckAccess())return false;
        var request=Interlocked.Read(ref _refresh);
        if(!ReadState(request,out var state)||!Allowed(kind,state))return false;
        var candidate=ReadCommand(kind);
        if(!Current(request)||candidate is null||_hostCommands.Length!=4||!ReferenceEquals(candidate,_hostCommands[kind]))return false;
        var enabled=candidate.CanExecute(parameter);
        if(!enabled||!Current(request))return false;
        if(!ReadState(request,out var latest)||latest!=state||!Current(request))return false;
        command=candidate;return true;
    }
    private void Detach()
    {
        if(_adding)return;
        var subscribed=_subscribed;_subscribed=false;
        var commands=_observedCommands;_observedCommands=new();_hostCommands=Array.Empty<ICommand>();
        var errors=new List<Exception>();
        if(subscribed)try{_source.Changed-=OnChanged;}catch(Exception error){errors.Add(error);}
        foreach(var command in commands)try{command.Dispose();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Editor command observers could not all detach.",errors);
    }
    private sealed class CommandObservation(ICommand command,EventHandler changed):IDisposable
    {
        private bool _adding,_attached,_disposed;
        internal void Attach()
        {
            _adding=true;_attached=true;
            try{command.CanExecuteChanged+=changed;}
            finally{_adding=false;if(_disposed)Detach();}
        }
        private void Detach(){if(!_attached||_adding)return;_attached=false;command.CanExecuteChanged-=changed;}
        public void Dispose(){_disposed=true;Detach();}
    }
    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;Interlocked.Increment(ref _refresh);
        Detach();
    }
    private sealed class GuardedCommand(NativeToolCommandPresenter owner,int kind):ICommand
    {
        public bool CanExecute(object? parameter)=>owner.Prepare(kind,parameter,out _);
        public void Execute(object? parameter){if(owner.Prepare(kind,parameter,out var command))command!.Execute(parameter);}
        public event EventHandler? CanExecuteChanged;
        internal void Notify()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
    }
}

internal sealed class NativeToolCommandItem(string templateKey,string displayName,ICommand command) : INotifyPropertyChanged
{
    private NativeToolCommandState _state;
    public string TemplateKey {get;}=templateKey;
    public string DisplayNameA {get;}=displayName;
    public ICommand ClickCommand {get;}=command;
    public bool CustomVisibleA {get;private set;}
    public bool IsContinuous=>_state.IsContinuous;
    public bool IsRunning=>_state.IsRunning;
    public bool RunVisible=>_state.RunVisible;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Project(bool visible,NativeToolCommandState state)
    {
        if(CustomVisibleA==visible&&_state==state)return;
        CustomVisibleA=visible;
        _state=state;
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
    }
}
