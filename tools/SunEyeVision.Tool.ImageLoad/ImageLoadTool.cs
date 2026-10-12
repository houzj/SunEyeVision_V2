using System;
using System.Diagnostics;
using System.IO;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.HalAdapter;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像源工具：本地文件或方案相机均输出统一图像契约。
    /// </summary>
[Tool("ImageLoad", "图像源",
        Description = "从本地图像或方案相机获取图像，作为工作流输入",
        Icon = "image", Order = 10, Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Acquisition,
        Version = "2.0.0", HasDebugWindow = false)]
    public class ImageLoadTool : IToolPlugin<ImageLoadParameters, ImageLoadResults>, IAsyncToolPlugin
    {
        public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            if (request.Parameters is not ImageLoadParameters parameters)
                throw new ArgumentException("图像源参数类型不正确", nameof(request));
            if (parameters.SourceKind == ImageSourceKind.Local)
                return Run(parameters);

            var result = new ImageLoadResults
            {
                ToolName = "ImageLoad",
                ToolId = "image_load",
                SourceKind = ImageSourceKind.Camera,
                SourceDeviceId = parameters.CameraDeviceId,
                Timestamp = DateTime.Now
            };
            var validation = parameters.Validate();
            if (!validation.IsValid)
            {
                result.SetError($"参数验证失败: {string.Join(", ", validation.Errors)}");
                return result;
            }
            if (request.CameraFrame == null && request.CameraCaptureService == null)
            {
                result.SetError("相机采集服务不可用");
                return result;
            }

            try
            {
                // 工作流帧驱动时只消费宿主已绑定的帧；旧调用方仍可使用单次采集兼容入口。
                var ownsFrame = request.CameraFrame == null;
                var frame = request.CameraFrame ?? await request.CameraCaptureService!.CaptureAsync(
                    new CameraCaptureRequest
                    {
                        CameraRef = new CameraDeviceRef(parameters.CameraDeviceId),
                        TriggerMode = CameraTriggerMode.Continuous,
                        Timeout = TimeSpan.FromMilliseconds(parameters.CameraTimeoutMs)
                    }, request.CancellationToken).ConfigureAwait(false);
                try
                {
                    if (!string.Equals(frame.CameraRef.Value, parameters.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        result.SetError($"本轮帧来自相机 {frame.CameraRef.Value}，与关联相机 {parameters.CameraDeviceId} 不一致");
                        return result;
                    }
                    // 厂商帧可能不是 HalImage；统一转换为现有算法/显示链所用的图像类型。
                    // CreateFromBytes 建立结果自有图像，之后释放 Frame 不影响下游。
                    var pixels = frame.Image.GetPixelData();
                    var channels = frame.Image.Channels;
                    var expectedLength = checked(frame.Image.Width * frame.Image.Height * channels);
                    if (channels is not (1 or 3) || pixels.Length != expectedLength)
                    {
                        result.SetError($"不支持的相机帧：{channels} 通道，数据长度 {pixels.Length}，预期 {expectedLength}");
                        return result;
                    }
                    if (channels == 3 && frame.Metadata.TryGetValue("pixelType", out var pixelType)
                        && pixelType?.ToString()?.Contains("BGR", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        for (var i = 0; i < pixels.Length; i += 3)
                            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                    }
                    var native = HalImage.CreateFromBytes(pixels, frame.Image.Width, frame.Image.Height, channels);
                    var output = ConvertOutput(native, parameters.OutputFormat);
                    if (output.IsEmpty)
                    {
                        output.Dispose();
                        result.SetError("相机返回空图像");
                        return result;
                    }
                    result.OutputImage = output;
                    result.GrayImage = output.Channels == 1 ? output : HalImage.CreateGrayFromColor(output);
                    result.Width = output.Width;
                    result.Height = output.Height;
                    result.Channels = output.Channels;
                    result.Depth = 8;
                    result.FrameId = frame.FrameId;
                    result.FrameTimestamp = frame.Timestamp;
                    result.SequenceNumber = parameters.SequenceNumber;
                    result.PixelFormat = output.Channels == 1 ? "MONO8" : "RGB24";
                    result.PrimePixelStatistics();
                    result.SetSuccess(0);
                }
                finally
                {
                    if (ownsFrame)
                        frame.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.SetError($"相机取帧失败: {ex.Message}", ex);
            }
            return result;
        }

        public ImageLoadResults Run(ImageLoadParameters parameters)
        {
            var result = new ImageLoadResults
            {
                ToolName = "ImageLoad",
                ToolId = "image_load",
                SourceKind = ImageSourceKind.Local,
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (parameters.SourceKind == ImageSourceKind.Camera)
                {
                    result.SetError("相机模式需要异步执行请求和宿主相机服务");
                    return result;
                }
                var validationResult = parameters.Validate();
                if (!validationResult.IsValid)
                {
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                if (string.IsNullOrEmpty(parameters.FilePath))
                {
                    result.SetError("文件路径为空");
                    return result;
                }

                if (!File.Exists(parameters.FilePath))
                {
                    result.SetError($"文件不存在: {parameters.FilePath}");
                    return result;
                }

                var fileInfo = new FileInfo(parameters.FilePath);
                result.FilePath = parameters.FilePath;
                result.FileSize = fileInfo.Length;
                result.FileFormat = fileInfo.Extension.ToUpperInvariant();

                // 经 HalAdapter 统一加载图像（SDK 门面不携带引擎实现，对称架构）
                var source = HalImage.CreateFromFile(parameters.FilePath);
                result.OutputImage = ConvertOutput(source, parameters.OutputFormat);
                if (result.OutputImage == null || result.OutputImage.IsEmpty)
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                result.Width = result.OutputImage.Width;
                result.Height = result.OutputImage.Height;
                result.Channels = result.OutputImage.Channels;
                result.Depth = 8; // Halcon "byte" 类型 = 8bit
                result.PixelFormat = result.Channels == 1 ? "MONO8" : "RGB24";
                result.SequenceNumber = parameters.SequenceNumber;

                // 灰度转换：彩色->灰度，灰度输入直接克隆
                if (result.Channels > 1)
                {
                    result.GrayImage = HalImage.CreateGrayFromColor(result.OutputImage);
                }
                else
                {
                    result.GrayImage = result.OutputImage;
                }

                result.PrimePixelStatistics();

                stopwatch.Stop();
                result.SetSuccess(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.SetError($"加载图像失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private static HalImage ConvertOutput(HalImage source, ImageOutputFormat format)
        {
            if (format == ImageOutputFormat.Original
                || (format == ImageOutputFormat.Mono8 && source.Channels == 1)
                || (format == ImageOutputFormat.Rgb24 && source.Channels == 3))
                return source;

            try
            {
                HalImage converted;
                if (format == ImageOutputFormat.Mono8 && source.Channels == 3)
                    converted = HalImage.CreateGrayFromColor(source);
                else if (format == ImageOutputFormat.Rgb24 && source.Channels == 1)
                {
                    var gray = source.GetPixelData();
                    var rgb = new byte[checked(gray.Length * 3)];
                    for (var i = 0; i < gray.Length; i++)
                        rgb[i * 3] = rgb[i * 3 + 1] = rgb[i * 3 + 2] = gray[i];
                    converted = HalImage.CreateFromBytes(rgb, source.Width, source.Height, 3);
                }
                else
                    throw new NotSupportedException($"无法把 {source.Channels} 通道图像转换为 {format}");

                return converted;
            }
            finally { source.Dispose(); }
        }
    }
}
