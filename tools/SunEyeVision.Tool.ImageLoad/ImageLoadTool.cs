using System;
using System.Diagnostics;
using System.IO;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.HalAdapter;

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

                // 经 HalAdapter 统一加载图像（SDK 门面不携带引擎实现，对称架构）
                result.OutputImage = HalImage.CreateFromFile(parameters.FilePath);
                if (result.OutputImage == null || result.OutputImage.IsEmpty)
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                result.Width = result.OutputImage.Width;
                result.Height = result.OutputImage.Height;
                result.Channels = result.OutputImage.Channels;
                result.Depth = 8; // Halcon "byte" 类型 = 8bit

                // 灰度转换：彩色->灰度，灰度输入直接克隆
                if (result.Channels > 1)
                {
                    result.GrayImage = HalImage.CreateGrayFromColor(result.OutputImage);
                }
                else
                {
                    result.GrayImage = result.OutputImage;
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
