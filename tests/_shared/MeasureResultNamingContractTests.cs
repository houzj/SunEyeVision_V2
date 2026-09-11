using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.TestShared;

/// <summary>
/// 测量工具"结果命名规范"契约测试（用户拍板，rule-016 声明式校验）。
/// </summary>
/// <remarks>
/// 背景：结果树/绑定树的显示名曾泄漏**实现域词汇**——内部槽位编号「元素1 / 元素2」，
/// 用户没有这个概念（"点测量的绑定树为什么会叫元素1？"）。
///
/// 命名规范（三条）：
/// <list type="number">
/// <item><b>三层语义分组</b>：顶层组名只允许 <c>输入</c> / <c>拟合结果</c> / <c>结果</c>
///   （+ 基类 <c>执行信息</c>）——分别回答"值从哪来 / 过程产物 / 最终量"；</item>
/// <item><b>语义名替代槽位号</b>：组名与显示名**不得出现内部槽位词「元素」**，
///   一律用具名元素（点1/点2/直线1/直线2/圆1/圆2）；</item>
/// <item><b>编号保留</b>：同类多元素（线-线/圆-圆/点-点）用"语义名+编号"区分，不歧义。</item>
/// </list>
///
/// 接入方式：测试 csproj 加一行
///   &lt;Compile Include="..\_shared\MeasureResultNamingContractTests.cs" Link="_shared\MeasureResultNamingContractTests.cs" /&gt;
/// 本测试按"测试程序集名去掉 .Tests = 工具名"约定只校验**自己所属**的工具，
/// 因此每个测量工具测试项目都会守卫自身命名（无跨项目耦合）。
/// </remarks>
public class MeasureResultNamingContractTests
{
    /// <summary>顶层组名白名单（R1）。</summary>
    private static readonly HashSet<string> AllowedTopLevelGroups = new(StringComparer.Ordinal)
    {
        "输入", "拟合结果", "结果", "执行信息",
    };

    /// <summary>内部槽位词黑名单（R2）——用户不可理解的实现域词汇。</summary>
    private const string ForbiddenSlotWord = "元素";

    [Fact]
    public void MeasureTool_ResultNaming_UsesUserVocabularyAndThreeLayerGroups()
    {
        var toolName = ResolveMeasureToolName();
        if (toolName == null)
            return; // 非测量工具测试项目：本契约不适用（接入本文件即安全）

        var map = LoadPropertyMap(toolName);

        Assert.NotEmpty(map);

        foreach (var entry in map)
        {
            // R2：显示名不得含内部槽位词，且非空
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName),
                $"[{toolName}] 空显示名: {entry.PropertyName}");
            Assert.False(entry.DisplayName.Contains(ForbiddenSlotWord, StringComparison.Ordinal),
                $"[{toolName}] 显示名泄漏内部槽位词: '{entry.DisplayName}'（属性 {entry.PropertyName}）");

            if (string.IsNullOrEmpty(entry.GroupName))
                continue;

            Assert.False(entry.GroupName.Contains(ForbiddenSlotWord, StringComparison.Ordinal),
                $"[{toolName}] 组名泄漏内部槽位词: '{entry.GroupName}'（属性 {entry.PropertyName}）");

            // R1：顶层组名必须来自三层语义（+ 执行信息）
            var top = entry.GroupName.Split('/')[0];
            Assert.True(AllowedTopLevelGroups.Contains(top),
                $"[{toolName}] 顶层组名 '{top}' 不在允许集合 {{{string.Join(" / ", AllowedTopLevelGroups)}}}（属性 {entry.PropertyName}）");
        }
    }

    /// <summary>由测试程序集名推导被测工具名；非 "SunEyeVision.Tool.*Measure.Tests" 返回 null。</summary>
    private static string? ResolveMeasureToolName()
    {
        var testAsmName = typeof(MeasureResultNamingContractTests).Assembly.GetName().Name ?? string.Empty;
        if (!testAsmName.EndsWith(".Tests", StringComparison.Ordinal))
            return null;

        var toolName = testAsmName[..^".Tests".Length];
        return toolName.StartsWith("SunEyeVision.Tool.", StringComparison.Ordinal)
               && toolName.EndsWith("Measure", StringComparison.Ordinal)
            ? toolName
            : null;
    }

    /// <summary>从 plugins 部署目录加载该工具，实例化其 ToolResults 并取输出契约。</summary>
    private static List<PropertyMapEntry> LoadPropertyMap(string toolAssemblyName)
    {
        var assembly = ToolPluginTestLoader.EnsureLoaded(toolAssemblyName);

        var resultsType = assembly.GetTypes()
            .FirstOrDefault(t => !t.IsAbstract && typeof(ToolResults).IsAssignableFrom(t))
            ?? throw new InvalidOperationException($"[{toolAssemblyName}] 未找到 ToolResults 派生类型");

        var results = (ToolResults)Activator.CreateInstance(resultsType)!;
        return results.GetPropertyMap();
    }
}
