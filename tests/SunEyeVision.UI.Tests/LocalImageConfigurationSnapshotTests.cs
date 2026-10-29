using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class LocalImageConfigurationSnapshotTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    public void Shared_plan_rejects_out_of_range_start_before_sequence_admission(int start)
    {
        var policy=new ImageSourcePolicy { NodeId="source",FilePaths=new(){"a.bmp"},CurrentIndex=start };
        Assert.Throws<InvalidOperationException>(()=>ImageSourcePolicy.CreateLocalPlan("source",policy,
            new SolutionResourceResolver(System.IO.Path.GetTempPath())));
    }

    [Fact]
    public void Plan_uses_formal_references_and_captured_base_without_any_preview_projection()
    {
        var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "snapshot-no-file-access"));
        var node=ImageSourceTestRuntime.Node();
        ImageSourceTestRuntime.Edit(node,config=>{config.FilePaths=new(){"a.bmp","b.bmp","a.bmp"};config.CurrentIndex=2;});
        var config=ImageSourceTestRuntime.Read(node);
        var resolver = new SolutionResourceResolver(directory);
        var plan = ImageSourcePolicy.CreateLocalPlan("source", config, resolver);
        Assert.Equal(3, plan.Entries.Count);
        Assert.Equal(3, plan.Entries.Select(entry => entry.EntryId).Distinct().Count());
        Assert.Equal(plan.Entries[2].EntryId, plan.SelectedEntryId);
        Assert.Equal(System.IO.Path.Combine(directory, "a.bmp"), plan.Entries[0].Address);
        var repeated = ImageSourcePolicy.CreateLocalPlan("source", config, resolver);
        Assert.Equal(plan.Entries, repeated.Entries);
        config.FilePaths.Clear(); config.CurrentIndex = -1; config.AutoSwitchEnabled = false;
        Assert.Equal(3, plan.Entries.Count); Assert.True(plan.AutoAdvance);
    }

    [Fact]
    public void Selected_run_filter_preserves_original_positions_and_repeated_reference_occurrences()
    {
        var node=ImageSourceTestRuntime.Node();
        ImageSourceTestRuntime.Edit(node,config=>{config.FilePaths=new(){"a.bmp","b.bmp","a.bmp"};config.ForRunPaths=new(){"a.bmp"};
            config.RunMode=1;config.CurrentIndex=2;config.AutoSwitchEnabled=false;config.StopAtLastImage=true;});
        var config=ImageSourceTestRuntime.Read(node);
        var resolver = new SolutionResourceResolver(System.IO.Path.GetTempPath());
        var plan = ImageSourcePolicy.CreateLocalPlan("source", config, resolver);
        Assert.Equal(new[] { 0, 2 }, plan.Entries.Select(entry => entry.OriginalIndex));
        Assert.False(plan.AutoAdvance); Assert.True(plan.StopAtLast);
        var sequences = new LocalImageSequenceService();
        var generation = sequences.Begin("root", new[] { plan });
        Assert.Equal(2, sequences.GetInputs("root", generation).Single().Entry!.OriginalIndex);
        config.CurrentIndex = 1;
        var excluded = ImageSourcePolicy.CreateLocalPlan("source", config, resolver);
        Assert.Throws<InvalidOperationException>(() => sequences.Begin("root", new[] { excluded }));
    }

    [Fact]
    public void Viewless_empty_source_remains_editable_but_not_ready_for_execution()
    {
        var config = ImageSourceTestRuntime.Read(ImageSourceTestRuntime.Node());
        var plan = ImageSourcePolicy.CreateLocalPlan("source", config, new SolutionResourceResolver(null));
        Assert.Empty(plan.Entries); Assert.Null(plan.SelectedEntryId);
        Assert.NotEmpty(RootWorkflowReadiness.EvaluateImageSource("source", config));
        config.SourceKind = SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera;
        Assert.Contains(RootWorkflowReadiness.EvaluateImageSource("source", config), error => error.Contains("相机"));
        config.CameraDeviceId = "camera";
        Assert.Empty(RootWorkflowReadiness.EvaluateImageSource("source", config));
    }

    [Fact]
    public void Resource_resolution_keeps_the_captured_base_and_rejects_unresolved_references()
    {
        var original = new Solution { ResourceBaseDirectory = System.IO.Path.GetTempPath() };
        var resolver = SolutionResourceResolver.For(original);
        original.ResourceBaseDirectory = System.IO.Path.GetPathRoot(System.IO.Path.GetTempPath());
        Assert.Equal(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "never-created.bmp")), resolver.ResolveAddress("source", "never-created.bmp"));
        Assert.Throws<System.IO.InvalidDataException>(() => new SolutionResourceResolver(null).ResolveAddress("source", "relative.bmp"));
        Assert.Throws<System.IO.InvalidDataException>(() => resolver.ResolveAddress("source", "{unresolved}"));
    }
}
