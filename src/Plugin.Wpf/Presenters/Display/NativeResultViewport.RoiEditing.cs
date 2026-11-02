using System.Collections;
using SunEyeVision.Plugin.Wpf.Adapters.Display;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

internal sealed class NativeRoiLeaseRetiredException(string message) : InvalidOperationException(message);

public interface INativeRoiEditLease : IDisposable
{
    bool IsCurrent { get; }
    bool HasPendingGesture { get; }
    void CancelGesture();
}

public sealed partial class NativeResultViewport
{
    private NativeRoiAttachment? _roiAttachment;
    private RoiShapeLayer? _roiShapes;
    private long _roiVersion, _roiRevision, _roiRequest;
    private bool _roiResources;

    /// <summary>Borrow one session on this actual frame. The lease owns interactions, never the session or image.</summary>
    public INativeRoiEditLease BeginRoiEditing(NativeResultFrameReceipt receipt, NativeRoiSession session, Func<bool> isCurrent)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(receipt); ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(isCurrent);
        var request = ++_roiRequest;
        if (!Current(receipt) || session.Frame != receipt.Frame || session.ImageWidth != _frame!.Width || session.ImageHeight != _frame.Height
            || session.Coordinates != receipt.Coordinates || session.Stamp.Frame != receipt.Frame
            || !session.IsCurrent || !isCurrent() || !Current(receipt) || request != _roiRequest)
            throw new InvalidOperationException("ROI编辑会话与实际视口的物理帧、坐标或尺寸不一致，或会话已失效。");
        // RoiDeclarations.ImageFrame identifies the parameter's input slot. Session binds that
        // exact ROI record to its supplied image through Stamp.Frame and its captured dimensions.
        // Actual Domain identity is checked above; no arbitrary GeometryValue mapping is inferred.
        RetireRoi(false);
        if (request != _roiRequest || _roiAttachment is not null) throw new NativeRoiLeaseRetiredException("ROI编辑接纳已被退休回调替换。");
        var version = _roiVersion;
        EnsureRoiResources();
        if (request != _roiRequest || version != _roiVersion || !Current(receipt) || request != _roiRequest || version != _roiVersion) throw new NativeRoiLeaseRetiredException("ROI编辑接纳已被新会话替换。");
        var candidate = new NativeRoiAttachment(this, receipt, isCurrent);
        if (request != _roiRequest || version != _roiVersion || !Current(receipt) || request != _roiRequest || version != _roiVersion) { candidate.Dispose(); throw new NativeRoiLeaseRetiredException("ROI编辑接纳已过期。"); }
        _roiAttachment = candidate; _roiShapes = new(this); _shapeLayers.Add(_roiShapes);
        try
        {
            candidate.Initialize(); candidate.Bind(session);
            if (request != _roiRequest || !OwnsRoi(candidate) || !candidate.IsCurrent || !OwnsRoi(candidate) || request != _roiRequest)
                throw new NativeRoiLeaseRetiredException("ROI编辑安装过程中会话已失效。");
            return candidate;
        }
        catch (Exception failure)
        {
            try { candidate.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }
    internal bool IsFrameCurrent(NativeResultFrameReceipt receipt) => Current(receipt);
    internal bool OwnsRoi(NativeRoiAttachment attachment) => !_disposed && ReferenceEquals(_roiAttachment, attachment);
    internal bool MayClearRoiCallbacks(NativeRoiAttachment attachment) => _roiAttachment is null || ReferenceEquals(_roiAttachment, attachment);
    internal bool MayCancelRoiSession(NativeRoiAttachment attachment, NativeRoiSession session)
        => _roiAttachment is not {} active || ReferenceEquals(active, attachment) || !ReferenceEquals(active.Session, session);
    internal bool SetRoiRows(NativeRoiAttachment attachment, IList rows)
    {
        if (!OwnsRoi(attachment) || !attachment.IsCurrent) return false;
        _view.ROIList = rows;
        return OwnsRoi(attachment) && attachment.IsCurrent;
    }
    internal bool SetRoiShapes(NativeRoiAttachment attachment, object[] shapes)
    {
        var layer = _roiShapes;
        if (layer is null || !OwnsRoi(attachment) || !attachment.IsCurrent) return false;
        return layer.Publish(attachment.Receipt, ++_roiRevision, shapes, () => OwnsRoi(attachment) && attachment.IsCurrent);
    }
    internal void DetachRoi(NativeRoiAttachment attachment)
    { if (ReferenceEquals(_roiAttachment, attachment)) RetireRoi(); }
    private void RetireRoi(bool invalidatePending = true)
    {
        if (invalidatePending) ++_roiRequest;
        var version = ++_roiVersion; var attachment = _roiAttachment; _roiAttachment = null;
        var shapes = _roiShapes; _roiShapes = null; _roiRevision = 0;
        ParameterCleanup.Run(new Action[] { () => attachment?.Dispose(),
            () => { if (version == _roiVersion && _roiAttachment is null) _view.ROIList = null; }, () => shapes?.Dispose() });
    }
    private void EnsureRoiResources()
    {
        if (_roiResources) return;
        var installed = new List<ResourceDictionary>();
        try
        {
            foreach (var path in new[] { "roiitemspagestyle", "subroiitemspagestyle", "rectroi/rectroistyle",
                "circleroi/circleroistyle", "polygonroi/polygonroistyle", "sectorroi/sectorroistyle" })
            {
                var resource = new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/roi/" + path + ".xaml", UriKind.Relative) };
                installed.Add(resource); _view.Resources.MergedDictionaries.Add(resource);
            }
            var shape = new ResourceDictionary { Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/ImageView/base/imageview/shape/annularsectorshape/annularsectorshapestyle.xaml", UriKind.Relative) };
            installed.Add(shape); _view.Resources.MergedDictionaries.Add(shape);
            _resources.AddRange(installed); _roiResources = true;
        }
        catch (Exception failure)
        {
            try { ParameterCleanup.Run(installed.Select<ResourceDictionary, Action>(resource => () => _view.Resources.MergedDictionaries.Remove(resource))); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }
    private sealed class RoiShapeLayer(NativeResultViewport owner) : ShapeLayer(owner, 50)
    {
        internal bool Publish(NativeResultFrameReceipt receipt, long revision, object[] shapes, Func<bool> current)
            => Present(receipt, revision, current, (_, proof) => { NativeResultRaster.CheckCurrent(proof); return new(shapes.ToArray(), shapes.Length, 0); });
    }
}
