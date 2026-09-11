using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.CircleCircleMeasure
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
    /// 圆-圆测量参数（v4 C3 范式落点，套线-线模板）。
    /// 关键差异（对比 LineDetect）：引导/卡尺数值全部为 <see cref="ParamValue{T}"/>，
    /// 常量↔绑定两态由 ConfigSetting 数据驱动，绑定模式上游值由执行器 ApplyBindings 注入，
    /// 避免"普通 double + ConfigSetting = 无效绑定"的旧缺陷。
    /// 元素1 = 圆（引导圆心/半径 + 径向卡尺），元素2 = 圆（同构）。
    /// </summary>
    [JsonDerivedType(typeof(CircleCircleMeasureParameters), "CircleCircleMeasure")]
    // P0.6-4 承载层拆分：测量工具不使用区域 → 继承"图像源 + 位置修正 + 测量引导"基类
    public class CircleCircleMeasureParameters : MeasureGuideParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，SDK MeasureThreshold 复用，六工具同构）
        // ============================================================
        /// <summary>测量输出项判定规格（圆心距/交点X/交点Y/半径1/半径2 各一行公差；UI 数据驱动 Toggle+Range）。可写以便 JSON 反序列化回填</summary>
        public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
        {
            new() { Name = "圆心距", Enabled = true, Min = 0, Max = 10, Unit = "px" },
            new() { Name = "交点X", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "交点Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px" },
            new() { Name = "半径1", Enabled = false, Min = 0, Max = 1000, Unit = "px" },
            new() { Name = "半径2", Enabled = false, Min = 0, Max = 1000, Unit = "px" },
        };

        // ============================================================
        // 元素1（卡尺1）：引导圆（圆心 + 半径）—— 全部 ParamValue 支持字段订阅
        // ============================================================
        // ★ 圆引导只有"圆心 + 半径"三个自由度（无起止角）：本工具两元素恒为整圆。
        public ParamValue<double> E1CenterX { get; set; } = ParamValue<double>.CreateConstant(0, "E1CenterX");
        public ParamValue<double> E1CenterY { get; set; } = ParamValue<double>.CreateConstant(0, "E1CenterY");
        public ParamValue<double> E1Radius { get; set; } = ParamValue<double>.CreateConstant(0, "E1Radius");

        // ---- 元素1 卡尺参数（MeasureLength1 = 径向半宽；MeasureLength2 = 切向半长）----
        public ParamValue<double> E1MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E1MeasureLength1");
        public ParamValue<double> E1MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0, "E1MeasureLength2");
        public ParamValue<int> E1NumMeasures { get; set; } = ParamValue<int>.CreateConstant(16, "E1NumMeasures");
        public ParamValue<double> E1Sigma { get; set; } = ParamValue<double>.CreateConstant(1.0, "E1Sigma");
        public ParamValue<double> E1Threshold { get; set; } = ParamValue<double>.CreateConstant(30.0, "E1Threshold");
        public ParamValue<EdgeTransition> E1Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E1Transition");
        public ParamValue<EdgeSelect> E1Select { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E1Select");
        public ParamValue<double> E1DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(5.0, "E1DistanceThreshold");

        // ============================================================
        // 元素2（卡尺2）：引导圆（圆心 + 半径）—— 与元素1 同构
        // ============================================================
        public ParamValue<double> E2CenterX { get; set; } = ParamValue<double>.CreateConstant(0, "E2CenterX");
        public ParamValue<double> E2CenterY { get; set; } = ParamValue<double>.CreateConstant(0, "E2CenterY");
        public ParamValue<double> E2Radius { get; set; } = ParamValue<double>.CreateConstant(0, "E2Radius");

        // ---- 元素2 卡尺参数 ----
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
        public GraphicsStyle GuideCircle1Style { get; set; } = new()
        {
            Name = "引导圆1", IsVisible = true,
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
        public GraphicsStyle FitCircle1Style { get; set; } = new()
        {
            Name = "拟合圆1", IsVisible = true,
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

        /// <summary>圆心连线（两拟合圆心之间的测量基准线，虚线）</summary>
        public GraphicsStyle CenterLineStyle { get; set; } = new()
        {
            Name = "圆心连线", IsVisible = true,
            LineDashStyle = LineDashStyle.DashDot, LineWidth = 1.0,
        };

        /// <summary>圆心距数值文字</summary>
        public TextStyle DistanceTextStyle { get; set; } = new()
        {
            Name = "圆心距", IsVisible = true,
        };

        public TextStyle Element1CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺1中心", IsVisible = true,
        };
        public TextStyle Element1RadiusTextStyle { get; set; } = new()
        {
            Name = "卡尺1半径", IsVisible = true,
        };
        public TextStyle Element2CenterTextStyle { get; set; } = new()
        {
            Name = "卡尺2中心", IsVisible = true,
        };
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
