using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Tool.PointLineMeasure
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
    /// 点-线测量参数（v4 C3 范式落点，套线-线/线-圆/圆-圆模板）。
    /// 关键差异（对比前 3 工具）：元素1 = 点（仅参数订阅 X/Y，无引导绘制入口），
    /// 元素2 = 线（引导 + 卡尺 measure）。
    /// </summary>
    [JsonDerivedType(typeof(PointLineMeasureParameters), "PointLineMeasure")]
    // P0.6-4 承载层拆分：测量工具不使用区域 → 继承"图像源 + 位置修正 + 测量引导"基类
    public class PointLineMeasureParameters : MeasureGuideParameters
    {
        /// <summary>结果页唯一配置源：模块、项目、顺序和样式均由工具在构造时声明。</summary>
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
        // 元素2（线）：引导 + 卡尺参数（全部纯 CLR 支持字段订阅）
        // ============================================================
        public double E2StartX { get; set; } = 100;
        public double E2StartY { get; set; } = 200;
        public double E2EndX { get; set; } = 300;
        public double E2EndY { get; set; } = 200;

        public double E2MeasureLength1 { get; set; } = 5.0;
        public double E2MeasureLength2 { get; set; } = 20.0;
        public int E2NumMeasures { get; set; } = 30;

        public double E2Sigma { get; set; } = 1.0;
        public double E2Threshold { get; set; } = 30.0;
        public EdgeTransition E2Transition { get; set; } = EdgeTransition.All;
        public EdgeSelect E2Select { get; set; } = EdgeSelect.All;
        public double E2DistanceThreshold { get; set; } = 3.5;

        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();

            config.Judgments.Add(new() { Key = "distance", Name = "距离", Enabled = true, Min = 0, Max = 1000, Unit = "px", Order = 10 });
            config.Judgments.Add(new() { Key = "foot-x", Name = "垂足X", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 20 });
            config.Judgments.Add(new() { Key = "foot-y", Name = "垂足Y", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px", Order = 30 });

            config.Graphics.Add(new() { Key = "point-1", Name = "点1", Group = "输入", Order = 10, Style = new() { Name = "点1", IsVisible = true, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "guide-line-2", Name = "引导线2", Group = "测量过程", Order = 20, Style = new() { Name = "引导线2", IsVisible = true } });
            config.Graphics.Add(new() { Key = "caliper-band-2", Name = "卡尺2", Group = "测量过程", Order = 30, Style = new() { Name = "卡尺2", IsVisible = true } });
            config.Graphics.Add(new() { Key = "fit-line-2", Name = "拟合结果2", Group = "测量结果", Order = 40, Style = new() { Name = "拟合线2", IsVisible = true, LineWidth = 2.0 } });
            config.Graphics.Add(new() { Key = "caliper-point-2", Name = "卡尺2拟合点", Group = "测量过程", Order = 50, Style = new() { Name = "卡尺2检测点", IsVisible = true, CrossSize = 4.0 } });
            config.Graphics.Add(new() { Key = "foot-point", Name = "垂足", Group = "测量结果", Order = 60, Style = new() { Name = "垂足", IsVisible = true, CrossSize = 6.0 } });
            config.Graphics.Add(new() { Key = "foot-line", Name = "垂线", Group = "测量结果", Order = 70, Style = new() { Name = "垂线", IsVisible = true, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0 } });

            config.Texts.Add(new() { Key = "point-1", Name = "点1", Group = "输入", Order = 10, Style = new() { Name = "点1坐标", IsVisible = false } });
            config.Texts.Add(new() { Key = "line-center", Name = "线2中心", Group = "测量结果", Order = 20, Style = new() { Name = "线2中心", IsVisible = true } });
            config.Texts.Add(new() { Key = "line-angle", Name = "线2角度", Group = "测量结果", Order = 30, Style = new() { Name = "线2角度", IsVisible = true } });
            config.Texts.Add(new() { Key = "foot", Name = "垂足", Group = "测量结果", Order = 40, Style = new() { Name = "垂足", IsVisible = true } });
            config.Texts.Add(new() { Key = "distance", Name = "距离", Group = "测量结果", Order = 50, Style = new() { Name = "距离", IsVisible = true } });
            return config;
        }
    }
}
