using HalconDotNet;
using SunEyeVision.Tool.ContourMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

public sealed class ContourMatchContourGeometryTests
{
    [Fact]
    public void Global_box_covers_all_xld_components_instead_of_the_first_component()
    {
        using var firstRows = new HTuple(new[] { 10d, 10d, 20d, 20d, 10d });
        using var firstColumns = new HTuple(new[] { 10d, 20d, 20d, 10d, 10d });
        using var secondRows = new HTuple(new[] { 70d, 70d, 80d, 80d, 70d });
        using var secondColumns = new HTuple(new[] { 70d, 80d, 80d, 70d, 70d });
        HOperatorSet.GenContourPolygonXld(out var first, firstRows, firstColumns);
        HOperatorSet.GenContourPolygonXld(out var second, secondRows, secondColumns);
        using (first) using (second)
        {
            HOperatorSet.ConcatObj(first, second, out var contours);
            using (contours)
            {
                var box = ContourMatchContourGeometry.SmallestRectangle2Xld(contours);

                Assert.InRange(box.Column, 44d, 46d);
                Assert.InRange(box.Row, 44d, 46d);
                Assert.True(Math.Max(box.Length1, box.Length2) * 2 >= 69d,
                    $"完整轮廓外接框应覆盖两个分离子轮廓，实际半长为 {box.Length1}、{box.Length2}。");
                Assert.Equal(10, ContourMatchContourGeometry.ReadPoints(contours).Count);
            }
        }
    }
}
