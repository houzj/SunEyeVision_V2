using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Already resolved host text in pixel coordinates. No expression evaluation or business defaults.</summary>
public sealed record NativeTextOverlay(string Content,Point Position,int FontSize,Color Color,double Opacity);

/// <summary>Owns one original image scene and its ShapeList. Never shares it with another presenter.</summary>
public sealed class NativeTextViewport : Decorator,IDisposable
{
    private NativeRoiImageFrame? _frame;
    private Func<bool>? _imageCurrent;
    private Func<bool>? _projectionCurrent;
    private ObservableCollection<object>? _shapes;
    private DispatcherOperation? _refresh;
    private ShapeItemsPage? _renderPage;
    private (double Width,double Height,double Ratio,double Thickness,double ViewWidth,double ViewHeight)? _renderContext;
    private long _renderRevision=-1;
    private long _generation,_revision=-1;
    private bool _disposed,_rendering,_renderDirty;
    public ImageViewBox NativeView {get;}=new();
    public ImageFrameAddress? Frame=>_frame?.Frame;
    public long Revision=>_revision;
    public NativeTextViewport()
    {
        NativeView.Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/OriginalTheme.xaml",UriKind.Relative)});
        NativeView.Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/base/imageview/shape/shapeitemspagestyle.xaml",UriKind.Relative)});
        NativeView.ShapeHitTestVisible=false;Child=NativeView;
        Loaded+=OnLoaded;NativeView.SizeChanged+=OnSizeChanged;NativeView.PropertyChanged+=OnNativeChanged;
    }
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))QueueRefresh();}
    private void OnSizeChanged(object sender,SizeChangedEventArgs args)=>QueueRefresh();
    private void OnNativeChanged(object? sender,PropertyChangedEventArgs args)
    {if(args.PropertyName is "ControlToImageRadio" or "NeedRefreshShapes")QueueRefresh();}
    private void QueueRefresh()
    {
        if(_rendering){_renderDirty=true;return;}
        if(_disposed||_frame is null||_shapes is null||_refresh is not null)return;
        var generation=_generation;
        DispatcherOperation? operation=null;
        operation=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {
            if(!ReferenceEquals(_refresh,operation))return;
            _refresh=null;
            _rendering=true;_renderDirty=false;
            var requestedRevision=_revision;
            try
            {
            if(_disposed||generation!=_generation||!IsLoaded)return;
            RefreshCurrentness();if(generation!=_generation||_shapes is null)return;
            NativeView.ApplyTemplate();NativeView.UpdateLayout();
            if(generation!=_generation||_shapes is null)return;
            if(NativeView.Source is null)NativeView.Init();
            if(generation!=_generation||_shapes is null)return;
            var page=Visuals<ShapeItemsPage>(NativeView).SingleOrDefault();
            var context=(Width:page?.ImageSourceWidth??0,Height:page?.ImageSourceHeight??0,Ratio:page?.ControlToImageRadio??0,Thickness:page?.ThicknessScale??0,
                ViewWidth:NativeView.ActualWidth,ViewHeight:NativeView.ActualHeight);
            if(page is null||context.Width<=0||context.Height<=0||!double.IsFinite(context.Ratio)||context.Ratio<=0
                ||!double.IsFinite(context.Thickness)||context.Thickness<=0)return;
            if(ReferenceEquals(page,_renderPage)&&context==_renderContext&&_renderRevision==_revision){_renderDirty=false;return;}
            // Original TextShape captures the current image transform when drawing; replace the collection once.
            var shapes=_shapes;var revision=_revision;var proof=_projectionCurrent;
            NativeView.ShapeList=new ObservableCollection<object>(shapes);
            if(generation!=_generation||revision!=_revision||!ReferenceEquals(proof,_projectionCurrent))return;
            RefreshCurrentness();
            if(generation==_generation&&revision==_revision&&ReferenceEquals(proof,_projectionCurrent)&&ReferenceEquals(shapes,_shapes))
            {_renderPage=page;_renderContext=context;_renderRevision=revision;_renderDirty=false;}
            }
            catch
            {
                if(generation==_generation&&requestedRevision==_revision)
                {_renderPage=null;_renderContext=null;_renderRevision=-1;_renderDirty=false;}
                throw;
            }
            finally
            {
                _rendering=false;
                if(_renderDirty){_renderDirty=false;QueueRefresh();}
            }
        }));
        _refresh=operation;
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Visuals<T>(child))yield return nested;}
    }
    private bool Current(long generation)
    {
        if(_disposed||generation!=_generation||_frame is null)return false;
        var receipt=_imageCurrent;var current=receipt?.Invoke()==true;
        return current&&!_disposed&&generation==_generation&&ReferenceEquals(receipt,_imageCurrent)&&_frame is not null;
    }
    public bool BindImage(IReadOnlyImage image,Func<bool> isCurrent)
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(image);ArgumentNullException.ThrowIfNull(isCurrent);
        var generation=++_generation;var previous=_frame;_frame=null;_imageCurrent=null;_projectionCurrent=null;_shapes=null;_revision=-1;
        var refresh=_refresh;_refresh=null;_renderPage=null;_renderContext=null;_renderRevision=-1;
        ParameterCleanup.Run(new Action[]{()=>refresh?.Abort(),()=>{if(generation==_generation)NativeView.ShapeList=null;},
            ()=>{if(generation==_generation)NativeView.ImageDataSource=null;},()=>previous?.Dispose()});
        if(_disposed||generation!=_generation)return false;
        var frame=new NativeRoiImageFrame(image);var attached=false;
        try
        {
            var current=isCurrent();if(!current||_disposed||generation!=_generation)return false;
            _frame=frame;_imageCurrent=isCurrent;attached=true;
            NativeView.ImageDataSource=frame;
            if(Current(generation))return true;
            if(generation==_generation)Release();return false;
        }
        catch(Exception failure)
        {
            if(attached&&generation==_generation)
            {
                try{Release();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            }
            throw;
        }
        finally{if(!attached)frame.Dispose();}
    }
    public bool Present(ImageFrameAddress frame,long revision,IReadOnlyList<NativeTextOverlay> texts,Func<bool> isCurrent)
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(texts);ArgumentNullException.ThrowIfNull(isCurrent);
        var generation=_generation;
        if(revision<0)throw new ArgumentOutOfRangeException(nameof(revision));
        if(!Current(generation)||_frame!.Frame!=frame||revision<=_revision)return false;
        var projected=new List<object>();
        foreach(var text in texts.ToArray())
        {
            if(text is null||text.Content is null||!double.IsFinite(text.Position.X)||!double.IsFinite(text.Position.Y)
                ||text.FontSize is <1 or >1000||!double.IsFinite(text.Opacity)||text.Opacity is <0 or >1)
                throw new ArgumentException("文字绘制需要有效文本、像素位置、字号和透明度。");
            projected.Add(new TextProjection(text.Content,text.Position,text.FontSize,text.Color.ToString(),text.Opacity));
        }
        // The original PretreatTexts assigns one opacity to a batch; do not merge different opacities.
        var shapes=new ObservableCollection<object>(projected.Cast<TextProjection>().GroupBy(text=>text.Opacity)
            .Select(group=>DrawShapeHelper.PretreatTexts(group.Cast<object>().ToList())));
        var current=isCurrent();
        if(!current||!Current(generation)||_frame!.Frame!=frame||revision<=_revision)return false;
        _revision=revision;_projectionCurrent=isCurrent;_shapes=shapes;
        bool Installed()=>generation==_generation&&revision==_revision&&ReferenceEquals(_projectionCurrent,isCurrent)&&ReferenceEquals(_shapes,shapes);
        try
        {
            NativeView.ShapeList=shapes;
            var valid=isCurrent();
            if(valid&&Current(generation)&&Installed()){QueueRefresh();return true;}
        }
        catch(Exception failure)
        {
            try{ClearInstalled();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
        ClearInstalled();return false;
        void ClearInstalled()
        {
            if(!Installed())return;
            _shapes=null;_projectionCurrent=null;NativeView.ShapeList=null;
        }
    }
    /// <summary>Explicitly removes a stale publication without evaluating or fetching a replacement.</summary>
    public void RefreshCurrentness()
    {
        Dispatcher.VerifyAccess();if(_disposed)return;var generation=_generation;
        if(!Current(generation)){if(generation==_generation)Release();return;}
        var receipt=_projectionCurrent;var valid=receipt?.Invoke()==true;
        if(generation!=_generation||!ReferenceEquals(receipt,_projectionCurrent))return;
        if(!valid){_shapes=null;_projectionCurrent=null;_renderPage=null;_renderContext=null;_renderRevision=-1;NativeView.ShapeList=null;}
    }
    private void Release()
    {
        var generation=++_generation;var frame=_frame;_frame=null;_imageCurrent=null;_projectionCurrent=null;_shapes=null;_revision=-1;
        var refresh=_refresh;_refresh=null;_renderPage=null;_renderContext=null;_renderRevision=-1;
        ParameterCleanup.Run(new Action[]{()=>refresh?.Abort(),()=>{if(generation==_generation)NativeView.ShapeList=null;},
            ()=>{if(generation==_generation)NativeView.ImageDataSource=null;},()=>frame?.Dispose()});
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;Loaded-=OnLoaded;NativeView.SizeChanged-=OnSizeChanged;NativeView.PropertyChanged-=OnNativeChanged;try{Release();}finally{Child=null;}}
    private sealed record TextProjection(string Content,Point Position,int FontSize,string Stroke,double Opacity) : IText;
}
