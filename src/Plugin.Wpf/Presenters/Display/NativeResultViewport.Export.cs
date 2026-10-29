using System.Windows.Media.Imaging;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;
using SunEyeVision.Plugin.Wpf.Native.Common;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

public sealed partial class NativeResultViewport
{
    // No borrowed image, shape or resource survives into the modal file picker.
    private BitmapSource PrepareExport(bool rendered)
    {
        _view.Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        var receipt = CurrentFrame ?? throw new InvalidOperationException("没有可导出的当前图像。");
        var frame = _frame!; var version = _shapeVersion; var shapes = _view.ShapeList;
        bool OwnsSource() => Owns(receipt.Epoch, frame) && ReferenceEquals(_view.ImageDataSource, frame)
            && (!rendered || version == _shapeVersion && ReferenceEquals(shapes, _view.ShapeList));
        bool Still() => OwnsSource() && Current(receipt) && OwnsSource();
        void Check() { if (!Still()) throw new InvalidOperationException("图像或图层已切换，请重新导出。"); }
        Check();
        if (rendered)
        {
            RefreshShapes(); Check();
            // Refresh may retire an invalid layer, so a changed composition must be retried.
            ValidateExportShapeBudget(receipt); Check();
            var visible = (shapes as IEnumerable<object>)?.Where(value => value is not IIsVisible item || item.IsVisible)
                .Select(value => value is ICross cross ? (object)new ExportCross(cross.CenterPoint, cross.Stroke,
                    cross.Fill, cross.StrokeThickness, cross.Opacity, cross.ToolTip,
                    value is CrosshairSettings settings && settings.IsLocked) : value).ToArray();
            Check();
            var prepared = NativeImageExport.PrepareRendered(frame, visible, _view);
            Check();
            // Image proof can retire a layer without changing the physical frame.
            // Consume every layer's proof again after rendering; a retirement changes
            // the composition and rejects this already rendered candidate.
            RefreshShapes();
            ValidateExportShapeBudget(receipt);
            if (!OwnsSource()) throw new InvalidOperationException("图层来源已失效，请重新导出。");
            return prepared;
        }
        var raw = frame.PrepareOriginalBitmap();
        Check(); return raw;
    }
    private sealed record ExportCross(Point CenterPoint, string Stroke, string Fill, double StrokeThickness,
        double Opacity, string ToolTip, bool IsLocked) : ICross, IIsVisible
    { public bool IsVisible { get; set; } = true; }
}
