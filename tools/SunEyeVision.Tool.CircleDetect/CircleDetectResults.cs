using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.CircleDetect
{
    /// <summary>
    /// 圆查找工具执行结果
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology：引导圆 + 测量带阵列 → 拟合圆/圆弧
    /// 结果渲染通过 Display 收集器（轮廓点/拟合圆/圆心文本）叠加到图像。
    /// </remarks>
    public class CircleDetectResults : ToolResults
    {
        /// <summary>
        /// 拟合圆数组（整圆模式）
        /// </summary>
        public Circle2d[]? Circle { get; set; }

        /// <summary>
        /// 拟合圆弧数组（圆弧模式）
        /// </summary>
        public Arc2d[]? Arc { get; set; }

        /// <summary>
        /// 轮廓点（每个卡尺提取的拟合点，实际参与拟合 = used_edges）
        /// </summary>
        public Point2d[] MeasurementPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>
        /// 诊断检测点（GetMetrologyObjectMeasures 检测到的全部边缘点，与拟合结果无关）。
        /// 默认不显示，Tab3 勾选"诊断检测点"后用于定位拟合失败原因（点在哪、为什么不共圆）。
        /// </summary>
        public Point2d[] DiagnosticPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>
        /// 被屏蔽区剔除的采样点（不参与拟合；默认不显示，Tab3 勾选"被屏蔽剔除点"后调试屏蔽效果）
        /// </summary>
        public Point2d[] ExcludedPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>
        /// HALCON 实际测量带轮廓（MeasureContour XLD → 每段轮廓点集）。
        /// 默认不显示，Tab3 勾选"HALCON测量带"后与本地卡尺叠加对比对齐偏差。
        /// </summary>
        public System.Windows.Point[][] HalconMeasureBands { get; set; } = Array.Empty<System.Windows.Point[]>();

        /// <summary>
        /// 拟合残差（RMSE，来自 Halcon 'rms'）
        /// </summary>
        public double RMSE { get; set; }

        /// <summary>
        /// 有效边缘点数（测量带数量）
        /// </summary>
        public int EdgePointCount { get; set; }

        /// <summary>
        /// 圆心（文本显示）
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// 判定结果（半径/中心X/中心Y/拟合误差/匹配点数均满足 = true；不用于执行状态）
        /// </summary>
        public bool IsPass { get; set; }

        #region 测量带(卡尺)输入回显（模块结果"输入区域/卡尺"展示，Run 从参数回填）

        /// <summary>引导几何（引导圆/圆弧 → ShapeParameters 包装，树中按"卡尺/引导线"下钻展示）</summary>
        [JsonIgnore]
        public SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters[]? GuideGeometry { get; set; }

        /// <summary>测量带数量（卡尺数，参数回显）</summary>
        public int CaliberCount { get; set; }

        /// <summary>测量带宽度（法线全长，参数回显）</summary>
        public double CaliberWidth { get; set; }

        /// <summary>测量带长度（切线全长，参数回显）</summary>
        public double CaliberLength { get; set; }

        /// <summary>边缘极性（参数回显文本）</summary>
        public string? CaliberTransition { get; set; }

        #endregion

        /// <summary>
        /// 几何描述（拟合结果文本，供结果显示）
        /// </summary>
        [JsonIgnore]
        public string GeometryDescription =>
            Circle is { Length: > 0 }
                ? $"拟合圆: 圆心({CenterPoint.X:F1}, {CenterPoint.Y:F1}) 半径 {Circle[0].Radius:F1}"
                : Arc is { Length: > 0 }
                    ? $"拟合圆弧: 圆心({CenterPoint.X:F1}, {CenterPoint.Y:F1}) 半径 {Arc[0].Radius:F1}"
                    : "未生成拟合结果";

        /// <summary>
        /// 填充显示项（在 Run 里调用：轮廓点 + 拟合圆 + 圆心文本，样式从 Parameters 传入）
        /// </summary>
        public void FillDisplay(bool isOk, CircleDetectParameters parameters)
        {
            _display.Clear();

            // 轮廓点（实际参与拟合的点）
            if (MeasurementPoints.Length > 0)
            {
                var pts = new System.Windows.Point[MeasurementPoints.Length];
                for (int i = 0; i < MeasurementPoints.Length; i++)
                    pts[i] = new System.Windows.Point(MeasurementPoints[i].X, MeasurementPoints[i].Y);
                // ★ 修复：AddPoints → AddCrosses。拟合点是十字标记，须走 GraphicsType.Cross 渲染
                //   （RenderCrosses 尊重 style.CrossSize）；AddPoints 走 RenderPoints 十字尺寸硬编码 2.5，
                //   导致拟合点十字远小于 Blob 质心（Blob 用 AddCross + CrossSize=8）。
                _display.AddCrosses(parameters.ContourPointStyle, pts);
            }

            // 诊断检测点（GetMetrologyObjectMeasures 全部检测点，默认不显示，Tab3 可控）
            if (parameters.DiagnosticPointStyle.IsVisible && DiagnosticPoints.Length > 0)
            {
                var diagPts = new System.Windows.Point[DiagnosticPoints.Length];
                for (int i = 0; i < DiagnosticPoints.Length; i++)
                    diagPts[i] = new System.Windows.Point(DiagnosticPoints[i].X, DiagnosticPoints[i].Y);
                _display.AddCrosses(parameters.DiagnosticPointStyle, diagPts);
            }

            // 被屏蔽剔除点（屏蔽区内的采样点，默认不显示，Tab3 可控）
            if (parameters.ExcludedPointStyle.IsVisible && ExcludedPoints.Length > 0)
            {
                var exclPts = new System.Windows.Point[ExcludedPoints.Length];
                for (int i = 0; i < ExcludedPoints.Length; i++)
                    exclPts[i] = new System.Windows.Point(ExcludedPoints[i].X, ExcludedPoints[i].Y);
                _display.AddCrosses(parameters.ExcludedPointStyle, exclPts);
            }

            // HALCON 实际测量带（诊断卡尺对齐，默认不显示，Tab3 可控）
            if (parameters.HalconBandStyle.IsVisible && HalconMeasureBands.Length > 0)
            {
                _display.AddContours(parameters.HalconBandStyle, HalconMeasureBands, isClosed: true);
            }

            // 拟合圆/圆弧 → 结果层统一画完整拟合圆（弧模式也不画弧）
            if (Circle is { Length: > 0 })
            {
                _display.AddCircle(parameters.FitResultStyle, Circle[0], isOk);
            }
            else if (Arc is { Length: > 0 })
            {
                var arc = Arc[0];
                _display.AddCircle(parameters.FitResultStyle,
                    new Circle2d(arc.Center, arc.Radius), isOk);
            }

            // 圆心标记（十字）
            if (parameters.CenterPointStyle.IsVisible &&
                (Circle is { Length: > 0 } || Arc is { Length: > 0 }))
            {
                _display.AddCross(parameters.CenterPointStyle, CenterPoint, isOk);
            }

            // 圆心坐标文本
            if (parameters.CenterTextStyle.IsVisible)
                _display.AddText(parameters.CenterTextStyle, CenterPoint.X, CenterPoint.Y,
                    $"圆心 ({CenterPoint.X:F1}, {CenterPoint.Y:F1})", isOk);
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                // 测量带(卡尺)输入回显：引导线/测量带 归"输入区域/卡尺"分组
                new("GuideGeometry",     "引导几何", "输入区域/卡尺/引导线"),
                new("CaliberCount",      "测量带数量", "输入区域/卡尺/测量带"),
                new("CaliberWidth",      "测量带宽度", "输入区域/卡尺/测量带"),
                new("CaliberLength",     "测量带长度", "输入区域/卡尺/测量带"),
                new("CaliberTransition", "边缘极性",   "输入区域/卡尺/测量带"),
                new("MeasurementPoints", "卡尺检测点", "输入区域/卡尺/测量带"),
                new("Circle",           "拟合圆",   "拟合结果"),
                new("Arc",              "拟合圆弧", "拟合结果"),
                new("RMSE",             "拟合残差", "拟合结果"),
                new("EdgePointCount",   "边缘点数", "拟合结果"),
                // 注：CenterPoint（WPF Point）仅供显示文本，不作为输出变量（圆心已在 Circle/Arc 几何内）
            ];
        }
    }
}
