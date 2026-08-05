using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.BlobAnalysis;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>
/// 工具结果类"通用类型化"契约测试（方案防御机制：防具体类型回流）。
/// 反射扫描工具结果类的公开属性类型，拦截"具体算法库/UI 专属类型"泄漏：
///   - OpenCvSharp.*（如 OpenCvSharp.Size、Mat）
///   - System.Windows.Media.*（如 Matrix、Brush）
///   - System.Drawing.* / System.Windows.Media.Imaging.*
/// 这些类型一旦回流进结果类契约即测试失败（rule-015 报错优先）。
/// 允许 SDK 通用类型（IVisionImage/IVisionRegion/IVisionContour/几何/数值/字符串）+ BCL 类型。
/// </summary>
public class ToolResultsTypeSanityTests
{
    /// <summary>返回 false = 该类型是"具体算法库/UI 专属类型"，禁止出现在结果类。</summary>
    private static bool IsAllowed(Type type)
    {
        if (type == null) return true;
        var ns = type.Namespace ?? string.Empty;

        // 具体实现/UI 专属命名空间：拦截（含数组/泛型元素的同名命名空间判断由调用方完成）
        if (ns.StartsWith("OpenCvSharp") ||
            ns.StartsWith("System.Windows.Media") ||
            ns.StartsWith("System.Drawing") ||
            ns.StartsWith("System.Windows.Media.Imaging"))
            return false;

        // 数组类型：递归检查元素类型
        if (type.IsArray && type.HasElementType)
            return IsAllowed(type.GetElementType());

        // 泛型类型（List<T> 等）：递归检查泛型参数
        if (type.IsGenericType)
            return type.GetGenericArguments().All(a => a != null && IsAllowed(a));

        return true;
    }

    /// <summary>断言结果类型的所有公开属性类型未泄漏具体算法库/UI 类型，违规项汇总列出。</summary>
    private static void AssertNoConcreteTypeLeak(Type resultType)
    {
        var offenders = resultType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsAllowed(p.PropertyType))
            .Select(p => $"{resultType.Name}.{p.Name} : {p.PropertyType}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"结果类 {resultType.Name} 泄漏了具体算法库/UI 专属类型：\n{string.Join("\n", offenders)}\n" +
            "工具结果类只允许 SDK 通用类型（IVisionImage/IVisionRegion/IVisionContour/几何/数值/字符串），" +
            "不允许 OpenCvSharp / System.Windows.Media / System.Drawing 等具体类型泄漏。");
    }

    [Fact]
    public void BlobAnalysisResults_NoConcreteTypeLeak()
    {
        AssertNoConcreteTypeLeak(typeof(BlobAnalysisResults));
    }

    [Fact]
    public void BlobResult_NoConcreteTypeLeak()
    {
        AssertNoConcreteTypeLeak(typeof(BlobResult));
    }
}
