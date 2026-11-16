using SunEyeVision.Tool.BlobLabels.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 阈值区域编码对照原件 <c>ThresholdAreaItem.ThresholdAreaData</c>：
/// <c>序号#是否新增#灰度#下限#上限#是否反选</c>，同一灰度不允许重复。
/// </summary>
public sealed class BlobLabelsThresholdAreaTests
{
    [Fact]
    public void Encoding_matches_the_original_item_format()
    {
        var item = new BlobLabelsThresholdArea(1, true, 128, 10, 999_999_999, false);
        Assert.Equal("1#1#128#10#999999999#0", item.Encode());
        Assert.Equal(item, BlobLabelsThresholdArea.Decode(item.Encode()));
    }

    [Fact]
    public void Inverse_and_subtract_flags_round_trip()
    {
        var item = new BlobLabelsThresholdArea(3, false, 200, 5, 60, true);
        var decoded = BlobLabelsThresholdArea.Decode(item.Encode());
        Assert.False(decoded.IsAdd);
        Assert.True(decoded.IsInverse);
        Assert.Equal(200, decoded.GrayValue);
        Assert.Equal(5, decoded.MinimumArea);
        Assert.Equal(60, decoded.MaximumArea);
    }

    [Fact]
    public void List_reindexes_rows_and_rejects_duplicate_gray_values()
    {
        var encoded = BlobLabelsThresholdAreas.Encode(new[]
        {
            new BlobLabelsThresholdArea(9, true, 10, 1, 100, false),
            new BlobLabelsThresholdArea(9, true, 20, 1, 100, false)
        });
        var parsed = BlobLabelsThresholdAreas.Parse(encoded);
        Assert.Equal(new[] { 1, 2 }, parsed.Select(item => item.Index));
        Assert.Throws<ArgumentException>(() => BlobLabelsThresholdAreas.Parse("1#1#10#1#100#0\n2#1#10#1#100#0"));
    }

    [Theory]
    [InlineData("1#1#128#10")]           // 段数不足
    [InlineData("1#1#0#10#100#0")]       // 灰度越界
    [InlineData("1#1#128#100#10#0")]     // 下限大于上限
    [InlineData("1#2#128#10#100#0")]     // 新增标记非法
    public void Malformed_rows_fail_explicitly(string encoded)
        => Assert.Throws<ArgumentException>(() => BlobLabelsThresholdAreas.Parse(encoded));

    [Fact]
    public void Append_picks_the_first_unused_gray_value()
    {
        var existing = BlobLabelsThresholdAreas.Parse("1#1#1#1#100#0\n2#1#2#1#100#0");
        var next = BlobLabelsThresholdAreas.Append(existing);
        Assert.Equal(3, next.Index);
        Assert.Equal(3, next.GrayValue);
        Assert.True(next.IsAdd);
        Assert.Equal(10, next.MinimumArea);
        Assert.Equal(999_999_999, next.MaximumArea);
    }

    [Fact]
    public void Count_limit_is_enforced()
    {
        var rows = string.Join('\n', Enumerable.Range(1, BlobLabelsThresholdAreas.MaximumCount)
            .Select(index => $"{index}#1#{index}#1#100#0"));
        Assert.Equal(BlobLabelsThresholdAreas.MaximumCount, BlobLabelsThresholdAreas.Parse(rows).Count);
        Assert.Throws<ArgumentException>(() => BlobLabelsThresholdAreas.Parse(rows + "\n256#1#255#1#100#0"));
    }
}
