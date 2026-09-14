using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// Roi 参数拼装几何合成器：按 <see cref="Roi.ComposeShapeType"/> 决定形状模板，
    /// 逐键从 <see cref="Roi.ParameterBindings"/>（键 = RoiShape 扁平参数名）取值，合成 SDK 几何对象。
    /// <list type="bullet">
    /// <item>绑定型参数：经输出契约 <see cref="OutputVariableResolver"/> 解析上游值（标量或数组）；</item>
    /// <item>常量型参数：直接取 <see cref="ParamSetting.ConstantValue"/>；</item>
    /// <item>未绑定/解析失败：使用形状默认值（矩形 100×100 于原点，圆半径 50 于原点，直线 (0,0)-(100,100)）。</item>
    /// </list>
    /// 支持**向量化多实例拼装**：绑定参数解析为数组（字段叶无索引 `[Type=X][].Geometry.X` = 该类型全部图形，
    /// N 颗 → double[N]）时，按 N 逐颗合成 N 个几何（"有几个解析几个"）；常量/标量参数广播到每颗。
    /// 产物与 <see cref="RoiShape.Geometry"/> 同类型体系，供 Hal/Cv 适配层转像素区域、UI 渲染共用。
    /// 语义定位：取代历史 <c>ComputedRoi</c> 的逐参数拼装职责（Hal 侧 <c>BuildFromComputedRegion</c>）。
    /// </summary>
    public static class RoiComposeResolver
    {
        /// <summary>
        /// 从节点结果解析拼装几何（便捷入口；绑定经 OutputVariableResolver 走输出契约 SSOT）。
        /// </summary>
        /// <param name="roi">拼装模式的 Roi</param>
        /// <param name="nodeResults">上游节点执行结果</param>
        /// <returns>SDK 几何对象序列（每个 = 上游该类型一颗图形），非拼装模式返回空序列</returns>
        public static IReadOnlyList<object?> ResolveComposedGeometries(Roi roi, IDictionary<string, ToolResults> nodeResults)
        {
            if (roi == null || !roi.IsComposed || roi.ParameterBindings == null)
                return Array.Empty<object?>();

            var rawValues = new Dictionary<string, object?>();
            foreach (var kv in roi.ParameterBindings)
            {
                rawValues[kv.Key] = ResolveBindingRawValue(kv.Value, nodeResults);
            }
            return ComposeAllByTemplate(roi.DeclaredShapeType, rawValues);
        }

        /// <summary>
        /// 向量化多实例拼装（"有几个解析几个"）：每个扁平参数原始值可为
        /// <c>double/int/long/float</c>（广播到每颗）或 <c>double[]</c>（长度 N = 该类型图形数，逐颗取值）。
        /// 多个数组长度不一致 → 显式报错（rule-015，禁止静默截断/错位）；未绑定键 → 模板默认。
        /// </summary>
        /// <param name="shapeType">拼装目标形状</param>
        /// <param name="rawValues">扁平参数名 → 原始值（标量 / double[] / null）</param>
        /// <returns>SDK 几何对象序列（长度 = N，N=1 等价单实例拼装）</returns>
        public static IReadOnlyList<object?> ComposeAllByTemplate(
            ShapeType shapeType,
            IReadOnlyDictionary<string, object?> rawValues)
        {
            // 确定颗数 N：取各数组长度（长度 1 数组按广播处理，不提升 N）
            int count = 1;
            foreach (var kv in rawValues)
            {
                if (kv.Value is not Array arr || arr.Length <= 1)
                    continue;
                if (count == 1)
                    count = arr.Length;
                else if (arr.Length != count)
                {
                    throw new InvalidOperationException(
                        $"几何参数拼装的数组长度不一致: 参数 '{kv.Key}' 为 {arr.Length} 颗，" +
                        $"其余参数为 {count} 颗（绑定无索引字段 = 上游该类型图形数，各参数应同源同数；" +
                        $"如需取单颗请在绑定路径中手写 [N] 索引）。");
                }
            }

            var results = new List<object?>(count);
            for (int i = 0; i < count; i++)
            {
                // 第 i 颗：数组按索引取该颗值、标量/常量广播；null/缺失返回 null → 模板默认值。
                double? ResolveAt(string name)
                    => ToScalar(rawValues.TryGetValue(name, out var v) ? v : null, i, name);
                results.Add(ComposeByTemplate(shapeType, ResolveAt));
            }
            return results;
        }

        /// <summary>取参数原始值在第 i 颗的标量值：数组按索引、标量广播、null/缺失 → null（用默认）。</summary>
        private static double? ToScalar(object? raw, int index, string name)
        {
            switch (raw)
            {
                case null:
                    return null;
                case double d:
                    return d;
                case float f:
                    return f;
                case int i:
                    return i;
                case long l:
                    return l;
                case Array arr when arr.Length == 1:
                    return ToScalar(arr.GetValue(0), index, name);
                case Array arr when index >= 0 && index < arr.Length:
                    return ToScalar(arr.GetValue(index), index, name);
                case string s when double.TryParse(s, out var parsed):
                    return parsed;
                default:
                    throw new InvalidOperationException(
                        $"几何参数拼装 '{name}' 的值类型 {raw.GetType().Name} 不支持（仅支持数值或 double[]）");
            }
        }

        /// <summary>
        /// 按模板合成单个几何（纯函数，便于单元测试）。
        /// </summary>
        /// <param name="shapeType">拼装目标形状（ComposeShapeType / DeclaredShapeType）</param>
        /// <param name="resolve">扁平参数名 → 数值（null=无值用默认）</param>
        public static object? ComposeByTemplate(ShapeType shapeType, Func<string, double?> resolve)
        {
            double Get(string name, double defaultValue)
                => resolve(name) ?? defaultValue;

            return shapeType switch
            {
                ShapeType.Point => new Point2D(Get("CenterX", 0), Get("CenterY", 0)),
                ShapeType.Line => BuildLine(Get("StartX", 0), Get("StartY", 0), Get("EndX", 100), Get("EndY", 100)),
                ShapeType.Circle => new Circle2d(Get("CenterX", 0), Get("CenterY", 0), Get("Radius", 50)),
                ShapeType.RotatedRectangle => new RotatedRect2d(
                    Get("CenterX", 0), Get("CenterY", 0),
                    Get("Width", 100), Get("Height", 100), Get("Angle", 0)),
                // 圆环/弧形（本版补全）：扁平参数名与 RoiFieldSchema / 参数行**同源**，
                // 默认值与脚本默认几何（ScriptTypeMapper）保持一致。
                // ★ 多边形**不在此列**：其几何是顶点数组，扁平参数无法表达顶点 → 拼装器无从实现
                //   （若列入即"能选不能用"）。多边形的绑定出口是区域订阅的 [Type=Polygon] 整片绑定。
                ShapeType.Annulus => new Annulus2d(
                    Get("CenterX", 0), Get("CenterY", 0),
                    Get("InnerRadius", 30), Get("OuterRadius", 80),
                    Get("StartAngle", 0), Get("EndAngle", 2 * Math.PI)),
                ShapeType.Arc => new Arc2d(
                    Get("CenterX", 0), Get("CenterY", 0), Get("Radius", 50),
                    Get("StartAngle", 0), Get("EndAngle", Math.PI)),
                _ => null,
            };
        }

        private static Line2d BuildLine(double sx, double sy, double ex, double ey)
        {
            var start = new Point(sx, sy);
            var end = new Point(ex, ey);
            if ((end - start).LengthSquared < 1e-20)
            {
                // 两点重合退化为默认方向（防 FromTwoPoints 抛参）
                end = new Point(sx + 100, sy);
            }
            return Line2d.FromTwoPoints(start, end);
        }

        /// <summary>
        /// 解析单个扁平参数的**原始值**（向量化拼装用，不做标量收敛）：
        /// 绑定 → 输出契约解析（可为标量 double，或字段叶无索引的 double[] = 该类型全部图形）；
        /// 常量 → ConstantValue；未知键/失败 → null（走默认）。
        /// </summary>
        private static object? ResolveBindingRawValue(
            ParamSetting setting,
            IDictionary<string, ToolResults> nodeResults)
        {
            switch (setting.BindingType)
            {
                case BindingType.Constant:
                    return setting.ConstantValue;

                case BindingType.Binding:
                    if (string.IsNullOrEmpty(setting.SourceNodeId) || string.IsNullOrEmpty(setting.SourceProperty))
                        return null;
                    try
                    {
                        return OutputVariableResolver.Resolve(nodeResults, setting.SourceNodeId!, setting.SourceProperty!);
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[RoiComposeResolver] 拼装参数绑定解析失败: {ex.Message}",
                            "RoiComposeResolver");
                        return null;
                    }

                default:
                    return null;
            }
        }
    }
}
