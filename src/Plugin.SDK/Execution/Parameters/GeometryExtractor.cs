using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 「抠几何」——把 ROI 容器（<c>RoiShape[]</c>）按目标几何类型提取为**真几何数组**（方案 P1 改动 1）。
/// </summary>
/// <remarks>
/// <para><b>解决的问题</b>：绑定 <c>检测区图形[Type=Circle]</c> 时，运行期拿到的一直是 <c>RoiShape[]</c>
/// （容器本身），下游工具还得自己取 <c>.Geometry</c> 并做类型判断 —— 工序重复且容易漏判。
/// 本类把它收敛为一步：目标词目是几何值（Point/Circle/Line/RotatedRect/Polygon/Annulus/Arc）时，
/// 直接产出对应几何数组。</para>
/// <para><b>不接管的场景</b>（保持原值，交调用方既有逻辑）：</para>
/// <list type="bullet">
/// <item>值不是 <c>RoiShape</c> 集合（例如已是几何数组、标量、图像）；</item>
/// <item>目标词目是 <c>ROI</c>（区域订阅：要的就是容器本身）；</item>
/// <item>目标词目非几何值（数值/图像/字符串等）。</item>
/// </list>
/// <para><b>失败即报错</b>（rule-015）：目标类型与元素几何类型不符时返回错误，**不静默跳过元素**。</para>
/// </remarks>
public static class GeometryExtractor
{
    /// <summary>
    /// 尝试抠几何。
    /// </summary>
    /// <param name="value">已解析出的运行时值（通常为 <c>RoiShape[]</c>）</param>
    /// <param name="targetType">目标参数类型（如 <c>Circle2d[]</c> / <c>RoiShape[]</c> / <c>double</c>）</param>
    /// <param name="extracted">提取结果（目标几何数组）</param>
    /// <param name="error">失败原因（非 <c>null</c> 表示已接管且失败）</param>
    /// <returns>是否已接管（<c>false</c> = 不适用，调用方保持原值）</returns>
    public static bool TryExtract(object? value, Type targetType, out object? extracted, out string? error)
    {
        extracted = null;
        error = null;

        if (value is not System.Collections.IEnumerable enumerable || value is string)
            return false;

        var shapes = new List<RoiShape>();
        foreach (var item in enumerable)
        {
            if (item is RoiShape shape)
                shapes.Add(shape);
        }

        if (shapes.Count == 0)
            return false;

        var targetVarType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(targetType);
        if (targetVarType == null || !ShapeTypeVocabulary.IsShapeVocabulary(targetVarType))
            return false; // ROI（区域订阅）/数值/图像目标：原样返回，不抠几何

        var elementType = targetType.IsArray ? targetType.GetElementType()! : targetType;
        var array = Array.CreateInstance(elementType, shapes.Count);

        for (var i = 0; i < shapes.Count; i++)
        {
            var geometry = shapes[i].Geometry;
            if (geometry == null)
            {
                error = $"第 {i} 个 ROI 图形没有几何值（Geometry=null），无法按 '{elementType.Name}' 提取。";
                return true;
            }

            if (!elementType.IsInstanceOfType(geometry))
            {
                error = $"第 {i} 个 ROI 图形的几何类型为 '{geometry.GetType().Name}'，与目标 '{elementType.Name}' 不符"
                    + "（rule-015 报错优先，不做静默跳过）。";
                return true;
            }

            array.SetValue(geometry, i);
        }

        extracted = array;
        return true;
    }
}
