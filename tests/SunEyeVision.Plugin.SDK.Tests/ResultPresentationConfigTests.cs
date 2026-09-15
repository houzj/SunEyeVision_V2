using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class ResultPresentationConfigTests
{
    [Fact]
    public void ValidateContract_AcceptsAnyCombinationOfPopulatedModules()
    {
        var config = new ResultPresentationConfig();
        config.Judgments.Add(new JudgmentConfigItem { Key = "count", Name = "数量" });
        config.Graphics.Add(new GraphicsPresentationItem { Key = "contour", Name = "轮廓" });

        Assert.Empty(config.ValidateContract());
    }

    [Fact]
    public void Collections_AreInitializedForDirectToolComposition()
    {
        var config = new ResultPresentationConfig();

        Assert.NotNull(config.Judgments);
        Assert.NotNull(config.Graphics);
        Assert.NotNull(config.Texts);
        Assert.Empty(config.ValidateContract());
    }

    [Fact]
    public void ValidateContract_ReportsEmptyAndDuplicateKeysWithinModule()
    {
        var config = new ResultPresentationConfig();
        config.Judgments.Add(new JudgmentConfigItem { Key = "distance", Name = "距离" });
        config.Judgments.Add(new JudgmentConfigItem { Key = "distance", Name = "距离2" });
        config.Judgments.Add(new JudgmentConfigItem { Key = " ", Name = "空键" });

        var errors = config.ValidateContract();

        Assert.Contains(errors, error => error.Contains("重复 Key"));
        Assert.Contains(errors, error => error.Contains("空 Key"));
    }
}
