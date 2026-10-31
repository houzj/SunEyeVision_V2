using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using System.Windows;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobRoiUi
{
    internal static FrameworkElement Add(DeclaredInputFieldsView inputs)
    {
        var controls=new NativeRoiUiDraftControls();
        try
        {
            var detection=inputs.TakeField("input.inspection.region");
            var mask=inputs.TakeField("input.mask.region");
            detection.SetLabel("区域");
            controls.BindSourceRows(detection,mask);
            var pose=inputs.TakeField("input.pose");
            inputs.AddAfterField("input.image",controls,"ROI区域");
            return new NativeAssistUiDraftControls(pose){GraphicVisibilitySource=controls.GraphicVisibilitySource};
        }
        catch {controls.Dispose();throw;}
    }
}
