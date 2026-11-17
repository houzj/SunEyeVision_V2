using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 模板创建的契约侧入口：编辑器只传冻结图像与搜索区域，原生句柄、灰度转换与释放都留在 Runtime，
/// 因此工具 Wpf 不依赖 HALCON。
/// </summary>
public static class ContourMatchTemplateBuilder
{
    public static string Create(IReadOnlyImage image, IReadOnlyRegion? roi, ContourMatchSettings settings,
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
            return ContourMatchTemplate.Create(gray, region, settings, cancellation).Encode();
        }
        finally
        {
            region?.Dispose();
        }
    }
}
