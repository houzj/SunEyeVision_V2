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
        // 原厂状态机（NameValueItem.cs:927-973）：红=写模块失败（SetParamErro），黄=读模块失败（GetParamErro），
        // 初始 Normal，静态界面永不红——原厂参数项解析即有值（DefaultValue/CurValue），不存在"必填未赋值"红态。
        // 故这里只映射 EditError（写失败）与 SourceError（读失败），HasInvalidInput 的未赋值态不驱动行红色。
        var readError=false;
        foreach(var state in states)
        {
            if(state.EditError.Length!=0)return NativeParameterItemState.WriteError;
            readError|=state.SourceError.Length!=0;
        }
        return readError?NativeParameterItemState.ReadError:otherInvalid?NativeParameterItemState.WriteError:NativeParameterItemState.Normal;
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
    // VM ParamTab exposes the flattened page through Tab.Collection. Keep the same contract
    // on the host page so CollectionViewSource groups the live row collection.
    internal ObservableCollection<NativeParameterRow> Collection => Rows;
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
