using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>
/// 一条框架级契约条目的完整声明。
/// </summary>
/// <param name="RequiredCapability">产出该条目所需的能力（未声明则条目不产出）。</param>
/// <param name="PropertyName">结果类上的属性名（契约键 / 绑定路径根）。</param>
/// <param name="DisplayName">结果树与绑定下拉显示名（用户域中文）。</param>
/// <param name="GroupName">结果树顶层组名。</param>
/// <param name="ValueType">契约类型（如 <c>RoiShape[]</c>，词目由 <c>SdkTypeVocabulary</c> 解析）。</param>
public sealed record FrameworkContractEntry(
    ToolCapabilities RequiredCapability,
    string PropertyName,
    string DisplayName,
    string GroupName,
    Type ValueType);

/// <summary>
/// 框架级契约条目<b>唯一真相源</b>。
/// </summary>
/// <remarks>
/// ★ 历史缺陷（P0.6-2 要根治）：同一批框架契约条目在<b>两个文件各写一遍</b>——
///   运行期 <c>ToolResults.GetOutputVariables()</c>、设计期 <c>ToolRegistry.RegisterRoiContractMetadata</c>，
///   属性名 / 显示名 / 组名 / 类型四要素重复声明，改一处必漏另一处（改名时已踩过）。
///   现把四要素收敛到本表，两阶段<b>同源生成</b>：
/// <list type="bullet">
/// <item>运行期：<c>ToolResults.GetOutputVariables()</c> 遍历 <see cref="Resolve"/>；</item>
/// <item>设计期：<c>ToolRegistry</c> 遍历同一个 <see cref="Resolve"/>。</item>
/// </list>
/// </remarks>
public static class FrameworkContractSchema
{
    /// <summary>检测区图形（区域能力的框架输出）。</summary>
    public static readonly FrameworkContractEntry InspectionGeometries = new(
        ToolCapabilities.RoiInput,
        nameof(ToolResults.InspectionGeometries),
        "检测区图形",
        "输入",
        typeof(RoiShape[]));

    /// <summary>屏蔽区图形（区域能力的框架输出）。</summary>
    public static readonly FrameworkContractEntry MaskGeometries = new(
        ToolCapabilities.RoiInput,
        nameof(ToolResults.MaskGeometries),
        "屏蔽区图形",
        "输入",
        typeof(RoiShape[]));

    /// <summary>框架级契约条目全表（顺序即结果树展示顺序）。</summary>
    public static readonly IReadOnlyList<FrameworkContractEntry> All = new[]
    {
        InspectionGeometries,
        MaskGeometries,
    };

    /// <summary>
    /// 按能力解析应产出的框架契约条目 —— 运行期与设计期的<b>唯一判定入口</b>。
    /// </summary>
    /// <remarks>
    /// 结构与值分离（P0.6-3）：本条只决定<b>结构</b>（条目在不在），与"值是否为空"无关；
    /// 空值由渲染层出占位 <c>--</c>，<b>不从结构里删条目</b>（消除"有时有行、有时没行"的抖动）。
    /// </remarks>
    public static IEnumerable<FrameworkContractEntry> Resolve(ToolCapabilities capabilities)
        => All.Where(entry => capabilities.HasFlag(entry.RequiredCapability));
}
