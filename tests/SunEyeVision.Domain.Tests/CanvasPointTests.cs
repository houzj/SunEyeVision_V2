using System.Text.Json;
using SunEyeVision.Domain.Workflows;
using Xunit;

namespace SunEyeVision.Domain.Tests;
public sealed class CanvasPointTests
{
    [Fact] public void Value_and_json_match_current_xy_contract()
    {
        var point = new CanvasPoint(12.5, 37.25);
        Assert.Equal(new CanvasPoint(12.5, 37.25),point);
        Assert.Equal(new CanvasPoint(0,0),default);
        Assert.Equal("{\"X\":12.5,\"Y\":37.25}",JsonSerializer.Serialize(point));
        Assert.Equal(point,JsonSerializer.Deserialize<CanvasPoint>("{\"X\":12.5,\"Y\":37.25}"));
    }
    [Theory]
    [InlineData(double.NaN,0)] [InlineData(double.PositiveInfinity,0)] [InlineData(0,double.NegativeInfinity)]
    public void Non_finite_coordinates_are_rejected(double x,double y)
        => Assert.Throws<ArgumentOutOfRangeException>(()=>new CanvasPoint(x,y));
    [Fact] public void Non_finite_json_cannot_be_loaded()
        => Assert.ThrowsAny<Exception>(()=>JsonSerializer.Deserialize<CanvasPoint>("{\"X\":1e999,\"Y\":0}"));
    [Fact] public void Domain_assembly_rejects_desktop_legacy_sdk_and_runtime_references()
    {
        var references=typeof(CanvasPoint).Assembly.GetReferencedAssemblies().Select(x=>x.Name).ToArray();
        Assert.All(references,name=>
        {
            var neutral=name is not null && (name=="SunEyeVision.Plugin.Contracts" ||
                (name.StartsWith("System.",StringComparison.Ordinal) &&
                 !name.StartsWith("System.Windows",StringComparison.Ordinal) &&
                 name is not "System.Xaml" and not "System.Drawing.Common"));
            Assert.True(neutral,$"Domain cannot reference host or desktop assembly: {name}");
        });
    }
}