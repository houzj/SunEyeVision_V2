using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Tool.NPointCalibration;

/// <summary>
/// Fixed-camera, planar 9+3 fit. Image Mark position = machine R-axis center
/// position + rotated axis-to-Mark offset. Encoder angle is a known observation.
/// </summary>
internal static class NPointCalibrationSolver
{
    private const int Unknowns = 8;
    internal sealed record FitResult(double[] Matrix, double AxisToMarkX, double AxisToMarkY,
        double MeanError, double MaxError, double RmsError, double TranslationRmsError,
        double RotationRmsError, double RobustScaleMm, double RobustThresholdMm,
        List<NPointSampleResidual> Residuals);

    internal static FitResult Fit(NPointCalibrationParameters p, IReadOnlyList<NPointSample> samples)
    {
        Validate(p, samples);
        var rows = samples.Select(s => BuildRows(p, s)).ToArray();
        var weights = Enumerable.Repeat(1.0, samples.Count).ToArray();
        var solution = Solve(rows, weights);
        double scale = 0, threshold = 0;
        if (p.WeightFunction != NPointWeightFunction.LeastSquares)
        {
            solution = RobustInitialEstimate(p, samples, rows, solution);
            var firstErrors = Residuals(rows, solution);
            // The median of a 2-D Gaussian residual magnitude is 1.177 sigma.
            scale = Math.Max(Median(firstErrors.Select(r => r.Length).ToArray()) / 1.1774100225,
                p.RobustScaleFloorMm);
            threshold = p.WeightCoefficient * scale;
            if (!double.IsFinite(threshold) || threshold <= 0)
                throw new InvalidOperationException("鲁棒阈值必须是有限正数。");

            bool converged = false;
            for (int iteration = 0; iteration < 100; iteration++)
            {
                var residuals = Residuals(rows, solution);
                for (int i = 0; i < weights.Length; i++)
                    weights[i] = Weight(p.WeightFunction, residuals[i].Length, threshold);
                EnsureEffectiveGeometry(p, samples, weights);
                var next = Solve(rows, weights);
                double step = 0, norm = 0;
                for (int j = 0; j < Unknowns; j++)
                {
                    step += Math.Pow(next[j] - solution[j], 2);
                    norm += next[j] * next[j];
                }
                solution = next;
                if (Math.Sqrt(step) <= 1e-10 * (1 + Math.Sqrt(norm)))
                {
                    converged = true;
                    break;
                }
            }
            if (!converged)
                throw new InvalidOperationException("鲁棒联合拟合未收敛；请检查点表或调整权重系数。");
        }

        var finalResiduals = Residuals(rows, solution);
        if (p.WeightFunction != NPointWeightFunction.LeastSquares)
        {
            for (int i = 0; i < weights.Length; i++)
                weights[i] = Weight(p.WeightFunction, finalResiduals[i].Length, threshold);
            EnsureEffectiveGeometry(p, samples, weights);
        }
        var details = samples.Select((s, i) => new NPointSampleResidual(s.Id, s.Kind,
            finalResiduals[i].X, finalResiduals[i].Y, finalResiduals[i].Length, weights[i])).ToList();
        double rms = Rms(details);
        double translationRms = Rms(details.Where(r => r.Kind == NPointSampleKind.Translation));
        double rotationRms = Rms(details.Where(r => r.Kind == NPointSampleKind.Rotation));
        return new FitResult(solution.Take(6).ToArray(), solution[6], solution[7],
            details.Average(r => r.ErrorMm), details.Max(r => r.ErrorMm), rms,
            translationRms, rotationRms, scale, threshold, details);
    }

    internal static (double X, double Y) TransformPoint(double[] matrix, double x, double y)
    {
        if (matrix.Length != 6)
            throw new ArgumentException("像素到 Mark 机台矩阵必须有 6 个系数。", nameof(matrix));
        return (matrix[0] * x + matrix[1] * y + matrix[2],
            matrix[3] * x + matrix[4] * y + matrix[5]);
    }

    internal static List<NPointSampleResidual> EvaluateHeldOut(NPointCalibrationParameters p,
        IReadOnlyList<NPointSample> samples, FitResult fitted)
    {
        var values = fitted.Matrix.Concat([fitted.AxisToMarkX, fitted.AxisToMarkY]).ToArray();
        var observations = samples.Select(s =>
        {
            if (!double.IsFinite(s.ImageX) || !double.IsFinite(s.ImageY)
                || !double.IsFinite(s.AxisX) || !double.IsFinite(s.AxisY)
                || !double.IsFinite(s.AxisAngle))
                throw new InvalidOperationException($"验证点 {s.Id} 含非有限数值。");
            return BuildRows(p, s);
        }).ToArray();
        var residuals = Residuals(observations, values);
        return samples.Select((s, i) => new NPointSampleResidual(s.Id, s.Kind,
            residuals[i].X, residuals[i].Y, residuals[i].Length, 1)).ToList();
    }

    private static void Validate(NPointCalibrationParameters p, IReadOnlyList<NPointSample> samples)
    {
        var translation = samples.Where(s => s.Kind == NPointSampleKind.Translation).ToArray();
        var rotation = samples.Where(s => s.Kind == NPointSampleKind.Rotation).ToArray();
        if (translation.Length < p.TranslationCount || rotation.Length < p.RotationCount)
            throw new InvalidOperationException("有效平移/旋转点数不足。");
        if (!double.IsFinite(p.ReferenceRAngle) || p.RotationSign is not (1 or -1))
            throw new InvalidOperationException("R 轴参考角或旋转正方向无效。");
        if (!double.IsFinite(p.TranslationAngleToleranceDeg) || p.TranslationAngleToleranceDeg < 0
            || !double.IsFinite(p.RotationAxisToleranceMm) || p.RotationAxisToleranceMm < 0
            || !double.IsFinite(p.MinimumRotationSpanDeg) || p.MinimumRotationSpanDeg <= 0)
            throw new InvalidOperationException("平移/旋转采样几何容差无效。");
        if (p.WeightFunction is not (NPointWeightFunction.LeastSquares or NPointWeightFunction.Huber or NPointWeightFunction.Tukey))
            throw new InvalidOperationException("未知权重函数。");
        if (p.WeightFunction != NPointWeightFunction.LeastSquares &&
            (!double.IsFinite(p.WeightCoefficient) || p.WeightCoefficient <= 0
             || !double.IsFinite(p.RobustScaleFloorMm) || p.RobustScaleFloorMm <= 0))
            throw new InvalidOperationException("权重系数和稳健尺度下限必须是有限正数。");
        foreach (var s in samples)
        {
            if (!double.IsFinite(s.ImageX) || !double.IsFinite(s.ImageY)
                || !double.IsFinite(s.AxisX) || !double.IsFinite(s.AxisY)
                || !double.IsFinite(s.AxisAngle))
                throw new InvalidOperationException($"点 {s.Id} 含非有限数值。");
        }
        foreach (var s in translation)
            if (Math.Abs(NormalizeAngle(s.AxisAngle - p.ReferenceRAngle)) > p.TranslationAngleToleranceDeg)
                throw new InvalidOperationException($"平移点 {s.Id} 的 R 角偏离参考角。");
        var axis = rotation[0];
        foreach (var s in rotation)
            if (Math.Sqrt(Math.Pow(s.AxisX - axis.AxisX, 2) + Math.Pow(s.AxisY - axis.AxisY, 2)) > p.RotationAxisToleranceMm)
                throw new InvalidOperationException($"旋转点 {s.Id} 的轴心 XY 发生移动。");
        double span = 0;
        for (int i = 0; i < rotation.Length; i++)
            for (int j = i + 1; j < rotation.Length; j++)
                span = Math.Max(span, Math.Abs(NormalizeAngle(rotation[i].AxisAngle - rotation[j].AxisAngle)));
        if (span < p.MinimumRotationSpanDeg)
            throw new InvalidOperationException("旋转采样角度跨度不足。");
    }

    private static Observation BuildRows(NPointCalibrationParameters p, NPointSample s)
    {
        double radians = p.RotationSign * NormalizeAngle(s.AxisAngle - p.ReferenceRAngle) * Math.PI / 180;
        double c = Math.Cos(radians), v = Math.Sin(radians);
        return new Observation(
            [s.ImageX, s.ImageY, 1, 0, 0, 0, -c, v], s.AxisX,
            [0, 0, 0, s.ImageX, s.ImageY, 1, -v, -c], s.AxisY);
    }

    private static double NormalizeAngle(double degrees)
    {
        double value = (degrees + 180) % 360;
        if (value < 0) value += 360;
        return value - 180;
    }

    private static double[] RobustInitialEstimate(NPointCalibrationParameters p,
        IReadOnlyList<NPointSample> samples, Observation[] rows, double[] initial)
    {
        double[] best = initial;
        double bestScore = CandidateScore(rows, initial);
        // One bad translation sample must not spoil the initial IRLS scale.
        for (int omitted = 0; omitted < samples.Count; omitted++)
        {
            int sameKind = samples.Count(s => s.Kind == samples[omitted].Kind);
            int required = samples[omitted].Kind == NPointSampleKind.Translation ? 4 : 3;
            if (sameKind - 1 < required) continue;
            var weights = Enumerable.Repeat(1.0, samples.Count).ToArray();
            weights[omitted] = 0;
            try
            {
                var candidate = Solve(rows, weights);
                double score = CandidateScore(rows, candidate);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            catch (InvalidOperationException) { /* Candidate was rank-deficient. */ }
        }
        return best;
    }

    private static double CandidateScore(Observation[] rows, double[] values)
    {
        var errors = Residuals(rows, values).Select(r => r.Length).ToArray();
        Array.Sort(errors);
        // Trim the largest sample to favor an inlier model, then use a small
        // mean term to break otherwise identical median scores.
        return Median(errors) + errors.Take(errors.Length - 1).Average() * 1e-3;
    }

    private static (double X, double Y, double Length)[] Residuals(Observation[] rows, double[] x)
    {
        var result = new (double X, double Y, double Length)[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            double rx = Dot(rows[i].X, x) - rows[i].AxisX;
            double ry = Dot(rows[i].Y, x) - rows[i].AxisY;
            result[i] = (rx, ry, Math.Sqrt(rx * rx + ry * ry));
        }
        return result;
    }

    private static double Weight(NPointWeightFunction function, double residual, double cutoff)
    {
        if (function == NPointWeightFunction.LeastSquares || residual <= 0) return 1;
        if (function == NPointWeightFunction.Huber) return Math.Min(1, cutoff / residual);
        if (residual >= cutoff) return 0;
        double ratio = residual / cutoff;
        return Math.Pow(1 - ratio * ratio, 2);
    }

    private static void EnsureEffectiveGeometry(NPointCalibrationParameters p,
        IReadOnlyList<NPointSample> samples, double[] weights)
    {
        int translations = 0, rotations = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            if (weights[i] <= 1e-6) continue;
            if (samples[i].Kind == NPointSampleKind.Translation) translations++;
            else rotations++;
        }
        if (translations < 4 || rotations < 3)
            throw new InvalidOperationException("鲁棒降权后有效平移/旋转点不足；请重采异常点或增加冗余点。");
    }

    private static double Rms(IEnumerable<NPointSampleResidual> rows)
    {
        var data = rows.ToArray();
        return Math.Sqrt(data.Sum(r => r.ErrorMm * r.ErrorMm) / data.Length);
    }

    private static double Median(double[] values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    private static double Dot(double[] a, double[] b)
    {
        double sum = 0;
        for (int j = 0; j < a.Length; j++) sum += a[j] * b[j];
        return sum;
    }

    /// <summary>Column-scaled, pivoted Householder QR; no normal-equation inverse.</summary>
    private static double[] Solve(Observation[] rows, double[] weights)
    {
        int m = rows.Length * 2;
        var a = new double[m, Unknowns];
        var b = new double[m];
        for (int i = 0; i < rows.Length; i++)
        {
            double w = Math.Sqrt(weights[i]);
            for (int j = 0; j < Unknowns; j++)
            {
                a[2 * i, j] = rows[i].X[j] * w;
                a[2 * i + 1, j] = rows[i].Y[j] * w;
            }
            b[2 * i] = rows[i].AxisX * w;
            b[2 * i + 1] = rows[i].AxisY * w;
        }
        var scales = new double[Unknowns];
        var permutation = Enumerable.Range(0, Unknowns).ToArray();
        for (int j = 0; j < Unknowns; j++)
        {
            double norm = 0;
            for (int i = 0; i < m; i++) norm += a[i, j] * a[i, j];
            scales[j] = Math.Sqrt(norm);
            if (scales[j] < 1e-12)
                throw new InvalidOperationException("联合设计矩阵缺少可观测参数。");
            for (int i = 0; i < m; i++) a[i, j] /= scales[j];
        }
        for (int k = 0; k < Unknowns; k++)
        {
            int pivot = k;
            double largest = -1;
            for (int j = k; j < Unknowns; j++)
            {
                double norm = 0;
                for (int i = k; i < m; i++) norm += a[i, j] * a[i, j];
                if (norm > largest) { largest = norm; pivot = j; }
            }
            if (largest < 1e-16)
                throw new InvalidOperationException("联合设计矩阵秩不足，请扩大平移/旋转采样分布。");
            if (pivot != k)
            {
                for (int i = 0; i < m; i++) (a[i, k], a[i, pivot]) = (a[i, pivot], a[i, k]);
                (permutation[k], permutation[pivot]) = (permutation[pivot], permutation[k]);
            }
            double normColumn = Math.Sqrt(largest);
            double alpha = a[k, k] >= 0 ? -normColumn : normColumn;
            var v = new double[m - k];
            v[0] = a[k, k] - alpha;
            for (int i = k + 1; i < m; i++) v[i - k] = a[i, k];
            double lengthSquared = v.Sum(x => x * x);
            for (int j = k; j < Unknowns; j++)
            {
                double dot = 0;
                for (int i = k; i < m; i++) dot += v[i - k] * a[i, j];
                double factor = 2 * dot / lengthSquared;
                for (int i = k; i < m; i++) a[i, j] -= factor * v[i - k];
            }
            double rhsDot = 0;
            for (int i = k; i < m; i++) rhsDot += v[i - k] * b[i];
            double rhsFactor = 2 * rhsDot / lengthSquared;
            for (int i = k; i < m; i++) b[i] -= rhsFactor * v[i - k];
            a[k, k] = alpha;
            for (int i = k + 1; i < m; i++) a[i, k] = 0;
        }
        var pivoted = new double[Unknowns];
        for (int i = Unknowns - 1; i >= 0; i--)
        {
            double rhs = b[i];
            for (int j = i + 1; j < Unknowns; j++) rhs -= a[i, j] * pivoted[j];
            pivoted[i] = rhs / a[i, i];
        }
        var result = new double[Unknowns];
        for (int j = 0; j < Unknowns; j++) result[permutation[j]] = pivoted[j] / scales[permutation[j]];
        if (result.Any(x => !double.IsFinite(x)))
            throw new InvalidOperationException("联合拟合产生非有限解。");
        return result;
    }

    private sealed record Observation(double[] X, double AxisX, double[] Y, double AxisY);
}
