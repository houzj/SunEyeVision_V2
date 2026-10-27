using NSubstitute;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class ImageFrameProvenanceTests
{
    public sealed class Parameters : ToolParameters { }
    public sealed class Results : ToolResults
    {
        public IVisionImage? Image { get; set; }
        public AffineMatrix[]? Matrices { get; set; }
        public override List<PropertyMapEntry> GetPropertyMap() => new() { new(nameof(Matrices), "位置修正") };
        public override Dictionary<string, IVisionImage?> GetDisplayImages() => new() { ["Image"] = Image };
    }
    private class ImageTool : IToolPlugin<Parameters, Results>, IImageFramePolicyProvider
    {
        public IVisionImage? Output;
        public AffineMatrix[]? Matrices;
        public ImageFramePolicy ImageFramePolicy { get; init; }
        public Results Run(Parameters p) => new() { Status = ExecutionStatus.Success, Image = Output, Matrices = Matrices };
    }
    private sealed class AsyncImageTool : ImageTool, IAsyncToolPlugin
    {
        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) => new(Run((Parameters)request.Parameters));
    }

    [Fact]
    public void Reused_source_image_gets_new_frame_but_old_matching_result_keeps_its_input_frame()
    {
        var image = Substitute.For<IVisionImage>();
        var derived = Substitute.For<IVisionImage>();
        var executor = new ToolExecutor(new ParameterResolver());
        var producer = new ImageTool { Output = image, ImageFramePolicy = ImageFramePolicy.NewFrame };
        executor.Run<Parameters, Results>(producer, new Parameters());
        var firstFrame = ImageFrameProvenance.CaptureInput(image);
        var filter = new ImageTool { Output = derived, ImageFramePolicy = ImageFramePolicy.PreserveCoordinates,
            Matrices = new[] { new AffineMatrix(1,0,0,1,3,4) } };
        var matching = executor.Run<Parameters, Results>(filter, new Parameters { ResolvedInputImage = image });
        Assert.Equal(firstFrame, ImageFrameProvenance.CaptureInput(derived));
        Assert.Equal(firstFrame, ImageFrameProvenance.GetInputFrame(matching));
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true, ResolvedInputImage = derived,
            PositionCorrectionBindingSource = new() { SourceNodeId = "match", PropertyName = "Matrices", CurrentValue = AffineMatrix.Identity },
            NodeResults = new Dictionary<string, ToolResults> { ["match"] = matching } };
        Assert.Equal(PositionCorrectionState.Valid, PositionCorrectionResolution.Capture(p).State);
        Assert.True(PositionCorrectionResolution.Capture(p).FramePairingVerified);
        var resolution = PositionCorrectionResolution.Capture(p);
        Assert.Equal(3, resolution.Matrix!.OffsetX); // source result wins over the editor's cached Identity
        Assert.Equal(4, resolution.Matrix.OffsetY);
        Assert.Equal(PositionCorrectionState.ImageFrameMismatch, resolution.ForPreview(Guid.NewGuid(), true).State);
        Assert.Equal(PositionCorrectionState.WaitingForSource, resolution.ForPreview(null, false).State);
        Assert.False(resolution.ForPreview(null, true).FramePairingVerified);
        Assert.True(resolution.ForPreview(firstFrame, true).FramePairingVerified);
        Assert.True(resolution.CanUse); // Preview checks never alter the execution snapshot.
        executor.Run((IToolPlugin)producer, new Parameters());
        Assert.NotEqual(firstFrame, ImageFrameProvenance.CaptureInput(image));
        Assert.Equal(firstFrame, ImageFrameProvenance.GetInputFrame(matching));
        p.ResolvedInputImage = image;
        Assert.Equal(PositionCorrectionState.ImageFrameMismatch, PositionCorrectionResolution.Capture(p).State);
        p.IsPositionCorrectionEnabled = false;
        Assert.Equal(PositionCorrectionState.Disabled, PositionCorrectionResolution.Capture(p).State);
    }

    [Fact]
    public void Explicit_result_snapshot_with_no_matrix_does_not_reuse_editor_cached_value()
    {
        var source = new Results { Status = ExecutionStatus.Success };
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new() { SourceNodeId="match", PropertyName="Matrices", CurrentValue=AffineMatrix.Identity },
            NodeResults = new Dictionary<string, ToolResults> { ["match"]=source } };
        var captured = PositionCorrectionResolution.Capture(p);
        Assert.Equal(PositionCorrectionState.WaitingForSource, captured.State);
        Assert.Null(captured.Matrix);
        Assert.False(captured.CanUse);
    }

    [Fact]
    public async Task Async_producer_and_preserving_tool_propagate_frame_without_exposing_output_fields()
    {
        var image = Substitute.For<IVisionImage>(); var clone = Substitute.For<IVisionImage>();
        var executor = new ToolExecutor(new ParameterResolver());
        await executor.ExecuteAsync(new AsyncImageTool { Output = image, ImageFramePolicy = ImageFramePolicy.NewFrame }, new(new Parameters()));
        var frame = ImageFrameProvenance.CaptureInput(image);
        var result = await executor.ExecuteAsync(new AsyncImageTool { Output = clone, ImageFramePolicy = ImageFramePolicy.PreserveCoordinates },
            new(new Parameters()) { ResolvedInputImage = image });
        Assert.Equal(frame, ImageFrameProvenance.GetInputFrame(result));
        Assert.Equal(frame, ImageFrameProvenance.CaptureInput(clone));
        Assert.DoesNotContain(result.GetPropertyMap(), e => e.PropertyName.Contains("Frame"));
    }

    [Fact]
    public void Live_query_rejects_old_frame_then_accepts_fresh_matching_result()
    {
        var oldImage = Substitute.For<IVisionImage>(); var newImage = Substitute.For<IVisionImage>();
        ImageFrameProvenance.RecordResult(new Results { Image = oldImage }, null, ImageFramePolicy.NewFrame);
        ImageFrameProvenance.RecordResult(new Results { Image = newImage }, null, ImageFramePolicy.NewFrame);
        var oldResult = new Results { Status = ExecutionStatus.Success };
        ImageFrameProvenance.RecordResult(oldResult, ImageFrameProvenance.CaptureInput(oldImage));
        var query = Substitute.For<IDataSourceQueryService>();
        var context = new NodeExecutionContext { Result = oldResult };
        query.GetNodeContext("match").Returns(context);
        query.GetPropertyValue("match", "Matrices").Returns(AffineMatrix.Identity);
        query.GetPropertyValue("input", "Image").Returns(newImage);
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true,
            ImageSource = new AvailableDataSource { SourceNodeId = "input", PropertyName = "Image" },
            PositionCorrectionBindingSource = new AvailableDataSource { SourceNodeId = "match", PropertyName = "Matrices" } };
        Assert.Equal(PositionCorrectionState.ImageFrameMismatch, PositionCorrectionResolution.Capture(p, query).State);
        var fresh = new Results { Status = ExecutionStatus.Success };
        ImageFrameProvenance.RecordResult(fresh, ImageFrameProvenance.CaptureInput(newImage)); context.Result = fresh;
        Assert.Equal(PositionCorrectionState.Valid, PositionCorrectionResolution.Capture(p, query).State);
        query.GetPropertyValue("input", "Image").Returns((object?)null);
        Assert.Equal(PositionCorrectionState.WaitingForSource, PositionCorrectionResolution.Capture(p, query).State);
    }

    [Fact]
    public void Unknown_legacy_image_has_no_guessed_lineage()
    {
        var legacy = Substitute.For<IVisionImage>();
        Assert.Null(ImageFrameProvenance.CaptureInput(legacy));
        var result = new Results { Image = legacy };
        ImageFrameProvenance.RecordResult(result, null);
        Assert.Null(ImageFrameProvenance.GetInputFrame(result));
        Assert.Null(ImageFrameProvenance.CaptureInput(legacy));
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new() { SourceNodeId = "legacy", PropertyName = "Matrix", CurrentValue = AffineMatrix.Identity } };
        Assert.False(PositionCorrectionResolution.Capture(p).FramePairingVerified);
        Assert.True(PositionCorrectionResolution.Capture(p).CanUse);
    }

    [Fact]
    public void Query_does_not_accept_values_if_source_result_was_replaced_during_capture()
    {
        var context = new NodeExecutionContext { Result = new Results { Status = ExecutionStatus.Success } };
        var query = Substitute.For<IDataSourceQueryService>();
        query.GetNodeContext("match").Returns(context);
        query.GetPropertyValue("match", "Matrix").Returns(_ =>
        {
            context.Result = new Results { Status = ExecutionStatus.Success };
            return AffineMatrix.Identity;
        });
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new() { SourceNodeId = "match", PropertyName = "Matrix" } };
        Assert.Equal(PositionCorrectionState.WaitingForSource, PositionCorrectionResolution.Capture(p, query).State);
    }
}
