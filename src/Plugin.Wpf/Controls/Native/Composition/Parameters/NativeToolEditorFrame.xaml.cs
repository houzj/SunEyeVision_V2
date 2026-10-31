using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Presenters.Commands;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;

/// <summary>Original parameter body/external/fixed-footer composition. Content and host commands are borrowed.</summary>
public partial class NativeToolEditorFrame:UserControl,IDisposable
{
    public static readonly DependencyProperty EditorContentProperty=DependencyProperty.Register(nameof(EditorContent),typeof(FrameworkElement),typeof(NativeToolEditorFrame));
    public static readonly DependencyProperty ExternalContentProperty=DependencyProperty.Register(nameof(ExternalContent),typeof(FrameworkElement),typeof(NativeToolEditorFrame));
    public static readonly DependencyProperty ResetVisibilityProperty=DependencyProperty.Register(nameof(ResetVisibility),typeof(Visibility),typeof(NativeToolEditorFrame),new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty ResetCommandProperty=DependencyProperty.Register(nameof(ResetCommand),typeof(ICommand),typeof(NativeToolEditorFrame),new PropertyMetadata(null,ResetCommandChanged));
    private static readonly DependencyPropertyKey ResetAvailablePropertyKey=DependencyProperty.RegisterReadOnly(nameof(ResetAvailable),typeof(bool),typeof(NativeToolEditorFrame),new PropertyMetadata(false));
    public static readonly DependencyProperty ResetAvailableProperty=ResetAvailablePropertyKey.DependencyProperty;
    public FrameworkElement? EditorContent {get=>(FrameworkElement?)GetValue(EditorContentProperty);set=>SetValue(EditorContentProperty,value);}
    public FrameworkElement? ExternalContent {get=>(FrameworkElement?)GetValue(ExternalContentProperty);set=>SetValue(ExternalContentProperty,value);}
    public Visibility ResetVisibility {get=>(Visibility)GetValue(ResetVisibilityProperty);set=>SetValue(ResetVisibilityProperty,value);}
    public ICommand? ResetCommand {get=>(ICommand?)GetValue(ResetCommandProperty);set=>SetValue(ResetCommandProperty,value);}
    public bool ResetAvailable=>(bool)GetValue(ResetAvailableProperty);
    private static void ResetCommandChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        var frame=(NativeToolEditorFrame)sender;
        frame.SetValue(ResetAvailablePropertyKey,args.NewValue is ICommand);
        if(frame.ResetButton is not null)frame.ResetButton.ToolTip=args.NewValue is null?"重置运行参数（尚未接入）":"重置运行参数";
    }
    private NativeToolCommandPresenter? _commands;
    private long _request;
    private bool _disposed;
    public NativeToolEditorFrame()
    {
        InitializeComponent();
        BottomCommands.ItemTemplateSelector=new CommandTemplates(this);
        Unloaded+=(_,_)=>RetireCommands();
    }
    public IDisposable BindCommands(INativeToolCommands source,NativeToolCommandLabels? labels=null)
    {
        VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(source);
        var request=++_request;var previous=_commands;_commands=null;
        try{previous?.Dispose();}
        catch(Exception failure)
        {
            try{if(request==_request)BottomCommands.ItemsSource=null;}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        if(_disposed||request!=_request)throw new InvalidOperationException("Editor command binding was superseded.");
        NativeToolCommandPresenter? candidate=null;
        bool Owns()=>!_disposed&&request==_request&&ReferenceEquals(_commands,candidate);
        candidate=new(Dispatcher,source,Owns,items=>{if(Owns())BottomCommands.ItemsSource=items;},labels??new());
        _commands=candidate;
        try{candidate.Initialize();return new CommandLease(this,candidate,request);}
        catch(Exception failure)
        {
            var clear=Owns();if(clear){_commands=null;++_request;}
            var cleanupRequest=_request;
            try{ParameterCleanup.Run(new Action[]{()=>{if(clear&&cleanupRequest==_request)BottomCommands.ItemsSource=null;},candidate.Dispose});}
            catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void RetireCommands()
    {
        var request=++_request;var previous=_commands;_commands=null;
        ParameterCleanup.Run(new Action[]{()=>previous?.Dispose(),()=>{if(request==_request)BottomCommands.ItemsSource=null;}});
    }
    public void Dispose()
    {
        VerifyAccess();if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{RetireCommands,()=>SetCurrentValue(ResetCommandProperty,null),()=>SetCurrentValue(EditorContentProperty,null),()=>SetCurrentValue(ExternalContentProperty,null)});
    }
    private sealed class CommandLease(NativeToolEditorFrame owner,NativeToolCommandPresenter presenter,long request):IDisposable
    {
        public void Dispose()
        {
            owner.VerifyAccess();
            if(owner._request==request&&ReferenceEquals(owner._commands,presenter))owner.RetireCommands();
            else presenter.Dispose();
        }
    }
    private sealed class CommandTemplates(NativeToolEditorFrame owner):DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item,DependencyObject container)=>item is NativeToolCommandItem command
            ?(DataTemplate)owner.FindResource(command.TemplateKey):base.SelectTemplate(item,container);
    }
}
