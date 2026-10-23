using System;
using System.Linq;
using System.Threading;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Calibration;

namespace SunEyeVision.HalAdapter;

/// <summary>One native route: affine translation fit, encoder-angle offset least squares, final map.</summary>
public static class HalconPlanarCalibration
{
    public static double AngleDifference(double angle, double reference) => Math.IEEERemainder(angle - reference, 360);
    private static double Length(double x, double y) => Math.Sqrt(x * x + y * y);
    public static (double X, double Y) Transform(double[] matrix, double x, double y)
    {
        if (matrix.Length != 6 || matrix.Any(v => !double.IsFinite(v)) || !double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentException("标定矩阵或坐标无效。");
        using var h = new HTuple(matrix);
        HOperatorSet.AffineTransPoint2d(h, x, y, out HTuple ox, out HTuple oy);
        using (ox) using (oy) return (ox.D, oy.D);
    }

    public static PlanarCalibrationResidual Evaluate(double[] matrix, double dx, double dy,
        double reference, int sign, PlanarCalibrationSample sample)
    {
        if (new[] { sample.ImageX, sample.ImageY, sample.AxisX, sample.AxisY, sample.AxisAngle, dx, dy, reference }
            .Any(v => !double.IsFinite(v)) || sign is not (1 or -1))
            throw new InvalidOperationException($"点 {sample.Id + 1} 数值或R方向无效。");
        var q = Transform(matrix, sample.ImageX, sample.ImageY);
        double phi = sign * AngleDifference(sample.AxisAngle, reference) * Math.PI / 180;
        double x = q.X - sample.AxisX - Math.Cos(phi) * dx + Math.Sin(phi) * dy;
        double y = q.Y - sample.AxisY - Math.Sin(phi) * dx - Math.Cos(phi) * dy;
        return new(sample.Id, sample.Kind, x, y, Length(x, y));
    }

    public static PlanarCalibrationFit Fit(PlanarCalibrationSample[] samples, PlanarCalibrationSettings settings,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var t = samples.Where(s => s.Kind == PlanarSampleKind.Translation).ToArray();
        var r = samples.Where(s => s.Kind == PlanarSampleKind.Rotation).ToArray();
        if (settings.TranslationCount < 9 || settings.RotationCount < 3 || settings.TranslationCount > 100 || settings.RotationCount > 100 || samples.Length > 200 ||
            t.Length < settings.TranslationCount || r.Length < settings.RotationCount)
            throw new InvalidOperationException("原生9+3标定需要至少9个有效平移点和3个有效旋转点。");
        if (settings.RotationSign is not (1 or -1) || !double.IsFinite(settings.ReferenceAngle) ||
            new[] { settings.TranslationAngleTolerance, settings.AxisToleranceMm, settings.MinimumRotationSpan,
                settings.RepeatToleranceMm }.Any(v => !double.IsFinite(v) || v <= 0))
            throw new InvalidOperationException("参考R角、方向或采样质量容差无效。");
        if (samples.Select(s => s.Id).Distinct().Count() != samples.Length || samples.Any(s =>
            !Enum.IsDefined(s.Kind) || new[] { s.ImageX, s.ImageY, s.AxisX, s.AxisY, s.AxisAngle }.Any(v => !double.IsFinite(v))))
            throw new InvalidOperationException("点号重复、类别无效或存在非有限坐标。");
        foreach (var s in t)
            if (Math.Abs(AngleDifference(s.AxisAngle, settings.ReferenceAngle)) > settings.TranslationAngleTolerance)
                throw new InvalidOperationException($"平移点 {s.Id + 1} 的R角偏离参考角。");
        foreach (var s in r)
            if (Length(s.AxisX - r[0].AxisX, s.AxisY - r[0].AxisY) > settings.AxisToleranceMm)
                throw new InvalidOperationException($"旋转点 {s.Id + 1} 的轴心XY发生移动。");
        double span = r.Max(a => r.Max(b => Math.Abs(AngleDifference(a.AxisAngle, b.AxisAngle))));
        if (span < settings.MinimumRotationSpan)
            throw new InvalidOperationException("旋转角度覆盖不足。");
        // Explicit geometric gate: SVD returning a pseudoinverse is not evidence of full rank.
        double maxArea = 0, extent = 0;
        foreach (var a in t) foreach (var b in t)
        {
            extent = Math.Max(extent, Length(a.ImageX - b.ImageX, a.ImageY - b.ImageY));
            if (a.Id != b.Id && Length(a.ImageX - b.ImageX, a.ImageY - b.ImageY) < 1e-8)
                throw new InvalidOperationException($"平移点 {a.Id + 1}/{b.Id + 1} 图像位置重复。");
            foreach (var c in t)
                maxArea = Math.Max(maxArea, Math.Abs((b.ImageX-a.ImageX)*(c.ImageY-a.ImageY)-(b.ImageY-a.ImageY)*(c.ImageX-a.ImageX)));
        }
        if (extent <= 1e-8 || maxArea / (extent * extent) < 1e-6)
            throw new InvalidOperationException("平移点重复或近共线，仿射映射不可观测。");
        using var u = new HTuple(t.Select(s => s.ImageX).ToArray());
        using var v = new HTuple(t.Select(s => s.ImageY).ToArray());
        using var sx = new HTuple(t.Select(s => s.AxisX).ToArray());
        using var sy = new HTuple(t.Select(s => s.AxisY).ToArray());
        HOperatorSet.VectorToHomMat2d(u, v, sx, sy, out HTuple h0);
        double[] auxiliary;
        using (h0) auxiliary = h0.ToDArr();
        double det = auxiliary[0] * auxiliary[4] - auxiliary[1] * auxiliary[3];
        double linearNorm = auxiliary[0]*auxiliary[0]+auxiliary[1]*auxiliary[1]+auxiliary[3]*auxiliary[3]+auxiliary[4]*auxiliary[4];
        if (!double.IsFinite(det) || Math.Abs(det) <= 1e-10 * linearNorm || linearNorm == 0)
            throw new InvalidOperationException("物理映射退化或条件性不足。");
        token.ThrowIfCancellationRequested();
        var lhs = new double[4 * r.Length]; var rhs = new double[2 * r.Length];
        double information = 0;
        for (int i = 0; i < r.Length; i++)
        {
            var q = Transform(auxiliary, r[i].ImageX, r[i].ImageY);
            double phi = settings.RotationSign * AngleDifference(r[i].AxisAngle, settings.ReferenceAngle) * Math.PI / 180;
            double c = Math.Cos(phi), sn = Math.Sin(phi);
            lhs[4*i] = c-1; lhs[4*i+1] = -sn; lhs[4*i+2] = sn; lhs[4*i+3] = c-1;
            rhs[2*i] = q.X-r[i].AxisX; rhs[2*i+1] = q.Y-r[i].AxisY;
            information += 2 * (1-c);
        }
        if (information < 1e-8) throw new InvalidOperationException("R角变化不足，偏心不可观测。");
        using var aa = new HTuple(lhs); using var bb = new HTuple(rhs);
        HOperatorSet.CreateMatrix(2*r.Length, 2, aa, out HTuple lhsId);
        double[] offset;
        using (lhsId)
        {
            try
            {
                HOperatorSet.CreateMatrix(2*r.Length, 1, bb, out HTuple b);
                using (b)
                {
                    try
                    {
                        HOperatorSet.SolveMatrix(lhsId, "general", 1e-12, b, out HTuple solution);
                        using (solution)
                        {
                            try { HOperatorSet.GetFullMatrix(solution, out HTuple values); using (values) offset = values.ToDArr(); }
                            finally { HOperatorSet.ClearMatrix(solution); }
                        }
                    }
                    finally { HOperatorSet.ClearMatrix(b); }
                }
            }
            finally { HOperatorSet.ClearMatrix(lhsId); }
        }
        if (offset.Any(x => !double.IsFinite(x))) throw new InvalidOperationException("原生偏心求解返回非有限值。");
        var reference = t.FirstOrDefault(s => s.Id == settings.ReferenceSampleId)
            ?? throw new InvalidOperationException("参考平移点不存在，请指定复拍参考点。");
        var repeats = r.Where(s => Math.Abs(AngleDifference(s.AxisAngle, settings.ReferenceAngle)) <= settings.TranslationAngleTolerance
            && Length(s.AxisX-reference.AxisX, s.AxisY-reference.AxisY) <= settings.AxisToleranceMm).ToArray();
        if (repeats.Length == 0) throw new InvalidOperationException("参考姿态复拍未采集；请在参考平移点XY、参考R角复拍。");
        var refQ = Transform(auxiliary, reference.ImageX, reference.ImageY);
        double repeatError = repeats.Max(s => { var q = Transform(auxiliary, s.ImageX, s.ImageY); return Length(q.X-refQ.X, q.Y-refQ.Y); });
        if (repeatError > settings.RepeatToleranceMm)
            throw new InvalidOperationException($"参考姿态复拍误差 {repeatError:F6} mm 超限。");
        var matrix = (double[])auxiliary.Clone(); matrix[2] += offset[0]; matrix[5] += offset[1];
        token.ThrowIfCancellationRequested();
        var residuals = samples.Select(s => Evaluate(matrix, offset[0], offset[1], settings.ReferenceAngle, settings.RotationSign, s)).ToArray();
        return new(matrix, auxiliary, offset[0], offset[1], span, repeatError, residuals);
    }
}
