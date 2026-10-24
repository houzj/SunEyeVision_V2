using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class RoiShapeValueAdapterTests
{
    [Fact]
    public void Existing_geometry_parameter_conventions_are_preserved_in_declared_records()
    {
        var cases = new (object Geometry,string Kind,string? Field,double Value)[] {
            (new Point2d(2,3),"point",null,0),
            (new Line2d(new Point(2,3),new Vector(0,1)),"line","direction-y",1),
            (new Circle2d(2,3,4),"circle","radius",4),
            (new RotatedRect2d(2,3,4,5,37),"rectangle","angle-degrees",37),
            (new Arc2d(new Point(2,3),4,.25,1.75,true),"arc","start-radians",.25),
            (new Annulus2d(new Point(2,3),2,4,.5,2),"annulus","start-radians",.5),
            (new Polygon2d(new[]{new Point(0,0),new Point(4,0),new Point(0,3)}),"polygon",null,0)
        };
        foreach(var item in cases)
        {
            var frozen=RoiShapeValueAdapter.Freeze(new RoiShape{Geometry=item.Geometry});
            Assert.Equal(item.Kind,frozen.Read("kind").GetScalar<string>());
            if(item.Field is not null)Assert.Equal(item.Value,frozen.Read(item.Field).GetScalar<double>());
        }
        Assert.True(RoiShapeValueAdapter.Freeze(new RoiShape{Geometry=cases[4].Geometry}).Read("major-arc").GetScalar<bool>());
    }

    [Fact]
    public void Polygon_projection_is_frozen_and_unsupported_geometry_is_rejected()
    {
        var vertices=new[]{new Point(0,0),new Point(4,0),new Point(0,3)};
        var shape=new RoiShape{Geometry=new Polygon2d(vertices,false)};
        var frozen=RoiShapeValueAdapter.Freeze(shape);
        vertices[0]=new Point(100,100);shape.Geometry=new Circle2d(0,0,2);
        Assert.Equal("polygon",frozen.Read("kind").GetScalar<string>());
        Assert.Equal(3,frozen.Read("vertices").Count);
        Assert.False(frozen.Read("closed").GetScalar<bool>());
        Assert.Throws<ArgumentException>(()=>RoiShapeValueAdapter.Freeze(new RoiShape{Geometry=new object()}));
        Assert.Equal("none",RoiShapeValueAdapter.Freeze(new RoiShape()).Read("kind").GetScalar<string>());
    }
}
