using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.HalAdapter;

/// <summary>Explicit image admission shared by segmentation and binary morphology. Never normalizes input.</summary>
public static class HalconImageAdmission
{
    public static void RequireBinary(IReadOnlyImage input, CancellationToken cancellation = default)
    {
        ResourceContractRules.ValidateImage(input);
        if (input.Layout.Format != PixelLayout.Gray8)
            throw new ArgumentException("BINARY_INPUT_FORMAT: 不二值化输入必须是原始Gray8图像。");
        using var pixels = input.AcquirePixels();
        using var domain = pixels.AcquireDomainReader();
        for (int offset = 0; offset < domain.RunCount;)
        {
            cancellation.ThrowIfCancellationRequested();
            var runs = domain.ReadRuns(offset, Math.Min(4096, domain.RunCount - offset));
            foreach (var run in runs.Span)
            {
                var row = pixels.GetRow(run.Row);
                for (int column = run.FirstColumn; column <= run.LastColumnInclusive; column++)
                {
                    if ((column & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    if (row[column] is not (0 or 255))
                        throw new ArgumentException("BINARY_INPUT_VALUES: 输入Domain内只允许0/255；请显式连接二值掩码输出。");
                }
            }
            offset += runs.Length;
        }
    }

    public static void RequireBinary(HImage input)
    {
        using var channels = input.CountChannels();
        if (channels.I != 1 || input.GetImageType() != "byte")
            throw new ArgumentException("BINARY_INPUT_FORMAT: 二值形态学只接受原始Gray8图像。");
        using var invalid = input.Threshold(1.0, 254.0);
        using var area = invalid.AreaCenter(out HTuple row, out HTuple column);
        using (row) using (column)
            if (area.D > 0) throw new ArgumentException("BINARY_INPUT_VALUES: 输入Domain内只允许0/255；请连接BinaryMask。");
    }

    public static HImage Gray(HImage input)
    {
        using var count = input.CountChannels();
        if (count.I == 1) return input.Clone();
        if (count.I == 3) return input.Rgb1ToGray();
        if (count.I != 4) throw new NotSupportedException("支持Gray8/Bgr24/Bgra32图像。");
        using var r = input.AccessChannel(1); using var g = input.AccessChannel(2); using var b = input.AccessChannel(3);
        HOperatorSet.Compose3(r, g, b, out var composed);
        using (composed) using (var rgb = new HImage(composed)) return rgb.Rgb1ToGray();
    }
}
