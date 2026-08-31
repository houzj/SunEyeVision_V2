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
using SunEyeVision.Tool.LineDetect.Models;

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
        private double _minScore = 0.5;
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
        /// RANSAC 拟合分数门槛（min_score：内点数 ÷ 测量带数，低于则判定无有效几何，默认 0.5）
        /// </summary>
        public double MinScore
        {
            get => _minScore;
            set => SetProperty(ref _minScore, Math.Clamp(value, 0.1, 1.0), "拟合分数门槛");
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

        #region 结果判断配置

        private LineDetectResultConfig _resultConfig = new();
        /// <summary>
        /// 结果判断配置（匹配点数/角度/拟合误差）
        /// </summary>
        public LineDetectResultConfig ResultConfig
        {
            get => _resultConfig;
            set => SetProperty(ref _resultConfig, value, "结果判断配置");
        }

        #endregion

        #region 显示样式（Tab3 图形/文本显示，用户可配置）

        /// <summary>轮廓点显示样式（黄）</summary>
        public GraphicsStyle ContourPointStyle { get; set; } = new()
        {
            Name = "轮廓点",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.Yellow,
            CrossSize = 5.0,
        };

        /// <summary>诊断检测点显示样式（青，默认不显示，用于定位拟合失败原因）</summary>
        public GraphicsStyle DiagnosticPointStyle { get; set; } = new()
        {
            Name = "诊断检测点",
            IsVisible = false,
            OkColor = System.Windows.Media.Colors.Cyan,
            CrossSize = 4.0,
        };

        /// <summary>HALCON 实际测量带诊断样式（橙红，默认不显示，与本地卡尺叠加对比对齐）</summary>
        public GraphicsStyle HalconBandStyle { get; set; } = new()
        {
            Name = "HALCON测量带",
            IsVisible = false,
            OkColor = ColorConstants.MaskOrange,
            LineWidth = 1.5,
        };

        /// <summary>拟合结果显示样式（绿）</summary>
        public GraphicsStyle FitResultStyle { get; set; } = new()
        {
            Name = "拟合直线",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.LimeGreen,
        };

        /// <summary>中点坐标文本样式</summary>
        public TextStyle CenterTextStyle { get; set; } = new()
        {
            Name = "中点坐标",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.Cyan,
        };

        /// <summary>直线角度文本样式</summary>
        public TextStyle AngleTextStyle { get; set; } = new()
        {
            Name = "直线角度",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.Cyan,
        };

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

        /// <summary>是否包含测量引导几何（引导直线 + 卡尺）显示到结果层</summary>
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
            if (GuideStyle.IsVisible)
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
            if (CaliberStyle.IsVisible)
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
            }
        }

        #endregion
    }
}
