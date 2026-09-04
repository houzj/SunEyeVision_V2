using OpenCvSharp;
using Xunit;

namespace SunEyeVision.CvEngine.Tests;

/// <summary>
/// CvEngine 纯化载体（CvImage/CvRegion/CvContour）的算子回归测试（方案 AC-3）。
/// 背景：Cv 类型由 SDK VisionAdapters 迁入 CvEngine 并剥离框架接口职责后，
/// 算子（CvOp.*）依赖的类型来源发生切换（编译通过 ≠ 运行正确），
/// 本测试用固定图样验证典型算子链的行为未变：阈值 → 连通域 → 区域特征 → 轮廓。
/// </summary>
public class CvOpPurifiedTypeRegressionTests
{
    /// <summary>构造 20x20 单通道图：左半 (0..9 列) 全白 255，右半全黑 0。</summary>
    private static CvImage CreateHalfWhiteImage()
    {
        var mat = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
        mat[0, 20, 0, 10].SetTo(Scalar.White);
        return CvImage.FromMat(mat);
    }

    [Fact]
    public void CvImage_FromMat_PreservesSizeAndChannels()
    {
        using var image = CreateHalfWhiteImage();

        Assert.False(image.Empty);
        Assert.Equal(20, image.Size.Width);
        Assert.Equal(20, image.Size.Height);
        Assert.Equal(1, image.Channels);
        Assert.Equal(1, image.Count);
    }

    [Fact]
    public void CvImage_CopyObj_IsIndependentDeepCopy()
    {
        using var image = CreateHalfWhiteImage();
        using var copy = image.CopyObj();

        Assert.Equal(image.Size.Width, copy.Size.Width);

        // 深拷贝：改动副本不影响原图
        copy.Image.SetTo(Scalar.Black);
        Assert.Equal(255, image.Image.At<byte>(0, 0));
    }

    [Fact]
    public void Threshold_LeftHalfWhite_ProducesHalfAreaRegion()
    {
        using var image = CreateHalfWhiteImage();

        CvOp.Threshold(image, null, 128, 255, out var region, out var actualThreshold);

        using (region)
        {
            Assert.Equal(128, actualThreshold);
            Assert.False(region.IsEmpty);
            Assert.Equal(200, region.PixelCount); // 20 行 x 10 列
        }
    }

    [Fact]
    public void Connection_And_RegionFeatures_SingleBlobAreaCenter()
    {
        using var image = CreateHalfWhiteImage();
        CvOp.Threshold(image, null, 128, 255, out var region, out _);

        using (region)
        {
            CvOp.Connection(region, out var connected);
            using (connected)
            {
                Assert.Equal(1, connected.Count);

                // RegionFeatures 按传入特征顺序返回值数组（此处 3 个特征 → 3 个值）
                CvOp.RegionFeatures(connected, out var values, RegionFeature.Area, RegionFeature.Row, RegionFeature.Column);

                Assert.Equal(3, values.Length);
                Assert.Equal(200, values[0], 0);   // Area：面积
                Assert.Equal(9.5, values[1], 5);   // Row：质心行 (0+19)/2
                Assert.Equal(4.5, values[2], 5);   // Column：质心列 (0+9)/2
            }
        }
    }

    [Fact]
    public void SmallestRectangle1_MatchesWhiteBlockBounds()
    {
        using var image = CreateHalfWhiteImage();
        CvOp.Threshold(image, null, 128, 255, out var region, out _);

        using (region)
        {
            CvOp.SmallestRectangle1(region, out var row1, out var col1, out var row2, out var col2);

            Assert.Single(row1);
            Assert.Equal(0, row1[0], 5);
            Assert.Equal(0, col1[0], 5);
            Assert.Equal(19, row2[0], 5);
            Assert.Equal(9, col2[0], 5);
        }
    }

    [Fact]
    public void GetContour_ReturnsClosedContourWithPoints()
    {
        using var image = CreateHalfWhiteImage();
        CvOp.Threshold(image, null, 128, 255, out var region, out _);

        using (region)
        {
            CvOp.GetContour(region, out var contours);

            Assert.Single(contours);
            var contour = contours[0];
            Assert.False(contour.IsEmpty);
            Assert.True(contour.IsClosed);
            Assert.True(contour.Points!.Length >= 4);

            // 轮廓可深拷贝（纯化后 Clone 语义由 CopyObj 承担）
            var copy = contour.CopyObj();
            Assert.Equal(contour.Points.Length, copy.Points!.Length);
            foreach (var c in contours) c.Dispose();
        }
    }

    [Fact]
    public void CvRegion_FromMask_EmptyMaskYieldsEmptyRegion()
    {
        using var emptyMask = new Mat(10, 10, MatType.CV_8UC1, Scalar.Black);
        using var region = CvRegion.FromMask(emptyMask);

        Assert.True(region.IsEmpty);
        Assert.Equal(0, region.Count);
        Assert.Equal(0, region.PixelCount);
    }

    [Fact]
    public void CvRegion_GenRectangle1_AreaMatches()
    {
        using var region = CvRegion.GenRectangle1(2, 3, 6, 7); // 行2..6 列3..7 → 5x5

        Assert.False(region.IsEmpty);
        Assert.Equal(25, region.PixelCount);
    }
}
