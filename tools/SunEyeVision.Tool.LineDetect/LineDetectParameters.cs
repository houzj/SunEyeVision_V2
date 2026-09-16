using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Constants;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;

namespace SunEyeVision.Tool.LineDetect
{
    /// <summary>
    /// 边缘极性（Halcon measure_transition）
    /// </summary>
    public enum EdgeTransition
    {
        /// <summary>全部</summary>
        All,

        /// <summary>正极性（亮到暗）</summary>
        Positive,

        /// <summary>负极性（暗到亮）</summary>
        Negative,
    }

    /// <summary>
    /// 边缘选取模式（Halcon measure_select）
    /// </summary>
    public enum EdgeSelect
    {
        /// <summary>全部</summary>
        All,

        /// <summary>第一个</summary>
        First,

        /// <summary>最后一个</summary>
        Last,
    }

    /// <summary>
    /// 直线查找工具参数
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology 直线测量：
    /// - 引导直线（两点）作为先验位置
    /// - 自动生成测量带阵列（measure bands）
    /// - 每条测量带内做 1D 边缘检测 → 多点收集 → 拟合直线
    /// </remarks>
    [JsonDerivedType(typeof(LineDetectParameters), "LineDetect")]
    public class LineDetectParameters : RoiParameters
    {
        #region 引导直线

        private Point _lineStartPoint;
        private Point _lineEndPoint;

        /// <summary>
        /// 引导直线起点（对齐 VisionMaster 两点参数化）
        /// </summary>
        public Point LineStartPoint
        {
            get => _lineStartPoint;
            set => SetProperty(ref _lineStartPoint, value, "引导直线起点");
        }

        /// <summary>
        /// 引导直线终点（对齐 VisionMaster 两点参数化）
        /// </summary>
        public Point LineEndPoint
        {
            get => _lineEndPoint;
            set => SetProperty(ref _lineEndPoint, value, "引导直线终点");
        }

        #endregion

        #region 测量带参数（对齐 Halcon 2D Metrology）

        private double _measureLength1 = 20.0;
        private double _measureLength2 = 5.0;
        private int _numMeasures = 10;
        private double _sigma = 1.0;
        private double _threshold = 30.0;
        private EdgeTransition _transition = EdgeTransition.All;
        private EdgeSelect _select = EdgeSelect.All;
        private double _distanceThreshold = 5.0;

        /// <summary>
        /// 测量带法线方向半长（宽度）
        /// </summary>
        public double MeasureLength1
        {
            get => _measureLength1;
            set => SetProperty(ref _measureLength1, value, "测量带宽度");
        }

        /// <summary>
        /// 测量带切线方向半长（长度）
        /// </summary>
        public double MeasureLength2
        {
            get => _measureLength2;
            set => SetProperty(ref _measureLength2, value, "测量带长度");
        }

        /// <summary>
        /// 测量带数量（产品决策：只支持数量主导，废弃间距模式）
        /// </summary>
        public int NumMeasures
        {
            get => _numMeasures;
            set => SetProperty(ref _numMeasures, Math.Max(1, value), "测量带数量");
        }

        /// <summary>
        /// 高斯平滑
        /// </summary>
        public double Sigma
        {
            get => _sigma;
            set => SetProperty(ref _sigma, value, "平滑Sigma");
        }

        /// <summary>
        /// 边缘阈值
        /// </summary>
        public double Threshold
        {
            get => _threshold;
            set => SetProperty(ref _threshold, value, "边缘阈值");
        }

        /// <summary>
        /// 边缘极性
        /// </summary>
        public EdgeTransition Transition
        {
            get => _transition;
            set => SetProperty(ref _transition, value, "边缘极性");
        }

        /// <summary>
        /// 选取模式
        /// </summary>
        public EdgeSelect Select
        {
            get => _select;
            set => SetProperty(ref _select, value, "选取模式");
        }

        /// <summary>
        /// RANSAC 距离阈值（distance_threshold：边缘点到拟合几何的最大距离，默认 5.0px）
        /// </summary>
        public double DistanceThreshold
        {
            get => _distanceThreshold;
            set => SetProperty(ref _distanceThreshold, Math.Max(0.1, value), "距离阈值");
        }

        #endregion

        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "edge-point-count", Name = "匹配点数", Enabled = false, Min = 5, Max = 1000, AllowedMinimum = 0, AllowedMaximum = 10000, DecimalPlaces = 0, Order = 10 });
            config.Judgments.Add(new() { Key = "angle", Name = "角度", Enabled = false, Min = -180, Max = 180, Unit = "°", AllowedMinimum = -180, AllowedMaximum = 180, Order = 20 });
            config.Judgments.Add(new() { Key = "rmse", Name = "拟合误差", Enabled = false, Min = 0, Max = 1, Unit = "px", AllowedMinimum = 0, AllowedMaximum = 1000, Order = 30 });
            config.Graphics.Add(new() { Key = "contour-point", Name = "轮廓点", Group = "检测", Order = 10, Style = new() { Name = "轮廓点", IsVisible = true, CrossSize = 5.0 } });
            config.Graphics.Add(new() { Key = "diagnostic-point", Name = "诊断检测点", Group = "检测", Order = 20, Style = new() { Name = "诊断检测点", IsVisible = false, OkColor = System.Windows.Media.Colors.Cyan, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "excluded-point", Name = "被屏蔽剔除点", Group = "检测", Order = 30, Style = new() { Name = "被屏蔽剔除点", IsVisible = false, OkColor = System.Windows.Media.Colors.Gray, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "halcon-band", Name = "HALCON测量带", Group = "测量过程", Order = 40, Style = new() { Name = "HALCON测量带", IsVisible = false, OkColor = ColorConstants.MaskOrange, LineWidth = 1.5 } });
            config.Graphics.Add(new() { Key = "fit-result", Name = "拟合直线", Group = "结果", Order = 50, Style = new() { Name = "拟合直线", IsVisible = true } });
            config.Graphics.Add(new() { Key = "guide", Name = "引导线", Group = "测量过程", Order = 60, Style = new() { Name = "引导线", IsVisible = true, LineWidth = 2.0 } });
            config.Graphics.Add(new() { Key = "caliper", Name = "卡尺", Group = "测量过程", Order = 70, Style = new() { Name = "卡尺", IsVisible = true, LineWidth = 1.0 } });
            config.Texts.Add(new() { Key = "center", Name = "中点坐标", Order = 10, Style = new() { Name = "中点坐标", IsVisible = true } });
            config.Texts.Add(new() { Key = "angle", Name = "直线角度", Order = 20, Style = new() { Name = "直线角度", IsVisible = true } });
            return config;
        }
        #region 测量引导显示（与检测/屏蔽区域同通道走结果层，对齐 RoiParameters 虚成员）

        public override GraphicsStyle? GuideStyle
        {
            get => ResultPresentation.GetGraphicsStyle("guide");
            set { if (value != null) ResultPresentation.Graphics.Single(item => item.Key == "guide").Style = value; }
        }

        public override GraphicsStyle? CaliberStyle
        {
            get => ResultPresentation.GetGraphicsStyle("caliper");
            set { if (value != null) ResultPresentation.Graphics.Single(item => item.Key == "caliper").Style = value; }
        }

        /// <summary>
        /// 构建引导直线 + 测量带显示项（与 MeasureBandOverlay 交互层共用同一几何计算）。
        /// 引导直线未设置（两端点重合）或样式均隐藏时返回空。
        /// </summary>
        public override IEnumerable<DisplayItemBase> BuildMeasureGuideItems(AffineMatrix? correctionMatrix)
        {
            if ((LineEndPoint - LineStartPoint).Length < 1e-6) yield break;

            var start = LineStartPoint;
            var end = LineEndPoint;
            if (correctionMatrix != null)
            {
                start = correctionMatrix.TransformWpfPoint(start);
                end = correctionMatrix.TransformWpfPoint(end);
            }

            // 引导线
            if (GuideStyle != null)
            {
                yield return new GraphicsDisplayItem
                {
                    Style = GuideStyle,
                    Type = GraphicsType.Line,
                    Geometries = new object[] { new LineSegment(start, end) },
                    RenderMode = DisplayRenderMode.Unified,
                };
            }

            // 测量带（卡尺）
            if (CaliberStyle != null)
            {
                var bands = new List<System.Windows.Point[]>();
                foreach (var quad in MeasureBandOverlay.BuildLineBands(
                             start, end, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures))
                {
                    bands.Add(quad);
                }
                if (bands.Count > 0)
                {
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = bands.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                    };
                }

                // 主卡尺极性方向箭头（与交互层 MeasureBandOverlay 同几何，所见即所得）。
                // ★ 方向 = 法向（measure 扫描方向，Halcon 沿线方向顺时针 90°），长度 = 法向尺寸的 1/4。
                var seg = end - start;
                var totalLen = seg.Length;
                if (totalLen >= 1e-6)
                {
                    var dir = seg / totalLen;
                    var normal = new Vector(-dir.Y, dir.X);   // ★ 极性/测量扫描方向（法向）
                    int mainIndex = NumMeasures / 2;
                    double bandLength = MeasureLength2 * 2;   // 切向全长（主卡尺沿线半宽用）
                    double halfBand = bandLength / 2;
                    double usable = totalLen - 2 * halfBand;
                    if (usable < 1e-6) usable = totalLen;
                    double step = NumMeasures > 1 ? usable / (NumMeasures - 1) : usable;
                    double t = NumMeasures > 1 ? halfBand + mainIndex * step : totalLen / 2;
                    var center = start + dir * t;
                    // ★ 位置 = 竖边中点沿 normal 走 法向全长×1/4；大小 = 独立指定三角尺寸
                    var midL = center - dir * MeasureLength2;    // 左竖直边中点
                    var midR = center + dir * MeasureLength2;    // 右竖直边中点
                    double posOffset = MeasureLength1 * 2 * 0.25;
                    double triHeight = Math.Max(MeasureLength1 * 2 * 0.15, 4.0);
                    double triHalfWidth = Math.Max(MeasureLength1 * 2 * 0.07, 2.0);

                    // 左三角：尖端锚在竖边中点 +normal×1/4 处，尖端朝 +normal
                    var tipL = midL + normal * posOffset;
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = new object[] { new[] { tipL,
                            tipL - normal * triHeight - dir * triHalfWidth,
                            tipL - normal * triHeight + dir * triHalfWidth } },
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    };

                    // 右三角
                    var tipR = midR + normal * posOffset;
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = new object[] { new[] { tipR,
                            tipR - normal * triHeight - dir * triHalfWidth,
                            tipR - normal * triHeight + dir * triHalfWidth } },
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    };
                }
            }
        }

        #endregion
    }
}
