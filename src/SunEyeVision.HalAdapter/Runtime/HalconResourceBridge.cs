using System.Runtime.InteropServices;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.HalAdapter;

/// <summary>Vendor boundary only. Public plugin contracts carry readers, never HImage/HRegion.</summary>
public static class HalconResourceBridge
{
    public static HRegion TransformRegion(HRegion source, RecordValue? pose)
    {
        if (pose is null) return source.Clone();
        SpatialDeclarations.ValidateAffine2D(pose);
        // Contract: X=column/Y=row, pixel centers. HALCON: X=row/Y=column, pixel corners.
        double a = pose.Get<double>("m22"), b = pose.Get<double>("m12"),
            d = pose.Get<double>("m21"), e = pose.Get<double>("m11");
        using var homography = new HTuple(new[] { a, b, pose.Get<double>("offset-y") + .5 - .5 * (a + b),
            d, e, pose.Get<double>("offset-x") + .5 - .5 * (d + e) });
        return CreateWithoutImplicitClipping(() =>
        {
            HOperatorSet.AffineTransRegion(source, out var transformed, homography, "nearest_neighbor");
            using (transformed) return new HRegion(transformed);
        });
    }


    public static HImage CopyImage(IReadOnlyImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = source.AcquirePixels();
        var layout = reader.Layout;
        int channels = layout.BytesPerPixel;
        int rowBytes = checked(layout.Width * channels);
        var packed = new byte[checked(rowBytes * layout.Height)];
        for (int row = 0; row < layout.Height; row++)
        {
            var input = reader.GetRow(row);
            if (input.Length != layout.RowStrideBytes) throw new InvalidDataException("Pixel row length differs from its layout.");
            input[..rowBytes].CopyTo(packed.AsSpan(row * rowBytes, rowBytes));
        }
        using var domainReader = reader.AcquireDomainReader();
        if (domainReader.SourceFrame != reader.Frame) throw new InvalidDataException("Image and actual Domain have different source frames.");
        using var domain = CopyRegion(domainReader, layout.Width, layout.Height);
        var pointer = Marshal.AllocHGlobal(packed.Length);
        try
        {
            Marshal.Copy(packed, 0, pointer, packed.Length);
            using var full = new HImage();
            if (channels == 1) full.GenImage1("byte", layout.Width, layout.Height, pointer);
            else
            {
                var order = layout.Format switch { PixelLayout.Rgb24 => "rgb", PixelLayout.Bgr24 => "bgr", _ => "bgrx" };
                full.GenImageInterleaved(pointer, order, layout.Width, layout.Height, 0, "byte", 0, 0, 0, 0, -1, 0);
            }
            if (channels == 4)
            {
                var alpha = new byte[checked(layout.Width * layout.Height)];
                for (int i = 0; i < alpha.Length; i++) alpha[i] = packed[i * 4 + 3];
                var alphaPointer = Marshal.AllocHGlobal(alpha.Length);
                try
                {
                    Marshal.Copy(alpha, 0, alphaPointer, alpha.Length);
                    using var alphaImage = new HImage(); alphaImage.GenImage1("byte", layout.Width, layout.Height, alphaPointer);
                    HOperatorSet.AppendChannel(full, alphaImage, out var appended);
                    using (appended) using (var rgba = new HImage(appended)) return rgba.ReduceDomain(domain);
                }
                finally { Marshal.FreeHGlobal(alphaPointer); }
            }
            return full.ReduceDomain(domain);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    public static HRegion CopyRegion(IReadOnlyRegion source, ImageFrameAddress expectedSource, int width, int height)
    {
        using var reader = source.AcquireReader();
        if (reader.SourceFrame != expectedSource) throw new InvalidDataException("Region source frame does not match the input image.");
        return CopyRegion(reader, width, height);
    }

    private static HRegion CopyRegion(IRegionReadLease reader, int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (reader.Frame.Domain != CoordinateDomain.Image || reader.Frame.Unit != CoordinateUnit.Pixel)
            throw new InvalidDataException("Blob region must use image pixel coordinates.");
        if (reader.RunCount < 0 || reader.RunCount > (long)width * height)
            throw new InvalidDataException("Region run count exceeds the pixel grid.");
        var runs = new List<RegionRun>();
        for (int offset = 0; offset < reader.RunCount;)
        {
            var batch = reader.ReadRuns(offset, Math.Min(4096, reader.RunCount - offset));
            if (batch.Length == 0 || batch.Length > Math.Min(4096, reader.RunCount - offset))
                throw new InvalidDataException("Region reader made no progress or exceeded its requested batch.");
            foreach (var run in batch.Span)
            {
                if (run.Row >= height || run.LastColumnInclusive >= width) throw new InvalidDataException("Region extends beyond the image grid.");
                runs.Add(run);
            }
            offset += batch.Length;
        }
        ResourceContractRules.ValidateRuns(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(runs));
        if (runs.Count == 0)
        {
            HOperatorSet.GenEmptyRegion(out var empty);
            using (empty) return new HRegion(empty);
        }
        using var rows = new HTuple(runs.Select(r => r.Row).ToArray());
        using var starts = new HTuple(runs.Select(r => r.FirstColumn).ToArray());
        using var ends = new HTuple(runs.Select(r => r.LastColumnInclusive).ToArray());
        return CreateWithoutImplicitClipping(() =>
        {
            HOperatorSet.GenRegionRuns(out var value, rows, starts, ends);
            using (value) return new HRegion(value);
        });
    }

    // Region runs carry an explicit validated pixel grid. Native creation must not
    // clip them to the previous image size of this worker thread. The same rule
    // applies to affine results before the caller's explicit target-domain intersection.
    /// <summary>Creates a region with thread-local clipping disabled until it returns.</summary>
    public static HRegion CreateWithoutImplicitClipping(Func<HRegion> create)
    {
        HOperatorSet.GetSystem("tsp_clip_region", out var previous);
        HRegion? result = null;
        Exception? failure = null;
        try
        {
            HOperatorSet.SetSystem("tsp_clip_region", "false");
            result = create();
            return result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            var cleanup = new List<Exception>();
            try { HOperatorSet.SetSystem("tsp_clip_region", previous); }
            catch (Exception error) { cleanup.Add(error); }
            try { previous.Dispose(); }
            catch (Exception error) { cleanup.Add(error); }
            if (cleanup.Count != 0)
            {
                if (result is not null)
                    try { result.Dispose(); } catch (Exception error) { cleanup.Add(error); }
                if (failure is not null) cleanup.Insert(0, failure);
                throw new AggregateException("区域创建后的线程状态或资源恢复失败。", cleanup);
            }
        }
    }

    public static FrozenRegionResource CaptureRegion(HRegion source, CoordinateFrame coordinates, ImageFrameAddress frame)
        => CaptureRegionCore(source, runs => new FrozenRegionResource(coordinates, frame, runs));

    public static IReadOnlyRegion CaptureRegion(HRegion source, CoordinateFrame coordinates, ImageFrameAddress frame, IOutputResources resources)
        => CaptureRegionCore(source, runs => resources.CreateRegion(coordinates, frame, runs));

    private static T CaptureRegionCore<T>(HRegion source, Func<RegionRun[], T> create)
    {
        using var union = source.Union1();
        union.GetRegionRuns(out var rows, out var first, out var last);
        using (rows) using (first) using (last)
        {
            if (rows.Length != first.Length || rows.Length != last.Length) throw new InvalidDataException("HALCON returned inconsistent region runs.");
            var runs = new RegionRun[rows.Length];
            for (int i = 0; i < runs.Length; i++) runs[i] = new(rows[i].I, first[i].I, last[i].I);
            return create(runs);
        }
    }
}
