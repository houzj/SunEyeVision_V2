namespace SunEyeVision.Plugin.Contracts;
/// <summary>Borrowed image; no disposal or writable buffer exposed. CopyPixels returns an independent copy.</summary>
public interface IReadOnlyImage { int Width { get; } int Height { get; } byte[] CopyPixels(); }
public interface IOutputResources
{
    IReadOnlyImage CreateGray8(int width,int height,ReadOnlySpan<byte> pixels);
    IReadOnlyImage RetainInput(IReadOnlyImage borrowedInput);
}
