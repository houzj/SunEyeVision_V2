using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Already resolved host text in pixel coordinates. No expression evaluation or business defaults.</summary>
public sealed record NativeTextOverlay(string Content,Point Position,int FontSize,Color Color,double Opacity);

/// <summary>Standalone consumer of the same frame owner and original text layer as the result workspace.</summary>
public sealed class NativeTextViewport : Decorator,IDisposable
{
    private readonly NativeResultViewport _owner=new();
    private readonly NativeResultViewport.TextLayer _text;
    private long _operation,_revision=-1;
    private bool _disposed;
    public ImageViewBox NativeView => (ImageViewBox)_owner.View;
    public ImageFrameAddress? Frame => _owner.CurrentFrame?.Frame;
    public long Revision => _revision;
    public NativeTextViewport() { _text=_owner.CreateTextLayer();Child=_owner.View; }
    public bool BindImage(IReadOnlyImage image,Func<bool> isCurrent)
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);var operation=++_operation;
        var accepted=_owner.BindImage(image,isCurrent);
        if(accepted&&operation==_operation)_revision=-1;
        return accepted&&operation==_operation;
    }
    public bool Present(ImageFrameAddress frame,long revision,IReadOnlyList<NativeTextOverlay> texts,Func<bool> isCurrent)
    {
        Dispatcher.VerifyAccess();ObjectDisposedException.ThrowIf(_disposed,this);
        var receipt=_owner.CurrentFrame;if(receipt is null||receipt.Frame!=frame)return false;
        var operation=++_operation;var accepted=_text.Present(receipt,revision,texts,isCurrent);
        if(accepted&&operation==_operation)_revision=revision;
        return accepted&&operation==_operation;
    }
    public void RefreshCurrentness() { Dispatcher.VerifyAccess();if(!_disposed)_owner.RefreshCurrentness(); }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;++_operation;
        ParameterCleanup.Run(new Action[]{_owner.Dispose,()=>Child=null});
    }
}
