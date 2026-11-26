namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>模板窗口当前训练图像的不可变 Gray8 快照，等价原厂 CurImage。</summary>
public sealed record TemplateImageSnapshot(byte[] GrayPixels, int Width, int Height);

/// <summary>
/// 当前模板窗口的两层掩膜状态。Specified 区分“尚未修改，允许沿用已存模板掩膜”和
/// “窗口明确清空/修改，必须以当前值为准”。两层状态独立传递。
/// </summary>
public sealed record TemplateMaskSnapshot(
    byte[]? EraseMask,
    bool EraseMaskSpecified,
    byte[]? PaintMask,
    bool PaintMaskSpecified,
    int Width,
    int Height);
