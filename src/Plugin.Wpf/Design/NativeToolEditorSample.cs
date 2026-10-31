using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Commands;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeToolEditorPreview:UserControl,IDisposable
{
    public NativeToolEditorPreview()=>InitializeComponent();
    public void Dispose(){var content=Content;Content=null;(content as IDisposable)?.Dispose();}
}

/// <summary>Presentation-only data over the real three-page field and fixed-footer composition.</summary>
public sealed class NativeToolEditorSample:Decorator,IDisposable
{
    public DeclaredFieldsView Fields {get;}
    public NativeToolEditorFrame Frame {get;}=new();
    public SampleCommands Commands {get;}=new();
    private bool _disposed;
    public NativeToolEditorSample()
    {
        var pages=new[]{"基本参数","运行参数","结果显示"};
        Fields=new(Enumerable.Range(0,24).Select(index=>(new Port("sample."+index,DataType.Boolean),
            new FieldPresentation("sample."+index,"独立示例 "+(index%8+1),"控件组合",index,FieldEditorKind.Scalar)
            {Layout=new(pages[index/8],"控件组合")},(ParameterSetting)new ConstantSetting(index%2==0))),_=>{});
        Frame.EditorContent=Fields;Frame.BindCommands(Commands);Frame.Loaded+=OnLoaded;Fields.PageChanged+=OnPageChanged;OnPageChanged();Child=Frame;
    }
    private void OnPageChanged()=>Frame.ResetVisibility=Fields.SelectedPageIndex==1?Visibility.Visible:Visibility.Collapsed;
    private void OnLoaded(object sender,RoutedEventArgs args){if(!_disposed)Frame.BindCommands(Commands);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Frame.Loaded-=OnLoaded;Fields.PageChanged-=OnPageChanged;
        SunEyeVision.Plugin.Wpf.Adapters.Fields.ParameterCleanup.Run(new Action[]{Frame.Dispose,Fields.Dispose,()=>Child=null});
    }

    public sealed class SampleCommands:INativeToolCommands
    {
        public NativeToolCommandState State {get;private set;}=new(false,false,true,true);
        public bool IsCurrent {get;set;}=true;
        public int ContinuousCalls,StopCalls,OnceCalls,ConfirmCalls;
        public SampleCommand ContinuousCommand {get;}=new();
        public SampleCommand StopCommand {get;}=new();
        public SampleCommand OnceCommand {get;}=new();
        public SampleCommand ConfirmCommand {get;}=new();
        public ICommand StartContinuous=>ContinuousCommand;
        public ICommand Stop=>StopCommand;
        public ICommand ExecuteOnce=>OnceCommand;
        public ICommand Confirm=>ConfirmCommand;
        public SampleCommands()
        {
            ContinuousCommand.Run=()=>{ContinuousCalls++;SetState(new(true,true,true,true));};
            StopCommand.Run=()=>{StopCalls++;SetState(new(false,false,true,true));};
            OnceCommand.Run=()=>OnceCalls++;ConfirmCommand.Run=()=>ConfirmCalls++;
        }
        public void SetState(NativeToolCommandState state){State=state;Notify();}
        public void Notify()=>Changed?.Invoke(this,EventArgs.Empty);
        public event EventHandler? Changed;
    }
    public sealed class SampleCommand:ICommand
    {
        public bool Enabled {get;set;}=true;
        public Action? BeforeCanExecute,Run;
        public bool CanExecute(object? parameter){BeforeCanExecute?.Invoke();return Enabled;}
        public void Execute(object? parameter)=>Run?.Invoke();
        public event EventHandler? CanExecuteChanged;
        public void Notify()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
    }
}
