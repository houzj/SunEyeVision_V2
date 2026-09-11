using System.Collections.ObjectModel;
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
        public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
        {
            new() { Name = "圆心距", Enabled = true, Min = 0, Max = 1000, Unit = "px" },
            new() { Name = "圆周距", Enabled = false, Min = 0, Max = 1000, Unit = "px" },
            new() { Name = "半径", Enabled = false, Min = 0, Max = 1000, Unit = "px" },
            new() { Name = "最近点X", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px" },
            new() { Name = "最近点Y", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px" },
        };

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
        public ParamValue<Point2d[]> P1Point { get; set; } = ParamValue<Point2d[]>.CreateConstant(System.Array.Empty<Point2d>(), "P1Point");

        /// <summary>按参数：X 坐标（常量输入或上游标量绑定）</summary>
        public ParamValue<double> P1X { get; set; } = ParamValue<double>.CreateConstant(100, "P1X");

        /// <summary>按参数：Y 坐标（常量输入或上游标量绑定）</summary>
        public ParamValue<double> P1Y { get; set; } = ParamValue<double>.CreateConstant(100, "P1Y");

        /// <summary>
        /// P1Point 绑定源的字符串投影（ParamBinding.BindingSource 契约："节点ID.属性名"）。
        /// 仅作控件桥接：真值存于 P1Point.BindingConfig（随参数一起序列化），故此处不序列化。
        /// </summary>
        [JsonIgnore]
        public string P1PointBindingSource
        {
            get
            {
                var config = P1Point.BindingConfig;
                return config != null && config.BindingType == BindingType.Binding
                       && !string.IsNullOrEmpty(config.SourceNodeId)
                    ? $"{config.SourceNodeId}.{config.SourceProperty}"
                    : string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    P1Point.BindingConfig = null;
                    return;
                }

                // nodeId 不含 '.'，其后整段是属性路径（可含几何字段下钻的嵌套点）。
                // 按 P6 约定用 IndexOf 只切首个点，避免多段路径被截断（Split 只取第二段会丢字段）。
                var dotIndex = value.IndexOf('.');
                P1Point.BindingConfig = ParamSetting.CreateBinding(
                    "P1Point",
                    dotIndex > 0 ? value[..dotIndex] : string.Empty,
                    dotIndex > 0 ? value[(dotIndex + 1)..] : string.Empty);
            }
        }

        // ============================================================
        // 元素2（圆）：引导（圆心 + 半径）+ 卡尺参数（全部 ParamValue 支持字段订阅）
        // ============================================================
        public ParamValue<double> E2CenterX { get; set; } = ParamValue<double>.CreateConstant(200, "E2CenterX");
        public ParamValue<double> E2CenterY { get; set; } = ParamValue<double>.CreateConstant(200, "E2CenterY");
        public ParamValue<double> E2Radius { get; set; } = ParamValue<double>.CreateConstant(100, "E2Radius");

        /// <summary>径向半宽（卡尺带宽度的一半，沿半径方向）</summary>
        public ParamValue<double> E2MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E2MeasureLength1");
        /// <summary>切向半长（卡尺带长度的一半，沿圆周方向）</summary>
        public ParamValue<double> E2MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0, "E2MeasureLength2");
        public ParamValue<int> E2NumMeasures { get; set; } = ParamValue<int>.CreateConstant(16, "E2NumMeasures");

        public ParamValue<double> E2Sigma { get; set; } = ParamValue<double>.CreateConstant(1.0, "E2Sigma");
        public ParamValue<double> E2Threshold { get; set; } = ParamValue<double>.CreateConstant(30.0, "E2Threshold");
        public ParamValue<EdgeTransition> E2Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E2Transition");
        public ParamValue<EdgeSelect> E2Select { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E2Select");
        public ParamValue<double> E2DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(3.5, "E2DistanceThreshold");

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示，对齐线-圆/圆-圆范式）
        // ============================================================
        public GraphicsStyle Point1Style { get; set; } = new()
        {
            Name = "点1", IsVisible = true, CrossSize = 4.0,
        };
        public GraphicsStyle GuideCircle2Style { get; set; } = new()
        {
            Name = "引导圆2", IsVisible = true,
        };
        public GraphicsStyle CaliberBand2Style { get; set; } = new()
        {
            Name = "卡尺2", IsVisible = true,
        };
        public GraphicsStyle FitCircle2Style { get; set; } = new()
        {
            Name = "拟合圆2", IsVisible = true, LineWidth = 2.0,
        };
        public GraphicsStyle CaliberPoint2Style { get; set; } = new()
        {
            Name = "卡尺2检测点", IsVisible = true, CrossSize = 4.0,
        };
        /// <summary>圆周最近点标记（点1 到拟合圆的最短距离落点）</summary>
        public GraphicsStyle ClosestPointStyle { get; set; } = new()
        {
            Name = "圆周最近点", IsVisible = true, CrossSize = 6.0,
        };
        /// <summary>点1 → 圆心 连线（主测量"圆心距"的几何表达）</summary>
        public GraphicsStyle CenterLineStyle { get; set; } = new()
        {
            Name = "圆心连线", IsVisible = true, LineWidth = 1.0,
        };
        /// <summary>点1 → 圆周最近点 连线（辅助测量"圆周距"的几何表达）</summary>
        public GraphicsStyle DistanceLineStyle { get; set; } = new()
        {
            Name = "圆周距连线", IsVisible = true, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };

        public TextStyle Point1TextStyle { get; set; } = new()
        {
            Name = "点1坐标", IsVisible = false,
        };
        public TextStyle CircleCenterTextStyle { get; set; } = new()
        {
            Name = "圆2中心", IsVisible = true,
        };
        public TextStyle CircleRadiusTextStyle { get; set; } = new()
        {
            Name = "圆2半径", IsVisible = true,
        };
        public TextStyle ClosestPointTextStyle { get; set; } = new()
        {
            Name = "最近点", IsVisible = true,
        };
        public TextStyle DistanceTextStyle { get; set; } = new()
        {
            Name = "圆心距", IsVisible = true,
        };
    }
}
