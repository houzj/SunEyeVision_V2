using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.NPointCalibration;

[Tool("NPointCalibration", "N点标定",
    Description = "采集图像/机构对应点并求解平面标定",
    Icon = "◎", Category = "标定", Version = "1.0.0", HasDebugWindow = true)]
public sealed class NPointCalibrationTool : IToolPlugin<NPointCalibrationParameters, NPointCalibrationResults>,
    IAsyncToolPlugin, IToolEditorFactory
{
    public NPointCalibrationResults Run(NPointCalibrationParameters parameters)
        => Execute(parameters, null);

    public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        => ValueTask.FromResult<ToolResults>(Execute((NPointCalibrationParameters)request.Parameters, request));

    private static NPointCalibrationResults Execute(NPointCalibrationParameters parameters, ToolExecutionRequest? request)
    {
        var result = new NPointCalibrationResults
        {
            ToolName = "N点标定", ToolId = "NPointCalibration",
            RequiredTranslationSamples = parameters.TranslationCount,
            RequiredRotationSamples = parameters.RotationCount,
            ExpectedRevision = parameters.SampleRevision,
        };
        try
        {
            if (parameters.TranslationCount < 4 || parameters.RotationCount < 0)
                throw new InvalidOperationException("平移至少 4 点；旋转次数不能为负数。");
            if (parameters.CameraMode != NPointCameraMode.FixedAbove)
                throw new NotSupportedException("当前仅支持固定相机上相机位；其他相机模式不能复用同一变换方向。");
            if (parameters.WeightFunction != NPointWeightFunction.LeastSquares)
                throw new NotSupportedException("Huber/Tukey 权重尚未实现，不能按最小二乘静默替代。");

            var samples = parameters.Samples.Where(s => s.Enabled).Select(s => s.Copy()).ToList();
            bool active = parameters.AcquisitionMode == NPointAcquisitionMode.Trigger
                && (parameters.TeachSignal || (parameters.TeachEnabled
                    && !string.IsNullOrWhiteSpace(parameters.TeachTriggerText)
                    && string.Equals(parameters.ExternalInputText, parameters.TeachTriggerText,
                        StringComparison.Ordinal)));
            result.TeachSignalActive = active;
            result.TeachEventId = parameters.TeachEventId;
            bool newTeachEvent = active && (parameters.TeachEventId > 0
                ? parameters.TeachEventId > parameters.LastTeachEventId
                : !parameters.LastTeachSignal);
            result.NeedsCommit = parameters.AcquisitionMode == NPointAcquisitionMode.Trigger
                && (active != parameters.LastTeachSignal || newTeachEvent);

            if (newTeachEvent)
            {
                if (parameters.Samples.Count >= parameters.TranslationCount + parameters.RotationCount)
                    throw new InvalidOperationException("标定点已采满；请检查点表或开始新一轮标定。");
                var sample = Capture(parameters, request?.CameraFrame?.FrameId);
                result.CollectedSample = sample;
                samples.Add(sample);
            }

            result.TranslationSamples = samples.Count(s => s.Kind == NPointSampleKind.Translation);
            result.RotationSamples = samples.Count(s => s.Kind == NPointSampleKind.Rotation);

            if (result.TranslationSamples >= parameters.TranslationCount
                && result.RotationSamples >= parameters.RotationCount)
            {
                var fitted = NPointCalibrationSolver.Fit(parameters, samples);
                result.HomMat2D = fitted.Matrix;
                result.CoordinateMode = parameters.UseRelativeCoordinates ? "Relative" : "Absolute";
                result.MeanError = fitted.MeanError;
                result.MaxError = fitted.MaxError;
                result.RotationCenterImageX = fitted.CenterImageX;
                result.RotationCenterImageY = fitted.CenterImageY;
                result.RotationCenterPhysicalX = fitted.CenterPhysicalX;
                result.RotationCenterPhysicalY = fitted.CenterPhysicalY;
                result.CalibrationReady = true;
                (double X, double Y)? livePoint = result.CollectedSample != null
                    ? (result.CollectedSample.ImageX, result.CollectedSample.ImageY)
                    : parameters.InputMode == NPointInputMode.Point && parameters.ImagePoint.Length == 1
                        ? (parameters.ImagePoint[0].X, parameters.ImagePoint[0].Y)
                        : parameters.InputMode == NPointInputMode.Coordinates
                            ? (parameters.ImageX, parameters.ImageY)
                            : ((double X, double Y)?)null;
                if (livePoint.HasValue)
                {
                    result.CurrentImageX = livePoint.Value.X;
                    result.CurrentImageY = livePoint.Value.Y;
                    (result.ConvertedPhysicalX, result.ConvertedPhysicalY) =
                        NPointCalibrationSolver.TransformPoint(result.HomMat2D,
                            parameters.TransformModel, result.CurrentImageX, result.CurrentImageY);
                    result.HasConvertedPoint = true;
                }
            }
            result.SetSuccess();
        }
        catch (Exception ex)
        {
            result.SetError($"N点标定失败：{ex.Message}", ex);
        }
        return result;
    }

    private static NPointSample Capture(NPointCalibrationParameters p, long? frameId)
    {
        int index = Enumerable.Range(0, p.TranslationCount + p.RotationCount)
            .First(i => p.Samples.All(sample => sample.Id != i));
        double ix, iy, px, py;
        if (p.InputMode == NPointInputMode.Point)
        {
            if (p.ImagePoint.Length != 1)
                throw new InvalidOperationException("图像点绑定必须恰好输出一个点。");
            ix = p.ImagePoint[0].X;
            iy = p.ImagePoint[0].Y;
            if (p.UseGeneratedPhysical)
                (px, py) = GeneratedPhysical(p, index);
            else
            {
                if (p.PhysicalPoint.Length != 1)
                    throw new InvalidOperationException("物理点绑定必须恰好输出一个点。");
                px = p.PhysicalPoint[0].X;
                py = p.PhysicalPoint[0].Y;
            }
        }
        else
        {
            ix = p.ImageX; iy = p.ImageY;
            (px, py) = p.UseGeneratedPhysical ? GeneratedPhysical(p, index) : (p.PhysicalX, p.PhysicalY);
        }
        if (!double.IsFinite(ix) || !double.IsFinite(iy) || !double.IsFinite(px) || !double.IsFinite(py))
            throw new InvalidOperationException("标定点存在非有限数值。");
        return new NPointSample
        {
            Id = index, Kind = index < p.TranslationCount ? NPointSampleKind.Translation : NPointSampleKind.Rotation,
            ImageX = ix, ImageY = iy, PhysicalX = px, PhysicalY = py,
            ImageAngle = p.ImageAngle,
            PhysicalAngle = index < p.TranslationCount ? p.PhysicalAngle
                : (p.UseGeneratedPhysical
                    ? p.BaseAngle + (index - p.TranslationCount - (p.RotationCount - 1) / 2.0) * p.AngleStep
                    : p.PhysicalAngle),
            FrameId = frameId,
        };
    }

    private static (double X, double Y) GeneratedPhysical(NPointCalibrationParameters p, int index)
    {
        if (index >= p.TranslationCount)
            return (p.BaseX, p.BaseY);
        if (p.TranslationCount != 9)
            throw new InvalidOperationException("自动生成物理点目前只支持九宫格 9 点；其他点数请绑定真实物理点。");
        int row = p.XFirst ? index / 3 : index % 3;
        int column = p.XFirst ? index % 3 : index / 3;
        if (p.ReverseAfter == 3 && row % 2 == 1)
            column = 2 - column;
        return (p.BaseX + (column - 1) * p.OffsetX,
            p.BaseY + (row - 1) * p.OffsetY);
    }

    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        => new Views.NPointCalibrationDebugControl();
}

internal static class NPointCalibrationSolver
{
    internal sealed record FitResult(double[] Matrix, double MeanError, double MaxError,
        double CenterImageX, double CenterImageY, double CenterPhysicalX, double CenterPhysicalY);

    public static FitResult Fit(NPointCalibrationParameters p, IReadOnlyList<NPointSample> samples)
    {
        var translation = samples.Where(s => s.Kind == NPointSampleKind.Translation).ToArray();
        if (translation.Length < p.TranslationCount)
            throw new InvalidOperationException("平移标定点不足。");
        if (translation.Select(s => (s.ImageX, s.ImageY)).Distinct().Count() < 4)
            throw new InvalidOperationException("图像点重复，无法求解稳定变换。");

        if (p.UseRelativeCoordinates)
        {
            var origin = translation.FirstOrDefault(s => s.Id == p.OriginIndex)
                ?? throw new InvalidOperationException("相对坐标原点对应的标定点不存在。");
            translation = translation.Select(s =>
            {
                var copy = s.Copy();
                copy.PhysicalX -= origin.PhysicalX;
                copy.PhysicalY -= origin.PhysicalY;
                return copy;
            }).ToArray();
        }

        // This matrix maps (image column X, image row Y) to physical (X,Y).
        // It is NOT the image-to-image AffineMatrix used for ROI correction.
        using var ix = new HTuple(translation.Select(s => s.ImageX).ToArray());
        using var iy = new HTuple(translation.Select(s => s.ImageY).ToArray());
        using var px = new HTuple(translation.Select(s => s.PhysicalX).ToArray());
        using var py = new HTuple(translation.Select(s => s.PhysicalY).ToArray());
        HTuple matrix;
        switch (p.TransformModel)
        {
            case NPointTransformModel.Similarity:
                HOperatorSet.VectorToSimilarity(ix, iy, px, py, out matrix);
                break;
            case NPointTransformModel.Affine:
                HOperatorSet.VectorToHomMat2d(ix, iy, px, py, out matrix);
                break;
            case NPointTransformModel.Projective:
                HOperatorSet.VectorToProjHomMat2d(ix, iy, px, py, "normalized_dlt",
                    new HTuple(), new HTuple(), new HTuple(), new HTuple(), new HTuple(), new HTuple(),
                    out matrix, out var covariance);
                covariance.Dispose();
                break;
            default:
                throw new InvalidOperationException("未知变换模型。");
        }
        using (matrix)
        {
            double sum = 0, max = 0;
            foreach (var sample in translation)
            {
                var (x, y) = Transform(matrix, p.TransformModel, sample.ImageX, sample.ImageY);
                double error = Math.Sqrt(Math.Pow(x - sample.PhysicalX, 2) + Math.Pow(y - sample.PhysicalY, 2));
                sum += error;
                max = Math.Max(max, error);
            }
            double centerX = 0, centerY = 0, centerPhysicalX = 0, centerPhysicalY = 0;
            if (p.RotationCount > 0)
            {
                var rotations = samples.Where(s => s.Kind == NPointSampleKind.Rotation).ToArray();
                if (rotations.Length < 3)
                    throw new InvalidOperationException("旋转中心至少需要三个有效且不共线的位置点。");
                (centerX, centerY) = CircleCenter(rotations[0], rotations[1], rotations[2]);
                (centerPhysicalX, centerPhysicalY) = Transform(matrix, p.TransformModel, centerX, centerY);
            }
            return new FitResult(matrix.DArr, sum / translation.Length, max,
                centerX, centerY, centerPhysicalX, centerPhysicalY);
        }
    }

    public static (double X, double Y) TransformPoint(double[] matrixValues,
        NPointTransformModel model, double x, double y)
    {
        using var matrix = new HTuple(matrixValues);
        return Transform(matrix, model, x, y);
    }

    private static (double X, double Y) Transform(HTuple matrix, NPointTransformModel model, double x, double y)
    {
        if (model == NPointTransformModel.Projective)
        {
            HOperatorSet.ProjectiveTransPoint2d(matrix, x, y, 1, out var qx, out var qy, out var qw);
            using (qx) using (qy) using (qw)
                return (qx.D / qw.D, qy.D / qw.D);
        }
        HOperatorSet.AffineTransPoint2d(matrix, x, y, out var tx, out var ty);
        using (tx) using (ty)
            return (tx.D, ty.D);
    }

    private static (double X, double Y) CircleCenter(NPointSample a, NPointSample b, NPointSample c)
    {
        double d = 2 * (a.ImageX * (b.ImageY - c.ImageY)
            + b.ImageX * (c.ImageY - a.ImageY)
            + c.ImageX * (a.ImageY - b.ImageY));
        if (Math.Abs(d) < 1e-8)
            throw new InvalidOperationException("旋转采样点共线或重复，无法求旋转中心。");
        double aa = a.ImageX * a.ImageX + a.ImageY * a.ImageY;
        double bb = b.ImageX * b.ImageX + b.ImageY * b.ImageY;
        double cc = c.ImageX * c.ImageX + c.ImageY * c.ImageY;
        return ((aa * (b.ImageY - c.ImageY) + bb * (c.ImageY - a.ImageY) + cc * (a.ImageY - b.ImageY)) / d,
            (aa * (c.ImageX - b.ImageX) + bb * (a.ImageX - c.ImageX) + cc * (b.ImageX - a.ImageX)) / d);
    }
}
