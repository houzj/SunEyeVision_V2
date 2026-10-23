namespace SunEyeVision.Plugin.SDK.Models.Calibration;

/// <summary>Data-only boundary for fixed-camera axis-encoder calibration. X is image column.</summary>
public enum PlanarSampleKind { Translation, Rotation }
public sealed record PlanarCalibrationSample(int Id, PlanarSampleKind Kind,
    double ImageX, double ImageY, double AxisX, double AxisY, double AxisAngle);
public sealed record PlanarCalibrationSettings(int TranslationCount, int RotationCount,
    double ReferenceAngle, int RotationSign, double TranslationAngleTolerance,
    double AxisToleranceMm, double MinimumRotationSpan, int ReferenceSampleId,
    double RepeatToleranceMm);
public sealed record PlanarCalibrationResidual(int Id, PlanarSampleKind Kind,
    double ErrorX, double ErrorY, double ErrorMm);
public sealed record PlanarCalibrationFit(double[] Matrix, double[] AuxiliaryMatrix,
    double OffsetX, double OffsetY, double RotationSpan, double RepeatErrorMm,
    PlanarCalibrationResidual[] Residuals)
{
    public const string Algorithm = "HalconAffineEncoderOffsetV1";
    public const int FileVersion = 3;
}

/// <summary>Evidence supplied by upstream workflow integration, never manufactured by the editor.</summary>
public sealed record CalibrationAcquisitionEvidence(string SessionId, long StepId, bool InPosition,
    long FrameId, System.DateTimeOffset FrameAt, System.DateTimeOffset AxisReadAt,
    string CameraId, int ImageWidth, int ImageHeight, string ImageDomain, string WorkPlaneId,
    double AxisX, double AxisY, double AxisAngle);
