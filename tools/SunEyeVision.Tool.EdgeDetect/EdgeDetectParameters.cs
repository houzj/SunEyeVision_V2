using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;

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
        private Line2d? _guideLine;
        private Circle2d? _guideCircle;

        /// <summary>
        /// 几何类型
        /// </summary>
        public GeometryType GeometryType
        {
            get => _geometryType;
            set => SetProperty(ref _geometryType, value, "几何类型");
        }

        /// <summary>
        /// 引导直线（Line 模式）
        /// </summary>
        public Line2d? GuideLine
        {
            get => _guideLine;
            set => SetProperty(ref _guideLine, value, "引导直线");
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
        private double _measureDistance = 10.0;
        private double _sigma = 1.0;
        private double _threshold = 30.0;
        private EdgeTransition _transition = EdgeTransition.Positive;
        private EdgeSelect _select = EdgeSelect.All;
        private double _startAngle = 0.0;
        private double _endAngle = Math.PI * 2;
        private int _minEdgePoints = 5;
        private double _maxRMSE = 1.0;

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
        /// 测量带间距（与数量互斥）
        /// </summary>
        public double MeasureDistance
        {
            get => _measureDistance;
            set => SetProperty(ref _measureDistance, value, "测量带间距");
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
    }
}
