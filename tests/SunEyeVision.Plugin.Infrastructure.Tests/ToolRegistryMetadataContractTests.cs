using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

/// <summary>
/// 守卫契约测试：设计期元数据注册表（<see cref="ToolRegistry"/>）的不变量。
/// </summary>
/// <remarks>
/// 覆盖决策 17（框架契约**唯一产出点**）在**设计期**这一侧的落地：
/// <list type="bullet">
/// <item>同一属性名在注册表内**不得出现两次** —— 历史事故：框架契约被"OutputVariables 遍历"
///   与"旁路注册"两条产线各产一遍 ⇒ 「选择绑定源 → 输入」出现**两套**「检测区图形/屏蔽区图形」；</item>
/// <item>能力门槛在注册表同样生效 —— 未声明 <see cref="ToolCapabilities.RoiInput"/> 的工具
///   不得凭空多出区域条目（历史缺陷：判定源退回"继承 RoiParameters"，对 14/14 工具恒真）。</item>
/// </list>
/// 本工程是**唯一**引用 Infrastructure 层的测试工程，专门用于锁死这一层（其他测试工程只引用 SDK/工具）。
/// </remarks>
public class ToolRegistryMetadataContractTests
{
    #region 测试用假工具（只测注册表行为，不依赖任何真实工具程序集）

    /// <summary>声明了区域能力的假工具结果。</summary>
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.RoiInput)]
    private class FakeRoiResults : ToolResults
    {
        public double Value { get; set; }
    }

    /// <summary>未声明任何能力的假工具结果（默认 None）。</summary>
    private class FakePlainResults : ToolResults
    {
        public double Value { get; set; }
    }

    private class FakeRoiParameters : ToolParameters { }

    private class FakePlainParameters : ToolParameters { }

    [Tool("FakeCapabilityProbe", "能力探针（测试专用）")]
    private class FakeCapabilityProbeTool : IToolPlugin<FakeRoiParameters, FakeRoiResults>
    {
        public FakeRoiResults Run(FakeRoiParameters parameters) => new();
    }

    [Tool("FakePlainProbe", "无能力探针（测试专用）")]
    private class FakePlainProbeTool : IToolPlugin<FakePlainParameters, FakePlainResults>
    {
        public FakePlainResults Run(FakePlainParameters parameters) => new();
    }

    #endregion

    /// <summary>框架契约属性名（检测区/屏蔽区）。</summary>
    private static readonly string[] FrameworkContractNames =
    [
        nameof(ToolResults.InspectionGeometries),
        nameof(ToolResults.MaskGeometries),
    ];

    [Fact]
    public void DesignTimeFrameworkContract_RegistersExactlyOnce()
    {
        ToolRegistry.RegisterTool(typeof(FakeCapabilityProbeTool));

        // 用"类型查询"而非按 toolId 查询：RoiShape[] 类型的元数据只可能来自框架契约条目，
        // 故本查询天然把检查范围收敛到"检测区/屏蔽区"这一对，且不依赖 toolId 的口径。
        var entries = ToolRegistry.GetMetadataByType(typeof(RoiShape[]));

        foreach (var name in FrameworkContractNames)
        {
            Assert.Equal(1, entries.Count(e => e.PropertyName == name));
        }

        // 更广的不变式：注册表内同一属性名不得重复（重复 ⇒ 绑定树出现"两套"条目）
        var duplicated = entries
            .GroupBy(e => e.PropertyName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicated);
    }

    [Fact]
    public void DesignTimeCapabilityGate_UnannotatedTool_AddsNoFrameworkEntries()
    {
        ToolRegistry.RegisterTool(typeof(FakePlainProbeTool));

        // 未声明区域能力的工具**不得**产出任何 RoiShape[] 契约条目
        //（历史缺陷：判定源是"继承 RoiParameters"，对 14/14 工具恒真 ⇒ 测量工具凭空多出区域条目）
        var entries = ToolRegistry.GetMetadataByType(typeof(RoiShape[]));
        Assert.DoesNotContain(entries, e => FrameworkContractNames.Contains(e.PropertyName));
    }
}
