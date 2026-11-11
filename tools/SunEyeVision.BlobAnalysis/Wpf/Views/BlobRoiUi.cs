using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal static class BlobRoiUi
{
    internal static NativeAssistUiDraftControls Add(DeclaredInputFieldsView inputs,Action<NativeRoiUiDraftControls> observe)
    {
        var modes=new[]{BlobRoiInheritance.Rectangle,BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.Annulus,BlobRoiInheritance.AnnulusParameters};
        int? ReadMode()
        {
            var mode=inputs.ReadUnpresentedInput(BlobRoiInheritance.ModeId) switch
            {
                UnsetSetting=>BlobRoiInheritance.Local,
                ConstantSetting {Value:string value}=>BlobRoiInheritance.ValidateMode(value),
                _=>throw new ArgumentException("ROI继承方式无效。")
            };
            return mode==BlobRoiInheritance.Local?null:Array.IndexOf(modes,mode);
        }
        var rectangleLabels=new[]{"roicenterx","roicentery","roiwidth","roiheight","roiangle"};
        var annulusLabels=new[]{"roiAnnulusCenterX","roiAnnulusCenterY","CircleInnerRadius","roiAnnulusOuterRadius","Angle Start","AngleRange"};
        IReadOnlyList<DeclaredFieldRow> Fields(string mode,IReadOnlyList<string> members,IReadOnlyList<string> labels)
            =>members.Select((member,index)=>inputs.ComposeInputEditor(BlobRoiInheritance.Leaf(mode,member),labels[index])).ToArray();
        var inheritance=new NativeRoiInheritanceBinding(ReadMode,selected=>
            inputs.WriteUnpresentedInput(BlobRoiInheritance.ModeId,new ConstantSetting(selected is null?BlobRoiInheritance.Local:modes[selected.Value])),
            inputs.ComposeInputEditor(BlobRoiInheritance.RectangleSource,"Region"),
            inputs.ComposeInputEditor(BlobRoiInheritance.AnnulusSource,"Region"),
            ()=>Fields(BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.RectangleMembers,rectangleLabels),
            ()=>Fields(BlobRoiInheritance.AnnulusParameters,BlobRoiInheritance.AnnulusMembers,annulusLabels));
        var maskImage=inputs.ComposeInputEditor(BlobImageRoi.ImageId,"Mask Image",imageSource:true);
        var outputMask=inputs.ComposeInputEditor(BlobImageRoi.OutputEnabledId,"OutputMask",defaultWhenUnset:new ConstantSetting(false),showClearButton:false);
        var area=inputs.ComposeInputEditor(BlobImageRoi.ModeId,"ROI Area Type",defaultWhenUnset:new ConstantSetting(0L),
            integerRadioOptions:new NativeIntegerRadioOptions(new[]
            {
                new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("Type")!),
                new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_Image Type")!)
            }));
        var maskSource=inputs.ComposeInputEditor(BlobMaskInheritance.SourceId,"屏蔽区");
        var controls=new NativeRoiUiDraftControls(inheritance,maskImage,area,outputMask,maskSource,
            ()=>BlobMaskInheritance.IsInherited(inputs.ReadUnpresentedInput(BlobMaskInheritance.ModeId)),
            value=>inputs.WriteUnpresentedInput(BlobMaskInheritance.ModeId,new ConstantSetting(value)));
        try
        {
            var pose=inputs.TakeField(BlobTool.PoseInput);
            inputs.AddAfterField("input.image",controls,"ROI区域");
            var mode=inputs.ComposeInputEditor(BlobFixtureInputs.PositionCorrectionModeId,"Choose Mode",defaultWhenUnset:new ConstantSetting(0L),
                integerRadioOptions:new NativeIntegerRadioOptions(new[]
                {
                    new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("RunParam_By Info","按信息")),
                    new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_By Point","按点")),
                    new NativeIntegerRadioChoice(2,AppLocalizationService.GetLang("RunParam_By Coordinate","按坐标"))
                },useFiveSlotTemplate:true));
            var assist=new NativeAssistUiDraftControls(mode,pose,
                Sources("reference","Fixtured Point","Init",BlobFixtureInputs.ReferencePoint)
                .Concat(Sources("run","Unfixtured Point","Run",BlobFixtureInputs.RunPoint)));
            observe(controls);
            return assist;
        }
        catch {controls.Dispose();throw;}
        IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> Sources(string pose,string pointLabel,string labelPrefix,string point)
        {
            (int, Func<DeclaredFieldRow>) Source(int mode,string id,string caption)
                => (mode,()=>inputs.ComposeInputEditor(id,caption,referenceOnly:true));
            var prefix=BlobTool.PoseInput+"."+pose;
            yield return Source(1,point,pointLabel);
            yield return Source(2,point+".x",labelPrefix+"PointX");
            yield return Source(2,point+".y",labelPrefix+"PointY");
            yield return Source(3,prefix+".angle",labelPrefix+"Angle");
            yield return Source(3,prefix+".scale-x",labelPrefix+"ScaleX");
            yield return Source(3,prefix+".scale-y",labelPrefix+"ScaleY");
        }
    }
}
