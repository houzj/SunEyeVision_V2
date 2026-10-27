using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>The original ImageViewBox and normalized ROI handles over the same objects as the parameter stack.</summary>
public sealed class NativeBoxRoiViewport : Decorator,IDisposable
{
    private NativeBoxRoiSession? _session;
    private NativeRoiImageFrame? _frame;
    private Action? _changed;
    private long _generation;
    private bool _disposed,_projecting;
    public ImageViewBox NativeView {get;}=new();
    public NativeBoxRoiViewport()
    {
        foreach(var resource in new[]{"Themes/Native/OriginalTheme.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/roiitemspagestyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/subroiitemspagestyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/rectroi/rectroistyle.xaml"})
            NativeView.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/"+resource,UriKind.Relative)});
        Child=NativeView;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
        NativeView.RoiSelectedCommand=new SelectionCommand(this);
        PreviewMouseDown+=OnPreviewMouse;PreviewKeyDown+=OnKey;
    }
    public void Bind(NativeBoxRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeBoxRoiViewport));
        var generation=++_generation;var previous=_session;_session=null;
        if(previous is not null&&_changed is not null)previous.Changed-=_changed;
        _changed=null;
        NativeRoiCallbacks.Run([()=>previous?.CancelPending(),ReleaseFrame]);
        if(_disposed||generation!=_generation)return;
        _session=session;
        if(session is not null)
        {
            _changed=()=>{if(!_disposed&&generation==_generation&&ReferenceEquals(_session,session))OnChanged();};
            session.Changed+=_changed;
        }
        if(IsLoaded)AttachFrame();
    }
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))AttachFrame();}
    private void AttachFrame()
    {
        if(_disposed||_frame is not null||_session?.IsCurrent!=true)return;
        var session=_session;var generation=_generation;
        var frame=new NativeRoiImageFrame(session.Image);
        if(_disposed||generation!=_generation||!ReferenceEquals(_session,session)||!session.IsCurrent||_frame is not null)
        {frame.Dispose();return;}
        if(frame.Frame!=session.Frame){frame.Dispose();throw new InvalidOperationException("视口和参数不属于同一物理帧。");}
        try
        {
            _frame=frame;NativeView.ImageDataSource=frame;
            if(generation!=_generation||!ReferenceEquals(_frame,frame))return;
            NativeView.ROIList=(IList)session.Items;OnChanged();
        }
        catch{if(ReferenceEquals(_frame,frame))ReleaseFrame();else frame.Dispose();throw;}
    }
    private void OnChanged()
    {
        if(_projecting||_disposed)return;
        if(_session?.IsCurrent!=true){ReleaseFrame();return;}
        _projecting=true;
        try
        {
            NativeView.UpdateLayout();
            foreach(var roi in Descendants<RectROI>(NativeView))
            {
                // Select the original base template explicitly for normalized derived native controls.
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(RectROI));
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeBoxRoiProjection.Enabled)));
                roi.ApplyTemplate();roi.RefreshGeometryLayout();
            }
            foreach(var list in Descendants<ListView>(NativeView).Where(list=>ReferenceEquals(list.ItemsSource,_session.Items)))
                list.SelectedItem=_session.CurROIData;
        }
        finally{_projecting=false;}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {if(args.Key==Key.Escape){CancelInteraction();_session?.CancelPending();args.Handled=true;}}
    private void CancelInteraction()
    {foreach(var roi in Descendants<RectROI>(NativeView))roi.CancelInteraction();}
    private void OnPreviewMouse(object sender,MouseButtonEventArgs args)
    {if(_session?.PrepareEditor()!=true)args.Handled=true;}
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {if(!ReferenceEquals(args.OriginalSource,this))return;NativeRoiCallbacks.Run([()=>_session?.CancelPending(),ReleaseFrame]);}
    private void ReleaseFrame()
    {
        var frame=_frame;_frame=null;
        NativeRoiCallbacks.Run([CancelInteraction,()=>NativeView.ROIList=null,()=>NativeView.ImageDataSource=null,()=>frame?.Dispose()]);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;PreviewMouseDown-=OnPreviewMouse;PreviewKeyDown-=OnKey;
        var previous=_session;_session=null;
        if(previous is not null&&_changed is not null)previous.Changed-=_changed;_changed=null;
        NativeRoiCallbacks.Run([()=>previous?.CancelPending(),ReleaseFrame,()=>NativeView.RoiSelectedCommand=null,()=>Child=null]);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class SelectionCommand(NativeBoxRoiViewport view):ICommand
    {
        public bool CanExecute(object? parameter)=>!view._disposed&&view._session?.IsCurrent==true;
        public void Execute(object? parameter)
        {
            if(CanExecute(parameter)&&parameter is RoiWithTagEventArgs {Roi:NativeBoxRoiProjection item}&&view._session!.Owns(item))
                {view._session!.Select(item.Group,item.Id);view.OnChanged();}
        }
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}
