using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// MeasureThreshold/MeasureJudge 判定基座契约测试（v4 C1）。
/// 锁定语义（方案防御机制"契约一致性测试" + rule-015 报错优先）：
///   - 含边界判定：value ∈ [Min, Max]（两端含）为 OK，越界 NG；
///   - Enabled=false 不参与判定（恒过，不入 resolver）；
///   - 启用却无实测值 → NG（禁止静默当 OK）；
///   - 多阈值聚合：全过 IsAllPass=true，任一 NG 即 false；
///   - 规格可序列化（保存进方案 JSON）且 Clone 独立（参数快照语义）。
/// </summary>
public class MeasureJudgeContractTests
{
    private static MeasureThreshold NewTh(string name, bool enabled, double min, double max, string? unit = null)
        => new() { Name = name, Enabled = enabled, Min = min, Max = max, Unit = unit };

    private static IReadOnlyList<MeasureJudgeResult> Judge(
        IEnumerable<MeasureThreshold>? thresholds,
        Func<MeasureThreshold, double?> valueResolver)
        => MeasureJudge.EvaluateAll(thresholds, valueResolver);

    // ---- 判定语义 ----

    [Fact]
    public void Value_AtLowerBoundary_Passes()
    {
        var t = NewTh("线距", true, 0, 10);

        var r = Judge(new[] { t }, _ => 0).Single();

        Assert.True(r.IsPass);
        Assert.Equal(0, r.Value);
    }

    [Fact]
    public void Value_AtUpperBoundary_Passes()
    {
        var t = NewTh("线距", true, 0, 10);

        var r = Judge(new[] { t }, _ => 10).Single();

        Assert.True(r.IsPass);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public void Value_BelowMin_Fails()
    {
        var t = NewTh("线距", true, 0, 10);

        var r = Judge(new[] { t }, _ => -0.01).Single();

        Assert.False(r.IsPass);
    }

    [Fact]
    public void Value_AboveMax_Fails()
    {
        var t = NewTh("线距", true, 0, 10);

        var r = Judge(new[] { t }, _ => 10.01).Single();

        Assert.False(r.IsPass);
    }

    [Fact]
    public void Disabled_AlwaysPass_And_DoesNotAskResolver()
    {
        var t = NewTh("夹角", false, 89.5, 90.5, "°");
        var resolverCalled = false;

        var r = Judge(new[] { t }, _ => { resolverCalled = true; return null; }).Single();

        Assert.True(r.IsPass);                          // 未启用恒过
        Assert.Equal(double.NaN, r.Value);              // 无实测值标记
        Assert.False(resolverCalled);                   // 未启用时不求值
        Assert.Contains("未启用", r.Message);
    }

    [Fact]
    public void Enabled_ButMissingValue_FailsExplicitly()
    {
        // rule-015：启用却取不到实测值 → NG，禁止静默跳过
        var t = NewTh("线距", true, 0, 10);

        var r = Judge(new[] { t }, _ => null).Single();

        Assert.False(r.IsPass);
        Assert.Contains("无实测值", r.Message);
    }

    [Fact]
    public void EvaluateAll_WithNullResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MeasureJudge.EvaluateAll(null, null!));
    }

    // ---- 聚合语义 ----

    [Fact]
    public void NullOrEmptyThresholds_ProducesEmptyAndAllPass()
    {
        Assert.Empty(MeasureJudge.EvaluateAll(null, _ => 0));
        Assert.True(MeasureJudge.IsAllPass(MeasureJudge.EvaluateAll(null, _ => 0)));
        Assert.True(MeasureJudge.IsAllPass(MeasureJudge.EvaluateAll(Array.Empty<MeasureThreshold>(), _ => 0)));
    }

    [Fact]
    public void Mixed_OneFails_AllFail()
    {
        var thresholds = new[]
        {
            NewTh("距离", true, 0, 100, "px"),
            NewTh("角度", true, 0, 180, "°"),
        };

        var results = Judge(thresholds, t => t.Name == "距离" ? 50 : 200); // 角度越界

        Assert.False(MeasureJudge.IsAllPass(results));
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => !r.IsPass);
    }

    [Fact]
    public void Mixed_AllPass_Ok()
    {
        var thresholds = new[]
        {
            NewTh("距离", true, 0, 100, "px"),
            NewTh("角度", true, 0, 180, "°"),
        };

        var results = Judge(thresholds, t => t.Name == "距离" ? 50 : 90);

        Assert.True(MeasureJudge.IsAllPass(results));
        Assert.All(results, r => Assert.True(r.IsPass));
    }

    [Fact]
    public void PassMessage_CarriesValueUnitAndRange()
    {
        var t = NewTh("线距", true, 9.9, 10.1, "mm");

        var r = Judge(new[] { t }, _ => 10).Single();

        Assert.Contains("线距", r.Message);
        Assert.Contains("10", r.Message);
        Assert.Contains("mm", r.Message);
        Assert.Contains("[9.9, 10.1]", r.Message);
    }

    // ---- 快照 / 序列化 ----

    [Fact]
    public void Clone_IsDeepIndependent()
    {
        var t = NewTh("线距", true, 0, 10, "px");

        var clone = t.Clone();
        clone.Min = 99;
        clone.Enabled = false;

        Assert.Equal(0, t.Min);          // 修改副本不影响源
        Assert.True(t.Enabled);
        Assert.Equal("线距", clone.Name);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var original = NewTh("夹角", true, 89.5, 90.5, "°");

        var json = JsonSerializer.Serialize(original);
        var back = JsonSerializer.Deserialize<MeasureThreshold>(json);

        Assert.NotNull(back);
        Assert.Equal(original.Name, back!.Name);
        Assert.Equal(original.Enabled, back.Enabled);
        Assert.Equal(original.Min, back.Min);
        Assert.Equal(original.Max, back.Max);
        Assert.Equal(original.Unit, back.Unit);
    }
}
