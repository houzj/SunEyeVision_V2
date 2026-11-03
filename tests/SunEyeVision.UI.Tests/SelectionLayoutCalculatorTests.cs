using System.Windows;
using SunEyeVision.UI.Services.Interaction;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class SelectionLayoutCalculatorTests
{
    private static readonly Rect[] Nodes = { new(20,30,110,30), new(200,150,80,40) };
    [Theory]
    [InlineData(NodeAlignment.Left,20,30,20,150)]
    [InlineData(NodeAlignment.Right,170,30,200,150)]
    [InlineData(NodeAlignment.CenterX,95,30,110,150)]
    [InlineData(NodeAlignment.Top,20,30,200,30)]
    [InlineData(NodeAlignment.Bottom,20,160,200,150)]
    [InlineData(NodeAlignment.CenterY,20,95,200,90)]
    public void Six_alignments_use_original_frame_margin_and_five_pixel_centers(NodeAlignment align,double ax,double ay,double bx,double by)
    {
        var result = SelectionLayoutCalculator.Align(Nodes,align);
        Assert.Equal(new Point(ax,ay),result[0]); Assert.Equal(new Point(bx,by),result[1]);
    }
    [Fact] public void Horizontal_distribution_uses_original_variable_sizes_and_sorts_without_reordering_identity()
    {
        var result = SelectionLayoutCalculator.Distribute(Nodes.Reverse().ToArray(),true);
        Assert.Equal(new Point(200,90),result[0]); Assert.Equal(new Point(20,95),result[1]);
    }
    [Fact] public void Vertical_distribution_expands_overlapping_frame_and_clamps_negative_origin()
    {
        var result = SelectionLayoutCalculator.Distribute(new[] {new Rect(-40,-30,110,30),new Rect(-20,-20,80,40)},false);
        Assert.Equal(new Point(5,10),result[0]); Assert.Equal(new Point(5,45),result[1]);
    }
}
