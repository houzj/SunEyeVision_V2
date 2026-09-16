using System.Text.Json.Serialization;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class PositionCorrectionDisplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedParameters_RenderSameCorrectionBeforeAndAfterEditorRestoresBinding(bool openEditor)
    {
        var matrix = new AffineMatrix(1, 0, 0, 1, -227.365, 229.0297);
        var saved = new RecordingParameters
        {
            IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new AvailableDataSource
            {
                SourceNodeId = "match",
                PropertyName = nameof(MatchResults.Matrices),
                CurrentValue = new[] { matrix }
            }
        };
        // Loading preserves the reference, but drops the editor-only binding cache.
        var loaded = Assert.IsType<RecordingParameters>(ToolParameterSnapshot.Create(saved));
        Assert.Null(loaded.PositionCorrectionBindingSource);
        Assert.NotNull(loaded.PositionCorrectionSource);

        // Execution restores its own parameter copy; the node's editable parameters stay separate.
        var execution = Assert.IsType<RecordingParameters>(ToolParameterSnapshot.Create(loaded));
        Assert.True(execution.RestorePositionCorrectionSource(new[] { saved.PositionCorrectionBindingSource! }));
        Assert.Null(loaded.PositionCorrectionBindingSource);
        if (openEditor)
            Assert.True(loaded.RestorePositionCorrectionSource(new[] { saved.PositionCorrectionBindingSource! }));

        Render(loaded, matrix);

        Assert.Same(matrix, loaded.RenderedCorrection);
        if (!openEditor)
            Assert.Null(loaded.PositionCorrectionBindingSource);
    }

    [Fact]
    public void Display_QueriesCurrentResultInsteadOfEditorCachedValue()
    {
        var cached = new AffineMatrix(1, 0, 0, 1, 10, 20);
        var current = new AffineMatrix(1, 0, 0, 1, -227, 229);
        var parameters = new RecordingParameters
        {
            IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new AvailableDataSource
            {
                SourceNodeId = "match",
                PropertyName = nameof(MatchResults.Matrices),
                CurrentValue = new[] { cached }
            }
        };

        Render(parameters, current);
        Assert.Same(current, parameters.RenderedCorrection);

        var nextRun = new AffineMatrix(1, 0, 0, 1, 30, 40);
        Render(parameters, nextRun);
        Assert.Same(nextRun, parameters.RenderedCorrection);
    }

    [Fact]
    public void DisabledCorrection_DoesNotTransformDisplayedGuides()
    {
        var parameters = new RecordingParameters
        {
            IsPositionCorrectionEnabled = false,
            PositionCorrectionSource = new ImageSourceReference("match", nameof(MatchResults.Matrices), "", "")
        };
        Render(parameters, new AffineMatrix(1, 0, 0, 1, -227, 229));
        Assert.Null(parameters.RenderedCorrection);
    }

    private static void Render(RecordingParameters parameters, AffineMatrix matrix)
    {
        var context = new WorkflowContext();
        context.SetNodeResult("match", new NodeExecutionResult
        {
            NodeId = "match", Success = true, Result = new MatchResults { Matrices = new[] { matrix } }
        });
        var manager = new NodeResultManager(() => context, Substitute.For<INodeResultSink>());
        var node = new WorkflowNodeBase("line", "Line", "Line", "Algorithm") { Parameters = parameters };
        manager.RefreshResultDisplay(node, new GenericToolResults());
        Assert.True(parameters.WasRendered);
    }

    public sealed class RecordingParameters : RoiParameters
    {
        public override bool HasMeasureGuide => true;
        [JsonIgnore] public bool WasRendered { get; private set; }
        [JsonIgnore] public AffineMatrix? RenderedCorrection { get; private set; }

        public override IEnumerable<DisplayItemBase> BuildMeasureGuideItems(AffineMatrix? correctionMatrix)
        {
            WasRendered = true;
            RenderedCorrection = correctionMatrix;
            return Array.Empty<DisplayItemBase>();
        }
    }

    public sealed class MatchResults : ToolResults
    {
        public AffineMatrix[] Matrices { get; set; } = Array.Empty<AffineMatrix>();
    }
}
