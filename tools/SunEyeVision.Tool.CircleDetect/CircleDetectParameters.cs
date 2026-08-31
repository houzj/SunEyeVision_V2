using System;
using System.Text.Json.Serialization;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.CircleDetect.Models;

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

        private CircleDetectResultConfig _resultConfig = new();
        /// <summary>
        /// 结果判断配置（半径/中心X/中心Y/拟合误差/匹配点数）
        /// </summary>
        public CircleDetectResultConfig ResultConfig
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
            Name = "拟合圆",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.LimeGreen,
        };

        /// <summary>圆心坐标文本样式</summary>
        public TextStyle CenterTextStyle { get; set; } = new()
        {
            Name = "圆心坐标",
            IsVisible = true,
            OkColor = System.Windows.Media.Colors.Cyan,
        };

        #endregion
    }
}
