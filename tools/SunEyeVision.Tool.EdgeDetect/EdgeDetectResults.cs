using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.EdgeDetect
{
    /// <summary>
    /// 边缘检测工具执行结果
    /// </summary>
    /// <remarks>
    /// 基于 Halcon 2D Metrology：引导几何 + 测量带阵列 → 拟合出几何对象
    /// 几何对象以数组输出（对齐 SdkTypeVocabulary 数组契约），单值 = 长度 1 数组。
    /// </remarks>
    public class EdgeDetectResults : ToolResults
    {
        #region 拟合几何对象（按 GeometryType 二选一，数组输出）

        /// <summary>
        /// 拟合直线数组（Line 模式）
        /// </summary>
        public Line2d[]? Line { get; set; }

        /// <summary>
        /// 拟合圆数组（Circle 模式）
        /// </summary>
        public Circle2d[]? Circle { get; set; }

        /// <summary>
        /// 拟合圆弧数组（Arc 模式）
        /// </summary>
        public Arc2d[]? Arc { get; set; }

        /// <summary>
        /// 拟合残差（RMSE，来自 Halcon 'rms'）
        /// </summary>
        public double RMSE { get; set; }

        /// <summary>
        /// 有效边缘点数（测量带数量）
        /// </summary>
        public int EdgePointCount { get; set; }

        /// <summary>
        /// 几何描述（拟合结果文本，供结果显示）
        /// </summary>
        public string GeometryDescription
        {
            get
            {
                if (Line is { Length: > 0 })
                    return $"拟合直线: ({Line[0].Point.X:F1}, {Line[0].Point.Y:F1}) 方向 {Line[0].Angle * 180 / Math.PI:F1}°";
                if (Circle is { Length: > 0 })
                    return $"拟合圆: 圆心({Circle[0].Center.X:F1}, {Circle[0].Center.Y:F1}) 半径 {Circle[0].Radius:F1}";
                if (Arc is { Length: > 0 })
                    return $"拟合圆弧: 圆心({Arc[0].Center.X:F1}, {Arc[0].Center.Y:F1}) 半径 {Arc[0].Radius:F1}";
                return "未生成拟合结果";
            }
        }

        #endregion

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                new("Line",                 "拟合直线",               "拟合结果"),
                new("Circle",               "拟合圆",                 "拟合结果"),
                new("Arc",                  "拟合圆弧",               "拟合结果"),
                new("RMSE",                 "拟合残差",               "拟合结果"),
                new("EdgePointCount",       "边缘点数",               "拟合结果"),
            ];
        }
    }
}
