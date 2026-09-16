using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using System.Windows;
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

    [Fact]
    public void ApplyLiveStyles_RebindsExistingResultItemsToTheEditableStyles()
    {
        var liveGraphics = new GraphicsStyle { Name = "当前轮廓", IsVisible = true, LineWidth = 3 };
        var liveText = new TextStyle { Name = "当前标签", IsVisible = true, FontSize = 16 };
        var config = new ResultPresentationConfig();
        config.Graphics.Add(new GraphicsPresentationItem { Key = "contour", Name = "轮廓", Style = liveGraphics });
        config.Texts.Add(new TextPresentationItem { Key = "label", Name = "标签", Style = liveText });

        // 执行结果由参数快照生成；快照中的显示名称可以与随后编辑的名称不同。
        var snapshotPresentation = new ResultPresentationConfig();
        snapshotPresentation.Graphics.Add(new GraphicsPresentationItem
        {
            Key = "contour", Name = "旧轮廓", Style = new GraphicsStyle { Name = "历史轮廓", IsVisible = true, LineWidth = 1 }
        });
        snapshotPresentation.Texts.Add(new TextPresentationItem
        {
            Key = "label", Name = "旧标签", Style = new TextStyle { Name = "历史标签", IsVisible = true, FontSize = 10 }
        });

        var collector = new DisplayCollector();
        collector.AddRect(snapshotPresentation.GetGraphicsStyle("contour"), new Rect(0, 0, 10, 10));
        collector.AddText(snapshotPresentation.GetTextStyle("label"), 0, 0, "测试");

        config.ApplyLiveStyles(collector);

        var graphicsItem = Assert.IsType<GraphicsDisplayItem>(collector.Items[0]);
        var textItem = Assert.IsType<TextDisplayItem>(collector.Items[1]);
        Assert.Same(liveGraphics, graphicsItem.Style);
        Assert.Same(liveText, textItem.Style);

        liveGraphics.IsVisible = false;
        liveText.IsVisible = false;
        Assert.False(graphicsItem.IsVisible);
        Assert.False(textItem.IsVisible);
    }
}
