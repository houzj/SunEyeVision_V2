using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>One host-granted borrow of an actually presented frame. This object cannot choose or publish images.</summary>
public sealed class NativeRoiFrameLease : IDisposable
{
    private Action? _release;
    private readonly Func<bool> _current;
    private readonly NativeResultViewport _viewport;
    private readonly NativeResultFrameReceipt _receipt;
    public IReadOnlyImage Image {get;}
    public bool IsCurrent=>_release is not null&&_current()&&_release is not null;
    public NativeRoiFrameLease(IReadOnlyImage image,NativeResultViewport viewport,NativeResultFrameReceipt receipt,Func<bool> current,Action release)
    {Image=image;_viewport=viewport;_receipt=receipt;_current=current;_release=release;}
    public NativeResultViewport.ReadOnlyRoiLayer CreateReadOnlyLayer()
    {
        if(!IsCurrent)throw new InvalidOperationException("区域输入图像已失效。");
        return _viewport.CreateReadOnlyRoiLayer(Image);
    }
    public NativeResultFrameReceipt Receipt=>_receipt;
    public INativeRoiEditLease BeginEditing(NativeRoiSession session)
    {
        if(!IsCurrent)throw new InvalidOperationException("区域输入图像已失效。");
        return _viewport.BeginRoiEditing(_receipt,session,()=>IsCurrent);
    }
    public void Dispose()=>Interlocked.Exchange(ref _release,null)?.Invoke();
}

/// <summary>The actual host announces input/publication changes; a tool never polls Workflow or queries live values.</summary>
/// <remarks><paramref name="borrow"/> 的 interactive 参数标记本次借帧是否由用户绘制请求触发：
/// 交互借帧允许宿主先把单图显示切到 ROI 输入图（VM IsCanDrawROI 前置），
/// 预览等非交互借帧不得改变用户当前的显示选择。</remarks>
public sealed class NativeRoiFrameSource(Func<string,bool,NativeRoiFrameLease?> borrow,Func<IReadOnlyCollection<string>,NativeRoiInputLease?>? captureInputs=null)
{
    public NativeRoiInputLease? CaptureInputs(IReadOnlyCollection<string> fields)=>captureInputs?.Invoke(fields);
    public event Action? Changed;
    public NativeRoiFrameLease? Borrow(string inputFieldId,bool interactive)=>borrow(inputFieldId,interactive);
    public void Refresh()=>Changed?.Invoke();
}

/// <summary>Borrowed, frozen declared inputs; the host alone owns resolver and publication readers.</summary>
public sealed class NativeRoiInputLease : IDisposable
{
    private Action? _release;
    private readonly Func<bool> _current;
    public InputValues Inputs {get;}
    public bool IsCurrent {get{var release=_release;return release is not null&&_current()&&ReferenceEquals(release,_release);}}
    public NativeRoiInputLease(InputValues inputs,Func<bool> current,Action release){Inputs=inputs;_current=current;_release=release;}
    public void Dispose()=>Interlocked.Exchange(ref _release,null)?.Invoke();
}
