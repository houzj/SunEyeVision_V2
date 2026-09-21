using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具执行结果
    /// </summary>
    // P0.6-1 能力声明：图像输入 + 位置修正 + 区域输入（产出"检测区/屏蔽区图形"框架契约条目）
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.PositionCorrection
        | ToolCapabilities.RoiInput)]
    public class BlobAnalysisResults : ToolResults
    {
        /// <summary>
        /// 原始灰度输入图像。预处理和 ROI 限定后的中间图像不应冒充原始输入。
        /// </summary>
        public IVisionImage? GrayImage { get; set; }

        /// <summary>
        /// 二值分割掩码图像（阈值得到的二值区域转图像）
        /// </summary>
        public IVisionImage? BinaryMask { get; set; }

        /// <summary>
        /// 合成显示图像
        /// </summary>
        public IVisionImage? OverlayImage { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            if (GrayImage != null && !GrayImage.IsEmpty)
                images["GrayImage"] = GrayImage;
            if (BinaryMask != null && !BinaryMask.IsEmpty)
                images["BinaryMask"] = BinaryMask;
            if (OverlayImage != null && !OverlayImage.IsEmpty)
                images["OverlayImage"] = OverlayImage;
            return images;
        }

        /// <summary>
        /// 阈值分割实际使用的下限。
        /// </summary>
        public int ThresholdMinUsed { get; set; }

        /// <summary>
        /// 阈值分割实际使用的上限。
        /// </summary>
        public int ThresholdMaxUsed { get; set; }

        /// <summary>
        /// 使用的阈值类型（如 "Binary", "Truncate"...）
        /// </summary>
        public string TypeUsed { get; set; } = string.Empty;

        /// <summary>
        /// 灰度直方图数据（256个bin）
        /// </summary>
        public double[] HistogramData { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 通道均值
        /// </summary>
        public double[] ChannelMeans { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 所有检测到的Blob（经过面积筛选和Per-Blob判断的最终集合）
        /// </summary>
        public List<BlobResult> FilteredBlobs { get; set; } = new();

        /// <summary>
        /// 筛选后Blob数量
        /// </summary>
        public int FilteredBlobCount => FilteredBlobs?.Count ?? 0;

        /// <summary>
        /// 筛选后Blob总面积
        /// </summary>
        public double FilteredTotalArea => FilteredBlobs?.Sum(b => b.Area) ?? 0;

        /// <summary>
        /// 筛选后平均面积
        /// </summary>
        public double AverageArea => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Area) : 0;

        /// <summary>
        /// 筛选后平均圆度
        /// </summary>
        public double AverageCircularity => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Circularity) : 0;

        /// <summary>
        /// 筛选后平均矩形度
        /// </summary>
        public double AverageRectangularity => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Rectangularity) : 0;

        /// <summary>
        /// 白色像素比例(%)
        /// </summary>
        public double WhitePixelRatio { get; set; }

        #region 判断结果

        /// <summary>
        /// 判断消息列表（每条判断的结果描述）
        /// </summary>
        public List<string> JudgmentMessages { get; set; } = new();

        #endregion

        #region Blob详细数据（数组类型）

        /// <summary>各Blob面积数组</summary>
        public double[] BlobAreas => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Area).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob周长数组</summary>
        public double[] BlobPerimeters => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Perimeter).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob中心X坐标数组</summary>
        public double[] BlobCentroidXs => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.CentroidX).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob中心Y坐标数组</summary>
        public double[] BlobCentroidYs => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.CentroidY).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob圆度数组</summary>
        public double[] BlobCircularities => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Circularity).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob矩形度数组</summary>
        public double[] BlobRectangularities => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Rectangularity).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob边框数组（宽×高格式）</summary>
        public string[] BlobBoundingRects => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingRectText).ToArray()
            : Array.Empty<string>();

        /// <summary>各Blob边框宽度数组</summary>
        public double[] BlobBoundingWidths => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingBox.Width).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob边框高度数组</summary>
        public double[] BlobBoundingHeights => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingBox.Height).ToArray()
            : Array.Empty<double>();

        #endregion

        /// <summary>
        /// 筛选后所有Blob的区域本体集合（Halcon HRegion，供下游区域运算与图像叠加消费）。
        /// 不进 GetPropertyMap（符合方案 D8：Region 是 iconic 运行时数据，禁止进工作流契约）。
        /// 生命周期由结果类统一管理，使用完毕由调用方 Dispose。
        /// </summary>
        public IVisionRegion? BlobRegions { get; set; }

        /// <summary>通过判断的区域集合，供结果层批量渲染。</summary>
        public IVisionRegion? OkBlobRegions { get; set; }

        /// <summary>未通过判断的区域集合，供结果层批量渲染。</summary>
        public IVisionRegion? NgBlobRegions { get; set; }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                new("GrayImage",                "灰度图像",               null),
                new("BinaryMask",               "二值掩码",               null),
                new("OverlayImage",             "合成显示图像",           null),
                new("ThresholdMinUsed",         "实际阈值下限",           "预处理"),
                new("ThresholdMaxUsed",         "实际阈值上限",           "预处理"),
                new("TypeUsed",                 "阈值类型",               "预处理"),
                new("FilteredBlobCount",        "Blob数量",               "统计"),
                new("FilteredTotalArea",        "总面积",                 "统计"),
                new("AverageArea",              "平均面积",               "统计"),
                new("AverageCircularity",       "平均圆度",               "统计"),
                new("AverageRectangularity",    "平均矩形度",             "统计"),
                new("WhitePixelRatio",          "白色像素比例",           "统计"),
                new("HistogramData",            "直方图数据",             "统计"),
                new("ChannelMeans",             "通道均值",               "统计"),
                // Blob详细数据（支持 / 分隔的层级分组）
                new("BlobAreas",                "面积",                   "Blob详细"),
                new("BlobPerimeters",           "周长",                   "Blob详细"),
                new("BlobCentroidXs",           "中心X",                  "Blob详细/中心"),
                new("BlobCentroidYs",           "中心Y",                  "Blob详细/中心"),
                new("BlobCircularities",        "圆度",                   "Blob详细/形状"),
                new("BlobRectangularities",     "矩形度",                 "Blob详细/形状"),
                new("BlobBoundingWidths",       "边框宽",                 "Blob详细/边框"),
                new("BlobBoundingHeights",      "边框高",                 "Blob详细/边框"),
                new("BlobBoundingRects",        "边框(文本)",             "Blob详细/边框"),
            ];
        }
    }
}
