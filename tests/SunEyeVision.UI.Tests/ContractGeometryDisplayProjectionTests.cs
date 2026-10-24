using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;
using SunEyeVision.UI.Services.Rendering;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ContractGeometryDisplayProjectionTests
{
    private static readonly CoordinateFrame Frame = new("camera-input", CoordinateDomain.Image, CoordinateUnit.Pixel);

    [Theory]
    [InlineData(0, -4, -2, 4, -2, 4, 2, -4, 2)]
    [InlineData(90, 2, -4, 2, 4, -2, 4, -2, -4)]
    [InlineData(-90, -2, 4, -2, -4, 2, -4, 2, 4)]
    [InlineData(180, 4, 2, -4, 2, -4, -2, 4, -2)]
    public void Known_corners_preserve_full_dimensions_order_and_image_angle_direction(
        double degrees, double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        var rectangle = new OrientedRectangleGeometry(Frame, new Point2(0, 0), 8, 4, degrees * Math.PI / 180);
        var result = ContractGeometryDisplayProjection.Project(rectangle);
        var expected = new[] { new Point(x0, y0), new Point(x1, y1), new Point(x2, y2), new Point(x3, y3) };
        Assert.Same(Frame, result.Frame);
        Assert.True(result.Contour.IsClosed);
        Assert.Null(result.Contour.Transform);
        Assert.Equal(4, result.Contour.Points.Length);
        for (var i = 0; i < 4; i++) EqualPoint(expected[i], result.Contour.Points[i]);
        Assert.Equal(8, (result.Contour.Points[1] - result.Contour.Points[0]).Length, 10);
        Assert.Equal(4, (result.Contour.Points[2] - result.Contour.Points[1]).Length, 10);
    }

    [Theory]
    [InlineData(0.125, -0.75, 8.5, 3.25, 37)]
    [InlineData(1000000000.125, -1000000000.75, 80.5, 32.25, -123)]
    public void Fractional_large_coordinates_match_existing_degree_display_helper(
        double x, double y, double width, double height, double degrees)
    {
        var rectangle = new OrientedRectangleGeometry(Frame, new Point2(x, y), width, height, degrees * Math.PI / 180);
        var result = ContractGeometryDisplayProjection.Project(rectangle);
        var expected = RotatedRectangleHelper.GetCorners(new Point(x, y), width, height, degrees);
        for (var i = 0; i < 4; i++) EqualPoint(expected[i], result.Contour.Points[i]);
        Assert.Equal(width, (result.Contour.Points[1] - result.Contour.Points[0]).Length, 5);
        Assert.Equal(height, (result.Contour.Points[2] - result.Contour.Points[1]).Length, 5);
    }

    [Fact]
    public void Arbitrary_radian_angle_has_known_corner_and_fresh_owned_array()
    {
        var rectangle = new OrientedRectangleGeometry(Frame, new Point2(10.5, 20.25), 8, 4, Math.PI / 4);
        var first = ContractGeometryDisplayProjection.Project(rectangle);
        var second = ContractGeometryDisplayProjection.Project(rectangle);
        EqualPoint(new Point(10.5 - Math.Sqrt(2), 20.25 - 3 * Math.Sqrt(2)), first.Contour.Points[0]);
        Assert.NotSame(first.Contour, second.Contour);
        Assert.NotSame(first.Contour.Points, second.Contour.Points);
        var original = second.Contour.Points[0];
        first.Contour.Points[0] = new Point(999, 999);
        EqualPoint(original, second.Contour.Points[0]);
        Assert.Equal(new Point2(10.5, 20.25), rectangle.Center);
        Assert.Equal(8, rectangle.Width);
        Assert.Equal(4, rectangle.Height);
        Assert.Equal(Math.PI / 4, rectangle.AngleRadians);
        Assert.Same(Frame, rectangle.Frame);
    }

    [Theory]
    [InlineData(CoordinateDomain.Image, CoordinateUnit.Millimeter)]
    [InlineData(CoordinateDomain.Model, CoordinateUnit.Pixel)]
    [InlineData(CoordinateDomain.Model, CoordinateUnit.Millimeter)]
    public void Frames_requiring_calibration_are_explicitly_rejected(CoordinateDomain domain, CoordinateUnit unit)
    {
        var rectangle = new OrientedRectangleGeometry(new CoordinateFrame("other", domain, unit), new Point2(0, 0), 8, 4, 0);
        Assert.Contains("Image/Pixel", Assert.Throws<NotSupportedException>(() => ContractGeometryDisplayProjection.Project(rectangle)).Message);
    }

    [Fact]
    public void Null_and_unrenderable_overflow_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ContractGeometryDisplayProjection.Project(null!));
        var rectangle = new OrientedRectangleGeometry(Frame, new Point2(double.MaxValue, 0), double.MaxValue, 4, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractGeometryDisplayProjection.Project(rectangle));
    }

    private static void EqualPoint(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }
}
