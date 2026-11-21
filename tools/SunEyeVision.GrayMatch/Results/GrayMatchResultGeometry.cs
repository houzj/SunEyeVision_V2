using HalconDotNet;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 同一次调用产生的叠加几何：实际参与搜索的区域（ROI ∩ 有效域，含位置修正后的位移）、
/// 以及位置修正的基准点/运行点。超出预算即整体标为不可用，不截断成“看起来正确”的图。
/// </summary>
internal static class GrayMatchResultGeometry
{
    private const int MaximumContours = 128;
    private const int MaximumVertices = 400_000;
    private static readonly TypeRef Vertices = RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type;

    internal static ResultGeometrySnapshot Capture(IReadOnlyImage image, bool passed, HRegion search,
        RecordValue? fixture, CancellationToken cancellation)
    {
        var groups = new List<ResultGeometryGroup> { Raster(ResultGeometryRole.Inspection, search, cancellation) };
        if (fixture is not null)
        {
            groups.Add(FixturePoint(ResultGeometryRole.ReferencePoint, fixture.Get<RecordValue>("reference")));
            groups.Add(FixturePoint(ResultGeometryRole.RunPoint, fixture.Get<RecordValue>("run")));
        }
        return new(image.Frame, GrayMatchSchema.ImageFrame, passed, groups);
    }

    private static ResultGeometryGroup Raster(ResultGeometryRole role, HRegion region, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        HOperatorSet.AreaCenter(region, out var area, out var centerRow, out var centerColumn);
        using (area) using (centerRow) using (centerColumn)
            if (area.Length == 0 || area.LArr.All(value => value == 0))
                return new(role, ResultGeometryCoverage.Explicit, Array.Empty<RecordValue>());

        HOperatorSet.GenContourRegionXld(region, out var contours, "border_holes");
        using var contourOwner = contours;
        HOperatorSet.CountObj(contours, out var count);
        using var countOwner = count;
        if (count.I > MaximumContours) return Unavailable(role);
        var shapes = new List<RecordValue>();
        var remaining = MaximumVertices;
        for (var index = 1; index <= count.I; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.SelectObj(contours, out var selected, index);
            using var contour = selected;
            HOperatorSet.GetContourXld(contour, out var rows, out var columns);
            using var rowOwner = rows;
            using var columnOwner = columns;
            if (rows.Length != columns.Length || rows.Length > remaining) return Unavailable(role);
            remaining -= rows.Length;
            var points = new PointGeometry[rows.Length];
            for (var point = 0; point < points.Length; point++)
            {
                if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                points[point] = GrayMatchSchema.Point(columns[point].D, rows[point].D);
            }
            if (points.Select(point => point.Point).Distinct().Take(3).Count() < 3) return Unavailable(role);
            shapes.Add(new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "polygon",
                ["vertices"] = new SequenceValue(Vertices, points),
                ["closed"] = true
            }));
        }
        return shapes.Count == 0 ? Unavailable(role) : new(role, ResultGeometryCoverage.Explicit, shapes, outlineOnly: true);
    }

    private static ResultGeometryGroup Unavailable(ResultGeometryRole role)
        => new(role, ResultGeometryCoverage.Unavailable, Array.Empty<RecordValue>());

    private static ResultGeometryGroup FixturePoint(ResultGeometryRole role, RecordValue pose)
    {
        var point = pose.Get<RecordValue>("point");
        var shape = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
        {
            ["kind"] = "point",
            ["center"] = GrayMatchSchema.Point(point.Get<double>("x"), point.Get<double>("y"))
        });
        return new(role, ResultGeometryCoverage.Explicit, new[] { shape });
    }
}
