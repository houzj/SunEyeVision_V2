using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何值（Circle2d/Line2d/Point2d/…）的可绑定字段声明。
    /// 字段路径形如 <c>Center.X</c>/<c>Radius</c>，运行期由 OutputVariableResolver
    /// 按 <c>数组.字段</c> 逐元素投影解析（集合语义：返回该几何数组全部元素的该字段）。
    /// </summary>
    public sealed record GeometryFieldDef(string DisplayName, string Path, Type ValueType)
    {
        /// <summary>是否为数值字段（数字互通 Integer↔Double 候选）</summary>
        public bool IsNumeric => ValueType == typeof(double) || ValueType == typeof(int)
            || ValueType == typeof(long) || ValueType == typeof(float);
    }

    /// <summary>
    /// 几何契约类型字段 Schema（声明式、不依赖运行值，设计期即可生成绑定树）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="RoiFieldSchema"/> 的区别：ROI 容器是异构数组（一筐图形混装多种类型），
    /// 需按 <c>ShapeType</c> 分组并以 <c>[Type=X]</c> 过滤；几何数组是同质数组
    /// （<c>Circle2d[]</c> 全为圆），直接按 CLR 几何类型取字段表，无需类型过滤。
    ///
    /// 仅物化可绑定的标量字段。<see cref="Polygon2d"/> 顶点数组不可作为标量字段下钻，
    /// 故不注册字段（与 ROI 的 Polygon 处理一致，仅整体可绑）。
    /// </remarks>
    public static class GeometryFieldSchema
    {
        private static readonly Dictionary<Type, IReadOnlyList<GeometryFieldDef>> FieldsByType = new()
        {
            [typeof(Circle2d)] = new[]
            {
                new GeometryFieldDef("圆心X", "Center.X", typeof(double)),
                new GeometryFieldDef("圆心Y", "Center.Y", typeof(double)),
                new GeometryFieldDef("半径", "Radius", typeof(double)),
            },
            [typeof(Line2d)] = new[]
            {
                new GeometryFieldDef("起点X", "Point.X", typeof(double)),
                new GeometryFieldDef("起点Y", "Point.Y", typeof(double)),
                new GeometryFieldDef("方向X", "Direction.X", typeof(double)),
                new GeometryFieldDef("方向Y", "Direction.Y", typeof(double)),
            },
            [typeof(Point2d)] = new[]
            {
                new GeometryFieldDef("X", "X", typeof(double)),
                new GeometryFieldDef("Y", "Y", typeof(double)),
            },
            [typeof(RotatedRect2d)] = new[]
            {
                new GeometryFieldDef("中心X", "Center.X", typeof(double)),
                new GeometryFieldDef("中心Y", "Center.Y", typeof(double)),
                new GeometryFieldDef("宽", "Width", typeof(double)),
                new GeometryFieldDef("高", "Height", typeof(double)),
                new GeometryFieldDef("角度", "Angle", typeof(double)),
            },
            [typeof(Arc2d)] = new[]
            {
                new GeometryFieldDef("圆心X", "Center.X", typeof(double)),
                new GeometryFieldDef("圆心Y", "Center.Y", typeof(double)),
                new GeometryFieldDef("半径", "Radius", typeof(double)),
                new GeometryFieldDef("起始角", "StartAngle", typeof(double)),
                new GeometryFieldDef("终止角", "EndAngle", typeof(double)),
            },
            [typeof(Annulus2d)] = new[]
            {
                new GeometryFieldDef("圆心X", "Center.X", typeof(double)),
                new GeometryFieldDef("圆心Y", "Center.Y", typeof(double)),
                new GeometryFieldDef("内半径", "InnerRadius", typeof(double)),
                new GeometryFieldDef("外半径", "OuterRadius", typeof(double)),
                new GeometryFieldDef("起始角", "StartAngle", typeof(double)),
                new GeometryFieldDef("终止角", "EndAngle", typeof(double)),
            },
        };

        /// <summary>
        /// 获取指定几何类型的可绑定字段表（空表 = 无字段可下钻，如 Polygon2d）。
        /// </summary>
        public static IReadOnlyList<GeometryFieldDef> GetFields(Type geometryType)
            => FieldsByType.TryGetValue(geometryType, out var fields)
                ? fields
                : Array.Empty<GeometryFieldDef>();

        /// <summary>是否存在该几何类型的字段声明。</summary>
        public static bool HasFields(Type geometryType) => FieldsByType.ContainsKey(geometryType);
    }
}
