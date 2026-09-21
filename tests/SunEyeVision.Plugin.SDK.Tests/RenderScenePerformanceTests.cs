using System.Diagnostics;
using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using Xunit;
using Xunit.Abstractions;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class RenderScenePerformanceTests
{
    private readonly ITestOutputHelper _output;

    public RenderScenePerformanceTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(3000)]
    [InlineData(10000)]
    [InlineData(71905)]
    [InlineData(100000)]
    public void CompileAndQuery_RecordsLargeResultBaseline(int itemCount)
    {
        var items = new DisplayItemBase[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            var x = (i % 1000) * 8;
            var y = (i / 1000) * 8;
            items[i] = new GraphicsDisplayItem
            {
                Geometries = new object[] { new Point(x, y) }
            };
        }

        var compiler = new RenderSceneCompiler();
        var compileTimer = Stopwatch.StartNew();
        var scene = compiler.Compile(items);
        compileTimer.Stop();

        var queryTimer = Stopwatch.StartNew();
        var candidates = scene.QueryCandidates(new Point(4000, 4000), 64);
        queryTimer.Stop();

        Assert.Equal(itemCount, scene.Tiles.Values.SelectMany(tile => tile.Items).Distinct().Count());
        _output.WriteLine($"items={itemCount}; tiles={scene.Tiles.Count}; candidates={candidates.Count}; compileMs={compileTimer.Elapsed.TotalMilliseconds:F2}; queryMs={queryTimer.Elapsed.TotalMilliseconds:F2}");
    }
}
