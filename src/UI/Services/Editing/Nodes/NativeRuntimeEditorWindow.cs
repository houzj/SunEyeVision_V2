using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Events;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Commands;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Blob's host for the shared parameter frame. Execution remains in the document adapter.</summary>
public sealed class NativeRuntimeEditorWindow : BaseWindow, INativeToolCommands
{
    private readonly NativeToolEditorFrame _frame;
    private IDisposable? _commandLease;
    private readonly WindowCommand _continuous,_stop,_execute,_confirm;
    private readonly ToolExecutionCompletedEventHandler _relay;
    private Func<bool> _hostAvailable=()=>false;
    private bool _running,_closed;
    public RuntimeNodeConfigurationControl EditorControl {get;}
    public event ToolExecutionCompletedEventHandler? ToolExecutionCompleted;
    public event EventHandler? Changed;

    public NativeRuntimeEditorWindow(RuntimeNodeConfigurationControl control)
    {
        EditorControl=control??throw new ArgumentNullException(nameof(control));
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/OriginalTheme.xaml",UriKind.Relative)});
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml",UriKind.Relative)});
        Resources.MergedDictionaries.Add(SunEyeVision.Plugin.Wpf.EditorResources.Create());
        Style=(Style)FindResource(typeof(BaseWindow));
        Width=SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Language")=="English"?680:420;
        Height=500;MinWidth=360;MinHeight=450;MinButtonVisibility=Visibility.Collapsed;MaxButtonVisibility=Visibility.Collapsed;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        _frame=new NativeToolEditorFrame{EditorContent=control};
        BindingOperations.SetBinding(_frame,NativeToolEditorFrame.ResetVisibilityProperty,new Binding("Content.SelectedPageIndex"){Source=control,Converter=RunPageVisibility.Instance});
        _continuous=new(()=>{},()=>false);_stop=new(()=>{},()=>false);_execute=new(()=>_ = ExecuteAsync(),CanRun);_confirm=new(ConfirmCore,()=>IsCurrent&&!_running);
        _frame.Loaded+=FrameLoaded;Content=_frame;
        _relay=(_,args)=>{if(!_closed)ToolExecutionCompleted?.Invoke(this,args);};
        control.AddHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,_relay);
        Closed+=OnClosed;
    }
    internal void SetHostAvailability(Func<bool> available){_hostAvailable=available??throw new ArgumentNullException(nameof(available));NotifyState();}
    private void FrameLoaded(object sender,RoutedEventArgs args)
    {
        if(_closed)return;
        _commandLease?.Dispose();_commandLease=_frame.BindCommands(this);NotifyState();
    }
    internal void NotifyState()
    {
        if(_closed)return;
        NotifyObservers(Changed,this);
        _execute.Notify();_confirm.Notify();
    }
    private static void NotifyObservers(EventHandler? observers,object sender)
    {
        if(observers is null)return;
        foreach(EventHandler observer in observers.GetInvocationList())
            try{observer(sender,EventArgs.Empty);}catch(Exception error){Log(error);}
    }
    private static void Log(Exception error)=>Plugin.SDK.Logging.PluginLogger.Error(error.Message,nameof(NativeRuntimeEditorWindow),error);
    private static void Cleanup(Action action){try{action();}catch(Exception error){Log(error);}}
    private bool CanRun()=>IsCurrent&&!_running&&_hostAvailable();
    public NativeToolCommandState State=>new(false,_running||!_hostAvailable(),true,true);
    public bool IsCurrent=>!_closed;
    public ICommand StartContinuous=>_continuous;
    public string? ContinuousUnavailableReason=>"连续执行尚未接入";
    public ICommand Stop=>_stop;
    public ICommand ExecuteOnce=>_execute;
    public ICommand Confirm=>_confirm;
    public bool TryConfirm(out string error)
    {
        if(_closed||_running){error="当前窗口不可提交。";return false;}
        if(!ToolEditorCommit.TryCommit(EditorControl,out error))return false;
        Close();
        if(!_closed){error=LastCloseError is null?"参数已提交，但窗口关闭已取消。":$"参数已提交，但窗口关闭失败：{LastCloseError.Message}";return false;}
        return true;
    }
    private void ConfirmCore(){if(!TryConfirm(out var error))MessageBox.Show(this,error,"配置未提交",MessageBoxButton.OK,MessageBoxImage.Warning);}
    private async Task ExecuteAsync()
    {
        if(!CanRun())return;_running=true;
        try{NotifyState();await EditorControl.RunDebugAsync();}
        catch(Exception error){if(!_closed)MessageBox.Show(this,error.Message,"执行失败",MessageBoxButton.OK,MessageBoxImage.Warning);}
        finally{_running=false;NotifyState();}
    }
    private void OnClosed(object? sender,EventArgs args)
    {
        if(_closed)return;_closed=true;
        Cleanup(()=>EditorControl.RemoveHandler(ToolDebugControlBase.ToolExecutionCompletedEvent,_relay));
        _frame.Loaded-=FrameLoaded;
        var lease=_commandLease;_commandLease=null;
        Cleanup(()=>lease?.Dispose());Cleanup(()=>_frame.Dispose());
        ToolExecutionCompleted=null;Changed=null;
    }
    private sealed class RunPageVisibility:IValueConverter
    {
        public static readonly RunPageVisibility Instance=new();
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is int index&&index==1?Visibility.Visible:Visibility.Collapsed;
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
    }
    private sealed class WindowCommand(Action execute,Func<bool> canExecute):ICommand
    {
        public bool CanExecute(object? parameter)=>canExecute();
        public void Execute(object? parameter){if(CanExecute(parameter))execute();}
        public event EventHandler? CanExecuteChanged;
        internal void Notify()=>NotifyObservers(CanExecuteChanged,this);
    }
}
