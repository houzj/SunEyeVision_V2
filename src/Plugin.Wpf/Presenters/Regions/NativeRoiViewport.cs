using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Standalone composition using the same single scene owner and borrowed ROI attachment.</summary>
public sealed class NativeRoiViewport : Decorator, IDisposable
{
    private readonly NativeResultViewport _owner = new();
    private NativeRoiSession? _session;
    private IDisposable? _attachment;
    private Action? _changed;
    private long _generation;
    private bool _disposed;
    public ImageViewBox NativeView => (ImageViewBox)_owner.View;
    public byte[]? ReadPixel(int x,int y)=>_owner.ReadPixel(x,y);
    public NativeRoiViewport() { Child=_owner.View;Loaded+=OnLoaded;Unloaded+=OnUnloaded; }
    private bool Current(long generation,NativeRoiSession session)=>!_disposed&&generation==_generation&&ReferenceEquals(session,_session);
    public void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        var generation=++_generation;var previous=_session;_session=null;
        if(previous is not null&&_changed is not null)previous.Changed-=_changed;
        _changed=null;var attachment=_attachment;_attachment=null;
        ParameterCleanup.Run(new Action[]{()=>attachment?.Dispose(),()=>{if(generation==_generation)_owner.Clear();}});
        if(_disposed||generation!=_generation)return;
        _session=session;
        if(session is not null)
        {
            _changed=()=>{if(Current(generation,session)&&!session.IsCurrent&&Current(generation,session))_owner.Clear();};
            session.Changed+=_changed;
        }
        if(IsLoaded||System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))Attach();
    }
    private void Attach()
    {
        var session=_session;var generation=_generation;
        if(session is null||_attachment is not null||!session.IsCurrent||!Current(generation,session))return;
        if(!_owner.BindImage(session.Image,()=>Current(generation,session)&&session.IsCurrent))return;
        if(!Current(generation,session))return;
        try
        {
            var candidate=_owner.BeginRoiEditing(_owner.CurrentFrame!,session,()=>Current(generation,session)&&session.IsCurrent);
            if(Current(generation,session))_attachment=candidate;else candidate.Dispose();
        }
        catch(NativeRoiLeaseRetiredException) when(!Current(generation,session)||!session.IsCurrent) { }
    }
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))Attach();}
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        if(!ReferenceEquals(args.OriginalSource,this))return;
        var generation=_generation;var attachment=_attachment;_attachment=null;
        ParameterCleanup.Run(new Action[]{()=>attachment?.Dispose(),()=>{if(generation==_generation)_owner.Clear();}});
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;++_generation;Loaded-=OnLoaded;Unloaded-=OnUnloaded;
        var session=_session;_session=null;if(session is not null&&_changed is not null)session.Changed-=_changed;_changed=null;
        var attachment=_attachment;_attachment=null;
        ParameterCleanup.Run(new Action[]{()=>attachment?.Dispose(),_owner.Dispose,()=>Child=null});
    }
}
