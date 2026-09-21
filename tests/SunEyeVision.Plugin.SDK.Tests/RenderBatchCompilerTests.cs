using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class RenderBatchCompilerTests
{
    [Fact]
    public void CompatibleContours_AreCompiledIntoOneContiguousBatch()
    {
        var style = new GraphicsStyle { OkColor = System.Windows.Media.Color.FromArgb(255, 0, 255, 0), LineWidth = 2 };
        var item = new GraphicsDisplayItem { Style = style, RenderMode = DisplayRenderMode.Unified };
        item.Geometries = new object[]
        {
            new ContourGeometry(new[] { new Point(0, 0), new Point(10, 0) }, false),
            new ContourGeometry(new[] { new Point(20, 20), new Point(30, 20) }, false)
        };
        var scene = new RenderBatchCompiler().Compile(new[] { item }, 7, 9);
        var batch = Assert.Single(scene.Batches);
        Assert.Equal(4, scene.VertexCount);
        Assert.Equal(2, batch.Ranges.Count);
        Assert.Equal(7, scene.ImageIdentity);
        Assert.Equal(9, scene.RenderGeneration);
    }
}
