using System.Windows;
using SunEyeVision.UI.Services.Interaction;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class VmPlacementGeometryTests
{
    [Fact] public void Guides_cover_left_right_center_and_use_inverse_zoom_width()
    {
        var result = VmPlacementCalculator.Calculate(new Rect(100, 170, 110, 30), new[] { new VmPlacementNode("a", new Rect(100, 100, 110, 30)) }, .5, null);
        Assert.Equal(3, result.Guides.Count); Assert.All(result.Guides, g => Assert.True(g.Vertical));
        Assert.Equal(new[] { 100d, 210d, 155d }, result.Guides.Select(g => g.Axis));
        Assert.All(result.Guides, g => { Assert.Equal(90d, g.Start); Assert.Equal(210d, g.End); });
        Assert.Equal(new Point(100, 170), result.Hot!.TopLeft);
    }
    [Fact] public void Variable_height_hot_region_and_retained_hysteresis_match_restored_formula()
    {
        var others = new[] { new VmPlacementNode("parent", new Rect(100, 100, 110, 40)) };
        var first = VmPlacementCalculator.Calculate(new Rect(100, 170, 110, 30), others, 1, null);
        Assert.Equal(new Point(100, 175), first.Hot!.TopLeft);
        var retained = VmPlacementCalculator.Calculate(new Rect(115, 185, 110, 30), others, 1, first.Hot);
        Assert.Equal(first.Hot, retained.Hot);
        Assert.Null(VmPlacementCalculator.Calculate(new Rect(125, 185, 110, 30), others, 1, first.Hot).Hot);
    }
}
