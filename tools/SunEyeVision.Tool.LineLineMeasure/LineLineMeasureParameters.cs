using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.LineLineMeasure
{
    /// <summary>边缘极性（卡尺参数语义，同 LineDetect）</summary>
    public enum EdgeTransition
    {
        All, Positive, Negative,      // 全部 / 正极性（暗到亮）/ 负极性（亮到暗）
    }

    /// <summary>卡尺测点选取模式（卡尺参数语义，同 LineDetect）</summary>
    public enum EdgeSelect
    {
        All, First, Last,
    }

    /// <summary>
    /// 线-线测量参数（v4 C3 范式落点）。
    /// 关键差异（对比 LineDetect）：引导/卡尺数值全部为 纯 CLR 参数，
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
        private ResultPresentationConfig _resultPresentation = CreateResultPresentation();
        public ResultPresentationConfig ResultPresentation
        {
            get => _resultPresentation;
            set
            {
                _resultPresentation = value ?? throw new System.ArgumentNullException(nameof(value));
                EdgeFitDiagnostics.EnsurePresentation(_resultPresentation);
                FittedLineDisplay.RemoveLegacyExtensions(value, "extension-line1");
                FittedLineDisplay.RemoveLegacyExtensions(value, "extension-line2");
            }
        }

        // ============================================================
        // 元素1（卡尺1）：引导直线（起点/终点）—— 全部纯 CLR 支持字段订阅
        // ============================================================
        // 默认无引导（对齐直线查找：绘制了才有）；坐标归 0，校验长度即报"未绘制"。
        // ★ 必须是可写属性：JSON 反序列化只回填带 setter 的属性，get-only 会导致方案保存后再打开引导/卡尺参数回默认。
        public double E1StartX { get; set; } = 0;
        public double E1StartY { get; set; } = 0;
        public double E1EndX { get; set; } = 0;
        public double E1EndY { get; set; } = 0;

        // ---- 元素1 卡尺参数（半长口径，同 LineDetect）----
        public double E1MeasureLength1 { get; set; } = 20.0;
        public double E1MeasureLength2 { get; set; } = 5.0;
        public int E1NumMeasures { get; set; } = 10;
        public double E1Sigma { get; set; } = 1.0;
        public double E1Threshold { get; set; } = 30.0;
        public EdgeTransition E1Transition { get; set; } = EdgeTransition.All;
        public EdgeSelect E1Select { get; set; } = EdgeSelect.All;
        public double E1DistanceThreshold { get; set; } = 5.0;

        // ============================================================
        // 元素2（卡尺2）：引导直线 + 卡尺参数（默认与 E1 呈约 26.6° 夹角示例）
        // ============================================================
        // ★ 可写属性（JSON 反序列化回填，参见 E1）
        public double E2StartX { get; set; } = 0;
        public double E2StartY { get; set; } = 0;
        public double E2EndX { get; set; } = 0;
        public double E2EndY { get; set; } = 0;

        public double E2MeasureLength1 { get; set; } = 20.0;
        public double E2MeasureLength2 { get; set; } = 5.0;
        public int E2NumMeasures { get; set; } = 10;
        public double E2Sigma { get; set; } = 1.0;
        public double E2Threshold { get; set; } = 30.0;
        public EdgeTransition E2Transition { get; set; } = EdgeTransition.All;
        public EdgeSelect E2Select { get; set; } = EdgeSelect.All;
        public double E2DistanceThreshold { get; set; } = 5.0;

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐直线查找 Tab3 范式）
        // ============================================================










        /// <summary>元素1 无限直线延长线（虚线；拟合线段为实线，延长段表达"交点取自无限直线"）</summary>

        /// <summary>元素2 无限直线延长线（虚线）</summary>

        /// <summary>两线夹角角标弧（交点处，半径见 <see cref="AngleArcRadius"/>）</summary>


        /// <summary>夹角角标半径（px，图像坐标；仅显示层使用，不参与测量与绑定）</summary>
        public double AngleArcRadius { get; set; } = 50.0;

        /// <summary>夹角数值文字（交点附近角平分方向）</summary>


        /// <summary>距离连线（平行场景画公垂段：线1中点 → 线2垂足）</summary>


        /// <summary>距离数值文字（间距/最短距离）</summary>


        /// <summary>最小法向距离投影段（默认隐藏，按需开启；极值点 → 线2 垂足）</summary>


        /// <summary>最大法向距离投影段（默认隐藏，按需开启）</summary>







        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "angle", Name = "角度", Enabled = true, Min = 0, Max = 90, Unit = "°", Order = 10 });
            config.Judgments.Add(new() { Key = "distance", Name = "距离", Enabled = false, Min = 0, Max = 10, Unit = "px", Order = 20 });
            config.Judgments.Add(new() { Key = "intersection-x", Name = "交点X", Enabled = false, Min = -10000, Max = 10000, Unit = "px", Order = 30 });
            config.Judgments.Add(new() { Key = "intersection-y", Name = "交点Y", Enabled = false, Min = -10000, Max = 10000, Unit = "px", Order = 40 });
            config.Graphics.Add(new() { Key = "guide-line1", Name = "引导线1", Order = 10, Style = new() { Name = "引导线1", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "guide-line2", Name = "引导线2", Order = 20, Style = new() { Name = "引导线2", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "caliber-band1", Name = "卡尺1", Order = 30, Style = new() { Name = "卡尺1", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "caliber-band2", Name = "卡尺2", Order = 40, Style = new() { Name = "卡尺2", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "fit-line1", Name = "拟合结果1", Order = 50, Style = new() { Name = "拟合结果1", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "fit-line2", Name = "拟合结果2", Order = 60, Style = new() { Name = "拟合结果2", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "caliber-point1", Name = "卡尺1拟合点", Order = 70, Style = new() { Name = "卡尺1拟合点", IsVisible = true, CrossSize = 4.0, } });
            config.Graphics.Add(new() { Key = "caliber-point2", Name = "卡尺2拟合点", Order = 80, Style = new() { Name = "卡尺2拟合点", IsVisible = true, CrossSize = 4.0, } });
            config.Graphics.Add(new() { Key = "intersection-point", Name = "角点", Order = 90, Style = new() { Name = "角点", IsVisible = true, CrossSize = 6.0, } });
            config.Graphics.Add(new() { Key = "angle-arc", Name = "夹角弧", Order = 120, Style = new() { Name = "夹角弧", IsVisible = true, LineWidth = 1.5, } });
            config.Texts.Add(new() { Key = "angle-text", Name = "夹角", Order = 10, Style = new() { Name = "夹角", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "distance-line", Name = "距离连线", Order = 130, Style = new() { Name = "距离连线", IsVisible = true,
            LineDashStyle = LineDashStyle.DashDot, LineWidth = 1.0, } });
            config.Texts.Add(new() { Key = "distance-text", Name = "距离", Order = 20, Style = new() { Name = "距离", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "distance-min-line", Name = "最小距离线", Order = 140, Style = new() { Name = "最小距离线", IsVisible = false,
            LineDashStyle = LineDashStyle.Dot, LineWidth = 1.0, } });
            config.Graphics.Add(new() { Key = "distance-max-line", Name = "最大距离线", Order = 150, Style = new() { Name = "最大距离线", IsVisible = false,
            LineDashStyle = LineDashStyle.Dot, LineWidth = 1.0, } });
            config.Texts.Add(new() { Key = "element1-center-text", Name = "卡尺1中心", Order = 30, Style = new() { Name = "卡尺1中心", IsVisible = true, } });
            config.Texts.Add(new() { Key = "element1-angle-text", Name = "卡尺1角度", Order = 40, Style = new() { Name = "卡尺1角度", IsVisible = true, } });
            config.Texts.Add(new() { Key = "element2-center-text", Name = "卡尺2中心", Order = 50, Style = new() { Name = "卡尺2中心", IsVisible = true, } });
            config.Texts.Add(new() { Key = "element2-angle-text", Name = "卡尺2角度", Order = 60, Style = new() { Name = "卡尺2角度", IsVisible = true, } });
            config.Texts.Add(new() { Key = "intersection-text", Name = "交点", Order = 70, Style = new() { Name = "交点", IsVisible = true, } });
            EdgeFitDiagnostics.EnsurePresentation(config);
            return config;
        }
    }
}
