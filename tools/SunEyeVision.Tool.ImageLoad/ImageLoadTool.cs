using System;
using System.Diagnostics;
using System.IO;
using HalconDotNet;
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

                // Halcon 原生加载图像
                var hImage = new HImage(parameters.FilePath);
                if (!hImage.IsInitialized())
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                result.OutputImage = hImage.Clone();
                HOperatorSet.GetImageSize(hImage, out var imgWidth, out var imgHeight);
                result.Width = (int)imgWidth.L;
                result.Height = (int)imgHeight.L;
                result.Channels = hImage.CountChannels();
                result.Depth = 8; // Halcon "byte" 类型 = 8bit

                // 灰度转换：彩色→灰度，灰度输入直接克隆
                if (result.Channels > 1)
                {
                    HOperatorSet.Rgb1ToGray(hImage, out var grayImage);
                    result.GrayImage = new HImage(grayImage);
                }
                else
                {
                    result.GrayImage = hImage.Clone();
                }

                hImage.Dispose();

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
