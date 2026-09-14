using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 配对广播契约测试（方案 §6 向量化核心契约 / 6d）。
/// </summary>
/// <remarks>
/// 守住的退化：有人把"长度不匹配"改成静默按短的一方截断（软件里最难查的一类错），
/// 或让某个工具自己实现一套配对规则（语义漂移）。
/// </remarks>
public class TupleBroadcastContractTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 3, 3)]
    [InlineData(1, 5, 5)]   // 元素1 广播
    [InlineData(5, 1, 5)]   // 元素2 广播
    [InlineData(20, 20, 20)]
    public void TryGetPairCount_EqualOrBroadcast_Succeeds(int len1, int len2, int expected)
    {
        var ok = TupleBroadcast.TryGetPairCount(len1, len2, out var count, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    [InlineData(3, 4)]
    public void TryGetPairCount_MismatchedLengths_ReportsError(int len1, int len2)
    {
        // rule-015：长度不匹配必须报错，**不得**静默按短的一方截断
        var ok = TupleBroadcast.TryGetPairCount(len1, len2, out var count, out var error);

        Assert.False(ok);
        Assert.Equal(0, count);
        Assert.NotNull(error);
        Assert.Contains("长度不匹配", error!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(0, 0)]
    public void TryGetPairCount_EmptyElement_ReportsError(int len1, int len2)
    {
        // 空元素 = 无输入：报错而非产出空结果（避免"静默零结果"）
        var ok = TupleBroadcast.TryGetPairCount(len1, len2, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("不能为空", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexFor_BroadcastSide_AlwaysZero()
    {
        // 长度 1 → 广播，恒取 0；长度 N → 取 i
        Assert.Equal(0, TupleBroadcast.IndexFor(1, 0));
        Assert.Equal(0, TupleBroadcast.IndexFor(1, 7));
        Assert.Equal(3, TupleBroadcast.IndexFor(5, 3));
    }

    [Fact]
    public void IndexFor_NonPositiveLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TupleBroadcast.IndexFor(0, 0));
    }
}
