using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Original Expand template with a presentation-only state, independent of tool settings.</summary>
public sealed class NativeAdvancedPresenter : ContentControl,IDisposable
{
    private bool _disposed;
    private long _textGeneration;
    private DispatcherOperation? _textPending;
    public static readonly DependencyProperty IsExpandedProperty=DependencyProperty.Register(nameof(IsExpanded),typeof(bool),typeof(NativeAdvancedPresenter),
        new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OnExpanded));
    public bool IsExpanded {get=>(bool)GetValue(IsExpandedProperty);set{ObjectDisposedException.ThrowIf(_disposed,this);SetCurrentValue(IsExpandedProperty,value);}}
    public ToggleButton NativeView {get;}
    public event Action? Changed;
    public NativeAdvancedPresenter()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/ExpandResources.xaml",UriKind.Relative)});
        Style=null;Padding=new Thickness(0);BorderThickness=new Thickness(0);HorizontalContentAlignment=HorizontalAlignment.Stretch;
        NativeView=(ToggleButton)((DataTemplate)FindResource("NativeAdvancedToggle")).LoadContent();
        NativeView.DataContext=this;Content=NativeView;
        NativeView.Loaded+=OnLoaded;
        NativeView.Unloaded+=OnUnloaded;
        NativeView.Checked+=OnChecked;
        NativeView.Unchecked+=OnChecked;
    }
    private static void OnExpanded(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        var view=(NativeAdvancedPresenter)sender;if(view._disposed)return;
        try { ParameterCleanup.Run((view.Changed?.GetInvocationList().Cast<Action>()??Enumerable.Empty<Action>()).Select(handler=>(Action)(()=>handler()))); }
        finally { if(view.NativeView is not null)view.QueueText(); }
    }
    private void OnLoaded(object sender,RoutedEventArgs args)=>QueueText();
    private void OnUnloaded(object sender,RoutedEventArgs args)=>CancelText();
    private void OnChecked(object sender,RoutedEventArgs args)=>QueueText();
    private void CancelText(){_textGeneration++;_textPending?.Abort();_textPending=null;}
    private void QueueText()
    {
        CancelText();if(_disposed||!NativeView.IsLoaded)return;
        var generation=_textGeneration;
        _textPending=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {
            if(_disposed||generation!=_textGeneration||!NativeView.IsLoaded)return;
            _textPending=null;
            if(NativeView.Template?.FindName("cp",NativeView) is ContentPresenter text)
                NativeTextInheritance.Restore(text);
        }));
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;CancelText();
        NativeView.Loaded-=OnLoaded;NativeView.Unloaded-=OnUnloaded;
        NativeView.Checked-=OnChecked;NativeView.Unchecked-=OnChecked;
        BindingOperations.ClearBinding(NativeView,ToggleButton.IsCheckedProperty);NativeView.DataContext=null;NativeView.IsEnabled=false;Content=null;
    }
}
