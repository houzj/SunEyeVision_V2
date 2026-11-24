using HalconDotNet;
using System.Runtime.InteropServices;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 模板创建的契约侧入口：编辑器只传冻结图像与搜索区域，原生句柄、灰度转换与释放都留在 Runtime，
/// 因此工具 Wpf 不依赖 HALCON。
/// </summary>
public static class ContourMatchTemplateBuilder
{
    /// <summary>
    /// Builds from the Gray8 snapshot selected in the template window. This is
    /// the external-image equivalent of the host-frame overload; it prevents a
    /// file selected through SelectOtherImage from being silently replaced by
    /// the current workflow input.
    /// </summary>
    public static string CreateGray8(byte[] pixels, int width, int height, ContourMatchSettings settings,
        byte[]? eraseMask = null, int maskWidth = 0, int maskHeight = 0,
        byte[]? paintMask = null, int paintMaskWidth = 0, int paintMaskHeight = 0,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length < checked(width * height))
            throw new ArgumentException("外部模板图像的 Gray8 像素尺寸无效。", nameof(pixels));
        cancellation.ThrowIfCancellationRequested();
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var image = new HImage();
            image.GenImage1("byte", width, height, pinned.AddrOfPinnedObject());
            var template = ContourMatchTemplate.Create(image, null, settings,
                eraseMask, maskWidth, maskHeight, paintMask, paintMaskWidth, paintMaskHeight, cancellation);
            return template.WithMasks(eraseMask, paintMask, maskWidth, maskHeight).Encode();
        }
        finally
        {
            pinned.Free();
        }
    }

    public static string Create(IReadOnlyImage image, IReadOnlyRegion? roi, ContourMatchSettings settings,
        byte[]? eraseMask = null, int maskWidth = 0, int maskHeight = 0,
        byte[]? paintMask = null, int paintMaskWidth = 0, int paintMaskHeight = 0,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(settings);
        ResourceContractRules.ValidateImage(image);
        cancellation.ThrowIfCancellationRequested();
        using var native = HalconResourceBridge.CopyImage(image);
        using var gray = HalconImageAdmission.Gray(native);
        HRegion? region = null;
        try
        {
            if (roi is not null)
            {
                ResourceContractRules.ValidateRegion(roi);
                region = HalconResourceBridge.CopyRegion(roi, image.Frame, image.Width, image.Height);
            }
            // 两层掩膜随建模一起生效并分别写进模板载荷（原厂 otherMask / otherMask2）：
            // 擦除层从训练域扣除，屏蔽层并入训练域。
            var template = ContourMatchTemplate.Create(gray, region, settings,
                eraseMask, maskWidth, maskHeight, paintMask, paintMaskWidth, paintMaskHeight, cancellation);
            return template.WithMasks(eraseMask, paintMask, maskWidth, maskHeight).Encode();
        }
        finally
        {
            region?.Dispose();
        }
    }
}
