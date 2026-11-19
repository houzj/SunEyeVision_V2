using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class BlobResultGeometry
{
    private const int MaximumRasterContours = 256;
    private const int MaximumRasterVertices = 65536;
    private static readonly TypeRef Vertices = RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type;

    internal static ResultGeometrySnapshot Capture(RecordValue? roi, IReadOnlyImage image, bool passed,
        HRegion domain, HRegion inspection, HRegion mask, RecordValue? pose, bool pixelInspection, bool pixelMask,
        SequenceValue maskPolygons, CancellationToken cancellation, RecordValue? fixture)
    {
        var definition = roi ?? BlobLocalRoi.Empty;
        var inspectionCoverage = Coverage(BlobLocalRoi.Inspection);
        var inspectionItems = Enabled(BlobLocalRoi.Inspection);
        var rings = inspectionItems.Where(item => item.Get<RecordValue>("shape").Get<string>("kind") is "annulus" or "sector").ToArray();
        var ordinary = inspectionItems.Except(rings).ToArray();
        var groups = new List<ResultGeometryGroup>();
        if (pixelInspection)
        {
            groups.Add(Raster(ResultGeometryRole.Inspection, inspection));
            groups.Add(Empty(ResultGeometryRole.Annulus));
        }
        else if (roi is not null && inspectionCoverage == ResultGeometryCoverage.FullDomain)
        {
            // The original full-screen selection publishes a nominal rectangle, independent of the threshold work domain.
            var rectangle = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "rectangle",
                ["center"] = new PointGeometry(BlobSchema.ImageFrame, new(image.Width / 2d, image.Height / 2d)),
                ["width"] = (double)image.Width,
                ["height"] = (double)image.Height,
                ["angle-degrees"] = 0d
            });
            groups.Add(new(ResultGeometryRole.Inspection, ResultGeometryCoverage.Explicit, new[] { rectangle }));
            groups.Add(Empty(ResultGeometryRole.Annulus));
        }
        else if (pose is not null && inspectionCoverage == ResultGeometryCoverage.Explicit)
        {
            groups.Add(Corrected(ResultGeometryRole.Inspection, ordinary));
            groups.Add(Corrected(ResultGeometryRole.Annulus, rings));
        }
        else
        {
            groups.Add(Nominal(ResultGeometryRole.Inspection, inspectionCoverage, ordinary));
            groups.Add(Nominal(ResultGeometryRole.Annulus, ResultGeometryCoverage.Explicit, rings));
        }
        if (pixelMask)
            groups.Add(Raster(ResultGeometryRole.Mask, mask));
        else if (maskPolygons.Count != 0)
            groups.Add(new(ResultGeometryRole.Mask, ResultGeometryCoverage.Explicit, maskPolygons.Cast<PolygonGeometry>().Select(PolygonShape)));
        else
            groups.Add(Empty(ResultGeometryRole.Mask));
        if (pose is not null && fixture is not null)
        {
            groups.Add(FixturePoint(ResultGeometryRole.ReferencePoint, fixture.Get<RecordValue>("reference")));
            groups.Add(FixturePoint(ResultGeometryRole.RunPoint, fixture.Get<RecordValue>("run")));
        }
        return new(image.Frame, BlobSchema.ImageFrame, passed, groups);

        ResultGeometryCoverage Coverage(string group) => BlobLocalRoi.Coverage(definition, group) switch
        {
            BlobLocalRoi.FullDomain => ResultGeometryCoverage.FullDomain,
            BlobLocalRoi.None => ResultGeometryCoverage.None,
            _ => ResultGeometryCoverage.Explicit
        };
        RecordValue[] Enabled(string group) => BlobLocalRoi.Items(definition, group).Where(item => item.Get<bool>("enabled")).ToArray();
        ResultGeometryGroup Raster(ResultGeometryRole role, HRegion region)
        {
            using var clipped = region.Intersection(domain);
            return ReadContours(role, clipped, cancellation);
        }
        ResultGeometryGroup Corrected(ResultGeometryRole role, RecordValue[] items)
        {
            if (items.Length == 0) return Empty(role);
            if (TryCorrectNominal(items, pose!, out var shapes))
                return new(role, ResultGeometryCoverage.Explicit, shapes);
            if (items.Length == inspectionItems.Length) return Raster(role, inspection);
            // Reuse the invocation rasterizer for mixed inspection roles, preserving their independent display switches.
            var subset = BlobLocalRoi.Root(BlobLocalRoi.Group(BlobLocalRoi.Explicit, items),
                BlobLocalRoi.Group(BlobLocalRoi.None, Array.Empty<RecordValue>()));
            using var original = BlobLocalRoiRasterizer.Build(subset, BlobLocalRoi.Inspection, domain, cancellation,
                clipToInputDomain: false);
            using var corrected = HalconResourceBridge.TransformRegion(original, pose);
            return Raster(role, corrected);
        }
    }

    private static ResultGeometryGroup Empty(ResultGeometryRole role) => new(role, ResultGeometryCoverage.None, Array.Empty<RecordValue>());

    private static bool TryCorrectNominal(RecordValue[] items, RecordValue pose, out RecordValue[] shapes)
    {
        shapes = [];
        var a = pose.Get<double>("m11");
        var b = pose.Get<double>("m12");
        var c = pose.Get<double>("m21");
        var d = pose.Get<double>("m22");
        var scale = double.Hypot(a, b);
        // Only orientation-preserving similarities retain both rectangle and circular-sector semantics.
        var tolerance = scale * 1e-12;
        if (scale <= 0 || !double.IsFinite(scale) || Math.Abs(a - d) > tolerance || Math.Abs(b + c) > tolerance)
            return false;
        var rotation = Math.Atan2(b, a);
        var offsetX = pose.Get<double>("offset-x");
        var offsetY = pose.Get<double>("offset-y");
        Point2 Transform(Point2 point) => new(point.X * a + point.Y * c + offsetX, point.X * b + point.Y * d + offsetY);
        var result = new List<RecordValue>(items.Length);
        foreach (var item in items)
        {
            var shape = BlobRoiDisplayProjection.Shape(item.Get<RecordValue>("shape"));
            var kind = shape.Get<string>("kind");
            if (kind == "polygon")
            {
                var polygon = new PolygonGeometry(BlobSchema.ImageFrame,
                    shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(point => Transform(point.Point)));
                result.Add(PolygonShape(polygon));
                continue;
            }
            var fields = shape.CopyFields().ToDictionary(pair => pair.Key, pair => pair.Value);
            fields["center"] = new PointGeometry(BlobSchema.ImageFrame, Transform(shape.Get<PointGeometry>("center").Point));
            if (kind == "rectangle")
            {
                fields["width"] = shape.Get<double>("width") * scale;
                fields["height"] = shape.Get<double>("height") * scale;
                fields["angle-degrees"] = BlobLocalRoi.NormalizeDegrees(shape.Get<double>("angle-degrees") + rotation * 180d / Math.PI);
            }
            else
            {
                var start = shape.Get<double>("start-radians");
                var sweep = shape.Get<double>("end-radians") - start;
                var correctedStart = Math.IEEERemainder(start, 2 * Math.PI) + rotation;
                fields["start-radians"] = correctedStart;
                fields["end-radians"] = correctedStart + sweep;
                fields["inner-radius"] = shape.Get<double>("inner-radius") * scale;
                fields["outer-radius"] = shape.Get<double>("outer-radius") * scale;
            }
            result.Add(new(RoiDeclarations.Shape, fields));
        }
        shapes = result.ToArray();
        return true;
    }

    private static RecordValue PolygonShape(PolygonGeometry polygon) => new(RoiDeclarations.Shape, new Dictionary<string, object?>
    {
        ["kind"] = "polygon",
        ["closed"] = true,
        ["vertices"] = new SequenceValue(Vertices, polygon.Vertices.Select(point => new PointGeometry(polygon.Frame, point)))
    });

    private static ResultGeometryGroup FixturePoint(ResultGeometryRole role, RecordValue pose)
    {
        var point = pose.Get<RecordValue>("point");
        var shape = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
        {
            ["kind"] = "point", ["center"] = new PointGeometry(BlobSchema.ImageFrame,new(point.Get<double>("x"),point.Get<double>("y")))
        });
        return new(role, ResultGeometryCoverage.Explicit, new[] { shape });
    }

    private static ResultGeometryGroup Nominal(ResultGeometryRole role, ResultGeometryCoverage coverage, RecordValue[] items)
        => coverage == ResultGeometryCoverage.Explicit && items.Length == 0 ? Empty(role)
            : new(role, coverage, items.Select(item => BlobRoiDisplayProjection.Shape(item.Get<RecordValue>("shape"))));

    private static ResultGeometryGroup ReadContours(ResultGeometryRole role, HRegion region, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        HOperatorSet.AreaCenter(region, out var area, out var centerRow, out var centerColumn);
        using (area) using (centerRow) using (centerColumn)
            if (area.Length == 0 || area.LArr.All(value => value == 0)) return Empty(role);

        HOperatorSet.GenContourRegionXld(region, out var contours, "border_holes");
        using var contourOwner = contours;
        HOperatorSet.CountObj(contours, out var count);
        using var countOwner = count;
        if (count.I > MaximumRasterContours) return Unavailable();
        var shapes = new List<RecordValue>();
        var remaining = MaximumRasterVertices;
        for (var index = 1; index <= count.I; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.SelectObj(contours, out var selected, index);
            using var contour = selected;
            HOperatorSet.GetContourXld(contour, out var rows, out var columns);
            using var rowOwner = rows;
            using var columnOwner = columns;
            if (rows.Length != columns.Length || rows.Length > remaining) return Unavailable();
            remaining -= rows.Length;
            var points = new PointGeometry[rows.Length];
            for (var point = 0; point < points.Length; point++)
            {
                if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                points[point] = new(BlobSchema.ImageFrame, new(columns[point].D, rows[point].D));
            }
            if (points.Select(point => point.Point).Distinct().Take(3).Count() < 3) return Unavailable();
            shapes.Add(new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "polygon", ["vertices"] = new SequenceValue(Vertices, points), ["closed"] = true
            }));
        }
        return shapes.Count == 0 ? Unavailable() : new(role, ResultGeometryCoverage.Explicit, shapes, outlineOnly: true);
        ResultGeometryGroup Unavailable() => new(role, ResultGeometryCoverage.Unavailable, Array.Empty<RecordValue>());
    }
}
