using HalconDotNet;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// 同一次调用产生的叠加几何：检测区域、扇环检测区域、屏蔽区、基准点/运行点。
/// 光栅区域按轮廓多边形投影，超过预算即整体标为不可用，不截断成“看起来正确”的图。
/// </summary>
internal static class BlobLabelsResultGeometry
{
    private const int MaximumContours = 128;
    private const int MaximumVertices = 400_000;
    private static readonly TypeRef Vertices = RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type;

    internal static ResultGeometrySnapshot Capture(IReadOnlyImage image, bool passed, HRegion domain,
        HRegion? inspection, HRegion? annulus, HRegion? mask, RecordValue? fixture, CancellationToken cancellation)
    {
        var groups = new List<ResultGeometryGroup>
        {
            inspection is null
                ? new(ResultGeometryRole.Inspection, ResultGeometryCoverage.FullDomain, Array.Empty<RecordValue>())
                : Raster(ResultGeometryRole.Inspection, inspection, domain, cancellation),
            annulus is null
                ? new(ResultGeometryRole.Annulus, ResultGeometryCoverage.Explicit, Array.Empty<RecordValue>())
                : Raster(ResultGeometryRole.Annulus, annulus, domain, cancellation),
            mask is null
                ? new(ResultGeometryRole.Mask, ResultGeometryCoverage.Explicit, Array.Empty<RecordValue>())
                : Raster(ResultGeometryRole.Mask, mask, domain, cancellation)
        };
        if (fixture is not null)
        {
            groups.Add(FixturePoint(ResultGeometryRole.ReferencePoint, fixture.Get<RecordValue>("reference")));
            groups.Add(FixturePoint(ResultGeometryRole.RunPoint, fixture.Get<RecordValue>("run")));
        }
        return new(image.Frame, BlobLabelsSchema.ImageFrame, passed, groups);
    }

    private static ResultGeometryGroup Raster(ResultGeometryRole role, HRegion region, HRegion domain, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var clipped = region.Intersection(domain);
        HOperatorSet.AreaCenter(clipped, out var area, out var centerRow, out var centerColumn);
        using (area) using (centerRow) using (centerColumn)
            if (area.Length == 0 || area.LArr.All(value => value == 0))
                return new(role, ResultGeometryCoverage.Explicit, Array.Empty<RecordValue>());

        HOperatorSet.GenContourRegionXld(clipped, out var contours, "border_holes");
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
                points[point] = new(BlobLabelsSchema.ImageFrame, new(columns[point].D, rows[point].D));
            }
            if (points.Select(point => point.Point).Distinct().Take(3).Count() < 3) return Unavailable(role);
            shapes.Add(new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "polygon",
                ["vertices"] = new SequenceValue(Vertices, points),
                ["closed"] = true
            }));
        }
        return shapes.Count == 0 ? Unavailable(role)
            : new(role, ResultGeometryCoverage.Explicit, shapes, outlineOnly: true);
    }

    private static ResultGeometryGroup Unavailable(ResultGeometryRole role)
        => new(role, ResultGeometryCoverage.Unavailable, Array.Empty<RecordValue>());

    private static ResultGeometryGroup FixturePoint(ResultGeometryRole role, RecordValue pose)
    {
        var point = pose.Get<RecordValue>("point");
        var shape = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
        {
            ["kind"] = "point",
            ["center"] = new PointGeometry(BlobLabelsSchema.ImageFrame, new(point.Get<double>("x"), point.Get<double>("y")))
        });
        return new(role, ResultGeometryCoverage.Explicit, new[] { shape });
    }
}
