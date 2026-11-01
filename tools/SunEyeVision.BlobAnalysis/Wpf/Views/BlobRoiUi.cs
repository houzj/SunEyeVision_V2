using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using System.Windows;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobRoiUi
{
    internal static FrameworkElement Add(DeclaredInputFieldsView inputs,bool supportedWindow=false,Action<NativeRoiUiDraftControls>? observe=null)
    {
        var controls=new NativeRoiUiDraftControls(geometryOnlyPresentation:supportedWindow);
        if(supportedWindow)controls.ToolTip="几何继承、图像区域及输出掩膜尚未接入执行。";
        try
        {
            var detection=inputs.TakeField("input.inspection.region");
            var mask=inputs.TakeField("input.mask.region");
            if(!supportedWindow){detection.SetLabel("区域");controls.BindSourceRows(detection,mask);}
            var pose=inputs.TakeField("input.pose");
            inputs.AddAfterField("input.image",controls,"ROI区域");
            var assist=new NativeAssistUiDraftControls(pose){GraphicVisibilitySource=controls.GraphicVisibilitySource};
            if(supportedWindow)
            {
                assist.ToolTip="仅预览，位置修正尚未实现；已有修正信息可清除。";
                System.Windows.Controls.ToolTipService.SetShowOnDisabled(assist,true);
            }
            observe?.Invoke(controls);
            return assist;
        }
        catch {controls.Dispose();throw;}
    }
}
