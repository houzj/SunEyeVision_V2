using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Presentation-only group metadata; parameter values remain in the existing field state.</summary>
public sealed class NativeParameterGroup : INotifyPropertyChanged
{
    private bool _first,_visible=true;
    public string NameA {get;}
    public string DisplayType {get;}
    public bool CustomVisibleA {get=>_visible;set{if(_visible==value)return;_visible=value;Notify(nameof(CustomVisibleA));}}
    public bool IsFirst {get=>_first;internal set{if(_first==value)return;_first=value;Notify(nameof(IsFirst));}}
    private void Notify(string property)=>ParameterCleanup.Run((PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>()??Enumerable.Empty<PropertyChangedEventHandler>()).Select(handler=>(Action)(()=>handler(this,new(property)))));
    public NativeParameterGroup(string title,bool noHeader=false){NameA=title;DisplayType=noHeader?"CategoryNoHead":"Category";}
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum NativeParameterItemState { Normal,ReadError,WriteError }

/// <summary>One borrowed original row template and command lease, without a second accepted value.</summary>
public sealed class NativeParameterRow : INotifyPropertyChanged,IDisposable
{
    private readonly Func<NativeParameterItemState> _state;
    private readonly DependencyPropertyDescriptor _visibility;
    private bool _disposed;
    public NativeParameterGroup GroupName {get;}
    private readonly FrameworkElement _view;
    public FrameworkElement? View=>_disposed?null:_view;
    public string DisplayType=>"DeclaredParameterRow";
    public bool EnableA=>!_disposed;
    public bool CustomVisibleA=>!_disposed&&_view.Visibility!=Visibility.Collapsed;
    public NativeParameterItemState MyItemState=>_disposed?NativeParameterItemState.Normal:_state();
    // The original row distinguishes read/write failures from an unfinished source
    // choice. MissingReference still blocks capture; it is not a failed write.
    internal static NativeParameterItemState ProjectState(IEnumerable<DeclaredFieldState> states,bool otherInvalid=false)
    {
        var readError=false;var invalid=otherInvalid;
        foreach(var state in states)
        {
            if(state.EditError.Length!=0)return NativeParameterItemState.WriteError;
            readError|=state.SourceError.Length!=0;
            invalid|=state.HasInvalidInput&&!state.MissingReference;
        }
        return readError?NativeParameterItemState.ReadError:invalid?NativeParameterItemState.WriteError:NativeParameterItemState.Normal;
    }
    public ICommand ParamItemSelectedCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public NativeParameterRow(NativeParameterGroup group,FrameworkElement view,Func<NativeParameterItemState>? state=null,Action? selected=null)
    {
        GroupName=group;_view=view;_state=state??(()=>NativeParameterItemState.Normal);
        ParamItemSelectedCommand=new RowCommand(()=>!_disposed,()=>selected?.Invoke());
        _visibility=DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty,view.GetType());
        _visibility.AddValueChanged(view,OnVisibility);
    }
    private void OnVisibility(object? sender,EventArgs args){if(!_disposed)Notify(nameof(CustomVisibleA));}
    internal void Refresh(){if(!_disposed)Notify(nameof(MyItemState));}
    private void Notify(string property)=>ParameterCleanup.Run((PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>()??Enumerable.Empty<PropertyChangedEventHandler>()).Select(handler=>(Action)(()=>handler(this,new(property)))));
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        try{ParameterCleanup.Run(new Action[]{()=>_visibility.RemoveValueChanged(_view,OnVisibility),()=>Notify(nameof(View))});}finally{PropertyChanged=null;}
    }
    private sealed class RowCommand(Func<bool> current,Action execute):ICommand
    {
        public bool CanExecute(object? parameter)=>current();
        public void Execute(object? parameter){if(current())execute();}
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}

/// <summary>Instance-owned grouping order and attachments for a single original page host.</summary>
internal sealed class NativeParameterPage : IDisposable
{
    internal ObservableCollection<NativeParameterRow> Rows {get;}=new();
    public void Dispose(){ParameterCleanup.Run(Rows.ToArray().Select(row=>(Action)row.Dispose).Append(()=>Rows.Clear()));}
}

internal static class ParameterCleanup
{
    internal static void Run(IEnumerable<Action> cleanup)
    {
        List<Exception>? errors=null;
        foreach(var action in cleanup.ToArray())try{action();}catch(Exception error){(errors??=[]).Add(error);}
        if(errors?.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors?.Count>1)throw new AggregateException(errors);
    }
}
