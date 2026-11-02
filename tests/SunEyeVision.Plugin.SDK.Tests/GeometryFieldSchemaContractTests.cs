using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;
namespace SunEyeVision.Plugin.SDK.Tests;
public sealed class GeometryFieldSchemaContractTests
{
    [Theory]
    [InlineData(typeof(Circle2d), 3)]
    [InlineData(typeof(Line2d), 4)]
    [InlineData(typeof(Point2d), 2)]
    [InlineData(typeof(RotatedRect2d), 5)]
    [InlineData(typeof(Arc2d), 5)]
    [InlineData(typeof(Annulus2d), 6)]
    public void Declared_geometry_fields_remain_available(Type type, int count)
    { Assert.True(GeometryFieldSchema.HasFields(type)); Assert.Equal(count, GeometryFieldSchema.GetFields(type).Count); }
    [Fact]
    public void Polygon_has_no_scalar_field()
    { Assert.False(GeometryFieldSchema.HasFields(typeof(Polygon2d))); Assert.Empty(GeometryFieldSchema.GetFields(typeof(Polygon2d))); }
}
