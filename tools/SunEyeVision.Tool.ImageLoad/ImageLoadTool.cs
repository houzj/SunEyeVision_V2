using System;
using System.Diagnostics;
using System.IO;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.ImageLoad
{
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

                HOperatorSet.ReadImage(out var hObj, parameters.FilePath);
                var loadedImage = new HImage(hObj);

                if (loadedImage == null || !loadedImage.IsInitialized())
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                result.OutputImage = loadedImage;

                HOperatorSet.GetImageSize(loadedImage, out var imgWidth, out var imgHeight);
                result.Width = imgWidth.I;
                result.Height = imgHeight.I;

                HOperatorSet.CountChannels(loadedImage, out var channels);
                result.Channels = channels.I;

                HOperatorSet.GetImageType(loadedImage, out var imgType);
                result.Depth = ImageTypeToDepth(imgType.S);

                // 灰度转换：彩色→灰度，灰度原图直接克隆
                if (result.Channels > 1)
                {
                    HOperatorSet.Rgb1ToGray(loadedImage, out var grayObj);
                    result.GrayImage = new HImage(grayObj);
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

        private static int ImageTypeToDepth(string type)
        {
            return type switch
            {
                "byte" => 0,
                "int1" => 1,
                "uint2" => 2,
                "int2" => 3,
                "int4" => 4,
                "real" => 5,
                "complex" => 6,
                _ => 0
            };
        }
    }
}
