using System.Collections.ObjectModel;
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
    public class PointPointMeasureParameters : MeasureGuideParameters
    {
        // ============================================================
        // 判定规格列表（v4 C1 基座，六工具同构）
        // ============================================================
        public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
        {
            new() { Name = "点距", Enabled = true, Min = 0, Max = 1000, Unit = "px" },
            new() { Name = "ΔX", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px" },
            new() { Name = "ΔY", Enabled = false, Min = -1e5, Max = 1e5, Unit = "px" },
            new() { Name = "连线角度", Enabled = false, Min = -180, Max = 180, Unit = "°" },
        };

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

        /// <summary>按图形：整体绑定上游点图形输出（词目 Point），一次绑定即得 X/Y</summary>
        public ParamValue<Point2d> P1Point { get; set; } = ParamValue<Point2d>.CreateConstant(default, "P1Point");

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
        // 元素2（点）：与元素1 同构（两个点元素均为纯订阅）
        // ============================================================
        private PointInputMode _p2InputMode = PointInputMode.ByShape;

        /// <summary>点2 输入方式：按图形（整体绑上游点图形输出）/ 按参数（X/Y 逐参数）</summary>
        public PointInputMode P2InputMode
        {
            get => _p2InputMode;
            set => SetProperty(ref _p2InputMode, value);
        }

        /// <summary>按图形：整体绑定上游点图形输出（词目 Point），一次绑定即得 X/Y</summary>
        public ParamValue<Point2d> P2Point { get; set; } = ParamValue<Point2d>.CreateConstant(default, "P2Point");

        /// <summary>按参数：X 坐标（常量输入或上游标量绑定）</summary>
        public ParamValue<double> P2X { get; set; } = ParamValue<double>.CreateConstant(300, "P2X");

        /// <summary>按参数：Y 坐标（常量输入或上游标量绑定）</summary>
        public ParamValue<double> P2Y { get; set; } = ParamValue<double>.CreateConstant(200, "P2Y");

        /// <summary>P2Point 绑定源的字符串投影（语义同 P1PointBindingSource）</summary>
        [JsonIgnore]
        public string P2PointBindingSource
        {
            get
            {
                var config = P2Point.BindingConfig;
                return config != null && config.BindingType == BindingType.Binding
                       && !string.IsNullOrEmpty(config.SourceNodeId)
                    ? $"{config.SourceNodeId}.{config.SourceProperty}"
                    : string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    P2Point.BindingConfig = null;
                    return;
                }

                // nodeId 不含 '.'，其后整段是属性路径（可含几何字段下钻的嵌套点）。
                // 按 P6 约定用 IndexOf 只切首个点，避免多段路径被截断（Split 只取第二段会丢字段）。
                var dotIndex = value.IndexOf('.');
                P2Point.BindingConfig = ParamSetting.CreateBinding(
                    "P2Point",
                    dotIndex > 0 ? value[..dotIndex] : string.Empty,
                    dotIndex > 0 ? value[(dotIndex + 1)..] : string.Empty);
            }
        }

        // ============================================================
        // 结果显示样式（Tab3 绑定：图形显示 + 文本显示）
        // ============================================================
        public GraphicsStyle Point1Style { get; set; } = new()
        {
            Name = "点1", IsVisible = true, CrossSize = 6.0,
        };
        public GraphicsStyle Point2Style { get; set; } = new()
        {
            Name = "点2", IsVisible = true, CrossSize = 6.0,
        };
        /// <summary>两点连线（主测量"点距"的几何表达）</summary>
        public GraphicsStyle DistanceLineStyle { get; set; } = new()
        {
            Name = "点距连线", IsVisible = true, LineWidth = 1.5,
        };
        /// <summary>X 分量辅助线（点1 → 投影点，虚线）</summary>
        public GraphicsStyle DeltaXLineStyle { get; set; } = new()
        {
            Name = "ΔX辅助线", IsVisible = false, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };
        /// <summary>Y 分量辅助线（投影点 → 点2，虚线）</summary>
        public GraphicsStyle DeltaYLineStyle { get; set; } = new()
        {
            Name = "ΔY辅助线", IsVisible = false, LineDashStyle = LineDashStyle.Dash, LineWidth = 1.0,
        };

        public TextStyle Point1TextStyle { get; set; } = new()
        {
            Name = "点1坐标", IsVisible = true,
        };
        public TextStyle Point2TextStyle { get; set; } = new()
        {
            Name = "点2坐标", IsVisible = true,
        };
        public TextStyle DistanceTextStyle { get; set; } = new()
        {
            Name = "点距", IsVisible = true,
        };
    }
}
