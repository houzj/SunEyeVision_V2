using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Tool.BlobAnalysis.Runtime;
public sealed partial class BlobSession
{
        private static HImage ApplyPreprocessing(HImage image, BlobSettings parameters)
        {
            var method = parameters.PreprocessMethod;
            int kernelSize = parameters.PreprocessKernelSize;
            using var domain = image.GetDomain();
            using var fullImage = image.FullDomain();

            switch (method)
            {
                case BlobPreprocess.Mean:
                    using (var mean = HalconLinearFilters.Apply(fullImage, kernelSize, kernelSize, 0, 0, false, 0))
                        return mean.ReduceDomain(domain);

                case BlobPreprocess.Gaussian:
                    using (var gaussian = HalconLinearFilters.Apply(fullImage, kernelSize, kernelSize, 0, 0, true, 0))
                        return gaussian.ReduceDomain(domain);

                case BlobPreprocess.Median:
                    HOperatorSet.MedianRect(fullImage, out var medianImg, kernelSize, kernelSize);
                    using (medianImg)
                    using (var median = new HImage(medianImg))
                        return median.ReduceDomain(domain);

                case BlobPreprocess.HistogramEqualization:
                    HOperatorSet.EquHistoImage(image, out var eqImg);
                    return new HImage(eqImg);

                default:
                    return image.Clone();
            }
        }

        private static HRegion ApplyMorphology(HRegion inputRegion, BlobSettings parameters)
        {
            var operation = parameters.MorphOperation;
            var elementShape = parameters.MorphElementShape;
            int width = parameters.MorphElementWidth;
            int height = parameters.MorphElementHeight;
            int iterations = parameters.MorphIterations;

            using var se = BuildStructuringElement(elementShape, width, height);

            return operation switch
            {
                BlobMorphology.Erosion => ApplyErosion(inputRegion, se, iterations),
                BlobMorphology.Dilation => ApplyDilation(inputRegion, se, iterations),
                BlobMorphology.Opening => ApplyOpening(inputRegion, se, iterations),
                BlobMorphology.Closing => ApplyClosing(inputRegion, se, iterations),
                BlobMorphology.TopHat => ApplyTopHat(inputRegion, se, iterations),
                BlobMorphology.BottomHat => ApplyBottomHat(inputRegion, se, iterations),
                _ => throw new NotSupportedException($"不支持的Blob形态学操作: {operation}")
            };
        }

        private static HRegion BuildStructuringElement(BlobElementShape shape, int width, int height)
        {
            if (shape == BlobElementShape.Rectangle)
            {
                HOperatorSet.GenRectangle1(out var rect, 0, 0, height - 1, width - 1);
                return new HRegion(rect);
            }
            else if (shape == BlobElementShape.Circle)
            {
                // Preserve the serialized Circle contract: larger dimension determines the diameter.
                int radius = Math.Max(width, height) / 2;
                HOperatorSet.GenCircle(out var circle, radius, radius, radius);
                return new HRegion(circle);
            }
            throw new NotSupportedException($"不支持的Blob结构元素: {shape}");
        }

        private static HRegion ApplyErosion(HRegion region, HRegion se, int iterations)
        {
            HOperatorSet.Erosion1(region, se, out var result, iterations);
            return new HRegion(result);
        }

        private static HRegion ApplyDilation(HRegion region, HRegion se, int iterations)
        {
            HOperatorSet.Dilation1(region, se, out var result, iterations);
            return new HRegion(result);
        }

        private static HRegion ApplyOpening(HRegion region, HRegion se, int iterations)
        {
            using var eroded = ApplyErosion(region, se, iterations);
            return ApplyDilation(eroded, se, iterations);
        }

        private static HRegion ApplyClosing(HRegion region, HRegion se, int iterations)
        {
            using var dilated = ApplyDilation(region, se, iterations);
            return ApplyErosion(dilated, se, iterations);
        }

        private static HRegion ApplyTopHat(HRegion region, HRegion se, int iterations)
        {
            using var opened = ApplyOpening(region, se, iterations);
            return region.Difference(opened);
        }

        private static HRegion ApplyBottomHat(HRegion region, HRegion se, int iterations)
        {
            using var closed = ApplyClosing(region, se, iterations);
            return closed.Difference(region);
        }

        private static HRegion SelectRegions(HRegion source, IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return EmptyRegion();
            using var tuple = new HTuple(indices.ToArray());
            HOperatorSet.SelectObj(source, out var selected, tuple);
            using (selected) return new HRegion(selected);
        }

        private static HRegion EmptyRegion()
        {
            HOperatorSet.GenEmptyRegion(out var empty);
            using (empty) return new HRegion(empty);
        }

        private static Point2[] BuildRotatedRectangle(double row, double col, double phi, double length1, double length2)
        {
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            // HALCON rectangle2 phi -> screen coordinates (X=column, Y=row).
            // Its first axis is (cos(phi), -sin(phi)), not (cos(phi), sin(phi)).
            var axis = new Point2(cos, -sin);
            var normal = new Point2(sin, cos);
            return new[]
            {
                new Point2(col - axis.X * length1 - normal.X * length2, row - axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 - normal.X * length2, row + axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 + normal.X * length2, row + axis.Y * length1 + normal.Y * length2),
                new Point2(col - axis.X * length1 + normal.X * length2, row - axis.Y * length1 + normal.Y * length2)
            };
        }
}
