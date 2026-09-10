using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// ROI 单元素几何（RoiShape.Geometry）的可绑定字段声明。
    /// 提供 ShapeType → 字段表 的静态映射，供绑定树在设计期（无上游运行值）生成
    /// "类型分组 → 字段"叶子，字段叶子路径形如 <c>Center.X</c>/<c>Radius</c>，
    /// 运行期由 OutputVariableResolver 按 <c>[i].Geometry.&lt;字段&gt;</c> 解析真实值。
    /// </summary>
    public sealed record RoiFieldDef(string DisplayName, string Path, Type ValueType)
    {
        /// <summary>是否为数值字段（数字互通 Integer↔Double 候选）</summary>
        public bool IsNumeric => ValueType == typeof(double) || ValueType == typeof(int)
            || ValueType == typeof(long) || ValueType == typeof(float);
    }

    /// <summary>
    /// ROI 几何字段 Schema（声明式、不依赖运行值）。
    /// 依据：几何类型 <c>GetMonitorFields</c>/公共数据字段（Circle2d: Center/Radius；RotatedRect2d: Center/Width/Height/Angle 等）
    /// 仅物化可绑定的标量与 Point/Vector 下钻字段，Polygon 顶点数组不可作为标量字段下钻。
    /// </summary>
    public static class RoiFieldSchema
    {
        /// <summary>
        /// 获取指定形状类型在 ROI 单元素上的可绑定字段表。
        /// 空表 = 该类型无可下钻标量字段（如多边形，仅整体可绑）。
        /// </summary>
        public static IReadOnlyList<RoiFieldDef> GetFields(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Circle => new[]
                {
                    new RoiFieldDef("圆心X", "Center.X", typeof(double)),
                    new RoiFieldDef("圆心Y", "Center.Y", typeof(double)),
                    new RoiFieldDef("半径", "Radius", typeof(double)),
                },
                ShapeType.RotatedRectangle => new[]
                {
                    new RoiFieldDef("中心X", "Center.X", typeof(double)),
                    new RoiFieldDef("中心Y", "Center.Y", typeof(double)),
                    new RoiFieldDef("宽", "Width", typeof(double)),
                    new RoiFieldDef("高", "Height", typeof(double)),
                    new RoiFieldDef("角度", "Angle", typeof(double)),
                },
                ShapeType.Line => new[]
                {
                    new RoiFieldDef("起点X", "Point.X", typeof(double)),
                    new RoiFieldDef("起点Y", "Point.Y", typeof(double)),
                    new RoiFieldDef("方向X", "Direction.X", typeof(double)),
                    new RoiFieldDef("方向Y", "Direction.Y", typeof(double)),
                },
                ShapeType.Point => new[]
                {
                    new RoiFieldDef("X", "X", typeof(double)),
                    new RoiFieldDef("Y", "Y", typeof(double)),
                },
                ShapeType.Annulus => new[]
                {
                    new RoiFieldDef("圆心X", "Center.X", typeof(double)),
                    new RoiFieldDef("圆心Y", "Center.Y", typeof(double)),
                    new RoiFieldDef("内半径", "InnerRadius", typeof(double)),
                    new RoiFieldDef("外半径", "OuterRadius", typeof(double)),
                    new RoiFieldDef("起始角", "StartAngle", typeof(double)),
                    new RoiFieldDef("终止角", "EndAngle", typeof(double)),
                },
                ShapeType.Arc => new[]
                {
                    new RoiFieldDef("圆心X", "Center.X", typeof(double)),
                    new RoiFieldDef("圆心Y", "Center.Y", typeof(double)),
                    new RoiFieldDef("半径", "Radius", typeof(double)),
                    new RoiFieldDef("起始角", "StartAngle", typeof(double)),
                    new RoiFieldDef("终止角", "EndAngle", typeof(double)),
                    new RoiFieldDef("大弧", "IsMajorArc", typeof(bool)),
                },
                _ => Array.Empty<RoiFieldDef>(),
            };
        }

        /// <summary>
        /// 判断该形状类型是否存在数值字段（供 ROI 容器可达词汇集合判断，double/int 目标放行依据）。
        /// </summary>
        public static bool HasNumericField(ShapeType shapeType)
            => GetFields(shapeType).Any(f => f.IsNumeric);
    }
}
