using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using System.Windows;
using System.Xml.Linq;
using System.Xml;
using System;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime
{

/// <summary>Blob运行参数的中性快照。宿主资源和ROI不在此状态中。</summary>

public static class BlobImageRoi
{
    public const string ModeId = "input.roi.area-type";
    public const string ImageId = "input.roi.mask-image";
    public const string OutputEnabledId = "input.roi.output-mask";
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Integer, Required:false, EditModes:FieldEditModes.Constant),
        new Port(ImageId, TypeRef.ResourceOf(ResourceKind.Image), Required:false, EditModes:FieldEditModes.Reference),
        // VM OutputMaskImage is a normal RW Boolean with a false default; it has
        // no unset/clear branch, so the original switch does not show the × action.
        // The host keeps this input optional for legacy configurations. The
        // formal editor projects the VM false default and suppresses its clear
        // affordance because the VM switch has no unset state.
        new Port(OutputEnabledId, DataType.Boolean, Required:false, EditModes:FieldEditModes.Constant)
    });

    public static bool IsImageMode(ParameterSetting setting) => setting switch
    {
        UnsetSetting or ConstantSetting { Value:0L } => false,
        ConstantSetting { Value:1L } => true,
        _ => throw new ArgumentException("ROI区域类型必须是图形或图像。")
    };
}

/// <summary>
/// Inputs borrowed from the host's frozen invocation. The host owns their lifetime;
/// constructing this view does not copy or dispose image/region resources.
/// </summary>

/// <summary>Blob 的 ROI 字段身份与容量上限；记录结构由平台 RoiDocumentValue 提供。</summary>
public static class BlobLocalRoi
{
    public const string InputId="input.roi.local";
    public static Port Port{get;}=new(InputId,RoiDocumentValue.Declaration,Required:false,EditModes:FieldEditModes.Constant);
    public const int MaximumLocalInspectionCount=16;
    public const int MaximumLocalMaskCount=8;
    internal const int MaximumInheritedCount=64;

    /// <summary>通用结构校验之后追加 Blob 的容量规则。</summary>
    public static void Validate(RecordValue root,int maximumInspectionCount=MaximumLocalInspectionCount,bool inheritedMask=false)
    {
        RoiDocumentValue.Validate(root);
        if(maximumInspectionCount is not (MaximumLocalInspectionCount or MaximumInheritedCount))
            throw new ArgumentException("区域数量合同无效。");
        foreach(var group in new[]{RoiDocumentValue.Inspection,RoiDocumentValue.Mask})
        {
            var count=RoiDocumentValue.Items(root,group).Count;
            if(group==RoiDocumentValue.Inspection&&count>maximumInspectionCount)
                throw new ArgumentException("检测区域数量超出允许范围。");
            if(group==RoiDocumentValue.Mask&&count>(inheritedMask?MaximumInheritedCount:MaximumLocalMaskCount))
                throw new ArgumentException("屏蔽区数量超出允许范围。");
        }
    }
}

public static class BlobMaskInheritance
{
    public const string ModeId = "input.roi.mask.inherit";
    public const string SourceId = "input.roi.mask.polygon-strings";
    internal const int MaximumVertices=(int)RoiDocumentValue.MaximumVertices;
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);
    public static TypeRef Polygons { get; } = PolygonStringInheritance.PolygonsType;
    public static IReadOnlyList<Port> Ports { get; } = Array.AsReadOnly(new[]
    {
        new Port(ModeId, DataType.Boolean, false, EditModes:FieldEditModes.Constant),
        new Port(SourceId, PolygonStrings, false, EditModes:FieldEditModes.Reference)
    });
    public static bool IsInherited(ParameterSetting setting) => setting switch
    {
        ConstantSetting {Value:bool enabled} => enabled,
        UnsetSetting => false,
        _ => throw new ArgumentException("屏蔽区创建方式必须是固定值。")
    };
    /// <summary>解析与替换由平台完成，工具只给字段身份与上限。</summary>
    public static RecordValue? Materialize(InputValues inputs, RecordValue? roi)
        => PolygonStringInheritance.Materialize(inputs, ModeId, SourceId,
            BlobLocalRoi.MaximumInheritedCount, MaximumVertices, roi);
    internal static string Encode(PolygonGeometry polygon) => PolygonStringInheritance.Encode(polygon);
    internal static SequenceValue Publish(RecordValue? roi, RecordValue? pose)
        => PolygonStringInheritance.Publish(roi, pose);
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class BlobNonFieldAttribute : Attribute;

/// <summary>Versioned tool metadata. It initializes new settings, never patches saved settings.</summary>

public static class BlobRoiInheritance
{
    public const string ModeId="input.roi.inheritance-mode";
    public const string Local="Local",Rectangle="Rectangle",RectangleParameters="RectangleParameters",Annulus="Annulus",AnnulusParameters="AnnulusParameters";
    public const string RectangleSource="input.roi.rectangle",AnnulusSource="input.roi.annulus";
    public static IReadOnlyList<string> RectangleMembers{get;}=Array.AsReadOnly(new[]{"center-x","center-y","width","height","angle-degrees"});
    public static IReadOnlyList<string> AnnulusMembers{get;}=Array.AsReadOnly(new[]{"center-x","center-y","inner-radius","outer-radius","start-degrees","sweep-degrees"});
    public static RoiInheritanceSpec Spec{get;}=new(ModeId,Local,
        new(Rectangle,RectangleParameters,RectangleSource,RectangleMembers,"rectangle"),
        new(Annulus,AnnulusParameters,AnnulusSource,AnnulusMembers,"annulus"),
        64);
    public static string Leaf(string mode,string member)
        =>mode==RectangleParameters?RoiInheritanceMaterializer.LeafId(Spec.Rectangle,member)
        :mode==AnnulusParameters?RoiInheritanceMaterializer.LeafId(Spec.Annulus,member)
        :throw new ArgumentException("Unknown inheritance parameter mode.");
    public static TypeRef RectangleType{get;}=Spec.Rectangle.SourceRecordType;
    public static TypeRef AnnulusType{get;}=Spec.Annulus.SourceRecordType;
    public static IReadOnlyList<Port> Ports{get;}=RoiInheritanceMaterializer.DeclarePorts(Spec);
    public static string ReadMode(FieldSettings fields)=>ReadMode(fields.Read(ModeId));
    internal static string ReadMode(ParameterSetting setting)=>setting switch{
        UnsetSetting=>Local,ConstantSetting {Value:string mode}=>ValidateMode(mode),_=>throw new ArgumentException("ROI继承方式必须是固定模式。")};
    public static string ValidateMode(string mode)=>Spec.ValidateMode(mode);
    internal static IEnumerable<string> SourcesToClear(string mode)=>RoiInheritanceMaterializer.SourcesToClear(Spec,mode);
    public static IReadOnlyList<string> ActiveInputs(FieldSettings fields,IEnumerable<Port> inputs)
    {
        var mode=ReadMode(fields);var own=Ports.Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(inputs.Where(port=>!own.Contains(port.Id)||port.Id==ModeId
            ||mode==Rectangle&&port.Id==RectangleSource||mode==Annulus&&port.Id==AnnulusSource
            ||mode==RectangleParameters&&RectangleMembers.Any(member=>port.Id==Leaf(mode,member))
            ||mode==AnnulusParameters&&AnnulusMembers.Any(member=>port.Id==Leaf(mode,member))).Select(port=>port.Id).ToArray());
    }
    public static RecordValue? Materialize(InputValues inputs,RecordValue? local)
        =>RoiInheritanceMaterializer.Materialize(inputs,Spec,local);
    public static IReadOnlyList<Diagnostic> ValidateConfiguration(FieldSettings fields)
    {
        var mode=ReadMode(fields);
        if(mode==Local)return Array.Empty<Diagnostic>();
        var ids=mode switch {
            Rectangle=>new[]{RectangleSource},Annulus=>new[]{AnnulusSource},
            RectangleParameters=>RectangleMembers.Select(member=>Leaf(mode,member)).ToArray(),
            _=>AnnulusMembers.Select(member=>Leaf(mode,member)).ToArray()};
        return Array.AsReadOnly(ids.Where(id=>fields.Read(id) is UnsetSetting)
            .Select(id=>new Diagnostic("BLOB_ROI_SOURCE_REQUIRED","请选择完整的ROI继承来源。",id)).ToArray());
    }
}

}

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views
{

/// <summary>One private formal-input owner; the two sessions are read-only projections of that same value.</summary>
/// <summary>ROI 文档的操作与历史：只处理 Blob 数据结构，不碰编辑器、帧和会话。</summary>
/// <summary>ROI 编辑的字段与组声明：读写、提案与历史由平台 RoiDocumentStore 承担。</summary>
internal static class BlobRoiDeclaration
{
    internal static IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(RoiDocumentValue.Inspection, "形状", false,
            BlobLocalRoi.MaximumLocalInspectionCount, RoiDocumentValue.FullDomain),
        new NativeRoiEditGroup(RoiDocumentValue.Mask, "屏蔽区", true,
            BlobLocalRoi.MaximumLocalMaskCount, RoiDocumentValue.None)
    });

    internal static readonly string[] ItemNames = { "检测区域", "屏蔽区域" };

    internal static MaskInheritanceSpec MaskInheritance { get; } = new(
        BlobMaskInheritance.ModeId, BlobMaskInheritance.SourceId, BlobLocalRoi.MaximumInheritedCount);

    internal static RoiDocumentStore Create(DeclaredInputFieldsView inputs) => new(
        inputs, BlobLocalRoi.InputId, Groups, BlobTool.ImageInput,
        BlobRoiInheritance.Spec, MaskInheritance, ItemNames);
}

internal static class BlobRoiUi
{
    internal static NativeAssistUiDraftControls? Add(DeclaredInputFieldsView inputs,Action<string,FrameworkElement,string?> attach,Action<NativeRoiUiDraftControls> observe)
    {
        var rectangleLabels=new[]{"roicenterx","roicentery","roiwidth","roiheight","roiangle"};
        var annulusLabels=new[]{"roiAnnulusCenterX","roiAnnulusCenterY","CircleInnerRadius","roiAnnulusOuterRadius","Angle Start","AngleRange"};
        IReadOnlyList<DeclaredFieldRow> Fields(string mode,IReadOnlyList<string> members,IReadOnlyList<string> labels)
            =>members.Select((member,index)=>inputs.ComposeInputEditor(BlobRoiInheritance.Leaf(mode,member),labels[index])).ToArray();
        // 控件行、继承绑定和插入位置都由平台按声明创建，工具只交字段名与模式取值。
        var controls=NativeRoiGroupComposer.Compose(inputs,new RoiGroupComposerSpec
        {
            ModeFieldId=BlobRoiInheritance.ModeId,
            LocalModeValue=BlobRoiInheritance.Local,
            InheritedModes=new[]{BlobRoiInheritance.Rectangle,BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.Annulus,BlobRoiInheritance.AnnulusParameters},
            RectangleSourceFieldId=BlobRoiInheritance.RectangleSource,
            AnnulusSourceFieldId=BlobRoiInheritance.AnnulusSource,
            RectangleParameterFields=()=>Fields(BlobRoiInheritance.RectangleParameters,BlobRoiInheritance.RectangleMembers,rectangleLabels),
            AnnulusParameterFields=()=>Fields(BlobRoiInheritance.AnnulusParameters,BlobRoiInheritance.AnnulusMembers,annulusLabels),
            AreaTypeFieldId=BlobImageRoi.ModeId,
            AreaTypeLabel="ROI Area Type",
            AreaTypeChoices=new[]
            {
                new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("Type")!),
                new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_Image Type")!)
            },
            MaskImageFieldId=BlobImageRoi.ImageId,
            MaskImageLabel="Mask Image",
            OutputMaskFieldId=BlobImageRoi.OutputEnabledId,
            OutputMaskLabel="OutputMask",
            MaskSourceFieldId=BlobMaskInheritance.SourceId,
            MaskSourceLabel="屏蔽区",
            ReadMaskInherited=()=>BlobMaskInheritance.IsInherited(inputs.ReadUnpresentedInput(BlobMaskInheritance.ModeId)),
            AcceptMaskInherited=value=>inputs.WriteUnpresentedInput(BlobMaskInheritance.ModeId,new ConstantSetting(value)),
            AttachAnchorFieldId=BlobTool.ImageInput,
            AttachSection="ROI区域"
        },attach,observe);
        try
        {
            var pose=inputs.Fields.SingleOrDefault(field=>field.FieldId==BlobTool.PoseInput);
            if(pose is null)return null;
            var mode=inputs.ComposeInputEditor(BlobFixtureInputs.PositionCorrectionModeId,"Choose Mode",defaultWhenUnset:new ConstantSetting(0L),
                integerRadioOptions:new NativeIntegerRadioOptions(new[]
                {
                    new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("RunParam_By Info","按信息")),
                    new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_By Point","按点")),
                    new NativeIntegerRadioChoice(2,AppLocalizationService.GetLang("RunParam_By Coordinate","按坐标"))
                },useFiveSlotTemplate:true));
            return new NativeAssistUiDraftControls(mode,pose,
                Sources("reference","Fixtured Point","Init",BlobFixtureInputs.ReferencePoint)
                .Concat(Sources("run","Unfixtured Point","Run",BlobFixtureInputs.RunPoint)));
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
}

