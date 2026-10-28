using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

/// <summary>Admission limits, never truncation limits. Canonical resources are not changed.</summary>
public sealed record NativeResultRasterLimits(int MaximumRasterBytes = 134217728, int MaximumRuns = 1000000,
    int MaximumRunBytes = 24000000)
{
    internal void Validate()
    {
        if (MaximumRasterBytes <= 0 || MaximumRuns <= 0 || MaximumRunBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(NativeResultRasterLimits));
    }
}

internal sealed class NativeProjectionRetiredException : OperationCanceledException { }

/// <summary>Exact inclusive RLE to straight-alpha BGRA, consumed by the original image-layer visual.</summary>
internal static class NativeResultRaster
{
    internal static void CheckCurrent(Func<bool> current)
    { if (!current()) throw new NativeProjectionRetiredException(); }

    internal static int BufferLength(int width, int height, NativeResultRasterLimits limits)
    {
        limits.Validate();
        var length = checked(checked(width * height) * 4);
        if (width <= 0 || height <= 0 || length > limits.MaximumRasterBytes)
            throw new ArgumentException("region-budget: 完整显示缓冲超过准入预算。");
        return length;
    }

    internal static RegionRun[] Read(IReadOnlyRegion region, ImageFrameAddress frame, CoordinateFrame coordinates,
        int width, int height, NativeResultRasterLimits limits, Func<bool> current)
    {
        CheckCurrent(current);
        if (region.SourceFrame != frame || region.Frame != coordinates)
            throw new ArgumentException("区域元信息与当前物理帧或坐标系不一致。");
        CheckCurrent(current);
        return ReadOwned(region.AcquireReader(), frame, coordinates, width, height, limits, current);
    }

    internal static RegionRun[] ReadOwned(IRegionReadLease reader, ImageFrameAddress frame, CoordinateFrame coordinates,
        int width, int height, NativeResultRasterLimits limits, Func<bool> current)
    {
        RegionRun[]? result = null; Exception? failure = null;
        try { result = Read(reader, frame, coordinates, width, height, limits, current); }
        catch (Exception error) { failure = error; }
        try { reader.Dispose(); }
        catch (Exception cleanup) { failure = failure is null ? cleanup : new AggregateException(failure, cleanup); }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result!;
    }

    internal static RegionRun[] Read(IRegionReadLease reader, ImageFrameAddress frame, CoordinateFrame coordinates,
        int width, int height, NativeResultRasterLimits limits, Func<bool> current)
    {
        limits.Validate(); CheckCurrent(current);
        if (reader.SourceFrame != frame || reader.Frame != coordinates)
            throw new ArgumentException("区域读取租约与当前物理帧或坐标系不一致。");
        var count = reader.RunCount;
        if (count < 0 || count > limits.MaximumRuns || checked((long)count * 12) > limits.MaximumRunBytes)
            throw new ArgumentException("region-budget: 完整区域超过准入预算。");
        if (reader.IsEmpty != (count == 0)) throw new ArgumentException("区域空值与行程数不一致。");
        CheckCurrent(current);
        var runs = new RegionRun[count]; var offset = 0;
        while (offset < count)
        {
            CheckCurrent(current);
            var requested = Math.Min(ResourceContractRules.MaximumRegionBatch, count - offset);
            var batch = reader.ReadRuns(offset, requested);
            CheckCurrent(current);
            if (batch.Length == 0 || batch.Length > requested)
                throw new ArgumentException("区域读取提前结束或返回超过请求的批次。");
            foreach (var run in batch.Span)
            {
                if (run.Row < 0 || run.Row >= height || run.FirstColumn < 0 || run.LastColumnInclusive < run.FirstColumn
                    || run.LastColumnInclusive >= width)
                    throw new ArgumentException("区域行程超出当前图像像素范围。");
                if (offset > 0 && (run.Row < runs[offset - 1].Row
                    || run.Row == runs[offset - 1].Row && run.FirstColumn <= runs[offset - 1].LastColumnInclusive))
                    throw new ArgumentException("区域行程跨批失序或重叠。");
                runs[offset++] = run;
            }
        }
        if (reader.RunCount != count || reader.SourceFrame != frame || reader.Frame != coordinates)
            throw new ArgumentException("冻结区域在读取期间发生变化。");
        CheckCurrent(current); return runs;
    }

    internal static byte[] Paint(int width, int height, IReadOnlyList<RegionRun> runs, Color color,
        NativeResultRasterLimits limits, Func<bool> current)
    {
        var pixels = new byte[BufferLength(width, height, limits)];
        foreach (var run in runs)
        {
            CheckCurrent(current);
            for (var x = run.FirstColumn; x <= run.LastColumnInclusive; x++)
            {
                var i = (run.Row * width + x) * 4;
                pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = color.A;
            }
        }
        CheckCurrent(current); return pixels;
    }
}
