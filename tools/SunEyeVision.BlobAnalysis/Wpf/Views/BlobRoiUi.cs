using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobRoiUi
{
    internal static FrameworkElement Add(DeclaredInputFieldsView inputs,bool supportedWindow=false,Action<NativeRoiUiDraftControls>? observe=null)
    {
        NativeRoiInheritanceBinding? inheritance=null;
        if(supportedWindow)
        {
            var setting=inputs.ReadUnpresentedInput(BlobRoiInheritance.ModeId);
            var mode=setting switch {UnsetSetting=>BlobRoiInheritance.Local,ConstantSetting {Value:string value}=>BlobRoiInheritance.ValidateMode(value),_=>throw new ArgumentException("ROI继承方式无效。")};
            var modes=new[]{BlobRoiInheritance.Rectangle,BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.Annulus,BlobRoiInheritance.AnnulusParameters};
            SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow Field(string id,string label)
            {
                var editor=inputs.ComposeInputEditor(id,label,numericSequence:BlobRoiInheritance.Ports.Single(port=>port.Id==id).Type.Element==TypeRef.ScalarOf(ScalarKind.Float64));
                editor.ToolTip="参数按当前输入图像像素坐标解释，角度为度；仅预览已发布来源。";return editor;
            }
            var rectangleLabels=new[]{"ROI中心点X","ROI中心点Y","ROI宽度","ROI高度","ROI角度"};
            var annulusLabels=new[]{"圆心点X","圆心点Y","内径","外径","起始角度","角度范围"};
            IReadOnlyList<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow> Fields(string mode,IReadOnlyList<string> members,IReadOnlyList<string> labels)
                =>members.Select((member,index)=>Field(BlobRoiInheritance.Leaf(mode,member),labels[index])).ToArray();
            inheritance=new(mode==BlobRoiInheritance.Local?null:Array.IndexOf(modes,mode),selected=>
                inputs.WriteUnpresentedInput(BlobRoiInheritance.ModeId,new ConstantSetting(selected is null?BlobRoiInheritance.Local:modes[selected.Value])),
                Field(BlobRoiInheritance.RectangleSource,"区域"),Field(BlobRoiInheritance.AnnulusSource,"区域"),
                ()=>Fields(BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.RectangleMembers,rectangleLabels),
                ()=>Fields(BlobRoiInheritance.AnnulusParameters,BlobRoiInheritance.AnnulusMembers,annulusLabels));
        }
        var controls=new NativeRoiUiDraftControls(geometryOnlyPresentation:supportedWindow,inheritance);
        if(supportedWindow)controls.ToolTip="继承按当前输入图像的像素坐标、角度及半径只读预览；图像区域、屏蔽继承及输出掩膜尚未实现。";
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
