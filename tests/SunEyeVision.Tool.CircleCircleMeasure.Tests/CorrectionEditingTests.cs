using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using Xunit;

namespace SunEyeVision.Tool.CircleCircleMeasure.Tests;

[Collection("Correction WPF")]
public class CorrectionEditingTests
{
    [Fact]
    public void Binding_preserves_reference_and_rotated_drag_follows_display_mouse() => Sta(() =>
    {
        var editor = new MeasureEditorControl();
        editor.SetOverlayCanvas(new Canvas());
        editor.SetGuide(new Circle2d(100, 80, 40));
        var m = new AffineMatrix(0, 2, -2, 0, 500, 20);
        editor.SetPositionCorrectionMatrix(m);
        Assert.Equal(100, editor.EditingCircle!.Value.Center.X);
        var edge = m.TransformWpfPoint(new Point(100, 80)) + new Vector(80, 0);
        Assert.True(editor.ProcessMouseDown(edge));
        editor.ProcessMouseMove(edge + new Vector(20, 10));
        editor.ProcessMouseUp();
        var actual = m.TransformWpfPoint(editor.EditingCircle!.Value.Center);
        Assert.Equal(360, actual.X, 6);
        Assert.Equal(230, actual.Y, 6);
        var reference = editor.EditingCircle.Value;
        editor.SetPositionCorrectionMatrix(new AffineMatrix(1, 0, 0, 1, 10, 30));
        Assert.Equal(reference, editor.EditingCircle.Value);
    });

    [Fact]
    public void Draw_after_binding_saves_inverse_center_and_scaled_radius() => Sta(() =>
    {
        var editor = new MeasureEditorControl();
        editor.SetOverlayCanvas(new Canvas());
        var m = new AffineMatrix(0, 2, -2, 0, 500, 20);
        editor.SetPositionCorrectionMatrix(m);
        editor.StartDraw(MeasureGeometryType.Circle);
        editor.ProcessMouseDown(new Point(340, 220));
        editor.ProcessMouseMove(new Point(420, 220));
        editor.ProcessMouseUp();
        Assert.Equal(100, editor.EditingCircle!.Value.Center.X, 6);
        Assert.Equal(80, editor.EditingCircle.Value.Center.Y, 6);
        Assert.Equal(40, editor.EditingCircle.Value.Radius, 6);
    });

    [Fact]
    public void Matrix_update_cancels_drag_and_does_not_keep_external_mutable_matrix() => Sta(() =>
    {
        var editor = new MeasureEditorControl(); editor.SetOverlayCanvas(new Canvas());
        editor.SetGuide(new Circle2d(100, 80, 40));
        var m = new AffineMatrix(1, 0, 0, 1, 50, 0);
        editor.SetPositionCorrectionMatrix(m); m.OffsetX = 999;
        Assert.True(editor.ProcessMouseDown(new Point(190, 80)));
        editor.ProcessMouseMove(new Point(200, 80));
        Assert.Equal(110, editor.EditingCircle!.Value.Center.X);
        editor.SetPositionCorrectionMatrix(new AffineMatrix(1, 0, 0, 1, 60, 0));
        Assert.False(editor.IsAnyInteractionInProgress);
        editor.ProcessMouseMove(new Point(300, 80)); editor.ProcessMouseUp();
        Assert.Equal(100, editor.EditingCircle!.Value.Center.X);
    });

    [Fact]
    public void Replacing_display_image_rolls_back_drag_and_cleanup_unsubscribes() => Sta(() =>
    {
        var image = new SunEyeVision.Plugin.SDK.UI.Controls.ImageControl();
        var elements = new MeasureElementsEditorControl();
        elements.SetMainImageControl(image);
        var editor = elements.Element1;
        editor.SetGuide(new Circle2d(100, 80, 40));
        Assert.True(editor.ProcessMouseDown(new Point(140, 80)));
        editor.ProcessMouseMove(new Point(150, 80));
        Assert.Equal(110, editor.EditingCircle!.Value.Center.X);
        image.SourceImage = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
            System.Windows.Media.PixelFormats.Gray8, null, new byte[] { 0 }, 1);
        Assert.False(editor.IsAnyInteractionInProgress);
        Assert.Equal(100, editor.EditingCircle.Value.Center.X);
        elements.ClearMainOverlay();
        editor.SetOverlayCanvas(new Canvas());
        Assert.True(editor.ProcessMouseDown(new Point(140, 80)));
        image.SourceImage = null;
        Assert.True(editor.IsAnyInteractionInProgress);
        editor.CancelDrawing();
    });

    [Fact]
    public void Changing_binding_with_equal_matrix_rolls_back_live_parameter_edits() => Sta(() =>
    {
        var p = new SunEyeVision.Tool.CircleCircleMeasure.CircleCircleMeasureParameters
        { E1CenterX = 100, E1CenterY = 80, E1Radius = 40, IsPositionCorrectionEnabled = true };
        var query = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Execution.Parameters.IDataSourceQueryService>();
        NSubstitute.SubstituteExtensions.Returns(query.GetPropertyValue("a", "Matrices"), AffineMatrix.Identity);
        NSubstitute.SubstituteExtensions.Returns(query.GetPropertyValue("b", "Matrices"), AffineMatrix.Identity);
        p.PositionCorrectionBindingSource = new() { SourceNodeId = "a", PropertyName = "Matrices" };
        var control = new SunEyeVision.Tool.CircleCircleMeasure.Views.CircleCircleMeasureToolDebugControl();
        try
        {
            control.SetParameters(p); control.SetDataProvider(query);
            var elements = (MeasureElementsEditorControl)control.FindName("ElementsEditor");
            var editor = elements.Element1; editor.SetOverlayCanvas(new Canvas());
            Assert.True(editor.ProcessMouseDown(new Point(140, 80)));
            editor.ProcessMouseMove(new Point(150, 80));
            Assert.Equal(110, p.E1CenterX);
            p.PositionCorrectionBindingSource = new() { SourceNodeId = "b", PropertyName = "Matrices" };
            Assert.False(editor.IsAnyInteractionInProgress);
            Assert.Equal(100, p.E1CenterX);
            editor.ProcessMouseMove(new Point(190, 80)); editor.ProcessMouseUp();
            Assert.Equal(100, p.E1CenterX);
        }
        finally { control.Cleanup(); }
    });

    private sealed class FrameResult : ToolResults
    {
        public SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage? Image { get; set; }
        public override Dictionary<string, SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage?> GetDisplayImages()
            => new() { ["Image"] = Image };
    }

    [Fact]
    public void Real_editor_blocks_known_old_frame_then_recovers_after_current_frame_match() => Sta(() =>
    {
        var oldImage = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage>();
        var currentImage = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage>();
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.RecordResult(new FrameResult { Image = oldImage }, null,
            SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy.NewFrame);
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.RecordResult(new FrameResult { Image = currentImage }, null,
            SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy.NewFrame);
        var match = new FrameResult { Status = ExecutionStatus.Success };
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.RecordResult(match,
            SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.CaptureInput(oldImage));
        var context = new SunEyeVision.Plugin.SDK.Execution.Parameters.NodeExecutionContext { Result = match };
        var query = NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Execution.Parameters.IDataSourceQueryService>();
        NSubstitute.SubstituteExtensions.Returns(query.GetNodeContext("match"), context);
        NSubstitute.SubstituteExtensions.Returns(query.GetPropertyValue("match", "Matrices"), AffineMatrix.Identity);
        NSubstitute.SubstituteExtensions.Returns(query.GetPropertyValue("input", "Image"), currentImage);
        var p = new SunEyeVision.Tool.CircleCircleMeasure.CircleCircleMeasureParameters
        { E1CenterX = 100, E1CenterY = 80, E1Radius = 40, IsPositionCorrectionEnabled = true,
            ImageSource = new() { SourceNodeId = "input", PropertyName = "Image" },
            PositionCorrectionBindingSource = new() { SourceNodeId = "match", PropertyName = "Matrices" } };
        var control = new SunEyeVision.Tool.CircleCircleMeasure.Views.CircleCircleMeasureToolDebugControl();
        try
        {
            control.SetParameters(p); control.SetDataProvider(query);
            var elements = (MeasureElementsEditorControl)control.FindName("ElementsEditor");
            Assert.False(elements.IsEnabled);
            Assert.Equal(SunEyeVision.Plugin.SDK.Execution.Helpers.PositionCorrectionState.ImageFrameMismatch, control.CorrectionPreview!.State);
            Assert.Equal(100, p.E1CenterX);
            var fresh = new FrameResult { Status = ExecutionStatus.Success };
            SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.RecordResult(fresh,
                SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.CaptureInput(currentImage));
            context.Result = fresh; control.OnExternalResultArrived();
            Assert.True(elements.IsEnabled); Assert.True(control.CorrectionPreview!.FramePairingVerified);
            Assert.Equal(100, p.E1CenterX);
            System.Windows.Media.Imaging.BitmapSource Bitmap(Guid? frame)
            {
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
                    System.Windows.Media.PixelFormats.Gray8, null, new byte[] { 0 }, 1);
                SunEyeVision.Plugin.SDK.UI.Controls.PreviewFrameProvenance.Attach(bitmap, frame);
                return bitmap;
            }
            var preview = new SunEyeVision.Plugin.SDK.UI.Controls.ImageControl
            { SourceImage = Bitmap(SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.CaptureInput(currentImage)) };
            control.SetMainImageControl(preview);
            Assert.True(elements.IsEnabled);
            Assert.True(elements.Element1.ProcessMouseDown(new Point(140, 80)));
            elements.Element1.ProcessMouseMove(new Point(150, 80));
            Assert.Equal(110, p.E1CenterX);
            preview.SourceImage = Bitmap(SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.CaptureInput(oldImage));
            Assert.False(elements.IsEnabled); Assert.Equal(100, p.E1CenterX);
            Assert.False(elements.Element1.IsAnyInteractionInProgress);
            Assert.Equal(SunEyeVision.Plugin.SDK.Execution.Helpers.PositionCorrectionState.ImageFrameMismatch, control.CorrectionPreview!.State);
            preview.SourceImage = Bitmap(null);
            Assert.True(elements.IsEnabled); Assert.False(control.CorrectionPreview!.FramePairingVerified);
            preview.SourceImage = Bitmap(SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFrameProvenance.CaptureInput(currentImage));
            Assert.True(elements.IsEnabled); Assert.True(control.CorrectionPreview!.FramePairingVerified);
            preview.SourceImage = null;
            Assert.False(elements.IsEnabled); Assert.Equal(100, p.E1CenterX);
        }
        finally { control.Cleanup(); }
    });

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
