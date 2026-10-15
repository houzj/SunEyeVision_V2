using System.Reflection;
using System.Runtime.ExceptionServices;
using NSubstitute;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using SunEyeVision.Tool.CircleCircleMeasure;
using SunEyeVision.Tool.CircleCircleMeasure.Views;
using Xunit;

namespace SunEyeVision.Tool.CircleCircleMeasure.Tests;

[Collection("Correction WPF")]
public class CorrectionPreviewLifecycleTests
{
    [Fact]
    public void Historical_guides_and_bands_do_not_follow_later_parameter_edits()
    {
        var p = new CircleCircleMeasureParameters { E1CenterX = 100, E1CenterY = 80, E1Radius = 40,
            E2CenterX = 300, E2CenterY = 80, E2Radius = 20, E1NumMeasures = 8, E2NumMeasures = 12 };
        var result = new CircleCircleMeasureResults();
        string Geometry() => System.Text.Json.JsonSerializer.Serialize(result.Display.Items
            .OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .Select(i => i.Geometries).ToArray());
        result.FillDisplay(true, p);
        var original = Geometry();
        p.E1CenterX = 999; p.E1CenterY = 555; p.E1Radius = 60;
        p.E2CenterX = 777; p.E2Radius = 80;
        p.E1MeasureLength1 = 1; p.E1MeasureLength2 = 2; p.E1NumMeasures = 3;
        p.E2MeasureLength1 = 4; p.E2MeasureLength2 = 6; p.E2NumMeasures = 5;
        result.FillDisplay(true, p);
        Assert.Equal(original, Geometry());
        p.ResultPresentation.GetGraphicsStyle("guide-circle1").IsVisible = false;
        result.FillDisplay(true, p);
        Assert.False(result.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .Single(i => i.Style?.PresentationKey == "guide-circle1").IsVisible);
        var next = new CircleCircleMeasureResults(); next.FillDisplay(true, p);
        Assert.Equal(999, next.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .SelectMany(i => i.Geometries).OfType<SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d>().First().Center.X);
    }

    [Fact]
    public void Result_redraw_uses_captured_matrix_and_missing_enabled_correction_fails_before_measurement()
    {
        var matrix = new AffineMatrix(1, 0, 0, 1, 50, 20);
        var p = new CircleCircleMeasureParameters { E1CenterX = 100, E1CenterY = 80, E1Radius = 40,
            E2CenterX = 300, E2CenterY = 80, E2Radius = 20, IsPositionCorrectionEnabled = true,
            PositionCorrectionBindingSource = new AvailableDataSource { SourceNodeId = "match", PropertyName = "Matrices", CurrentValue = new[] { matrix } } };
        var result = new CircleCircleMeasureResults();
        result.FillDisplay(true, p);
        double GuideX() => result.Display.Items.OfType<SunEyeVision.Plugin.SDK.Models.Visualization.GraphicsDisplayItem>()
            .SelectMany(i => i.Geometries).OfType<SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d>().First().Center.X;
        Assert.Equal(150, GuideX());
        matrix.OffsetX = 999; p.IsPositionCorrectionEnabled = false;
        result.FillDisplay(true, p); Assert.Equal(150, GuideX());
        p.IsPositionCorrectionEnabled = true; p.PositionCorrectionBindingSource!.CurrentValue = null;
        var failed = new CircleCircleMeasureTool().Run(p);
        Assert.False(failed.IsSuccess); Assert.Contains("位置修正", failed.ErrorMessage);
    }
    [Fact]
    public void Real_editor_reads_live_matrix_refreshes_on_toggle_and_never_moves_saved_guides()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var cached = new AffineMatrix(1, 0, 0, 1, 180, 0);
                AffineMatrix[] current = { new(1, 0, 0, 1, -50, 20) };
                ToolResults SourceResult(AffineMatrix[] matrices)
                {
                    var source = new GenericToolResults { Status = ExecutionStatus.Success };
                    source.AddResultItem("Matrices", matrices);
                    return source;
                }
                var sourceResult = SourceResult(current);
                var query = Substitute.For<IDataSourceQueryService>();
                query.GetPropertyValue("match", "Matrices").Returns(_ => current);
                query.GetNodeContext("match").Returns(_ => new NodeExecutionContext { NodeId = "match", Result = sourceResult });
                var p = new CircleCircleMeasureParameters { E1CenterX = 100, E1CenterY = 80, E1Radius = 40,
                    E2CenterX = 300, E2CenterY = 80, E2Radius = 20, IsPositionCorrectionEnabled = true,
                    PositionCorrectionBindingSource = new AvailableDataSource { SourceNodeId = "match", PropertyName = "Matrices", CurrentValue = new[] { cached } } };
                var editor = new CircleCircleMeasureToolDebugControl();
                try
                {
                    editor.SetParameters(p); editor.SetDataProvider(query);
                    var elements = Assert.IsType<MeasureElementsEditorControl>(editor.FindName("ElementsEditor"));
                    AffineMatrix? Matrix() => (AffineMatrix?)typeof(MeasureEditorControl).GetField("_positionCorrectionMatrix", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(elements.Element1);
                    Assert.Equal(-50, Matrix()!.OffsetX);
                    var snapshot = ToolDebugResultSnapshot.Capture(query, new[] { "match" });
                    var executionCopy = Assert.IsType<CircleCircleMeasureParameters>(editor.GetType()
                        .GetMethod("CreateCorrectionExecutionParameters", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, new object[] { snapshot, null! }));
                    Assert.Equal(-50, Assert.IsType<AffineMatrix>(executionCopy.PositionCorrectionBindingSource!.CurrentValue).OffsetX);
                    Assert.Equal(180, Assert.IsType<AffineMatrix[]>(p.PositionCorrectionBindingSource!.CurrentValue)[0].OffsetX);
                    Assert.NotSame(p, executionCopy);
                    current = new[] { new AffineMatrix(1, 0, 0, 1, 15, 30) };
                    sourceResult = SourceResult(current);
                    editor.OnExternalResultArrived(); Assert.Equal(15, Matrix()!.OffsetX);
                    p.IsPositionCorrectionEnabled = false; Assert.Null(Matrix());
                    p.IsPositionCorrectionEnabled = true; Assert.Equal(15, Matrix()!.OffsetX);
                    current = Array.Empty<AffineMatrix>();
                    editor.OnExternalResultArrived(); Assert.False(elements.IsEnabled);
                    Assert.False(elements.Element1.ProcessMouseDown(new System.Windows.Point(100, 80)));
                    elements.Element1.HandleDelete(); Assert.True(elements.Element1.HasGuide);
                    current = new[] { AffineMatrix.Identity };
                    editor.OnExternalResultArrived(); Assert.True(elements.IsEnabled);
                    Assert.Equal(100, p.E1CenterX); Assert.Equal(40, p.E1Radius);
                    Assert.Equal(300, p.E2CenterX);
                    var lastPreview = editor.CorrectionPreview;
                    editor.Cleanup(); p.IsPositionCorrectionEnabled = false;
                    Assert.Same(lastPreview, editor.CorrectionPreview);
                }
                finally { editor.Cleanup(); }
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
