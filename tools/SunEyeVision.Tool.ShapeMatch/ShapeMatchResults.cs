using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.ShapeMatch.Models;

namespace SunEyeVision.Tool.ShapeMatch
{
    /// <summary>
    /// 轮廓匹配工具执行结果
    /// </summary>
    public class ShapeMatchResults : ToolResults
    {
        /// <summary>输入图像（用于显示）</summary>
        public Mat? InputImage { get; set; }

        /// <summary>合成显示图像（含匹配结果叠加）</summary>
        public Mat? OverlayImage { get; set; }

        // ===== 调试中间图像（运行时生成，仅用于可视化，不参与序列化）=======
        /// <summary>灰度输入图（ReduceDomain 后）</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Mat? DebugGrayImage { get; set; }
        /// <summary>搜索区域掩码（如果有）</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Mat? DebugSearchRegionMask { get; set; }
        /// <summary>梯度幅值图（Level 0）</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Mat? DebugMagnitude { get; set; }
        /// <summary>NMS 边缘图（Level 0）</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Mat? DebugEdgeMap { get; set; }

        /// <summary>匹配到的结果列表</summary>
        public List<ShapeMatchResultItem> MatchedItems { get; set; } = new();

        /// <summary>匹配数量</summary>
        public int MatchCount => MatchedItems?.Count ?? 0;

        /// <summary>是否找到匹配</summary>
        public bool HasMatch => MatchCount > 0;

        /// <summary>最佳匹配得分</summary>
        public double BestScore => MatchedItems?.Count > 0 ? MatchedItems.Max(m => m.Score) : 0;

        /// <summary>最佳匹配项</summary>
        public ShapeMatchResultItem? BestMatch => MatchedItems?.Count > 0 ? MatchedItems.OrderByDescending(m => m.Score).First() : null;

        /// <summary>输入图像尺寸</summary>
        public OpenCvSharp.Size InputSize { get; set; }

        /// <summary>处理时间戳</summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

        #region 判断结果

        /// <summary>所有判断是否通过</summary>
        public bool JudgmentPassed { get; set; } = true;

        /// <summary>判断消息列表</summary>
        public List<string> JudgmentMessages { get; set; } = new();

        #endregion

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, Mat?> GetDisplayImages()
        {
            var images = new Dictionary<string, Mat?>();
            if (InputImage != null && !InputImage.Empty())
                images["InputImage"] = InputImage;
            if (OverlayImage != null && !OverlayImage.Empty())
                images["OverlayImage"] = OverlayImage;

            // 调试中间图像
            AddIfValid(images, "DebugGray", DebugGrayImage);
            AddIfValid(images, "DebugSearchRegionMask", DebugSearchRegionMask);
            AddIfValid(images, "DebugMagnitude", DebugMagnitude);
            AddIfValid(images, "DebugEdgeMap", DebugEdgeMap);

            return images;
        }

        private static void AddIfValid(Dictionary<string, Mat?> dict, string key, Mat? mat)
        {
            if (mat != null && !mat.Empty()) dict[key] = mat;
        }

        /// <summary>
        /// 获取结果显示项
        /// </summary>
        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>
            {
                ResultItem.Boolean("判断通过", JudgmentPassed),
                ResultItem.Numeric("匹配数量", MatchCount),
                ResultItem.Numeric("最佳得分", BestScore)
            };

            if (MatchedItems != null && MatchedItems.Count > 0)
            {
                items.Add(ResultItem.Text("得分", string.Join(", ", MatchedItems.Select(m => $"{m.Score:F3}"))));
                items.Add(ResultItem.Text("中心X", string.Join(", ", MatchedItems.Select(m => $"{m.CenterX:F1}"))));
                items.Add(ResultItem.Text("中心Y", string.Join(", ", MatchedItems.Select(m => $"{m.CenterY:F1}"))));
                items.Add(ResultItem.Text("角度", string.Join(", ", MatchedItems.Select(m => $"{m.Angle:F1}"))));
                items.Add(ResultItem.Text("缩放", string.Join(", ", MatchedItems.Select(m => $"{m.Scale:F3}"))));
                items.Add(ResultItem.Text("模板", string.Join(", ", MatchedItems.Select(m => m.TemplateName))));

                if (MatchedItems[0].BoundingBox != null)
                {
                    items.Add(ResultItem.Text("边框", string.Join(", ",
                        MatchedItems.Select(m => $"({m.BoundingBox.Value.X:F0},{m.BoundingBox.Value.Y:F0} {m.BoundingBox.Value.Width:F0}x{m.BoundingBox.Value.Height:F0})"))));
                }
            }

            return items;
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("InputImage",      "输入图像",          null),
                new("OverlayImage",    "合成显示图像",      null),
                new("JudgmentPassed",  "判断通过",          "判断"),
                new("MatchCount",      "匹配数量",          "结果"),
                new("BestScore",       "最佳得分",          "结果"),
                new("InputSize",       "输入图像尺寸",      "结果"),
                new("ProcessedAt",     "处理时间戳",        "执行信息"),
            ];
        }

        /// <summary>
        /// 转换为字典
        /// </summary>
        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["JudgmentPassed"] = JudgmentPassed;
            dict["MatchCount"] = MatchCount;
            dict["HasMatch"] = HasMatch;
            dict["BestScore"] = BestScore;
            dict["InputSize"] = InputSize;
            dict["ProcessedAt"] = ProcessedAt;
            return dict;
        }
    }

    /// <summary>
    /// 单个匹配结果项
    /// </summary>
    public class ShapeMatchResultItem
    {
        /// <summary>匹配分数（0~1）</summary>
        public double Score { get; set; }

        /// <summary>中心X坐标</summary>
        public double CenterX { get; set; }

        /// <summary>中心Y坐标</summary>
        public double CenterY { get; set; }

        /// <summary>旋转角度（度）</summary>
        public double Angle { get; set; }

        /// <summary>缩放比例</summary>
        public double Scale { get; set; }

        /// <summary>匹配到的模板名称</summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>匹配到的模板ID</summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>包围矩形</summary>
        public OpenCvSharp.Rect2d? BoundingBox { get; set; }

        /// <summary>匹配位置上的轮廓点（原图坐标系）</summary>
        public List<OpenCvSharp.Point2f>? ContourPoints { get; set; }

        /// <summary>原始模板轮廓点（未变换，用于对比显示）</summary>
        public List<OpenCvSharp.Point2f>? OriginalTemplateContour { get; set; }

        /// <summary>轮廓点数量</summary>
        public int ContourPointCount => ContourPoints?.Count ?? 0;

        /// <summary>该匹配项是否通过判断</summary>
        public bool JudgmentPassed { get; set; } = true;
    }
}
