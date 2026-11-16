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
/// 交互借帧在显示图不是 ROI 输入图时被宿主阻断并给出原因（原件 RoiDrawTips 语义，
/// 阻断原因经宿主 RoiDrawBlockReason 传递），预览等非交互借帧不受影响。</remarks>
public sealed class NativeRoiFrameSource(Func<string,bool,NativeRoiFrameLease?> borrow,Func<IReadOnlyCollection<string>,NativeRoiInputLease?>? captureInputs=null)
{
    public NativeRoiInputLease? CaptureInputs(IReadOnlyCollection<string> fields)=>captureInputs?.Invoke(fields);
    public event Action? Changed;
    /// <summary>视口已经换代，必须让编辑器丢弃旧回执并重新挂接当前帧。</summary>
    public event Action? FrameReplaced;
    /// <summary>用户在显示图像视口上按下鼠标（原件两段式：形状按钮只选中，
    /// 落笔时才检查 IsCanDrawROI 并弹阻断提示）；宿主仅在存在阻断意图时上报。</summary>
    public event Action? InputViewportClicked;
    public NativeRoiFrameLease? Borrow(string inputFieldId,bool interactive)=>borrow(inputFieldId,interactive);
    public void Refresh()=>Changed?.Invoke();
    public void RebindCurrentFrame()=>FrameReplaced?.Invoke();
    public void NotifyInputViewportClicked()=>InputViewportClicked?.Invoke();
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
