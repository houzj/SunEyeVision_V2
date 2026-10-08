using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.NPointCalibration.Tests;

public class NPointCalibrationTests
{
    private const double A = 2, B = 0.3, C = 100, D = -0.2, E = 3, F = 50;
    private const double Dx = 5, Dy = -3;
    private const double ReferenceAngle = 17;

    private static NPointCalibrationParameters CreateExact()
    {
        var p = new NPointCalibrationParameters
        {
            AcquisitionMode = NPointAcquisitionMode.Manual,
            ReferenceRAngle = ReferenceAngle,
            SourceDeviceId = "camera-1", ImageWidth = 1920, ImageHeight = 1080,
        };
        for (int i = 0; i < 9; i++)
        {
            double u = (i % 3) * 20, v = (i / 3) * 20;
            p.Samples.Add(new NPointSample
            {
                Id = i, Kind = NPointSampleKind.Translation,
                ImageX = u, ImageY = v, AxisX = A * u + B * v + C - Dx,
                AxisY = D * u + E * v + F - Dy, AxisAngle = ReferenceAngle,
            });
        }
        const double centerU = 20, centerV = 20;
        double sx = A * centerU + B * centerV + C - Dx;
        double sy = D * centerU + E * centerV + F - Dy;
        double determinant = A * E - B * D;
        var angles = new[] { 0.0, 90.0, 180.0 };
        for (int i = 0; i < angles.Length; i++)
        {
            double radians = angles[i] * Math.PI / 180;
            double markX = sx + Math.Cos(radians) * Dx - Math.Sin(radians) * Dy;
            double markY = sy + Math.Sin(radians) * Dx + Math.Cos(radians) * Dy;
            double x = markX - C, y = markY - F;
            p.Samples.Add(new NPointSample
            {
                Id = 9 + i, Kind = NPointSampleKind.Rotation,
                ImageX = (E * x - B * y) / determinant,
                ImageY = (-D * x + A * y) / determinant,
                AxisX = sx, AxisY = sy, AxisAngle = ReferenceAngle + angles[i],
            });
        }
        return p;
    }

    private static NPointSample[] CreateIndependentValidation(NPointCalibrationParameters p)
    {
        double u = 70, v = 10;
        var now = DateTimeOffset.UtcNow;
        var translation = new NPointSample
        {
            Id = 0, Kind = NPointSampleKind.Translation,
            ImageX = u, ImageY = v,
            AxisX = A * u + B * v + C - Dx,
            AxisY = D * u + E * v + F - Dy,
            AxisAngle = ReferenceAngle,
            Source = "VerificationWorkflow", FrameId = 100,
            FrameCapturedAt = now, AxisReadAt = now,
        };
        var axis = p.Samples[9];
        double phi = 45 * Math.PI / 180;
        double mx = axis.AxisX + Math.Cos(phi) * Dx - Math.Sin(phi) * Dy - C;
        double my = axis.AxisY + Math.Sin(phi) * Dx + Math.Cos(phi) * Dy - F;
        double det = A * E - B * D;
        var rotation = new NPointSample
        {
            Id = 1, Kind = NPointSampleKind.Rotation,
            ImageX = (E * mx - B * my) / det,
            ImageY = (-D * mx + A * my) / det,
            AxisX = axis.AxisX, AxisY = axis.AxisY,
            AxisAngle = ReferenceAngle + 45,
            Source = "VerificationWorkflow", FrameId = 101,
            FrameCapturedAt = now.AddSeconds(1), AxisReadAt = now.AddSeconds(1),
        };
        return [translation, rotation];
    }

    [Fact]
    public void JointFitRecoversAffineAndAxisToMarkOffset()
    {
        var p = CreateExact();
        var snapshot = (NPointCalibrationParameters)ToolParameterSnapshot.Create(p);
        Assert.Equal(12, snapshot.Samples.Count);
        var result = new NPointCalibrationTool().Run(snapshot);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.InRange(result.RmsError, 0, 1e-8);
        Assert.InRange(result.AxisToMarkX, Dx - 1e-8, Dx + 1e-8);
        Assert.InRange(result.AxisToMarkY, Dy - 1e-8, Dy + 1e-8);
        Assert.Equal(12, result.Residuals.Count);
        Assert.InRange(result.HomMat2D[0], A - 1e-8, A + 1e-8);
        Assert.InRange(result.HomMat2D[1], B - 1e-8, B + 1e-8);
        Assert.InRange(result.HomMat2D[2], C - 1e-8, C + 1e-8);
        Assert.InRange(result.HomMat2D[3], D - 1e-8, D + 1e-8);
        Assert.InRange(result.HomMat2D[4], E - 1e-8, E + 1e-8);
        Assert.InRange(result.HomMat2D[5], F - 1e-8, F + 1e-8);
        Assert.Contains(result.GetOutputVariables(), variable => variable.Name == "AxisToMarkX");
    }

    [Fact]
    public void HuberFitsCleanData() => AssertRobustFitsCleanData(NPointWeightFunction.Huber);

    [Fact]
    public void TukeyFitsCleanData() => AssertRobustFitsCleanData(NPointWeightFunction.Tukey);

    private static void AssertRobustFitsCleanData(NPointWeightFunction function)
    {
        var p = CreateExact();
        p.WeightFunction = function;
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.InRange(result.RmsError, 0, 1e-8);
        Assert.True(result.RobustThresholdMm > 0);
    }

    [Fact]
    public void TukeyRejectsTranslationOutlierWithoutDeletingSample()
    {
        var p = CreateExact();
        p.Samples[0].ImageX += 100;
        p.WeightFunction = NPointWeightFunction.Tukey;
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.Equal(0, result.Residuals.Single(r => r.Id == 0).Weight);
        Assert.InRange(result.AxisToMarkX, Dx - 1e-6, Dx + 1e-6);
        Assert.Equal(12, p.Samples.Count);
    }

    [Fact]
    public void HuberDownweightsTranslationOutlier()
    {
        var p = CreateExact();
        p.Samples[0].ImageX += 100;
        p.WeightFunction = NPointWeightFunction.Huber;
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.InRange(result.Residuals.Single(r => r.Id == 0).Weight, 0, 0.1);
    }

    [Fact]
    public void RotationOutlierAmongOnlyThreeCannotBeAssumedCorrect()
    {
        var p = CreateExact();
        p.Samples[9].ImageX += 100;
        p.WeightFunction = NPointWeightFunction.Tukey;
        var result = new NPointCalibrationTool().Run(p);
        // Three rotations contain no redundant rotational observation. A gross
        // error must not be mistaken for independently verified calibration.
        Assert.True(!result.CalibrationReady || result.MaxError > 1
            || Math.Abs(result.AxisToMarkX - Dx) > 1);
    }

    [Fact]
    public void RotationAxisDriftAndWrongModelAreRejected()
    {
        var p = CreateExact();
        p.Samples[10].AxisX += 0.1;
        var drift = new NPointCalibrationTool().Run(p);
        Assert.False(drift.CalibrationReady);
        Assert.Contains("轴心 XY", drift.ErrorMessage);
        p.Samples[10].AxisX -= 0.1;
        p.TransformModel = NPointTransformModel.Projective;
        var projective = new NPointCalibrationTool().Run(p);
        Assert.False(projective.CalibrationReady);
        Assert.Contains("仿射", projective.ErrorMessage);
    }

    [Fact]
    public void TranslationAngleDriftIsRejected()
    {
        var p = CreateExact();
        p.Samples[2].AxisAngle += 1;
        Assert.Contains("参考角", new NPointCalibrationTool().Run(p).ErrorMessage);
    }

    [Fact]
    public void ReplacedOrClearedPointsRemainInDraftAuditHistory()
    {
        var p = CreateExact();
        var revised = p.Samples.Select(s => s.Copy()).ToList();
        revised[0].ImageX += 1;
        p.ReplaceSamples(revised);
        Assert.Single(p.ArchivedSamples);
        Assert.False(p.ArchivedSamples[0].Enabled);
        p.ReplaceSamples(Array.Empty<NPointSample>());
        Assert.Empty(p.Samples);
        Assert.Equal(13, p.ArchivedSamples.Count);
    }

    [Fact]
    public void DisabledPointIsArchivedAndCanBeReenabled()
    {
        var p = CreateExact();
        var edited = p.Samples.Select(s => s.Copy()).ToList();
        edited[0].Enabled = false;
        p.ReplaceSamples(edited);
        Assert.Equal(11, p.Samples.Count);
        Assert.Contains(p.ArchivedSamples, s => s.Id == 0 && !s.Enabled);
        var restored = p.ArchivedSamples.Last(s => s.Id == 0).Copy();
        restored.Enabled = true;
        p.ReplaceSamples(p.Samples.Append(restored));
        Assert.Equal(12, p.Samples.Count);
    }

    [Fact]
    public void TriggerCommitsOnePairPerEventAndRejectsStaleRevision()
    {
        var now = DateTimeOffset.UtcNow;
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates,
            FrameIdText = "1", FrameTimestampText = now.ToString("O"),
            AxisReadUnixMs = now.ToUnixTimeMilliseconds(),
        };
        p.TeachSignal = true;
        var tool = new NPointCalibrationTool();
        var first = tool.Run(p);
        var stale = tool.Run(p);
        Assert.True(p.TryCommit(first, out _));
        Assert.False(p.TryCommit(stale, out _));
        Assert.Single(p.Samples);
        Assert.True(p.TryCommit(tool.Run(p), out _));
        Assert.Single(p.Samples);
        p.TeachSignal = false;
        Assert.True(p.TryCommit(tool.Run(p), out _));
        p.TeachSignal = true;
        Assert.True(p.TryCommit(tool.Run(p), out _));
        Assert.Equal(2, p.Samples.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task WorkflowNodeCommitsCollectedPointOutsideRunSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates, TeachSignal = true,
            ImageX = 12, ImageY = 34, AxisX = 56, AxisY = 78,
            FrameIdText = "7", FrameTimestampText = now.ToString("O"),
            AxisReadUnixMs = now.ToUnixTimeMilliseconds(),
        };
        var node = new AlgorithmNode("calibration-test", "N点标定1", "N点标定1",
            new NPointCalibrationTool()) { Parameters = p };
        var outcome = await node.ExecuteAsync(null);
        Assert.True(outcome.Success, outcome.ErrorMessage);
        Assert.Single(p.Samples);
        Assert.Equal(56, p.Samples[0].AxisX);
        Assert.Equal(7, p.Samples[0].FrameId);
    }

    [Fact]
    public void TriggerRejectsMissingOrStaleImageEncoderPair()
    {
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates, TeachSignal = true,
        };
        var tool = new NPointCalibrationTool();
        Assert.Contains("帧时间", tool.Run(p).ErrorMessage);
        var now = DateTimeOffset.UtcNow;
        p.FrameTimestampText = now.ToString("O");
        p.FrameIdText = "1";
        p.AxisReadUnixMs = now.AddSeconds(-2).ToUnixTimeMilliseconds();
        Assert.Contains("配对时间窗", tool.Run(p).ErrorMessage);
    }

    [Fact]
    public void CandidateFileIsNeverPublishedByTrainingTool()
    {
        var p = CreateExact();
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        string active = Path.Combine(Path.GetTempPath(), "NPoint-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(active);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(active, result, p.Samples, 0.01);
            Assert.True(File.Exists(candidate));
            Assert.False(File.Exists(active));
            var (loaded, _) = NPointCalibrationFile.ReadCandidate(candidate);
            Assert.Equal("SolvedUnverified", loaded.State);
            Assert.Throws<InvalidDataException>(() => NPointCalibrationFile.ReadVerified(candidate,
                p.SourceDeviceId, p.ImageWidth, p.ImageHeight, p.ImageDomain));
        }
        finally { if (File.Exists(candidate)) File.Delete(candidate); }
    }

    [Fact]
    public void FileContainsNewJointFitContract()
    {
        var p = CreateExact();
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        string path = Path.Combine(Path.GetTempPath(), "NPointCalibration-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(path);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(path, result, p.Samples, 0.01);
            using var json = JsonDocument.Parse(File.ReadAllText(candidate));
            Assert.Equal(2, json.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal("ImageColumnRowToMachineMarkXY", json.RootElement.GetProperty("Direction").GetString());
            Assert.Equal(12, json.RootElement.GetProperty("Samples").GetArrayLength());
            Assert.Equal(Dx, json.RootElement.GetProperty("AxisToMarkX").GetDouble(), 7);
            Assert.Equal("SolvedUnverified", json.RootElement.GetProperty("State").GetString());
        }
        finally { if (File.Exists(candidate)) File.Delete(candidate); }
    }

    [Fact]
    public void VerificationWorkflowPublishesOnlyIndependentPassingCandidate()
    {
        var p = CreateExact();
        var result = new NPointCalibrationTool().Run(p);
        string active = Path.Combine(Path.GetTempPath(), "NPoint-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(active);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(active, result, p.Samples, 0.01);
            var validation = CreateIndependentValidation(p);
            var (_, sha256) = NPointCalibrationFile.ReadCandidate(candidate);
            var config = new NPointVerificationParameters
            {
                CandidateFilePath = candidate, ActiveFilePath = active,
                SourceDeviceId = p.SourceDeviceId, ImageWidth = p.ImageWidth,
                ImageHeight = p.ImageHeight, ImageDomain = p.ImageDomain,
                MaximumValidationErrorMm = 0.01, PublishOnPass = true,
                CandidateSha256 = sha256,
                ValidationSamples = validation.ToList(),
            };
            var verified = new NPointVerificationTool().Run(config);
            Assert.True(verified.ValidationPassed, verified.ErrorMessage);
            Assert.True(config.TryCommit(verified, out var error), error);
            Assert.True(verified.Published);
            using var json = JsonDocument.Parse(File.ReadAllText(active));
            Assert.Equal("Verified", json.RootElement.GetProperty("State").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("ValidationSamples").GetArrayLength());
            validation[0].AxisX += 2;
            Assert.Throws<InvalidOperationException>(() => NPointCalibrationFile.Publish(
                candidate, active, verified.CandidateSha256, validation, 0.01));
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
            if (File.Exists(active)) File.Delete(active);
        }
    }

    [Fact]
    public void VerificationNodeCommitsTwoNewEventsAndPublishesExactCandidate()
    {
        var training = CreateExact();
        var fit = new NPointCalibrationTool().Run(training);
        string active = Path.Combine(Path.GetTempPath(), "NPoint-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(active);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(active, fit, training.Samples, 0.01);
            var points = CreateIndependentValidation(training);
            var settings = new NPointVerificationParameters
            {
                CandidateFilePath = candidate, ActiveFilePath = active,
                SourceDeviceId = training.SourceDeviceId, ImageWidth = training.ImageWidth,
                ImageHeight = training.ImageHeight, ImageDomain = training.ImageDomain,
                MaximumValidationErrorMm = 0.01, CaptureSample = true, PublishOnPass = true,
            };
            var tool = new NPointVerificationTool();
            for (int i = 0; i < 2; i++)
            {
                settings.SampleEventId = i + 1;
                settings.SampleKind = points[i].Kind;
                settings.ImageX = points[i].ImageX; settings.ImageY = points[i].ImageY;
                settings.AxisX = points[i].AxisX; settings.AxisY = points[i].AxisY;
                settings.AxisAngle = points[i].AxisAngle;
                settings.FrameId = points[i].FrameId!.Value;
                settings.FrameTimestampText = points[i].FrameCapturedAt!.Value.ToString("O");
                settings.AxisReadUnixMs = points[i].AxisReadAt!.Value.ToUnixTimeMilliseconds();
                var snapshot = (NPointVerificationParameters)ToolParameterSnapshot.Create(settings);
                var result = tool.Run(snapshot);
                Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.True(settings.TryCommit(result, out var error), error);
                Assert.Equal(i + 1, settings.ValidationSamples.Count);
                if (i == 0) Assert.False(File.Exists(active));
                else Assert.True(result.Published);
            }
            var published = NPointCalibrationFile.ReadVerified(active, training.SourceDeviceId,
                training.ImageWidth, training.ImageHeight, training.ImageDomain);
            Assert.Equal(2, published.ValidationSamples.Length);
            Assert.False(tool.Run(settings).NeedsCommit);
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
            if (File.Exists(active)) File.Delete(active);
        }
    }

    [Fact]
    public void ChangedCandidateCannotReuseEarlierVerification()
    {
        var training = CreateExact();
        var fit = new NPointCalibrationTool().Run(training);
        string active = Path.Combine(Path.GetTempPath(), "NPoint-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(active);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(active, fit, training.Samples, 0.01);
            var (_, digest) = NPointCalibrationFile.ReadCandidate(candidate);
            var first = CreateIndependentValidation(training)[0];
            var settings = new NPointVerificationParameters
            {
                CandidateFilePath = candidate, SourceDeviceId = training.SourceDeviceId,
                ImageWidth = training.ImageWidth, ImageHeight = training.ImageHeight,
                ImageDomain = training.ImageDomain, CandidateSha256 = digest,
                ValidationSamples = [first], MaximumValidationErrorMm = 0.01,
            };
            NPointCalibrationParameters.WriteCandidateFile(active, fit, training.Samples, 0.01);
            var result = new NPointVerificationTool().Run(settings);
            Assert.False(result.IsSuccess);
            Assert.Contains("候选文件已变化", result.ErrorMessage);
            Assert.False(File.Exists(active));
        }
        finally { if (File.Exists(candidate)) File.Delete(candidate); }
    }

    [Fact]
    public void MissingCameraIdentityCannotBePublished()
    {
        var p = CreateExact();
        p.SourceDeviceId = string.Empty;
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        string active = Path.Combine(Path.GetTempPath(), "NPoint-" + Guid.NewGuid().ToString("N") + ".json");
        string candidate = NPointCalibrationParameters.CandidatePath(active);
        try
        {
            NPointCalibrationParameters.WriteCandidateFile(active, result, p.Samples, 0.01);
            var (file, _) = NPointCalibrationFile.ReadCandidate(candidate);
            Assert.Contains("相机", Assert.Throws<InvalidOperationException>(() =>
                file.Validate(CreateIndependentValidation(p), 0.01)).Message);
        }
        finally { if (File.Exists(candidate)) File.Delete(candidate); }
    }

    [Fact]
    public void DebugControlAndPointEditorLoadWithoutMainWindow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new Views.NPointCalibrationDebugControl();
                Assert.NotNull(control.Parameters);
                Assert.Equal("平移 0/9，旋转 0/3", control.ProgressText);
                var triggerInputs = Assert.IsType<System.Windows.Controls.StackPanel>(control.FindName("TriggerInputsPanel"));
                var teachFields = Assert.IsType<System.Windows.Controls.StackPanel>(control.FindName("TeachFieldsPanel"));
                control.Parameters.AcquisitionMode = NPointAcquisitionMode.Manual;
                control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                control.UpdateLayout();
                Assert.Equal(System.Windows.Visibility.Collapsed, triggerInputs.Visibility);
                control.Parameters.AcquisitionMode = NPointAcquisitionMode.Trigger;
                control.Parameters.TeachEnabled = true;
                control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                control.UpdateLayout();
                Assert.Equal(System.Windows.Visibility.Visible, triggerInputs.Visibility);
                Assert.Equal(System.Windows.Visibility.Visible, teachFields.Visibility);
                control.Parameters.ReplaceSamples([new NPointSample { Id = 0, Kind = NPointSampleKind.Translation }]);
                Assert.Equal("平移 1/9，旋转 0/3", control.ProgressText);
                var snapshotDir = Environment.GetEnvironmentVariable("NPOINT_UI_SNAPSHOT_DIR");
                if (!string.IsNullOrWhiteSpace(snapshotDir))
                {
                    Directory.CreateDirectory(snapshotDir);
                    var tabs = Assert.IsType<System.Windows.Controls.TabControl>(control.FindName("ParameterTabs"));
                    control.Measure(new System.Windows.Size(600, 720));
                    control.Arrange(new System.Windows.Rect(0, 0, 600, 720));
                    for (int page = 0; page < 2; page++)
                    {
                        tabs.SelectedIndex = page;
                        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                        control.UpdateLayout();
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                            600, 720, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(control);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(snapshotDir, page == 0 ? "npoint-basic.png" : "npoint-runtime.png"));
                        encoder.Save(output);
                    }
                }
                var verificationControl = new Views.NPointVerificationDebugControl();
                Assert.NotNull(verificationControl.Parameters);
                var editor = new Views.NPointSampleEditor(new NPointCalibrationParameters());
                var grid = Assert.IsType<System.Windows.Controls.DataGrid>(editor.FindName("SampleGrid"));
                Assert.Equal(9, grid.Columns.Count);
                Assert.Equal(12, grid.Items.Count);
                editor.Close();
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }
}
