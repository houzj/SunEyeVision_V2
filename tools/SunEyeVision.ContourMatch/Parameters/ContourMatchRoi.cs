using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配基本参数页「ROI区域」的正式字段体系，按原厂 IMVSContourMatchModuAlgorithmTab.xml:9-41
/// Tab_ROI Area 的 GroupLinkItem 结构（OutputMaskImage/InputMaskImage/RoiSelect/ROISelecter/
/// InheritWay/ExternROIType/Assist）与 PixelCount/Blob 已验证的平台 ROI 体系同构实现。
///
/// 中立命名；上游 Region 输入仍由 ContourMatchSchema.RoiInput（原厂 InputROI）承担，二者并存：
/// - 本类字段 = 平台 ROI 组合的本地绘制/形状参数继承/掩膜语义（UI 对齐原厂 Tab_ROI Area）
/// - ContourMatchSearchRois = 运行时搜索域二选一（Local 本地文档 / Upstream 上游 Region 连线）
/// 本地文档字段与 ContourMatchSearchRois.LocalId 共用 input.roi.local。
/// </summary>
public static class ContourMatchRoi
{
    public const string ModeId = "input.roi.inheritance-mode";
    public const string Local = "Local";
    public const string Rectangle = "Rectangle";
    public const string RectangleParameters = "RectangleParameters";
    public const string Annulus = "Annulus";
    public const string AnnulusParameters = "AnnulusParameters";
    public const string RectangleSource = "input.roi.rectangle";
    public const string AnnulusSource = "input.roi.annulus";
    public const string AreaType = "input.roi.area-type";
    public const string MaskImage = "input.roi.mask-image";
    public const string OutputMask = "input.roi.output-mask";
    public const string MaskSource = "input.roi.mask-source";
    public const string MaskInherited = "input.roi.mask-inherited";

    public const string LocalDocumentId = "input.roi.local";
    public const int MaximumInherited = RoiDocumentValue.MaximumInheritedCount;

    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("contourmatch.roi.rectangle", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("width", DataType.Number), new Port("height", DataType.Number),
        new Port("angle-degrees", DataType.Number)
    });
    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("contourmatch.roi.annulus", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("inner-radius", DataType.Number), new Port("outer-radius", DataType.Number),
        new Port("start-degrees", DataType.Number), new Port("sweep-degrees", DataType.Number)
    });
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);

    /// <summary>ROI 相关输入字段；本地文档字段由 ContourMatchSearchRois.LocalPort 声明，此处不重复。</summary>
    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(ModeId, DataType.Text, Required: false, EditModes: FieldEditModes.Constant),
        new Port(RectangleSource, RectangleType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(AnnulusSource, AnnulusType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(AreaType, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new Port(MaskImage, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(OutputMask, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant),
        new Port(MaskSource, PolygonStrings, Required: false, EditModes: FieldEditModes.Reference),
        new Port(MaskInherited, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant)
    }.Concat(new[] { "center-x", "center-y", "width", "height", "angle-degrees" }
        .Select(id => new Port(RectangleSource + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)))
     .Concat(new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }
        .Select(id => new Port(AnnulusSource + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)))
     .ToArray();
}
