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
    // P0.6-4 承载层拆分：测量工具不使用区域 → 继承"图像源 + 位置修正 + 测量引导"基类
    public class LineLineMeasureParameters : MeasureGuideParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，SDK MeasureThreshold 复用，六工具同构）
        // ============================================================
        /// <summary>测量输出项判定规格（角度/距离/交点X/交点Y 各一行公差；UI 数据驱动 Toggle+Range）。可写以便 JSON 反序列化回填</summary>
        public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
        {
            new() { Name = "角度", Enabled = true, Min = 0, Max = 90, Unit = "°" },
            new() { Name = "距离", Enabled = false, Min = 0, Max = 10, Unit = "px" },
            new() { Name = "交点X", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "交点Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
        };

        // ============================================================
        // 元素1（卡尺1）：引导直线（起点/终点）—— 全部 ParamValue 支持字段订阅
        // ============================================================
        // 默认无引导（对齐直线查找：绘制了才有）；坐标归 0，校验长度即报"未绘制"。
        // ★ 必须是可写属性：JSON 反序列化只回填带 setter 的属性，get-only 会导致方案保存后再打开引导/卡尺参数回默认。
        public ParamValue<double> E1StartX { get; set; } = ParamValue<double>.CreateConstant(0, "E1StartX");
        public ParamValue<double> E1StartY { get; set; } = ParamValue<double>.CreateConstant(0, "E1StartY");
        public ParamValue<double> E1EndX { get; set; } = ParamValue<double>.CreateConstant(0, "E1EndX");
        public ParamValue<double> E1EndY { get; set; } = ParamValue<double>.CreateConstant(0, "E1EndY");

        // ---- 元素1 卡尺参数（半长口径，同 LineDetect）----
        public ParamValue<double> E1MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E1MeasureLength1");
        public ParamValue<double> E1MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0, "E1MeasureLength2");
        public ParamValue<int> E1NumMeasures { get; set; } = ParamValue<int>.CreateConstant(10, "E1NumMeasures");
        public ParamValue<double> E1Sigma { get; set; } = ParamValue<double>.CreateConstant(1.0, "E1Sigma");
        public ParamValue<double> E1Threshold { get; set; } = ParamValue<double>.CreateConstant(30.0, "E1Threshold");
        public ParamValue<EdgeTransition> E1Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E1Transition");
        public ParamValue<EdgeSelect> E1Select { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E1Select");
        public ParamValue<double> E1DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(5.0, "E1DistanceThreshold");

        // ============================================================
        // 元素2（卡尺2）：引导直线 + 卡尺参数（默认与 E1 呈约 26.6° 夹角示例）
        // ============================================================
        // ★ 可写属性（JSON 反序列化回填，参见 E1）
        public ParamValue<double> E2StartX { get; set; } = ParamValue<double>.CreateConstant(0, "E2StartX");
        public ParamValue<double> E2StartY { get; set; } = ParamValue<double>.CreateConstant(0, "E2StartY");
        public ParamValue<double> E2EndX { get; set; } = ParamValue<double>.CreateConstant(0, "E2EndX");
        public ParamValue<double> E2EndY { get; set; } = ParamValue<double>.CreateConstant(0, "E2EndY");

        public ParamValue<double> E2MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E2MeasureLength1");
        public ParamValue<double> E2MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0, "E2MeasureLength2");
        public ParamValue<int> E2NumMeasures { get; set; } = ParamValue<int>.CreateConstant(10, "E2NumMeasures");
        public ParamValue<double> E2Sigma { get; set; } = ParamValue<double>.CreateConstant(1.0, "E2Sigma");
        public ParamValue<double> E2Threshold { get; set; } = ParamValue<double>.CreateConstant(30.0, "E2Threshold");
        public ParamValue<EdgeTransition> E2Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E2Transition");
        public ParamValue<EdgeSelect> E2Select { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E2Select");
        public ParamValue<double> E2DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(5.0, "E2DistanceThreshold");

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐直线查找 Tab3 范式）
        // ============================================================
        public GraphicsStyle GuideLine1Style { get; set; } = new()
        {
            Name = "引导线1", IsVisible = true,
        };
        public GraphicsStyle GuideLine2Style { get; set; } = new()
        {
            Name = "引导线2", IsVisible = true,
        };
        public GraphicsStyle CaliberBand1Style { get; set; } = new()
        {
            Name = "卡尺1", IsVisible = true,
        };
        public GraphicsStyle CaliberBand2Style { get; set; } = new()
        {
            Name = "卡尺2", IsVisible = true,
        };
        public GraphicsStyle FitLine1Style { get; set; } = new()
        {
            Name = "拟合结果1", IsVisible = true,
        };
        public GraphicsStyle FitLine2Style { get; set; } = new()
        {
            Name = "拟合结果2", IsVisible = true,
        };
        public GraphicsStyle CaliberPoint1Style { get; set; } = new()
        {
            Name = "卡尺1拟合点", IsVisible = true, CrossSize = 4.0,
        };
        public GraphicsStyle CaliberPoint2Style { get; set; } = new()
        {
            Name = "卡尺2拟合点", IsVisible = true, CrossSize = 4.0,
        };
        public GraphicsStyle IntersectionPointStyle { get; set; } = new()
        {
            Name = "角点", IsVisible = true, CrossSize = 6.0,
        };

        /// <summary>元素1 无限直线延长线（虚线；拟合线段为实线，延长段表达"交点取自无限直线"）</summary>
        public GraphicsStyle ExtensionLine1Style { get; set; } = new()
        {
            Name = "延长线1", IsVisible = true,
            LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };
        /// <summary>元素2 无限直线延长线（虚线）</summary>
        public GraphicsStyle ExtensionLine2Style { get; set; } = new()
        {
            Name = "延长线2", IsVisible = true,
            LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };
        /// <summary>两线夹角角标弧（交点处，半径见 <see cref="AngleArcRadius"/>）</summary>
        public GraphicsStyle AngleArcStyle { get; set; } = new()
        {
            Name = "夹角弧", IsVisible = true, LineWidth = 1.5,
        };

        /// <summary>夹角角标半径（px，图像坐标；仅显示层使用，不参与测量与绑定）</summary>
        public double AngleArcRadius { get; set; } = 50.0;

        /// <summary>夹角数值文字（交点附近角平分方向）</summary>
        public TextStyle AngleTextStyle { get; set; } = new()
        {
            Name = "夹角", IsVisible = true,
        };

        /// <summary>距离连线（平行场景画公垂段：线1中点 → 线2垂足）</summary>
        public GraphicsStyle DistanceLineStyle { get; set; } = new()
        {
            Name = "距离连线", IsVisible = true,
            LineDashStyle = LineDashStyle.DashDot, LineWidth = 1.0,
        };

        /// <summary>距离数值文字（间距/最短距离）</summary>
        public TextStyle DistanceTextStyle { get; set; } = new()
        {
            Name = "距离", IsVisible = true,
        };

        /// <summary>最小法向距离投影段（默认隐藏，按需开启；极值点 → 线2 垂足）</summary>
        public GraphicsStyle DistanceMinLineStyle { get; set; } = new()
        {
            Name = "最小距离线", IsVisible = false,
            LineDashStyle = LineDashStyle.Dot, LineWidth = 1.0,
        };

        /// <summary>最大法向距离投影段（默认隐藏，按需开启）</summary>
        public GraphicsStyle DistanceMaxLineStyle { get; set; } = new()
        {
            Name = "最大距离线", IsVisible = false,
            LineDashStyle = LineDashStyle.Dot, LineWidth = 1.0,
        };
        public TextStyle Element1CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺1中心", IsVisible = true,
        };
        public TextStyle Element1AngleTextStyle { get; set; } = new()
        {
            Name = "卡尺1角度", IsVisible = true,
        };
        public TextStyle Element2CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺2中心", IsVisible = true,
        };
        public TextStyle Element2AngleTextStyle { get; set; } = new()
        {
            Name = "卡尺2角度", IsVisible = true,
        };
        public TextStyle IntersectionTextStyle { get; set; } = new()
        {
            Name = "交点", IsVisible = true,
        };
    }
}
