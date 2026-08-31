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
        /// 圆心（文本显示）
        /// </summary>
        public Point CenterPoint { get; set; }

        /// <summary>
        /// 判定结果（半径/中心X/中心Y/拟合误差/匹配点数均满足 = true；不用于执行状态）
        /// </summary>
        public bool IsPass { get; set; }

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

            // 轮廓点
            if (MeasurementPoints.Length > 0)
            {
                var pts = new System.Windows.Point[MeasurementPoints.Length];
                for (int i = 0; i < MeasurementPoints.Length; i++)
                    pts[i] = new System.Windows.Point(MeasurementPoints[i].X, MeasurementPoints[i].Y);
                _display.AddPoints(parameters.ContourPointStyle, pts);
            }

            // 拟合圆/圆弧
            if (Circle is { Length: > 0 })
            {
                _display.AddCircle(parameters.FitResultStyle, Circle[0], isOk);
            }
            else if (Arc is { Length: > 0 })
            {
                // 圆弧用轮廓点近似（Arc2d 采样）
                var arc = Arc[0];
                var pts = new System.Windows.Point[48];
                for (int i = 0; i < 48; i++)
                {
                    double t = (double)i / 48;
                    double angle = arc.StartAngle + arc.AngularSpan * t;
                    pts[i] = new System.Windows.Point(
                        arc.Center.X + arc.Radius * Math.Cos(angle),
                        arc.Center.Y + arc.Radius * Math.Sin(angle));
                }
                _display.AddContour(parameters.FitResultStyle, pts, isOk, false);
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
                new("Circle",           "拟合圆",   "拟合结果"),
                new("Arc",              "拟合圆弧", "拟合结果"),
                new("RMSE",             "拟合残差", "拟合结果"),
                new("EdgePointCount",   "边缘点数", "拟合结果"),
                // 注：CenterPoint（WPF Point）仅供显示文本，不作为输出变量（圆心已在 Circle/Arc 几何内）
            ];
        }
    }
}
