using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed partial class BlobSession
{
    private sealed record NativeLoop(long Id, Point2[] Points, HRegion Filled, double Area,
        double MinX, double MinY, double MaxX, double MaxY);

    private static BlobBoundaryPath[] ReadBoundary(HRegion source, CancellationToken cancellation)
    {
        // border_holes preserves every unsmoothed outer/hole contour. Region remains area truth.
        HOperatorSet.GenContourRegionXld(source, out var contours, "border_holes");
        using var xld = contours;
        HOperatorSet.CountObj(xld, out var count);
        using var n = count;
        var loops = new List<NativeLoop>();
        try
        {
            for (int i = 1; i <= count.I; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(xld, out var selected, i);
                using var single = selected;
                HOperatorSet.GetContourXld(single, out var rows, out var columns);
                using var r = rows; using var c = columns;
                if (rows.Length != columns.Length || rows.Length == 0)
                    throw new InvalidDataException("HALCON returned an invalid boundary path.");
                var points = new Point2[rows.Length];
                for (int p = 0; p < points.Length; p++) points[p] = new(columns[p].D, rows[p].D);
                HOperatorSet.GenRegionContourXld(single, out var fillObject, "filled");
                using var nativeFill = fillObject;
                var filled = new HRegion(nativeFill);
                try
                {
                    HOperatorSet.AreaCenter(filled, out var area, out var cy, out var cx);
                    using (area) using (cy) using (cx)
                        loops.Add(new(i, points, filled, area.Length == 0 ? 0 : area[0].D,
                            points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y)));
                }
                catch { filled.Dispose(); throw; }
            }
            var parents = new int?[loops.Count];
            for (int i = 0; i < loops.Count; i++)
            {
                var child = loops[i];
                foreach (var j in Enumerable.Range(0, loops.Count).Where(j => loops[j].Area > child.Area).OrderBy(j => loops[j].Area))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var candidate = loops[j];
                    if (candidate.MinX > child.MinX || candidate.MinY > child.MinY || candidate.MaxX < child.MaxX || candidate.MaxY < child.MaxY) continue;
                    // Actual region containment avoids orientation guesses or a centroid outside a concave ring.
                    using var outside = child.Filled.Difference(candidate.Filled);
                    HOperatorSet.AreaCenter(outside, out var area, out var row, out var column);
                    using (area) using (row) using (column)
                        if (area.Length == 0 || area.LArr.All(x => x == 0)) { parents[i] = j; break; }
                }
            }
            var result = new BlobBoundaryPath[loops.Count];
            for (int i = 0; i < loops.Count; i++)
            {
                int depth = 0; var parent = parents[i];
                while (parent is { } index)
                {
                    if (++depth > loops.Count) throw new InvalidDataException("Boundary parent cycle.");
                    parent = parents[index];
                }
                result[i] = new(loops[i].Id, parents[i] is { } p ? loops[p].Id : null,
                    depth % 2 == 0 ? "outer" : "hole", Array.AsReadOnly(loops[i].Points));
            }
            return result;
        }
        finally { foreach (var loop in loops) loop.Filled.Dispose(); }
    }
}
