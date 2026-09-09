using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.LineLineMeasure
{
    /// <summary>边缘极性（卡尺参数语义，同 LineDetect）</summary>
    public enum EdgeTransition
    {
        All, Positive, Negative,      // 全部 / 正极性（亮到暗）/ 负极性（暗到亮）
    }

    /// <summary>卡尺测点选取模式（卡尺参数语义，同 LineDetect）</summary>
    public enum EdgeSelect
    {
        All, First, Last,
    }

    /// <summary>
    /// 线-线测量参数（v4 C3 范式落点）。
    /// 关键差异（对比 LineDetect）：引导/卡尺数值全部为 <see cref="ParamValue{T}"/>，
    /// 常量↔绑定两态由 ConfigSetting 数据驱动，绑定模式上游值由执行器 ApplyBindings 注入，
    /// 避免"普通 double + ConfigSetting = 无效绑定"的旧缺陷。
    /// 每个元素独立一套引导 + 卡尺参数（E1/E2），运行沿两条引导各自做 measure 拟合。
    /// </summary>
    [JsonDerivedType(typeof(LineLineMeasureParameters), "LineLineMeasure")]
    public class LineLineMeasureParameters : RoiParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，SDK MeasureThreshold 复用，六工具同构）
        // ============================================================
        private readonly ObservableCollection<MeasureThreshold> _judgments = new()
        {
            new() { Name = "角度", Enabled = true, Min = 0, Max = 90, Unit = "°" },
            new() { Name = "距离", Enabled = false, Min = 0, Max = 10, Unit = "px" },
            new() { Name = "交点X", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "交点Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
        };

        /// <summary>测量输出项判定规格（角度/距离/交点X/交点Y 各一行公差；UI 数据驱动 Toggle+Range）</summary>
        public ObservableCollection<MeasureThreshold> Judgments => _judgments;

        // ============================================================
        // 元素1（卡尺1）：引导直线（起点/终点）—— 全部 ParamValue 支持字段订阅
        // ============================================================
        // 默认无引导（对齐直线查找：绘制了才有）；坐标归 0，校验长度即报"未绘制"。
        private readonly ParamValue<double> _e1StartX = ParamValue<double>.CreateConstant(0, "E1StartX");
        private readonly ParamValue<double> _e1StartY = ParamValue<double>.CreateConstant(0, "E1StartY");
        private readonly ParamValue<double> _e1EndX = ParamValue<double>.CreateConstant(0, "E1EndX");
        private readonly ParamValue<double> _e1EndY = ParamValue<double>.CreateConstant(0, "E1EndY");

        public ParamValue<double> E1StartX => _e1StartX;
        public ParamValue<double> E1StartY => _e1StartY;
        public ParamValue<double> E1EndX => _e1EndX;
        public ParamValue<double> E1EndY => _e1EndY;

        // ---- 元素1 卡尺参数（半长口径，同 LineDetect）----
        private readonly ParamValue<double> _e1MeasureLength1 = ParamValue<double>.CreateConstant(20.0, "E1MeasureLength1");
        private readonly ParamValue<double> _e1MeasureLength2 = ParamValue<double>.CreateConstant(5.0, "E1MeasureLength2");
        private readonly ParamValue<int> _e1NumMeasures = ParamValue<int>.CreateConstant(10, "E1NumMeasures");
        private readonly ParamValue<double> _e1Sigma = ParamValue<double>.CreateConstant(1.0, "E1Sigma");
        private readonly ParamValue<double> _e1Threshold = ParamValue<double>.CreateConstant(30.0, "E1Threshold");
        private readonly ParamValue<EdgeTransition> _e1Transition = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E1Transition");
        private readonly ParamValue<EdgeSelect> _e1Select = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E1Select");
        private readonly ParamValue<double> _e1DistanceThreshold = ParamValue<double>.CreateConstant(5.0, "E1DistanceThreshold");

        public ParamValue<double> E1MeasureLength1 => _e1MeasureLength1;
        public ParamValue<double> E1MeasureLength2 => _e1MeasureLength2;
        public ParamValue<int> E1NumMeasures => _e1NumMeasures;
        public ParamValue<double> E1Sigma => _e1Sigma;
        public ParamValue<double> E1Threshold => _e1Threshold;
        public ParamValue<EdgeTransition> E1Transition => _e1Transition;
        public ParamValue<EdgeSelect> E1Select => _e1Select;
        public ParamValue<double> E1DistanceThreshold => _e1DistanceThreshold;

        // ============================================================
        // 元素2（卡尺2）：引导直线 + 卡尺参数（默认与 E1 呈约 26.6° 夹角示例）
        // ============================================================
        private readonly ParamValue<double> _e2StartX = ParamValue<double>.CreateConstant(0, "E2StartX");
        private readonly ParamValue<double> _e2StartY = ParamValue<double>.CreateConstant(0, "E2StartY");
        private readonly ParamValue<double> _e2EndX = ParamValue<double>.CreateConstant(0, "E2EndX");
        private readonly ParamValue<double> _e2EndY = ParamValue<double>.CreateConstant(0, "E2EndY");

        public ParamValue<double> E2StartX => _e2StartX;
        public ParamValue<double> E2StartY => _e2StartY;
        public ParamValue<double> E2EndX => _e2EndX;
        public ParamValue<double> E2EndY => _e2EndY;

        private readonly ParamValue<double> _e2MeasureLength1 = ParamValue<double>.CreateConstant(20.0, "E2MeasureLength1");
        private readonly ParamValue<double> _e2MeasureLength2 = ParamValue<double>.CreateConstant(5.0, "E2MeasureLength2");
        private readonly ParamValue<int> _e2NumMeasures = ParamValue<int>.CreateConstant(10, "E2NumMeasures");
        private readonly ParamValue<double> _e2Sigma = ParamValue<double>.CreateConstant(1.0, "E2Sigma");
        private readonly ParamValue<double> _e2Threshold = ParamValue<double>.CreateConstant(30.0, "E2Threshold");
        private readonly ParamValue<EdgeTransition> _e2Transition = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E2Transition");
        private readonly ParamValue<EdgeSelect> _e2Select = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E2Select");
        private readonly ParamValue<double> _e2DistanceThreshold = ParamValue<double>.CreateConstant(5.0, "E2DistanceThreshold");

        public ParamValue<double> E2MeasureLength1 => _e2MeasureLength1;
        public ParamValue<double> E2MeasureLength2 => _e2MeasureLength2;
        public ParamValue<int> E2NumMeasures => _e2NumMeasures;
        public ParamValue<double> E2Sigma => _e2Sigma;
        public ParamValue<double> E2Threshold => _e2Threshold;
        public ParamValue<EdgeTransition> E2Transition => _e2Transition;
        public ParamValue<EdgeSelect> E2Select => _e2Select;
        public ParamValue<double> E2DistanceThreshold => _e2DistanceThreshold;

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐直线查找 Tab3 范式）
        // ============================================================
        public GraphicsStyle GuideLine1Style { get; set; } = new()
        {
            Name = "引导线1", IsVisible = true, OkColor = System.Windows.Media.Colors.DodgerBlue,
        };
        public GraphicsStyle GuideLine2Style { get; set; } = new()
        {
            Name = "引导线2", IsVisible = true, OkColor = System.Windows.Media.Colors.Orange,
        };
        public GraphicsStyle CaliberBand1Style { get; set; } = new()
        {
            Name = "卡尺1", IsVisible = true, OkColor = System.Windows.Media.Colors.LightSteelBlue,
        };
        public GraphicsStyle CaliberBand2Style { get; set; } = new()
        {
            Name = "卡尺2", IsVisible = true, OkColor = System.Windows.Media.Colors.NavajoWhite,
        };
        public GraphicsStyle FitLine1Style { get; set; } = new()
        {
            Name = "拟合结果1", IsVisible = true, OkColor = System.Windows.Media.Colors.LimeGreen,
        };
        public GraphicsStyle FitLine2Style { get; set; } = new()
        {
            Name = "拟合结果2", IsVisible = true, OkColor = System.Windows.Media.Colors.DeepSkyBlue,
        };
        public GraphicsStyle CaliberPoint1Style { get; set; } = new()
        {
            Name = "卡尺1拟合点", IsVisible = true, OkColor = System.Windows.Media.Colors.White, CrossSize = 4.0,
        };
        public GraphicsStyle CaliberPoint2Style { get; set; } = new()
        {
            Name = "卡尺2拟合点", IsVisible = true, OkColor = System.Windows.Media.Colors.White, CrossSize = 4.0,
        };
        public GraphicsStyle IntersectionPointStyle { get; set; } = new()
        {
            Name = "角点", IsVisible = true, OkColor = System.Windows.Media.Colors.Red, CrossSize = 6.0,
        };
        public TextStyle Element1CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺1中心", IsVisible = true, OkColor = System.Windows.Media.Colors.Cyan,
        };
        public TextStyle Element1AngleTextStyle { get; set; } = new()
        {
            Name = "卡尺1角度", IsVisible = true, OkColor = System.Windows.Media.Colors.Cyan,
        };
        public TextStyle Element2CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺2中心", IsVisible = true, OkColor = System.Windows.Media.Colors.Orange,
        };
        public TextStyle Element2AngleTextStyle { get; set; } = new()
        {
            Name = "卡尺2角度", IsVisible = true, OkColor = System.Windows.Media.Colors.Orange,
        };
        public TextStyle IntersectionTextStyle { get; set; } = new()
        {
            Name = "交点", IsVisible = true, OkColor = System.Windows.Media.Colors.Yellow,
        };
    }
}
