using System;
using System.Diagnostics;
using System.IO;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入工具 - 从文件读取图像，作为工作流的图像源
    /// </summary>
    [Tool("ImageLoad", "图像载入",
        Description = "从文件载入图像，作为工作流的图像源",
        Icon = "📁", Category = "采集",
        Version = "2.0.0", HasDebugWindow = false)]
    public class ImageLoadTool : IToolPlugin<ImageLoadParameters, ImageLoadResults>
    {
        public ImageLoadResults Run(ImageLoadParameters parameters)
        {
            var result = new ImageLoadResults
            {
                ToolName = "ImageLoad",
                ToolId = "image_load",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
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

                var loadedImage = Cv2.ImRead(parameters.FilePath, ImreadModes.Unchanged);

                if (loadedImage == null || loadedImage.Empty())
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                result.OutputImage = loadedImage;
                result.Width = loadedImage.Width;
                result.Height = loadedImage.Height;
                result.Channels = loadedImage.Channels();
                result.Depth = (int)loadedImage.Depth();

                // 灰度转换：彩色→灰度，灰度原图直接克隆
                if (loadedImage.Channels() > 1)
                {
                    var grayMat = new Mat();
                    Cv2.CvtColor(loadedImage, grayMat, ColorConversionCodes.BGR2GRAY);
                    result.GrayImage = grayMat;
                }
                else
                {
                    result.GrayImage = loadedImage.Clone();
                }

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.SetError($"加载图像失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }
    }
}
