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

namespace SunEyeVision.Tool.EdgeDetect
{
    /// <summary>
    /// 几何类型（引导对象类型）
    /// </summary>
    public enum GeometryType
    {
        /// <summary>直线查找</summary>
        Line,

        /// <summary>圆查找</summary>
        Circle,

        /// <summary>圆弧查找</summary>
        Arc,
    }

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
    /// 边缘检测工具参数
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology 实现：
    /// - 引导几何（Line/Circle/Arc）作为先验位置
    /// - 自动生成测量带阵列（measure bands）
    /// - 每条测量带内做 1D 边缘检测 → 多点收集 → 拟合
    /// </remarks>
    [JsonDerivedType(typeof(EdgeDetectParameters), "EdgeDetect")]
    public class EdgeDetectParameters : RoiParameters
    {
        #region 引导几何类型

        private GeometryType _geometryType = GeometryType.Line;
        private Circle2d? _guideCircle;
        private Point _lineStartPoint;
        private Point _lineEndPoint;

        /// <summary>
        /// 几何类型
        /// </summary>
        public GeometryType GeometryType
        {
            get => _geometryType;
            set => SetProperty(ref _geometryType, value, "几何类型");
        }

        /// <summary>
        /// 引导直线起点（Line 模式，对齐 VisionMaster 两点参数化）
        /// </summary>
        public Point LineStartPoint
        {
            get => _lineStartPoint;
            set => SetProperty(ref _lineStartPoint, value, "引导直线起点");
        }

        /// <summary>
        /// 引导直线终点（Line 模式，对齐 VisionMaster 两点参数化）
        /// </summary>
        public Point LineEndPoint
        {
            get => _lineEndPoint;
            set => SetProperty(ref _lineEndPoint, value, "引导直线终点");
        }

        /// <summary>
        /// 引导圆（Circle/Arc 模式）
        /// </summary>
        public Circle2d? GuideCircle
        {
            get => _guideCircle;
            set => SetProperty(ref _guideCircle, value, "引导圆");
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
        private double _startAngle = 0.0;
        private double _endAngle = Math.PI * 2;

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
        /// 圆弧起始角（弧度）
        /// </summary>
        public double StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value, "起始角");
        }

        /// <summary>
        /// 圆弧终止角（弧度）
        /// </summary>
        public double EndAngle
        {
            get => _endAngle;
            set => SetProperty(ref _endAngle, value, "终止角");
        }

        private bool _isMajorArc;
        /// <summary>
        /// 是否大弧（张角 &gt; 180°，Arc 模式有效）
        /// </summary>
        public bool IsMajorArc
        {
            get => _isMajorArc;
            set => SetProperty(ref _isMajorArc, value, "大弧/小弧");
        }

        private bool _isClockwise;
        /// <summary>
        /// 圆弧方向（true=顺时针，false=逆时针，Arc 模式有效）
        /// </summary>
        public bool IsClockwise
        {
            get => _isClockwise;
            set => SetProperty(ref _isClockwise, value, "圆弧方向");
        }

        #endregion

        #region 测量引导显示（与检测/屏蔽区域同通道走结果层，对齐 RoiParameters 虚成员）

        private GraphicsStyle _guideStyle = new()
        {
            Name = "引导线",
            IsVisible = true,
            OkColor = ColorConstants.InspectionBlue,
            LineWidth = 2.0,
        };

        private GraphicsStyle _caliberStyle = new()
        {
            Name = "卡尺",
            IsVisible = true,
            OkColor = ColorConstants.InspectionBlue,
            LineWidth = 1.0,
        };

        /// <summary>是否包含测量引导几何（引导 + 卡尺）显示到结果层</summary>
        public override bool HasMeasureGuide => true;

        /// <summary>引导线显示样式（基类虚属性，供 BuildRegionItems 统一读取与 Tab3 双向绑定）</summary>
        public override GraphicsStyle? GuideStyle
        {
            get => _guideStyle;
            set { if (value != null) _guideStyle = value; }
        }

        /// <summary>卡尺显示样式（基类虚属性，供 BuildRegionItems 统一读取与 Tab3 双向绑定）</summary>
        public override GraphicsStyle? CaliberStyle
        {
            get => _caliberStyle;
            set { if (value != null) _caliberStyle = value; }
        }

        /// <summary>
        /// 构建引导几何 + 测量带显示项（与 MeasureBandOverlay 交互层共用同一几何计算）。
        /// 按 GeometryType 分派：Line → 引导直线+测量带；Circle/Arc → 引导圆/弧+环形测量带。
        /// 引导/卡尺样式均隐藏或引导几何未设置时返回空。
        /// </summary>
        public override IEnumerable<DisplayItemBase> BuildMeasureGuideItems(AffineMatrix? correctionMatrix)
        {
            var items = new List<DisplayItemBase>();
            switch (GeometryType)
            {
                case GeometryType.Line:
                    AddIfNotNull(items, BuildLineGuide(correctionMatrix));
                    break;
                case GeometryType.Circle:
                    AppendCircleGuide(items, correctionMatrix, isArc: false);
                    break;
                case GeometryType.Arc:
                    AppendCircleGuide(items, correctionMatrix, isArc: true);
                    break;
            }
            return items;
        }

        private static void AddIfNotNull(List<DisplayItemBase> items, DisplayItemBase? item)
        {
            if (item != null) items.Add(item);
        }

        private DisplayItemBase? BuildLineGuide(AffineMatrix? correctionMatrix)
        {
            var start = LineStartPoint;
            var end = LineEndPoint;
            if (correctionMatrix != null)
            {
                start = correctionMatrix.TransformWpfPoint(start);
                end = correctionMatrix.TransformWpfPoint(end);
            }

            if (GuideStyle.IsVisible)
            {
                return new GraphicsDisplayItem
                {
                    Style = GuideStyle,
                    Type = GraphicsType.Line,
                    Geometries = new object[] { new LineSegment(start, end) },
                    RenderMode = DisplayRenderMode.Unified,
                };
            }

            if (CaliberStyle.IsVisible)
            {
                return new GraphicsDisplayItem
                {
                    Style = CaliberStyle,
                    Type = GraphicsType.Polygon,
                    Geometries = MeasureBandOverlay.BuildLineBands(
                        start, end, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures).Cast<object>().ToArray(),
                    RenderMode = DisplayRenderMode.Unified,
                };
            }

            return null;
        }

        private void AppendCircleGuide(List<DisplayItemBase> items, AffineMatrix? correctionMatrix, bool isArc)
        {
            if (!GuideCircle.HasValue) return;

            var circle = GuideCircle.Value;
            double radiusScale = 1.0;
            if (correctionMatrix != null)
            {
                circle = new Circle2d(
                    correctionMatrix.TransformWpfPoint(circle.Center),
                    circle.Radius * correctionMatrix.ScaleFactor());
                radiusScale = correctionMatrix.ScaleFactor();
            }

            if (GuideStyle.IsVisible)
            {
                // ★ 弧模式引导线必须传 Arc2d（含 Start/End/IsMajorArc），与卡尺 BuildArcBands 同源；
                //   若传 Circle2d 会画整圆。ResultRenderLayer.RenderArcs 已支持 Arc2d 连续扫掠渲染。
                object guide = isArc
                    ? (object)new Arc2d(circle.Center, circle.Radius, StartAngle, EndAngle, IsMajorArc)
                    : circle;
                items.Add(new GraphicsDisplayItem
                {
                    Style = GuideStyle,
                    Type = isArc ? GraphicsType.Arc : GraphicsType.Circle,
                    Geometries = new[] { guide },
                    RenderMode = DisplayRenderMode.Unified,
                });
            }

            if (CaliberStyle.IsVisible)
            {
                var bands = new List<System.Windows.Point[]>();
                if (isArc)
                {
                    var arc = new Arc2d(circle.Center, circle.Radius, StartAngle, EndAngle, IsMajorArc);
                    foreach (var quad in MeasureBandOverlay.BuildArcBands(
                                 arc, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures, radiusScale))
                        bands.Add(quad);

                    items.Add(new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = bands.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                    });

                    // 主卡尺极性方向三角（Halcon circle/arc 扫描方向 = 圆内侧→外侧；几何与交互层共用）
                    double spanArc = arc.AngularSpan;
                    double halfBandAngleArc = (MeasureLength2 * 2 / 2 * radiusScale) / arc.Radius;
                    double directionArc = spanArc >= 0 ? 1.0 : -1.0;
                    double sweepEndArc = arc.StartAngle + spanArc;
                    double firstAngleArc = arc.StartAngle + directionArc * halfBandAngleArc;
                    double lastAngleArc = sweepEndArc - directionArc * halfBandAngleArc;
                    double mainAngleArc = NumMeasures > 1
                        ? firstAngleArc + (NumMeasures / 2) * ((lastAngleArc - firstAngleArc) / (NumMeasures - 1))
                        : arc.StartAngle + spanArc / 2;
                    var arcTriangles = MeasureBandOverlay.BuildRadialDirectionArrowTriangles(
                        arc.Center, arc.Radius, mainAngleArc, MeasureLength1 * 2, MeasureLength2 * 2 * radiusScale, zoom: 1.0);
                    items.Add(new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = arcTriangles.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    });
                }
                else
                {
                    foreach (var quad in MeasureBandOverlay.BuildCircleBands(
                                 circle, MeasureLength1 * 2, MeasureLength2 * 2, NumMeasures))
                        bands.Add(quad);

                    items.Add(new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = bands.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                    });

                    // 主卡尺极性方向三角（Halcon circle/arc 扫描方向 = 圆内侧→外侧；几何与交互层共用）
                    double mainAngle = (NumMeasures / 2) * (Math.PI * 2 / NumMeasures);
                    var circleTriangles = MeasureBandOverlay.BuildRadialDirectionArrowTriangles(
                        circle.Center, circle.Radius, mainAngle, MeasureLength1 * 2, MeasureLength2 * 2, zoom: 1.0);
                    items.Add(new GraphicsDisplayItem
                    {
                        Style = CaliberStyle,
                        Type = GraphicsType.Polygon,
                        Geometries = circleTriangles.Cast<object>().ToArray(),
                        RenderMode = DisplayRenderMode.Unified,
                        IsFixedScreenSize = true,
                    });
                }
            }
        }

        #endregion
    }
}
