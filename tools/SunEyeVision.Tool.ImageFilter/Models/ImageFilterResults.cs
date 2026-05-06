using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.ImageFilter.Models
{
    public class ImageFilterResults : ToolResults
    {
        public Mat? OutputImage { get; set; }
        public override Mat? GetOutputImage() => OutputImage;
        public FilterType FilterTypeUsed { get; set; }
        public int KernelSizeUsed { get; set; }
        public OpenCvSharp.Size InputSize { get; set; }
        public double MeanValue { get; set; }
        public double StdDevValue { get; set; }
        public double PSNR { get; set; }
        public double SigmaUsed { get; set; }
        public string ProcessingMode { get; set; } = string.Empty;
        public bool JudgmentPassed { get; set; } = true;
        public List<string> JudgmentMessages { get; set; } = new();

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                // 执行信息
                new("Status",           "执行状态",     "执行信息"),
                new("ExecutionTimeMs",  "执行耗时",     "执行信息"),
                // 图像输出
                new("OutputImage",      "输出图像",     null),
                // 参数
                new("FilterTypeUsed",   "滤波类型",     "参数"),
                new("KernelSizeUsed",   "核大小",       "参数"),
                new("SigmaUsed",        "Sigma值",      "参数"),
                // 输入信息
                new("InputSize",        "输入尺寸",     "输入"),
                // 统计
                new("MeanValue",        "均值",         "统计"),
                new("StdDevValue",      "标准差",       "统计"),
                new("PSNR",             "PSNR",         "统计"),
                // 结果
                new("ProcessingMode",   "处理模式",     "结果"),
                // 判断
                new("JudgmentPassed",   "判断通过",     "判断"),
            ];
        }
    }
}