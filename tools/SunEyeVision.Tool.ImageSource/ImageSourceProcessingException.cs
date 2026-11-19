namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Only explicit tool-processing branches with a known native counterpart may create this error.</summary>
internal sealed class ImageSourceProcessingException(uint nativeErrorCode,string message) : Exception(message)
{
    internal uint NativeErrorCode { get; } = nativeErrorCode;
}
