using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.IO;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Tool.NPointCalibration.Tests;

public class NPointCalibrationTests
{
    [Fact]
    public void TriggerCapturesOnlyRisingEdgeAndSolvesNinePointAffine()
    {
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates,
            RotationCount = 0,
        };
        var tool = new NPointCalibrationTool();
        for (int i = 0; i < 9; i++)
        {
            p.ImageX = i % 3 * 20;
            p.ImageY = i / 3 * 20;
            p.PhysicalX = 2 * p.ImageX + 3 * p.ImageY + 10;
            p.PhysicalY = -p.ImageX + 4 * p.ImageY + 20;
            p.TeachSignal = true;
            var result = tool.Run(p);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(p.TryCommit(result, out var error), error);
            Assert.Equal(i + 1, p.Samples.Count);

            var held = tool.Run(p);
            Assert.True(p.TryCommit(held, out error), error);
            Assert.Equal(i + 1, p.Samples.Count);

            p.TeachSignal = false;
            var released = tool.Run(p);
            Assert.True(p.TryCommit(released, out error), error);
        }
        var fitted = tool.Run(p);
        Assert.True(fitted.CalibrationReady, fitted.ErrorMessage);
        Assert.InRange(fitted.MaxError, 0, 1e-6);
        Assert.Equal(9, fitted.TranslationSamples);
        Assert.Contains(fitted.GetOutputVariables(), variable => variable.Name == "HomMat2D");
    }

    [Fact]
    public void TwelvePointsFitRotationCenterAndSurviveParameterSnapshot()
    {
        var p = new NPointCalibrationParameters { AcquisitionMode = NPointAcquisitionMode.Manual };
        for (int i = 0; i < 9; i++)
        {
            double x = i % 3 * 20, y = i / 3 * 20;
            p.Samples.Add(new NPointSample
            {
                Id = i, Kind = NPointSampleKind.Translation,
                ImageX = x, ImageY = y,
                PhysicalX = x * 2 + 10, PhysicalY = y * 3 + 5,
            });
        }
        var rotationPoints = new[] { (60.0, 30.0), (50.0, 40.0), (40.0, 30.0) };
        for (int i = 0; i < 3; i++)
            p.Samples.Add(new NPointSample
            {
                Id = 9 + i, Kind = NPointSampleKind.Rotation,
                ImageX = rotationPoints[i].Item1, ImageY = rotationPoints[i].Item2,
            });

        var snapshot = (NPointCalibrationParameters)ToolParameterSnapshot.Create(p);
        Assert.Equal(12, snapshot.Samples.Count);
        var result = new NPointCalibrationTool().Run(snapshot);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.InRange(result.RotationCenterImageX, 49.9999, 50.0001);
        Assert.InRange(result.RotationCenterImageY, 29.9999, 30.0001);
        Assert.InRange(result.RotationCenterPhysicalX, 109.9999, 110.0001);
        Assert.InRange(result.RotationCenterPhysicalY, 94.9999, 95.0001);
    }

    [Fact]
    public void StaleConcurrentSampleIsRejected()
    {
        var p = new NPointCalibrationParameters { InputMode = NPointInputMode.Coordinates, RotationCount = 0 };
        p.TeachSignal = true;
        var tool = new NPointCalibrationTool();
        var a = tool.Run(p);
        var b = tool.Run(p);
        Assert.True(p.TryCommit(a, out _));
        Assert.False(p.TryCommit(b, out _));
        Assert.Single(p.Samples);
    }

    [Fact]
    public void DebugControlLoadsWithoutMainWindow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new Views.NPointCalibrationDebugControl();
                Assert.NotNull(control.Parameters);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    [Fact]
    public void PointEditorLoadsThemedGridWithoutMainWindow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
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

    [Fact]
    public void RelativeCoordinatesUseSelectedOrigin()
    {
        var p = new NPointCalibrationParameters
        {
            AcquisitionMode = NPointAcquisitionMode.Manual,
            RotationCount = 0,
            UseRelativeCoordinates = true,
            OriginIndex = 4,
        };
        for (int i = 0; i < 9; i++)
        {
            double x = i % 3 * 10, y = i / 3 * 10;
            p.Samples.Add(new NPointSample
            {
                Id = i, Kind = NPointSampleKind.Translation,
                ImageX = x, ImageY = y,
                PhysicalX = x * 2 + 100, PhysicalY = y * 3 + 200,
            });
        }
        p.InputMode = NPointInputMode.Coordinates;
        p.ImageX = 12;
        p.ImageY = 13;
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        Assert.Equal("Relative", result.CoordinateMode);
        Assert.InRange(result.MaxError, 0, 1e-6);
        Assert.True(result.HasConvertedPoint);
        Assert.InRange(result.ConvertedPhysicalX, 3.9999, 4.0001);
        Assert.InRange(result.ConvertedPhysicalY, 8.9999, 9.0001);
    }

    [Fact]
    public void RepeatedTeachMessageRequiresNewEventId()
    {
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates,
            TeachEnabled = true,
            ExternalInputText = "TEACH",
            TeachEventId = 1,
        };
        var tool = new NPointCalibrationTool();
        var first = tool.Run(p);
        Assert.True(p.TryCommit(first, out _));
        Assert.Single(p.Samples);
        var replay = tool.Run(p);
        Assert.True(p.TryCommit(replay, out _));
        Assert.Single(p.Samples);
        p.TeachEventId = 2;
        p.ImageX = 10;
        var second = tool.Run(p);
        Assert.True(p.TryCommit(second, out _));
        Assert.Equal(2, p.Samples.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task WorkflowNodeCommitsCollectedPointOutsideRunSnapshot()
    {
        var p = new NPointCalibrationParameters
        {
            InputMode = NPointInputMode.Coordinates,
            RotationCount = 0,
            TeachSignal = true,
            ImageX = 12,
            ImageY = 34,
            PhysicalX = 56,
            PhysicalY = 78,
        };
        var node = new AlgorithmNode("calibration-test", "N点标定1", "N点标定1",
            new NPointCalibrationTool()) { Parameters = p };
        var outcome = await node.ExecuteAsync(null);
        Assert.True(outcome.Success, outcome.ErrorMessage);
        Assert.Single(p.Samples);
        Assert.Equal(12, p.Samples[0].ImageX);
        Assert.Equal(56, p.Samples[0].PhysicalX);
    }

    [Fact]
    public void CalibrationFileIsVersionedAndContainsPointTable()
    {
        var p = new NPointCalibrationParameters
        {
            AcquisitionMode = NPointAcquisitionMode.Manual,
            RotationCount = 0,
        };
        for (int i = 0; i < 9; i++)
        {
            double x = i % 3 * 5, y = i / 3 * 5;
            p.Samples.Add(new NPointSample
            {
                Id = i, Kind = NPointSampleKind.Translation,
                ImageX = x, ImageY = y,
                PhysicalX = x + 10, PhysicalY = y + 20,
            });
        }
        var result = new NPointCalibrationTool().Run(p);
        Assert.True(result.CalibrationReady, result.ErrorMessage);
        string path = Path.Combine(Path.GetTempPath(), "NPointCalibration-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            NPointCalibrationParameters.WriteCalibrationFile(path, result, p.Samples);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(1, document.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(9, document.RootElement.GetProperty("Samples").GetArrayLength());
            Assert.Equal("ImageColumnRowToPhysicalXY", document.RootElement.GetProperty("Direction").GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
