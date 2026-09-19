using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Tool.PointCircleMeasure
{
    /// <summary>边缘极性（卡尺参数语义）</summary>
    public enum EdgeTransition
    {
        All, Positive, Negative,
    }

    /// <summary>卡尺测点选取模式</summary>
    public enum EdgeSelect
    {
        All, First, Last,
    }

    /// <summary>
    /// 点-圆测量参数（v4 C3 范式落点，套线-线/线-圆/圆-圆/点-线模板）。
    /// 关键差异（对比前 4 工具）：元素1 = 点（仅参数订阅 X/Y，无引导绘制入口），
    /// 元素2 = 圆（引导圆心/半径 + 径向卡尺 measure）。
    /// </summary>
    [JsonDerivedType(typeof(PointCircleMeasureParameters), "PointCircleMeasure")]
    // P0.6-4 承载层拆分：测量工具不使用区域 → 继承"图像源 + 位置修正 + 测量引导"基类
    public class PointCircleMeasureParameters : MeasureGuideParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，六工具同构）
        // ============================================================
        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        // ============================================================
        // 元素1（点）：输入方式二分「按图形 / 按参数」（无引导绘制入口 —— v4 与线/圆等引导元素的核心区别）
        // 三工具统一范式：点-线 / 点-圆 / 点-点 的点元素共用同一套输入语义。
        // ============================================================
        private PointInputMode _p1InputMode = PointInputMode.ByShape;

        /// <summary>点1 输入方式：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）</summary>
        public PointInputMode P1InputMode
        {
            get => _p1InputMode;
            set => SetProperty(ref _p1InputMode, value);
        }

        /// <summary>按图形：整体绑定上游点图形输出（词目 Point），一次绑定即得 X/Y</summary>
        public Point2d[] P1Point { get; set; } = System.Array.Empty<Point2d>();

        /// <summary>按参数：X 坐标（常量输入或上游标量绑定）</summary>
        public double P1X { get; set; } = 100;

        /// <summary>按参数：Y 坐标（常量输入或上游标量绑定）</summary>
        public double P1Y { get; set; } = 100;

        /// <summary>绑定源路径（由参数绑定 UI 写入节点参数设置）。</summary>
        [JsonIgnore]
        public string P1PointBindingSource { get; set; } = string.Empty;

        // ============================================================
        // 元素2（圆）：引导（圆心 + 半径）+ 卡尺参数（全部纯 CLR 支持字段订阅）
        // ============================================================
        public double E2CenterX { get; set; } = 200;
        public double E2CenterY { get; set; } = 200;
        public double E2Radius { get; set; } = 100;

        /// <summary>径向半宽（卡尺带宽度的一半，沿半径方向）</summary>
        public double E2MeasureLength1 { get; set; } = 20.0;
        /// <summary>切向半长（卡尺带长度的一半，沿圆周方向）</summary>
        public double E2MeasureLength2 { get; set; } = 5.0;
        public int E2NumMeasures { get; set; } = 16;

        public double E2Sigma { get; set; } = 1.0;
        public double E2Threshold { get; set; } = 30.0;
        public EdgeTransition E2Transition { get; set; } = EdgeTransition.All;
        public EdgeSelect E2Select { get; set; } = EdgeSelect.All;
        public double E2DistanceThreshold { get; set; } = 3.5;

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐线-圆/圆-圆范式）
        // ============================================================





        /// <summary>圆周最近点标记（点1 到拟合圆的最短距离落点）</summary>

        /// <summary>点1 → 圆心 连线（主测量"圆心距"的几何表达）</summary>

        /// <summary>点1 → 圆周最近点 连线（辅助测量"圆周距"的几何表达）</summary>








        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "center-distance", Name = "圆心距", Enabled = true, Min = 0, Max = 1000, Unit = "px", Order = 10 });
            config.Judgments.Add(new() { Key = "circumference-distance", Name = "圆周距", Enabled = false, Min = 0, Max = 1000, Unit = "px", Order = 20 });
            config.Judgments.Add(new() { Key = "radius", Name = "半径", Enabled = false, Min = 0, Max = 1000, Unit = "px", Order = 30 });
            config.Judgments.Add(new() { Key = "closest-x", Name = "最近点X", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 40 });
            config.Judgments.Add(new() { Key = "closest-y", Name = "最近点Y", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 50 });
            config.Graphics.Add(new() { Key = "point1", Name = "点1", Order = 10, Style = new() { Name = "点1", IsVisible = true, CrossSize = 4.0, } });
            config.Graphics.Add(new() { Key = "guide-circle2", Name = "引导圆2", Order = 20, Style = new() { Name = "引导圆2", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "caliber-band2", Name = "卡尺2", Order = 30, Style = new() { Name = "卡尺2", IsVisible = true, } });
            config.Graphics.Add(new() { Key = "fit-circle2", Name = "拟合圆2", Order = 40, Style = new() { Name = "拟合圆2", IsVisible = true, LineWidth = 2.0, } });
            config.Graphics.Add(new() { Key = "caliber-point2", Name = "卡尺2检测点", Order = 50, Style = new() { Name = "卡尺2检测点", IsVisible = true, CrossSize = 4.0, } });
            config.Graphics.Add(new() { Key = "closest-point", Name = "圆周最近点", Order = 60, Style = new() { Name = "圆周最近点", IsVisible = true, CrossSize = 6.0, } });
            config.Graphics.Add(new() { Key = "center-line", Name = "圆心连线", Order = 70, Style = new() { Name = "圆心连线", IsVisible = true, LineWidth = 1.0, } });
            config.Graphics.Add(new() { Key = "distance-line", Name = "圆周距连线", Order = 80, Style = new() { Name = "圆周距连线", IsVisible = true, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0, } });
            config.Texts.Add(new() { Key = "point1-text", Name = "点1坐标", Order = 10, Style = new() { Name = "点1坐标", IsVisible = false, } });
            config.Texts.Add(new() { Key = "circle-center-text", Name = "圆2中心", Order = 20, Style = new() { Name = "圆2中心", IsVisible = true, } });
            config.Texts.Add(new() { Key = "circle-radius-text", Name = "圆2半径", Order = 30, Style = new() { Name = "圆2半径", IsVisible = true, } });
            config.Texts.Add(new() { Key = "closest-point-text", Name = "最近点", Order = 40, Style = new() { Name = "最近点", IsVisible = true, } });
            config.Texts.Add(new() { Key = "distance-text", Name = "圆心距", Order = 50, Style = new() { Name = "圆心距", IsVisible = true, } });
            return config;
        }
    }
}
