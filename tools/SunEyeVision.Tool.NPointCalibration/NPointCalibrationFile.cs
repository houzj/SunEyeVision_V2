using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace SunEyeVision.Tool.NPointCalibration;

/// <summary>Candidate files are never production calibrations. Only the verification workflow promotes them.</summary>
public sealed class NPointCalibrationFile
{
    public int SchemaVersion { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string PhysicalUnit { get; set; } = string.Empty;
    public string AxisCoordinates { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double[] Matrix { get; set; } = Array.Empty<double>();
    public double ReferenceRAngle { get; set; }
    public int RotationSign { get; set; }
    public double AxisToMarkX { get; set; }
    public double AxisToMarkY { get; set; }
    public string SourceDeviceId { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ImageDomain { get; set; } = string.Empty;
    public NPointSample[] Samples { get; set; } = Array.Empty<NPointSample>();
    public string CandidateSha256 { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public double ValidationMaxErrorMm { get; set; }
    public double ValidationThresholdMm { get; set; }
    public NPointSampleResidual[] ValidationResiduals { get; set; } = Array.Empty<NPointSampleResidual>();
    public NPointSample[] ValidationSamples { get; set; } = Array.Empty<NPointSample>();

    public static (NPointCalibrationFile File, string Sha256) ReadCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("未指定候选标定文件。");
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var data = new MemoryStream();
        stream.CopyTo(data);
        byte[] bytes = data.ToArray();
        var file = JsonSerializer.Deserialize<NPointCalibrationFile>(bytes)
            ?? throw new InvalidDataException("候选标定文件为空。");
        if (file.SchemaVersion != 2 || file.State != "SolvedUnverified"
            || file.Direction != "ImageColumnRowToMachineMarkXY" || file.PhysicalUnit != "mm"
            || file.AxisCoordinates != "R-axis center encoder XY"
            || file.Matrix is not { Length: 6 } || file.Matrix.Any(v => !double.IsFinite(v))
            || !double.IsFinite(file.AxisToMarkX) || !double.IsFinite(file.AxisToMarkY)
            || !double.IsFinite(file.ReferenceRAngle) || file.RotationSign is not (1 or -1)
            || file.Samples is null || file.Samples.Count(s => s.Kind == NPointSampleKind.Translation) < 4
            || file.Samples.Count(s => s.Kind == NPointSampleKind.Rotation) < 3)
            throw new InvalidDataException("候选标定文件模型、状态或数值无效。");
        return (file, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static NPointCalibrationFile ReadVerified(string path, string deviceId,
        int imageWidth, int imageHeight, string imageDomain)
    {
        var file = JsonSerializer.Deserialize<NPointCalibrationFile>(File.ReadAllBytes(Path.GetFullPath(path)))
            ?? throw new InvalidDataException("标定文件为空。");
        if (file.SchemaVersion != 2 || file.State != "Verified"
            || file.Direction != "ImageColumnRowToMachineMarkXY" || file.PhysicalUnit != "mm"
            || file.AxisCoordinates != "R-axis center encoder XY"
            || file.Matrix is not { Length: 6 } || file.Matrix.Any(v => !double.IsFinite(v))
            || file.RotationSign is not (1 or -1) || !double.IsFinite(file.AxisToMarkX)
            || !double.IsFinite(file.AxisToMarkY) || !double.IsFinite(file.ReferenceRAngle)
            || string.IsNullOrWhiteSpace(file.CandidateSha256) || file.PublishedAt == null
            || file.ValidationSamples is null || file.ValidationSamples.Length < 2
            || file.ValidationResiduals is null || file.ValidationResiduals.Length != file.ValidationSamples.Length
            || !double.IsFinite(file.ValidationThresholdMm) || file.ValidationThresholdMm <= 0
            || !double.IsFinite(file.ValidationMaxErrorMm)
            || file.ValidationMaxErrorMm > file.ValidationThresholdMm
            || file.SourceDeviceId != deviceId || file.ImageWidth != imageWidth
            || file.ImageHeight != imageHeight || file.ImageDomain != imageDomain)
            throw new InvalidDataException("标定文件未验证或与当前工位图像环境不匹配。");
        return file;
    }

    public NPointSampleResidual Evaluate(NPointSample sample)
    {
        if (!double.IsFinite(sample.ImageX) || !double.IsFinite(sample.ImageY)
            || !double.IsFinite(sample.AxisX) || !double.IsFinite(sample.AxisY)
            || !double.IsFinite(sample.AxisAngle))
            throw new InvalidOperationException("独立验证点包含非有限数值。");
        double radians = RotationSign * (sample.AxisAngle - ReferenceRAngle) * Math.PI / 180;
        double cos = Math.Cos(radians), sin = Math.Sin(radians);
        double rx = Matrix[0] * sample.ImageX + Matrix[1] * sample.ImageY + Matrix[2]
            - sample.AxisX - cos * AxisToMarkX + sin * AxisToMarkY;
        double ry = Matrix[3] * sample.ImageX + Matrix[4] * sample.ImageY + Matrix[5]
            - sample.AxisY - sin * AxisToMarkX - cos * AxisToMarkY;
        return new NPointSampleResidual(sample.Id, sample.Kind, rx, ry, Math.Sqrt(rx * rx + ry * ry), 1);
    }

    public IReadOnlyList<NPointSampleResidual> Validate(IReadOnlyList<NPointSample> heldOut,
        double maximumErrorMm)
    {
        if (string.IsNullOrWhiteSpace(SourceDeviceId) || ImageWidth <= 0 || ImageHeight <= 0
            || string.IsNullOrWhiteSpace(ImageDomain))
            throw new InvalidOperationException("候选文件缺少相机或图像域身份，不能发布。");
        if (!double.IsFinite(maximumErrorMm) || maximumErrorMm <= 0)
            throw new InvalidOperationException("必须设置正的独立验证最大误差。");
        if (!heldOut.Any(s => s.Kind == NPointSampleKind.Translation)
            || !heldOut.Any(s => s.Kind == NPointSampleKind.Rotation))
            throw new InvalidOperationException("至少需要一个独立平移点和一个独立旋转点。");
        foreach (var sample in heldOut)
        {
            if (!sample.Enabled || sample.Source == "Placeholder"
                || sample.Source != "VerificationWorkflow" || sample.FrameId is null or <= 0
                || sample.FrameCapturedAt == null || sample.AxisReadAt == null
                || Samples.Any(s => s.FrameId.HasValue && sample.FrameId.HasValue
                        && s.FrameId == sample.FrameId
                    || s.ImageX == sample.ImageX && s.ImageY == sample.ImageY
                        && s.AxisX == sample.AxisX && s.AxisY == sample.AxisY
                        && s.AxisAngle == sample.AxisAngle))
                throw new InvalidOperationException($"验证点 {sample.Id} 无效或与训练点重复。");
            if (sample.Kind == NPointSampleKind.Translation && Samples.Any(s =>
                    s.Kind == NPointSampleKind.Translation
                    && Math.Abs(s.AxisX - sample.AxisX) < 1e-6
                    && Math.Abs(s.AxisY - sample.AxisY) < 1e-6))
                throw new InvalidOperationException($"验证点 {sample.Id} 复用了训练平移位置。");
            if (sample.Kind == NPointSampleKind.Rotation && Samples.Any(s =>
                    s.Kind == NPointSampleKind.Rotation
                    && Math.Abs(Math.IEEERemainder(s.AxisAngle - sample.AxisAngle, 360)) < 1e-3))
                throw new InvalidOperationException($"验证点 {sample.Id} 复用了训练旋转角度。");
        }
        if (heldOut.Select(s => s.FrameId).Distinct().Count() != heldOut.Count)
            throw new InvalidOperationException("独立验证点复用了图像帧。");
        var residuals = heldOut.Select(Evaluate).ToArray();
        if (residuals.Any(r => !double.IsFinite(r.ErrorMm) || r.ErrorMm > maximumErrorMm))
            throw new InvalidOperationException("独立验证误差超限，拒绝发布。");
        return residuals;
    }

    public static void Publish(string candidatePath, string activePath,
        string expectedSha256, IReadOnlyList<NPointSample> heldOut, double maximumErrorMm)
    {
        if (string.IsNullOrWhiteSpace(activePath) || string.IsNullOrWhiteSpace(expectedSha256))
            throw new InvalidOperationException("缺少活动文件路径或已验证的候选摘要。");
        string fullCandidate = Path.GetFullPath(candidatePath);
        string fullActive = Path.GetFullPath(activePath);
        if (string.Equals(fullCandidate, fullActive, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("候选文件与活动文件不得是同一路径。");
        // Keep a read handle without delete-sharing until the exact validated bytes are published.
        using var source = new FileStream(fullCandidate, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        byte[] original = copy.ToArray();
        string actual = Convert.ToHexString(SHA256.HashData(original));
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("候选文件在验证期间发生变化，请重新验证。");
        var file = JsonSerializer.Deserialize<NPointCalibrationFile>(original)
            ?? throw new InvalidDataException("候选文件为空。");
        if (file.State != "SolvedUnverified" || file.SchemaVersion != 2)
            throw new InvalidDataException("只有待验证候选文件可以发布。");
        var residuals = file.Validate(heldOut, maximumErrorMm);
        file.State = "Verified";
        file.CandidateSha256 = actual;
        file.PublishedAt = DateTimeOffset.UtcNow;
        file.ValidationMaxErrorMm = residuals.Max(r => r.ErrorMm);
        file.ValidationThresholdMm = maximumErrorMm;
        file.ValidationResiduals = residuals.ToArray();
        file.ValidationSamples = heldOut.Select(s => s.Copy()).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(fullActive)!);
        string temp = fullActive + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(file,
                new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(fullActive)) File.Replace(temp, fullActive, fullActive + ".bak");
            else File.Move(temp, fullActive);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
