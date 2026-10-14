using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Tool.PointPointMeasure
{
    /// <summary>
    /// 点-点测量参数（v4 C3 范式落点，点类工具第三件）。
    /// 关键差异（对比其余 5 工具）：两个元素均为点、均无引导绘制入口，
    /// 全部由参数订阅承载（不产生 measure 调用，纯几何计算）。
    /// </summary>
    [JsonDerivedType(typeof(PointPointMeasureParameters), "PointPointMeasure")]
    // P0.6-4 承载层拆分：测量工具**不使用区域**（Probe 实测：无 InspectionGeometries/MaskGeometries 赋值），
    // 故继承"只含 图像源 + 位置修正 + 测量引导"的基类，参数类上不再物理存在 InspectionRois/MaskRois。
    public class PointPointMeasureParameters : MeasureGuideParameters, IBindingInputActivityProvider
    {
        public bool IsBindingInputActive(string key) => key switch
        {
            nameof(P1Point) => P1InputMode == PointInputMode.ByShape,
            nameof(P1X) or nameof(P1Y) => P1InputMode == PointInputMode.ByCoordinate,
            nameof(P2Point) => P2InputMode == PointInputMode.ByShape,
            nameof(P2X) or nameof(P2Y) => P2InputMode == PointInputMode.ByCoordinate,
            _ => true
        };
        // ============================================================
        // 判定规格列表（v4 C1 基座，六工具同构）
        // ============================================================
        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        // ============================================================
        // 元素1（点）：输入方式二分「按图形 / 按参数」（无引导绘制入口）
        // 三工具统一范式：点-线 / 点-圆 / 点-点 的点元素共用同一套输入语义。
        // ============================================================
        private PointInputMode _p1InputMode = PointInputMode.ByShape;

        /// <summary>点1 输入方式：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）</summary>
        public PointInputMode P1InputMode
        {
            get => _p1InputMode;
            set => SetProperty(ref _p1InputMode, value);
        }

        /// <summary>
        /// 按图形：绑定上游点图形输出（词目 Point）。**数组口子**（决策 4）：来多少算多少 ——
        /// 单元素即原"一次绑定即得 X/Y"语义；多元素配对（向量化）由 6d 承接。
        /// </summary>
        public Point2d[] P1Point { get; set; } = System.Array.Empty<Point2d>();

        /// <summary>按参数：X 坐标（常量输入或上游标量绑定）</summary>
        public double P1X { get; set; } = 100;

        /// <summary>按参数：Y 坐标（常量输入或上游标量绑定）</summary>
        public double P1Y { get; set; } = 100;

        /// <summary>绑定源路径（由参数绑定 UI 写入节点参数设置）。</summary>
        [JsonIgnore]
        public string P1PointBindingSource { get; set; } = string.Empty;

        // ============================================================
        // 元素2（点）：与元素1 同构（两个点元素均为纯订阅）
        // ============================================================
        private PointInputMode _p2InputMode = PointInputMode.ByShape;

        /// <summary>点2 输入方式：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）</summary>
        public PointInputMode P2InputMode
        {
            get => _p2InputMode;
            set => SetProperty(ref _p2InputMode, value);
        }

        /// <summary>
        /// 按图形：绑定上游点图形输出（词目 Point）。**数组口子**（决策 4）：同 P1Point。
        /// </summary>
        public Point2d[] P2Point { get; set; } = System.Array.Empty<Point2d>();

        /// <summary>按参数：X 坐标（常量输入或上游标量绑定）</summary>
        public double P2X { get; set; } = 300;

        /// <summary>按参数：Y 坐标（常量输入或上游标量绑定）</summary>
        public double P2Y { get; set; } = 200;

        /// <summary>绑定源路径（由参数绑定 UI 写入节点参数设置）。</summary>
        [JsonIgnore]
        public string P2PointBindingSource { get; set; } = string.Empty;

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示）
        // ============================================================


        /// <summary>两点连线（主测量"点距"的几何表达）</summary>

        /// <summary>X 分量辅助线（点1 → 投影点，虚线）</summary>

        /// <summary>Y 分量辅助线（投影点 → 点2，虚线）</summary>






        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "point-distance", Name = "点距", Enabled = true, Min = 0, Max = 1000, Unit = "px", Order = 10 });
            config.Judgments.Add(new() { Key = "delta-x", Name = "ΔX", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 20 });
            config.Judgments.Add(new() { Key = "delta-y", Name = "ΔY", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 30 });
            config.Judgments.Add(new() { Key = "line-angle", Name = "连线角度", Enabled = false, Min = -180, Max = 180, Unit = "°", Order = 40 });
            config.Graphics.Add(new() { Key = "point1", Name = "点1", Order = 10, Style = new() { Name = "点1", IsVisible = true, CrossSize = 6.0, } });
            config.Graphics.Add(new() { Key = "point2", Name = "点2", Order = 20, Style = new() { Name = "点2", IsVisible = true, CrossSize = 6.0, } });
            config.Graphics.Add(new() { Key = "distance-line", Name = "点距连线", Order = 30, Style = new() { Name = "点距连线", IsVisible = true, LineWidth = 1.5, } });
            config.Graphics.Add(new() { Key = "delta-xline", Name = "ΔX辅助线", Order = 40, Style = new() { Name = "ΔX辅助线", IsVisible = false, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0, } });
            config.Graphics.Add(new() { Key = "delta-yline", Name = "ΔY辅助线", Order = 50, Style = new() { Name = "ΔY辅助线", IsVisible = false, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0, } });
            config.Texts.Add(new() { Key = "point1-text", Name = "点1坐标", Order = 10, Style = new() { Name = "点1坐标", IsVisible = true, } });
            config.Texts.Add(new() { Key = "point2-text", Name = "点2坐标", Order = 20, Style = new() { Name = "点2坐标", IsVisible = true, } });
            config.Texts.Add(new() { Key = "distance-text", Name = "点距", Order = 30, Style = new() { Name = "点距", IsVisible = true, } });
            return config;
        }
    }
}
