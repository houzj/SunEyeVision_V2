using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>
/// ROI 记录形状与平台几何之间的标准映射：矩形、扇环、多边形三类。
/// 对应原厂平台的 InputItem：按字段把形状拆成参数值，或把参数值拼回形状。
/// </summary>
public static class RoiShapeProjection
{
    /// <summary>记录形状 → 平台几何。角度归一化后输出，多边形自动闭合。</summary>
    public static RecordValue ToNative(RecordValue shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        RoiDocumentValue.ValidateShape(shape);
        var fields = shape.CopyFields().ToDictionary(pair => pair.Key, pair => pair.Value);
        if (shape.Contains("angle-degrees"))
            fields["angle-degrees"] = RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees"));
        PointGeometry Point(RecordValue point)
            => new(RoiDeclarations.ImageFrame, new(point.Get<double>("x"), point.Get<double>("y")));
        if (shape.Contains("center")) fields["center"] = Point(shape.Get<RecordValue>("center"));
        if (shape.Contains("vertices"))
        {
            fields["vertices"] = new SequenceValue(
                RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type,
                shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(point => (object?)Point(point)));
            fields["closed"] = true;
        }
        var projected = new RecordValue(RoiDeclarations.Shape, fields);
        RoiGeometryEditing.Validate(projected);
        return projected;
    }

    /// <summary>平台几何 → 记录形状。矩形角度沿用 previous 的原始值，避免反复归一化导致的抖动。</summary>
    public static RecordValue FromNative(RecordValue shape, RecordValue? previous = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        RoiGeometryEditing.Validate(shape);
        var fields = shape.CopyFields().Where(pair => pair.Key != "closed")
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (shape.Contains("center"))
        {
            var point = shape.Get<PointGeometry>("center").Point;
            fields["center"] = RoiDocumentValue.Point(point.X, point.Y);
        }
        if (shape.Contains("vertices"))
            fields["vertices"] = new SequenceValue(
                RoiDocumentValue.ShapeType.Fields.Single(port => port.Id == "vertices").Type,
                shape.Get<SequenceValue>("vertices").Cast<PointGeometry>()
                    .Select(point => (object?)RoiDocumentValue.Point(point.Point.X, point.Point.Y)));
        if (previous?.Get<string>("kind") == "rectangle" && shape.Get<string>("kind") == "rectangle"
            && RoiDocumentValue.NormalizeDegrees(previous.Get<double>("angle-degrees"))
                == RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees")))
            fields["angle-degrees"] = previous.Get<double>("angle-degrees");
        var result = new RecordValue(RoiDocumentValue.ShapeType, fields);
        RoiDocumentValue.ValidateShape(result);
        return result;
    }
}
