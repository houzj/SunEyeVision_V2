using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
public sealed class CanvasGoldenRoundTripTests
{
    [Fact]
    public void Baseline_v3_real_solution_loads_and_saves_same_manual_path_shape()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var golden = Path.Combine(AppContext.BaseDirectory,"manual-path-v3.golden.solution");
        var source = Path.Combine(root,"source.solution");
        File.Copy(golden,source);
        var manager = new SolutionManager(Path.Combine(root,"catalog"),RootCanvasFixture.Directory);
        var document = manager.OpenSolution(source);
        Assert.NotNull(document);
        var edge = Assert.Single(Assert.Single(document!.Workflows).Connections);
        Assert.Equal(new[]{new CanvasPoint(12.5,37.25),new CanvasPoint(80.75,37.25),new CanvasPoint(80.75,300.25),new CanvasPoint(200.5,300.25)},edge.ManualPathPoints);
        var output=Path.Combine(root,"saved.solution");
        Assert.True(manager.SaveSolution(output),manager.LastError);
        using var before=JsonDocument.Parse(File.ReadAllText(golden));
        using var after=JsonDocument.Parse(File.ReadAllText(output));
        var left=before.RootElement.GetProperty("Workflows")[0].GetProperty("Connections")[0].GetProperty("ManualPathPoints");
        var right=after.RootElement.GetProperty("Workflows")[0].GetProperty("Connections")[0].GetProperty("ManualPathPoints");
        Assert.Equal(left.GetRawText(),right.GetRawText());
        for(var i=0;i<2;i++)
        {
            foreach(var field in new[]{"PositionX","PositionY","Width","Height"})
                Assert.Equal(before.RootElement.GetProperty("Workflows")[0].GetProperty("Nodes")[i].GetProperty(field).GetRawText(),after.RootElement.GetProperty("Workflows")[0].GetProperty("Nodes")[i].GetProperty(field).GetRawText());
            var beforeConfig=before.RootElement.GetProperty("Workflows")[0].GetProperty("Nodes")[i].GetProperty("RuntimeConfiguration");
            var afterConfig=after.RootElement.GetProperty("Workflows")[0].GetProperty("Nodes")[i].GetProperty("RuntimeConfiguration");
            foreach(var field in new[]{"Fields","CurrentValues","ParameterBindingOrder"})
                Assert.Equal(beforeConfig.GetProperty(field).GetRawText(),afterConfig.GetProperty(field).GetRawText());
        }
        Assert.Equal(3,after.RootElement.GetProperty("SchemaVersion").GetInt32());
    }

    [Fact]
    public void Invalid_nonfinite_manual_path_is_rejected_without_modifying_file()
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var golden=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"manual-path-v3.golden.solution"));
        var corrupted=golden.Replace("\"X\":12.5","\"X\":1e999",StringComparison.Ordinal);
        Assert.NotEqual(golden,corrupted);
        var source=Path.Combine(root,"invalid.solution");File.WriteAllText(source,corrupted);
        var before=SHA256.HashData(File.ReadAllBytes(source));
        var manager=new SolutionManager(Path.Combine(root,"catalog"),RootCanvasFixture.Directory);
        Assert.Null(manager.LoadSolutionOnly(source));
        Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Theory]
    [InlineData("PositionX")] [InlineData("PositionY")] [InlineData("Width")] [InlineData("Height")]
    public void Nonfinite_node_layout_cannot_overwrite_existing_v3_solution(string field)
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source=Path.Combine(root,"source.solution");File.Copy(Path.Combine(AppContext.BaseDirectory,"manual-path-v3.golden.solution"),source);
        var manager=new SolutionManager(Path.Combine(root,"catalog"),RootCanvasFixture.Directory);
        var document=manager.OpenSolution(source);Assert.NotNull(document);
        var before=SHA256.HashData(File.ReadAllBytes(source));
        typeof(WorkflowNodeBase).GetProperty(field)!.SetValue(document!.Workflows[0].Nodes[0],double.NaN);
        Assert.False(manager.SaveSolution(source));
        Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Theory]
    [InlineData("PositionX","12.5")]
    [InlineData("PositionY","37.25")]
    [InlineData("Width","140")]
    [InlineData("Height","90")]
    public void Nonfinite_node_layout_load_preserves_source_and_active_document(string field,string original)
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var golden=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"manual-path-v3.golden.solution"));
        var activePath=Path.Combine(root,"active.solution");File.WriteAllText(activePath,golden);
        var invalidPath=Path.Combine(root,"invalid.solution");
        var corrupted=golden.Replace("\""+field+"\":"+original,"\""+field+"\":1e999",StringComparison.Ordinal);
        Assert.NotEqual(golden,corrupted);File.WriteAllText(invalidPath,corrupted);
        var before=SHA256.HashData(File.ReadAllBytes(invalidPath));
        var manager=new SolutionManager(Path.Combine(root,"catalog"),RootCanvasFixture.Directory);
        var active=manager.OpenSolution(activePath);Assert.NotNull(active);
        Assert.Null(manager.LoadSolutionOnly(invalidPath));
        Assert.Same(active,manager.CurrentSolution);
        Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(invalidPath)));
    }
}
