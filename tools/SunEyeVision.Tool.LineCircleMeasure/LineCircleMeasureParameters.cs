using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.LineCircleMeasure
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
    /// 线-圆测量参数（v4 C3 范式落点，套线-线模板）。
    /// 关键差异（对比 LineDetect）：引导/卡尺数值全部为 <see cref="ParamValue{T}"/>，
    /// 常量↔绑定两态由 ConfigSetting 数据驱动，绑定模式上游值由执行器 ApplyBindings 注入，
    /// 避免"普通 double + ConfigSetting = 无效绑定"的旧缺陷。
    /// 元素1 = 直线（引导起终点 + 直线卡尺），元素2 = 圆（引导圆心/半径 + 径向卡尺）。
    /// </summary>
    [JsonDerivedType(typeof(LineCircleMeasureParameters), "LineCircleMeasure")]
    public class LineCircleMeasureParameters : RoiParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，SDK MeasureThreshold 复用，六工具同构）
        // ============================================================
        /// <summary>测量输出项判定规格（距离/交点X/交点Y/半径 各一行公差；UI 数据驱动 Toggle+Range）。可写以便 JSON 反序列化回填</summary>
        public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
        {
            new() { Name = "距离", Enabled = true, Min = 0, Max = 10, Unit = "px" },
            new() { Name = "交点X", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "交点Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "半径", Enabled = false, Min = 0, Max = 1000, Unit = "px" },
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
        // 元素2（卡尺2）：引导圆（圆心 + 半径）—— 全部 ParamValue 支持字段订阅
        // ============================================================
        // ★ 圆引导只有"圆心 + 半径"三个自由度（无起止角）：圆弧引导属 CircleDetect 范畴，
        //   本工具元素2 恒为整圆，故交点在圆上取 0~2π 全域（见 Tool 中 intersection_line_circle 调用）。
        public ParamValue<double> E2CenterX { get; set; } = ParamValue<double>.CreateConstant(0, "E2CenterX");
        public ParamValue<double> E2CenterY { get; set; } = ParamValue<double>.CreateConstant(0, "E2CenterY");
        public ParamValue<double> E2Radius { get; set; } = ParamValue<double>.CreateConstant(0, "E2Radius");

        // ---- 元素2 卡尺参数（MeasureLength1 = 径向半宽；MeasureLength2 = 切向半长）----
        public ParamValue<double> E2MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E2MeasureLength1");
        public ParamValue<double> E2MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0, "E2MeasureLength2");
        public ParamValue<int> E2NumMeasures { get; set; } = ParamValue<int>.CreateConstant(16, "E2NumMeasures");
        public ParamValue<double> E2Sigma { get; set; } = ParamValue<double>.CreateConstant(1.0, "E2Sigma");
        public ParamValue<double> E2Threshold { get; set; } = ParamValue<double>.CreateConstant(30.0, "E2Threshold");
        public ParamValue<EdgeTransition> E2Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E2Transition");
        public ParamValue<EdgeSelect> E2Select { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E2Select");
        public ParamValue<double> E2DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(5.0, "E2DistanceThreshold");

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐线-线 Tab3 范式）
        // ============================================================
        public GraphicsStyle GuideLine1Style { get; set; } = new()
        {
            Name = "引导线1", IsVisible = true,
        };
        public GraphicsStyle GuideCircle2Style { get; set; } = new()
        {
            Name = "引导圆2", IsVisible = true,
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
            Name = "拟合直线1", IsVisible = true,
        };
        public GraphicsStyle FitCircle2Style { get; set; } = new()
        {
            Name = "拟合圆2", IsVisible = true,
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
            Name = "交点", IsVisible = true, CrossSize = 6.0,
        };

        /// <summary>元素1 无限直线延长线（虚线；拟合线段为实线，延长段表达"交点/垂足取自无限直线"）</summary>
        public GraphicsStyle ExtensionLine1Style { get; set; } = new()
        {
            Name = "延长线1", IsVisible = true,
            LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };

        /// <summary>圆心到直线距离连线（圆心 → 直线垂足，虚线点划线）</summary>
        public GraphicsStyle DistanceLineStyle { get; set; } = new()
        {
            Name = "距离连线", IsVisible = true,
            LineDashStyle = LineDashStyle.DashDot, LineWidth = 1.0,
        };

        /// <summary>距离数值文字（圆心到直线距离）</summary>
        public TextStyle DistanceTextStyle { get; set; } = new()
        {
            Name = "距离", IsVisible = true,
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
        /// <summary>拟合圆半径文字（圆无方向角，故以半径替代线-线的"卡尺2角度"）</summary>
        public TextStyle Element2RadiusTextStyle { get; set; } = new()
        {
            Name = "卡尺2半径", IsVisible = true,
        };
        public TextStyle IntersectionTextStyle { get; set; } = new()
        {
            Name = "交点", IsVisible = true,
        };
    }
}
