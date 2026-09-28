namespace SunEyeVision.Plugin.SDK.Vision.Contracts;

/// <summary>图像源本轮输出格式；转换只作用于结果，不修改源文件或相机参数。</summary>
public enum ImageOutputFormat
{
    Original = 0,
    Mono8 = 1,
    Rgb24 = 2
}
