using System;
using HalconDotNet;

namespace SunEyeVision.HalAdapter;

/// <summary>Shared threshold semantics. All complements are relative to the current effective image domain.</summary>
public static class HalconThresholdOperations
{
    public static HRegion InvertWithinDomain(HImage image, HRegion region)
    {
        using var domain = image.GetDomain();
        return domain.Difference(region);
    }

    // Modes retain the existing serialized values: Binary=0, BinaryInv=1, Trunc=2, ToZero=3, ToZeroInv=4.
    public static HRegion SelectBasic(HImage image, int mode, double threshold)
    {
        if (mode < 0 || mode > 4) throw new ArgumentOutOfRangeException(nameof(mode));
        using var lower = mode == 4 ? new HTuple("min") : new HTuple(threshold);
        using var upper = mode == 4 ? new HTuple(threshold) : new HTuple("max");
        HOperatorSet.Threshold(image, out var selected, lower, upper);
        using var region = new HRegion(selected);
        return mode == 1 ? InvertWithinDomain(image, region) : region.Clone();
    }

    public static HRegion SelectAdaptive(HImage image, int blockSize, double c, bool gaussian)
    {
        // Neighborhood operators need valid context, not a reduced-domain filtering shortcut.
        using var full = image.FullDomain();
        HImage local;
        if (gaussian)
        {
            local = HalconLinearFilters.Apply(full, blockSize, blockSize, 0, 0, true, 0);
        }
        else
        {
            HOperatorSet.MeanImage(full, out var mean, blockSize, blockSize);
            local = new HImage(mean);
        }
        using (local)
        {
            // light: original >= local + offset. C is subtracted from the local threshold.
            HOperatorSet.DynThreshold(image, local, out var selected, -c, "light");
            using var region = new HRegion(selected);
            using var domain = image.GetDomain();
            return region.Intersection(domain);
        }
    }

    public static HImage Render(HImage image, HRegion selection, int mode,
        double threshold, double maxValue, int width, int height, bool invertGray = false)
    {
        if (mode is not (2 or 3 or 4))
        {
            HOperatorSet.RegionToBin(selection, out var binary, maxValue, 0, width, height);
            using (binary) return new HImage(binary);
        }

        using var domain = image.GetDomain();
        using var zero = new HImage();
        zero.GenImageConst(image.GetImageType(), width, height);
        HImage source;
        if (mode == 2)
        {
            HOperatorSet.PaintRegion(selection, image, out var clipped, threshold, "fill");
            using (clipped) source = new HImage(clipped);
        }
        else source = image.Clone();
        using (source)
        using (var retained = source.ReduceDomain(mode == 2 ? domain : selection))
        {
            HOperatorSet.PaintGray(retained, zero, out var pixels);
            HImage output;
            using (pixels) output = new HImage(pixels);
            if (!invertGray) return output;
            using (output)
            using (var inverse = output.InvertImage())
            using (var limited = inverse.ReduceDomain(domain))
            {
                HOperatorSet.PaintGray(limited, zero, out var inverted);
                using (inverted) return new HImage(inverted);
            }
        }
    }
}
