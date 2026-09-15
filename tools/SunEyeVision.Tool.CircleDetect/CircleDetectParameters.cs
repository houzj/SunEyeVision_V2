using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Constants;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;

namespace SunEyeVision.Tool.CircleDetect
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
    /// 圆查找工具参数
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology 圆测量：
    /// - 引导圆（圆心+半径）作为先验位置
    /// - 自动生成测量带阵列（measure bands）
    /// - 每条测量带内做 1D 边缘检测 → 多点收集 → 拟合圆
    /// 支持整圆（IsArc=false，默认 0~2π）与圆弧（IsArc=true，StartAngle/EndAngle 限定）。
    /// </remarks>
    [JsonDerivedType(typeof(CircleDetectParameters), "CircleDetect")]
    public class CircleDetectParameters : RoiParameters
    {
        #region 引导圆

        private Circle2d? _guideCircle;
        private bool _isArc;
        private double _startAngle = 0.0;
        private double _endAngle = Math.PI * 2;

        /// <summary>
        /// 引导圆
        /// </summary>
        public Circle2d? GuideCircle
        {
            get => _guideCircle;
            set => SetProperty(ref _guideCircle, value, "引导圆");
        }

        /// <summary>
        /// 是否圆弧模式（true=仅检测圆弧，false=整圆）
        /// </summary>
        public bool IsArc
        {
            get => _isArc;
            set => SetProperty(ref _isArc, value, "圆弧模式");
        }

        /// <summary>
        /// 圆弧起始角（弧度，圆弧模式有效）
        /// </summary>
        public double StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value, "起始角");
        }

        /// <summary>
        /// 圆弧终止角（弧度，圆弧模式有效）
        /// </summary>
        public double EndAngle
        {
            get => _endAngle;
            set => SetProperty(ref _endAngle, value, "终止角");
        }

        private bool _isMajorArc;
        /// <summary>
        /// 是否大弧（张角 &gt; 180°，圆弧模式有效）
        /// </summary>
        public bool IsMajorArc
        {
            get => _isMajorArc;
            set => SetProperty(ref _isMajorArc, value, "大弧/小弧");
        }

        private bool _isClockwise;
        /// <summary>
        /// 圆弧方向（true=顺时针，false=逆时针，圆弧模式有效）
        /// </summary>
        public bool IsClockwise
        {
            get => _isClockwise;
            set => SetProperty(ref _isClockwise, value, "圆弧方向");
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
        /// RANSAC 距离阈值（distance_threshold：边缘点到拟合几何的最大距离，默认 3.5px）
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
            config.Judgments.Add(new() { Key = "radius", Name = "半径", Enabled = false, Min = 0, Max = 1000, Unit = "px", AllowedMinimum = 0, AllowedMaximum = 10000, Order = 10 });
            config.Judgments.Add(new() { Key = "center-x", Name = "中心X", Enabled = false, Min = -10000, Max = 10000, Unit = "px", Order = 20 });
            config.Judgments.Add(new() { Key = "center-y", Name = "中心Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px", Order = 30 });
            config.Judgments.Add(new() { Key = "rmse", Name = "拟合误差", Enabled = false, Min = 0, Max = 1, Unit = "px", AllowedMinimum = 0, AllowedMaximum = 1000, Order = 40 });
            config.Judgments.Add(new() { Key = "edge-point-count", Name = "匹配点数", Enabled = false, Min = 5, Max = 1000, AllowedMinimum = 0, AllowedMaximum = 10000, DecimalPlaces = 0, Order = 50 });
            config.Graphics.Add(new() { Key = "contour-point", Name = "轮廓点", Group = "检测", Order = 10, Style = new() { Name = "轮廓点", IsVisible = true, CrossSize = 5.0 } });
            config.Graphics.Add(new() { Key = "diagnostic-point", Name = "诊断检测点", Group = "检测", Order = 20, Style = new() { Name = "诊断检测点", IsVisible = false, OkColor = System.Windows.Media.Colors.Cyan, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "excluded-point", Name = "被屏蔽剔除点", Group = "检测", Order = 30, Style = new() { Name = "被屏蔽剔除点", IsVisible = false, OkColor = System.Windows.Media.Colors.Gray, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "halcon-band", Name = "HALCON测量带", Group = "测量过程", Order = 40, Style = new() { Name = "HALCON测量带", IsVisible = false, OkColor = ColorConstants.MaskOrange, LineWidth = 1.5 } });
            config.Graphics.Add(new() { Key = "fit-result", Name = "拟合圆", Group = "结果", Order = 50, Style = new() { Name = "拟合圆", IsVisible = true } });
            config.Graphics.Add(new() { Key = "center-point", Name = "圆心标记", Group = "结果", Order = 60, Style = new() { Name = "圆心标记", IsVisible = true, CrossSize = 8.0 } });
            config.Graphics.Add(new() { Key = "guide", Name = "引导线", Group = "测量过程", Order = 70, Style = new() { Name = "引导线", IsVisible = true, LineWidth = 2.0 } });
            config.Graphics.Add(new() { Key = "caliper", Name = "卡尺", Group = "测量过程", Order = 80, Style = new() { Name = "卡尺", IsVisible = true, LineWidth = 1.0 } });
            config.Texts.Add(new() { Key = "center", Name = "圆心坐标", Order = 10, Style = new() { Name = "圆心坐标", IsVisible = true } });
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
        /// 构建引导圆/圆弧 + 测量带显示项（与 MeasureBandOverlay 交互层共用同一几何计算）。
        /// 未绘制引导圆或引导圆样式/卡尺样式均隐藏时返回空。
        /// </summary>
        public override IEnumerable<DisplayItemBase> BuildMeasureGuideItems(AffineMatrix? correctionMatrix)
        {
            if (!GuideCircle.HasValue) yield break;
            var circle = GuideCircle.Value;

            // 位置修正：圆心变换 + 半径缩放（对齐 Run 中算法 + RenderCircle/RenderArc 内部逻辑）
            if (correctionMatrix != null)
            {
                circle = new Circle2d(
                    correctionMatrix.TransformWpfPoint(circle.Center),
                    circle.Radius * correctionMatrix.ScaleFactor());
            }

            if (IsArc)
            {
                // ★ 圆弧模式：与交互层一致（圆弧引导 + 圆弧卡尺），避免结果层画整圆与交互层圆弧重叠
                // 弧为有向形状：起止角随位置修正矩阵旋转（与交互层 RenderArc / 执行层同口径）
                double ArcDeg(double rad) => correctionMatrix != null
                    ? PositionCorrectionResolver.DisplayAngleDeg(correctionMatrix, rad * 180.0 / Math.PI) * Math.PI / 180.0
                    : rad;
                var arc = new Arc2d(circle.Center, circle.Radius, ArcDeg(StartAngle), ArcDeg(EndAngle), IsMajorArc);

                if (GuideStyle.IsVisible)
                {
                    // ★ 用 GraphicsType.Arc + Arc2d 真弧渲染，避免 Polygon 采样首尾闭合成"直径+弧"
                    yield return new GraphicsDisplayItem
                    {
                        Style = GuideStyle,
                        Type = GraphicsType.Arc,
                        Geometries = new object[] { arc },
                        RenderMode = DisplayRenderMode.Unified,
                    };
                }

                if (CaliberStyle.IsVisible)
                {
                    var bands = new List<System.Windows.Point[]>();
                    foreach (var quad in MeasureBandOverlay.BuildArcBands(
                                 arc, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures))
                    {
                        bands.Add(quad);
                    }
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = bands.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                    };

                    // 主卡尺极性方向三角（Halcon circle/arc 扫描方向 = 圆内侧→外侧；几何与交互层共用）
                    double spanArc = arc.AngularSpan;
                    double halfBandAngleArc = MeasureLength2 / arc.Radius;
                    double directionArc = spanArc >= 0 ? 1.0 : -1.0;
                    double sweepEndArc = arc.StartAngle + spanArc;
                    double firstAngleArc = arc.StartAngle + directionArc * halfBandAngleArc;
                    double lastAngleArc = sweepEndArc - directionArc * halfBandAngleArc;
                    double mainAngleArc = NumMeasures > 1
                        ? firstAngleArc + (NumMeasures / 2) * ((lastAngleArc - firstAngleArc) / (NumMeasures - 1))
                        : arc.StartAngle + spanArc / 2;
                    var arcTriangles = MeasureBandOverlay.BuildRadialDirectionArrowTriangles(
                        arc.Center, arc.Radius, mainAngleArc, MeasureLength1 * 2, MeasureLength2 * 2, zoom: 1.0);
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = arcTriangles.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    };
                }
            }
            else
            {
                // 整圆：引导圆 + 环形测量带
                if (GuideStyle.IsVisible)
                {
                    yield return new GraphicsDisplayItem
                    {
                        Style = GuideStyle,
                        Type = GraphicsType.Circle,
                        Geometries = new object[] { circle },
                        RenderMode = DisplayRenderMode.Unified,
                    };
                }

                if (CaliberStyle.IsVisible)
                {
                    // 整圆卡尺阵列起始相位随位置修正旋转（与屏蔽区/内容同口径；无修正=0）
                    double startPhase = correctionMatrix != null
                        ? PositionCorrectionResolver.DisplayAngleDeg(correctionMatrix, 0.0) * Math.PI / 180.0
                        : 0.0;
                    var bands = new List<System.Windows.Point[]>();
                    foreach (var quad in MeasureBandOverlay.BuildCircleBands(
                                 circle, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures, startPhase))
                    {
                        bands.Add(quad);
                    }
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = bands.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                    };

                    // 主卡尺极性方向三角（Halcon circle/arc 扫描方向 = 圆内侧→外侧；几何与交互层共用）
                    double mainAngle = startPhase + (NumMeasures / 2) * (Math.PI * 2 / NumMeasures);
                    var circleTriangles = MeasureBandOverlay.BuildRadialDirectionArrowTriangles(
                        circle.Center, circle.Radius, mainAngle, MeasureLength1 * 2, MeasureLength2 * 2, zoom: 1.0);
                    yield return new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = circleTriangles.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    };
                }
            }
        }

        #endregion
    }
}
