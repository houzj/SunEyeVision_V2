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
    /// <item>绑定型参数：经输出契约 <see cref="OutputVariableResolver"/> 解析上游值；</item>
    /// <item>常量型参数：直接取 <see cref="ParamSetting.ConstantValue"/>；</item>
    /// <item>未绑定/解析失败：使用形状默认值（矩形 100×100 于原点，圆半径 50 于原点，直线 (0,0)-(100,100)）。</item>
    /// </list>
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
        /// <returns>SDK 几何对象（Circle2d/RotatedRect2d/Rect/Line2d/Point2D/Polygon2d），非拼装模式返回 null</returns>
        public static object? ResolveComposedGeometry(Roi roi, IDictionary<string, ToolResults> nodeResults)
        {
            if (roi == null || !roi.IsComposed || roi.ParameterBindings == null)
                return null;

            return ComposeByTemplate(
                roi.DeclaredShapeType,
                name => ResolveBindingValue(roi.ParameterBindings, name, nodeResults));
        }

        /// <summary>
        /// 按模板合成几何（纯函数，便于单元测试）。
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
                ShapeType.Rectangle => BuildRect(Get("CenterX", 0), Get("CenterY", 0), Get("Width", 100), Get("Height", 100)),
                ShapeType.RotatedRectangle => new RotatedRect2d(
                    Get("CenterX", 0), Get("CenterY", 0),
                    Get("Width", 100), Get("Height", 100), Get("Angle", 0)),
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

        private static Rect BuildRect(double cx, double cy, double w, double h)
        {
            // 轴对齐矩形：中心 + 宽高 → Rect(左上 + 尺寸)
            return new Rect(cx - w / 2, cy - h / 2, Math.Max(w, 0), Math.Max(h, 0));
        }

        /// <summary>
        /// 解析单个扁平参数值：绑定 → 输出契约解析；常量 → ConstantValue；未知键/失败 → null（走默认）。
        /// </summary>
        private static double? ResolveBindingValue(
            Dictionary<string, ParamSetting> bindings,
            string parameterName,
            IDictionary<string, ToolResults> nodeResults)
        {
            if (!bindings.TryGetValue(parameterName, out var setting) || setting == null)
                return null;

            object? value;
            switch (setting.BindingType)
            {
                case BindingType.Constant:
                    value = setting.ConstantValue;
                    break;

                case BindingType.Binding:
                    if (string.IsNullOrEmpty(setting.SourceNodeId) || string.IsNullOrEmpty(setting.SourceProperty))
                        return null;
                    try
                    {
                        value = OutputVariableResolver.Resolve(nodeResults, setting.SourceNodeId!, setting.SourceProperty!);
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"[RoiComposeResolver] 拼装参数 {parameterName} 绑定解析失败: {ex.Message}",
                            "RoiComposeResolver");
                        return null;
                    }
                    break;

                default:
                    return null;
            }

            return value switch
            {
                double d => d,
                int i => i,
                long l => l,
                float f => f,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => null,
            };
        }
    }
}
