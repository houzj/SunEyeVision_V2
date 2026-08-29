using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.LineDetect
{
    /// <summary>
    /// 直线查找工具执行结果
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology：引导直线 + 测量带阵列 → 拟合直线
    /// 结果渲染通过 Display 收集器（轮廓点/拟合直线/文本）叠加到图像。
    /// </remarks>
    public class LineDetectResults : ToolResults
    {
        /// <summary>
        /// 拟合直线数组（输出契约，下游可绑定）
        /// </summary>
        public Line2d[]? Line { get; set; }

        /// <summary>
        /// 拟合线段起点（渲染用）
        /// </summary>
        public Point LineStartPoint { get; set; }

        /// <summary>
        /// 拟合线段终点（渲染用）
        /// </summary>
        public Point LineEndPoint { get; set; }

        /// <summary>
        /// 轮廓点（每个卡尺提取的拟合点）
        /// </summary>
        public Point2d[] MeasurementPoints { get; set; } = Array.Empty<Point2d>();

        /// <summary>
        /// 拟合残差（RMSE，来自 Halcon 'rms'）
        /// </summary>
        public double RMSE { get; set; }

        /// <summary>
        /// 有效边缘点数（测量带数量）
        /// </summary>
        public int EdgePointCount { get; set; }

        /// <summary>
        /// 直线中点（文本显示）
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// 判定结果（匹配点数/角度/拟合误差均满足 = true；不用于执行状态）
        /// </summary>
        public bool IsPass { get; set; }

        /// <summary>
        /// 直线角度（度，文本显示）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 几何描述（拟合结果文本，供结果显示）
        /// </summary>
        [JsonIgnore]
        public string GeometryDescription =>
            Line is { Length: > 0 }
                ? $"拟合直线: 中点({CenterPoint.X:F1}, {CenterPoint.Y:F1}) 角度 {Angle:F1}°"
                : "未生成拟合结果";

        /// <summary>
        /// 填充显示项（在 Run 里调用：轮廓点 + 拟合直线 + 文本，样式从 Parameters 传入）
        /// </summary>
        public void FillDisplay(bool isOk, LineDetectParameters parameters)
        {
            _display.Clear();

            // 轮廓点
            if (MeasurementPoints.Length > 0)
            {
                var pts = new System.Windows.Point[MeasurementPoints.Length];
                for (int i = 0; i < MeasurementPoints.Length; i++)
                    pts[i] = new System.Windows.Point(MeasurementPoints[i].X, MeasurementPoints[i].Y);
                _display.AddPoints(parameters.ContourPointStyle, pts);
            }

            // 拟合直线（线段起点/终点）
            if (Line is { Length: > 0 })
            {
                _display.AddLine(parameters.FitResultStyle, LineStartPoint, LineEndPoint, isOk);
            }

            // 中点坐标文本
            if (parameters.CenterTextStyle.IsVisible)
                _display.AddText(parameters.CenterTextStyle, CenterPoint.X, CenterPoint.Y,
                    $"中点 ({CenterPoint.X:F1}, {CenterPoint.Y:F1})", isOk);
            if (parameters.AngleTextStyle.IsVisible)
                _display.AddText(parameters.AngleTextStyle, CenterPoint.X, CenterPoint.Y - 20,
                    $"角度 {Angle:F1}°", isOk);
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                new("Line",             "拟合直线",   "拟合结果"),
                new("RMSE",             "拟合残差",   "拟合结果"),
                new("EdgePointCount",   "边缘点数",   "拟合结果"),
                new("CenterPoint",      "中点坐标",   "拟合结果"),
                new("Angle",            "直线角度",   "拟合结果"),
            ];
        }
    }
}
