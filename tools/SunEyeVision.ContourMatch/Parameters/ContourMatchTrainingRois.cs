using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配模板训练区域。原厂把训练样本放在模板对话框的 SignList（正样本/背景，见
/// docs/07-工具逆向/decompiled/ModuleControls/IMVSContourMatchModuControl/IMVSContourMatchModuControl/SignViewItem.cs:3-24
/// 的 IsBackgroundImage，以及 hppyramidfeaturepatmatchdialoguistyle.xaml:798-855 的可见/标记/删除行）。
/// 本方复用平台 <see cref="RoiDocumentValue"/>：inspection 组=正样本区域，mask 组=背景/屏蔽区域。
/// 正样本合并为训练域，背景样本从训练域扣除；空正样本直接报错，不生成空模板。
/// </summary>
public static class ContourMatchTrainingRois
{
    public const string InputId = "template.training-rois";
    public const int MaximumPositiveCount = 16;
    public const int MaximumBackgroundCount = 8;

    public static Port Port { get; } = new(InputId, RoiDocumentValue.Declaration,
        Required: false, EditModes: FieldEditModes.Constant);

    /// <summary>结构校验之后追加容量规则；空正样本不是校验错误（可能尚未编辑），由训练入口单独 Fail-Fast。</summary>
    public static void Validate(RecordValue root)
    {
        RoiDocumentValue.Validate(root);
        var positive = RoiDocumentValue.Items(root, RoiDocumentValue.Inspection).Count;
        var background = RoiDocumentValue.Items(root, RoiDocumentValue.Mask).Count;
        if (positive > MaximumPositiveCount)
            throw new ArgumentException($"正样本区域最多 {MaximumPositiveCount} 个。");
        if (background > MaximumBackgroundCount)
            throw new ArgumentException($"背景样本区域最多 {MaximumBackgroundCount} 个。");
    }

    /// <summary>
    /// 把样本文档转成 HALCON 训练域：正样本合并，背景样本从结果中扣除。
    /// 返回 null 表示没有正样本；调用方必须据此明确报错而不是退化成全图训练。
    /// 形状不受支持时直接抛异常（rule-015，禁止静默忽略）。
    /// </summary>
    public static HRegion? BuildDomain(RecordValue root, int imageWidth, int imageHeight)
    {
        ArgumentNullException.ThrowIfNull(root);
        Validate(root);
        var positive = RoiDocumentValue.Items(root, RoiDocumentValue.Inspection)
            .Where(item => item.Get<bool>("enabled"))
            .Select(item => item.Get<RecordValue>("shape"))
            .ToArray();
        if (positive.Length == 0) return null;

        HRegion? domain = null;
        try
        {
            foreach (var shape in positive)
            {
                var region = ShapeRegion(shape, imageWidth, imageHeight);
                domain = domain is null ? region : Union(domain, region);
            }
            foreach (var item in RoiDocumentValue.Items(root, RoiDocumentValue.Mask))
            {
                if (!item.Get<bool>("enabled")) continue;
                var background = ShapeRegion(item.Get<RecordValue>("shape"), imageWidth, imageHeight);
                var reduced = domain!.Difference(background);
                background.Dispose();
                domain.Dispose();
                domain = reduced;
            }
            return domain;
        }
        catch
        {
            domain?.Dispose();
            throw;
        }
    }

    private static HRegion ShapeRegion(RecordValue shape, int imageWidth, int imageHeight)
    {
        RoiDocumentValue.ValidateShape(shape);
        var kind = shape.Get<string>("kind");
        switch (kind)
        {
            case "rectangle":
            {
                var center = shape.Get<RecordValue>("center");
                double cx = center.Get<double>("x"), cy = center.Get<double>("y");
                double width = shape.Get<double>("width"), height = shape.Get<double>("height");
                double angleDegrees = shape.Get<double>("angle-degrees");
                // HALCON 行列坐标与屏幕 x/y 互换；角度按 Tools/RegionConverter 的屏幕→HALCON 取负约定。
                double phi = -angleDegrees * Math.PI / 180d;
                using var row = new HTuple(cy);
                using var column = new HTuple(cx);
                using var phiTuple = new HTuple(phi);
                using var length1 = new HTuple(width / 2d);
                using var length2 = new HTuple(height / 2d);
                HOperatorSet.GenRectangle2(out var region, row, column, phiTuple, length1, length2);
                return new HRegion(region);
            }
            case "annulus":
            case "sector":
            {
                var center = shape.Get<RecordValue>("center");
                double cx = center.Get<double>("x"), cy = center.Get<double>("y");
                double inner = shape.Get<double>("inner-radius"), outer = shape.Get<double>("outer-radius");
                double start = shape.Get<double>("start-radians"), end = shape.Get<double>("end-radians");
                var sweep = end - start;
                bool full = Math.Abs(Math.Abs(sweep) - 2 * Math.PI) < 1e-9;
                // 屏幕顺时针区间 [start,end] 映射到数学坐标 [-end,-start]；整圆必须用 GenCircle，
                // GenCircleSector(0,2pi) 在 HALCON 中为空（对齐 ToolRegionHelper 的既有约定）。
                double mathStart = NormalizeAngle(-end), mathEnd = NormalizeAngle(-start);
                using var outerRegion = full
                    ? new HRegion(cy, cx, outer)
                    : new HRegion(cy, cx, outer, mathStart, mathEnd);
                if (inner <= 0) return outerRegion.Clone();
                using var innerRegion = new HRegion(cy, cx, inner);
                return outerRegion.Difference(innerRegion);
            }
            case "polygon":
            {
                var points = shape.Get<SequenceValue>("vertices").Cast<RecordValue>()
                    .Select(point => new[] { point.Get<double>("y"), point.Get<double>("x") })
                    .ToList();
                // GenRegionPolygon 不自动闭合首尾顶点。
                if (points.Count >= 3) points.Add(points[0]);
                using var rows = new HTuple(points.Select(p => p[0]).ToArray());
                using var columns = new HTuple(points.Select(p => p[1]).ToArray());
                HOperatorSet.GenRegionPolygon(out var region, rows, columns);
                HOperatorSet.FillUp(region, out var filled);
                region.Dispose();
                return new HRegion(filled);
            }
            default:
                throw new NotSupportedException($"训练区域形状 {kind} 不支持。");
        }
    }

    private static double NormalizeAngle(double angle)
        => (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);

    private static HRegion Union(HRegion left, HRegion right)
    {
        HOperatorSet.Union2(left, right, out var merged);
        left.Dispose();
        right.Dispose();
        return new HRegion(merged);
    }

    /// <summary>供训练入口使用的完整图像域（没有正样本时返回图像域，由调用方 Fail-Fast 拒绝）。</summary>
    public static HRegion FullDomain(int imageWidth, int imageHeight)
    {
        using var width = new HTuple(imageWidth);
        using var height = new HTuple(imageHeight);
        HOperatorSet.GenRectangle1(out var region, 0, 0, height, width);
        return new HRegion(region);
    }
}
