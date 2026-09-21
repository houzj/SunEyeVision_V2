using System.Windows;
using Xunit;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class RenderHitTestingContractTests
{
    [Fact]
    public void CircleHit_UsesToleranceAroundCircumference()
    {
        var item = new GraphicsDisplayItem
        {
            Geometries = new object[] { new Circle2d(new Point(100, 100), 20) }
        };

        Assert.True(RenderHitTester.IsHit(item, new Point(120, 100), 1));
        Assert.True(RenderHitTester.IsHit(item, new Point(120.8, 100), 1));
        Assert.False(RenderHitTester.IsHit(item, new Point(100, 100), 1));
    }

    [Fact]
    public void RotatedRectangleHit_UsesInteriorAndEdgeDistance()
    {
        var item = new GraphicsDisplayItem
        {
            Geometries = new object[] { new RotatedRect2d(new Point(100, 100), 40, 20, 30) }
        };

        Assert.True(RenderHitTester.IsHit(item, new Point(100, 100), 0));
        Assert.True(RenderHitTester.IsHit(item, new Point(120, 100), 2));
        Assert.False(RenderHitTester.IsHit(item, new Point(150, 150), 1));
    }

    [Fact]
    public void RenderSceneCandidates_DeduplicateItemsCrossingTiles()
    {
        var item = new GraphicsDisplayItem
        {
            Geometries = new object[]
            {
                new ContourGeometry(new[]
                {
                    new Point(500, 500), new Point(540, 540)
                }, false)
            }
        };
        var scene = new RenderSceneCompiler().Compile(new[] { item }, 512);

        var candidates = scene.QueryCandidates(new Point(512, 512), 40);

        Assert.Single(candidates);
        Assert.Same(item, candidates[0]);
    }
}
