using System;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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
        private double _measureDistance = 10.0;
        private int _numMeasures = 10;
        private double _sigma = 1.0;
        private double _threshold = 30.0;
        private EdgeTransition _transition = EdgeTransition.Positive;
        private EdgeSelect _select = EdgeSelect.All;

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
        /// 测量带间距（与数量互斥，间距优先）
        /// </summary>
        public double MeasureDistance
        {
            get => _measureDistance;
            set => SetProperty(ref _measureDistance, value, "测量带间距");
        }

        /// <summary>
        /// 测量带数量（与间距互斥，间距优先）
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

        #endregion

        #region 拟合筛选

        private int _minEdgePoints = 5;
        private double _maxRMSE = 1.0;

        /// <summary>
        /// 最小边缘点数（拟合筛选）
        /// </summary>
        public int MinEdgePoints
        {
            get => _minEdgePoints;
            set => SetProperty(ref _minEdgePoints, value, "最小边缘点数");
        }

        /// <summary>
        /// 最大残差 RMSE（拟合筛选）
        /// </summary>
        public double MaxRMSE
        {
            get => _maxRMSE;
            set => SetProperty(ref _maxRMSE, value, "最大残差");
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
    }
}
