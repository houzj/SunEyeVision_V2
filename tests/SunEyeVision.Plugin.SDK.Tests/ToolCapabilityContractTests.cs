using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 守卫契约测试：能力声明 ↔ 框架契约 一致性（方案 P0.6-1/2/3/5/6，决策 10/11/12）。
/// </summary>
/// <remarks>
/// 覆盖四类"很容易被改回去、且改回去后表面看不出来"的退化：
/// <list type="bullet">
/// <item>判定源退回"继承某基类"（对 14/14 工具恒真 ⇒ 测量工具又冒出检测区/屏蔽区）；</item>
/// <item>框架契约四要素重新双份书写（运行期 / 设计期各一份）；</item>
/// <item>结构随值起伏（空值删条目 ⇒ "有时有行、有时没行"）；</item>
/// <item>分组占位声明未随契约传递（Shell 折叠失效，改回硬编码中文组名）。</item>
/// </list>
/// </remarks>
public class ToolCapabilityContractTests
{
    /// <summary>未声明能力的普通结果类（默认 None）。</summary>
    private class PlainResults : ToolResults
    {
        public double Value { get; set; }
    }

    /// <summary>声明了区域能力的结果类。</summary>
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.RoiInput)]
    private class RoiResults : ToolResults
    {
        public double Value { get; set; }
    }

    /// <summary>声明了"分组占位"条目的结果类（P0.6-5 声明式折叠）。</summary>
    private class PlaceholderResults : ToolResults
    {
        public double Guide { get; set; }

        public override List<PropertyMapEntry> GetPropertyMap() =>
        [
            .. base.GetPropertyMap(),
            new("Guide", "引导几何", "输入/卡尺/引导线", IsGroupPlaceholder: true),
        ];
    }

    #region 能力解析

    [Fact]
    public void Resolve_UnannotatedType_IsNone()
    {
        // 默认 None：忘记声明的工具不会暴露任何框架能力（宁可少产出 + 报错可见，不要多产出 + 静默误导）
        Assert.Equal(ToolCapabilities.None, ToolCapability.Resolve(typeof(PlainResults)));
        Assert.Equal(ToolCapabilities.None, ToolCapability.Resolve(new PlainResults()));
        Assert.Equal(ToolCapabilities.None, ToolCapability.Resolve((Type?)null));
        Assert.Equal(ToolCapabilities.None, ToolCapability.Resolve((object?)null));
    }

    [Fact]
    public void Resolve_AnnotatedType_ReturnsDeclaredCapabilities()
    {
        Assert.Equal(ToolCapabilities.ImageSource | ToolCapabilities.RoiInput,
            ToolCapability.Resolve(typeof(RoiResults)));
    }

    #endregion

    #region 框架契约单源（P0.6-2）

    [Fact]
    public void FrameworkContractSchema_Resolve_ByCapability()
    {
        // 区域能力 → 恰好两条（检测区 + 屏蔽区）；其余能力当前不产出框架条目
        Assert.Equal(2, FrameworkContractSchema.Resolve(ToolCapabilities.RoiInput).Count());
        Assert.Empty(FrameworkContractSchema.Resolve(ToolCapabilities.None));
        Assert.Empty(FrameworkContractSchema.Resolve(ToolCapabilities.MeasureGuide));
        Assert.Empty(FrameworkContractSchema.Resolve(ToolCapabilities.PositionCorrection));
    }

    [Fact]
    public void FrameworkContractSchema_Entries_CarrySingleSourceMetadata()
    {
        // 四要素（属性名/显示名/组名/类型）+ 所需能力，全部在单源表声明（运行期与设计期同源生成）
        var inspection = FrameworkContractSchema.InspectionGeometries;
        Assert.Equal(nameof(ToolResults.InspectionGeometries), inspection.PropertyName);
        Assert.Equal("检测区图形", inspection.DisplayName);
        Assert.Equal("输入", inspection.GroupName);
        Assert.Equal(typeof(RoiShape[]), inspection.ValueType);
        Assert.Equal(ToolCapabilities.RoiInput, inspection.RequiredCapability);

        var mask = FrameworkContractSchema.MaskGeometries;
        Assert.Equal(nameof(ToolResults.MaskGeometries), mask.PropertyName);
        Assert.Equal("屏蔽区图形", mask.DisplayName);
        Assert.Equal("输入", mask.GroupName);
        Assert.Equal(typeof(RoiShape[]), mask.ValueType);
        Assert.Equal(ToolCapabilities.RoiInput, mask.RequiredCapability);
    }

    #endregion

    #region 能力 ↔ 契约一致（P0.6-1/3：结构由能力决定，与值无关）

    [Fact]
    public void GetOutputVariables_NoCapability_ProducesNoFrameworkEntries()
    {
        // 守卫：未声明 RoiInput 的"工具"**不得**出现 检测区/屏蔽区。
        // 历史缺陷：判定源是 typeof(RoiParameters).IsAssignableFrom(ParamsType)（对 14/14 恒真）
        // ⇒ 不使用区域的测量工具也凭空多出这两个条目。
        var names = new PlainResults().GetOutputVariables().Select(v => v.Name).ToList();

        Assert.DoesNotContain(nameof(ToolResults.InspectionGeometries), names);
        Assert.DoesNotContain(nameof(ToolResults.MaskGeometries), names);
    }

    [Fact]
    public void GetOutputVariables_WithRoiCapability_ProducesBothEntries_EvenWhenValueEmpty()
    {
        // 守卫（结构与值分离，决策 12）：声明了区域能力 ⇒ 即使区域**无值**（基类默认空数组），
        // 条目**仍然产出**（由渲染层出占位 "--"），不允许"看值删条目"⇒ 结构恒定。
        var variables = new RoiResults().GetOutputVariables();
        var names = variables.Select(v => v.Name).ToList();

        Assert.Contains(nameof(ToolResults.InspectionGeometries), names);
        Assert.Contains(nameof(ToolResults.MaskGeometries), names);

        var inspection = variables.Single(v => v.Name == nameof(ToolResults.InspectionGeometries));
        Assert.Equal("检测区图形", inspection.DisplayName);
        Assert.Equal("输入", inspection.Group);
        Assert.Equal(OutputVariableKind.Data, inspection.Kind);
        Assert.Equal("ROI", inspection.VariableType);
    }

    [Fact]
    public void GetOutputVariables_FrameworkEntries_AppearExactlyOnce()
    {
        // 守卫（决策 17：框架契约**唯一产出点**）：声明了区域能力的契约里，检测区/屏蔽区**各只能出现一次**。
        // 历史缺陷：设计期又"旁路注册"了一份等价元数据（补丁），病根（空值跳过）治好后补丁未撤
        // ⇒ 结构产出两遍，表现为「选择绑定源 → 输入」下出现**两套**「检测区图形/屏蔽区图形」。
        var variables = new RoiResults().GetOutputVariables();

        Assert.Equal(1, variables.Count(v => v.Name == nameof(ToolResults.InspectionGeometries)));
        Assert.Equal(1, variables.Count(v => v.Name == nameof(ToolResults.MaskGeometries)));

        // 更强不变式：契约里**任何**变量名都不得重复（重名会让绑定树出现"幽灵条目"）
        var duplicates = variables.GroupBy(v => v.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void FrameworkContractSchema_ResolvedEntries_HaveUniquePropertyNames()
    {
        // 守卫（单源表自身）：任一能力组合解析出的条目属性名不得重复，
        // 否则契约层会按"两条同名字段"产出（绑定树同样出现两套）。
        var allCapabilities = Enum.GetValues<ToolCapabilities>()
            .Aggregate(ToolCapabilities.None, (acc, c) => acc | c);
        var names = FrameworkContractSchema.Resolve(allCapabilities).Select(e => e.PropertyName).ToList();

        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    #endregion

    #region 分组占位声明（P0.6-5 声明式折叠）

    [Fact]
    public void GetOutputVariables_GroupPlaceholder_FlagFlowsToContract()
    {
        // 守卫：占位声明必须随契约从 PropertyMapEntry 传到 OutputVariable，
        // 否则 Shell 只能退回"认工具的中文分组名"（改组名即静默失效，结果树多一层重复节点）。
        var guide = new PlaceholderResults().GetOutputVariables().Single(v => v.Name == "Guide");

        Assert.True(guide.IsGroupPlaceholder, "分组占位声明必须随契约传递");
        Assert.Equal("输入/卡尺/引导线", guide.Group);
    }

    [Fact]
    public void GetOutputVariables_OrdinaryEntry_IsNotGroupPlaceholder()
    {
        // 边界：普通条目默认非占位（不折叠），避免"到处折叠"把正常属性行也吃掉
        var pass = new PlainResults().GetOutputVariables();
        Assert.All(pass, v => Assert.False(v.IsGroupPlaceholder));
    }

    #endregion
}
