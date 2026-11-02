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
        Assert.DoesNotContain(references,name=>name is not null && IsForbidden(name));
    }
    [Theory]
    [InlineData("SunEyeVision.Plugin.Contracts",false)] [InlineData("System.Runtime",false)]
    [InlineData("SunEyeVision.Plugin.SDK",true)] [InlineData("SunEyeVision.Plugin.Runtime",true)]
    [InlineData("SunEyeVision.Plugin.Wpf",true)] [InlineData("SunEyeVision.Plugin.Editing.Wpf",true)]
    [InlineData("SunEyeVision.Plugin.Infrastructure",true)] [InlineData("SunEyeVision.UI",true)]
    [InlineData("PresentationCore",true)] [InlineData("WindowsBase",true)] [InlineData("System.Xaml",true)]
    public void Reference_policy_allows_neutral_contracts_and_rejects_desktop_and_host(string name,bool forbidden)
        => Assert.Equal(forbidden,IsForbidden(name));
    private static bool IsForbidden(string name)
        => name.StartsWith("Presentation",StringComparison.Ordinal) || name is "WindowsBase" or "System.Xaml" or
            "SunEyeVision.Plugin.SDK" or "SunEyeVision.Plugin.Runtime" or "SunEyeVision.Plugin.Wpf" or
            "SunEyeVision.Plugin.Editing.Wpf" or "SunEyeVision.Plugin.Infrastructure" or "SunEyeVision.UI";
}
