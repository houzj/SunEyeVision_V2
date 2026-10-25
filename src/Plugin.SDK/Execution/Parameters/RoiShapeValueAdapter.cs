using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Explicit data projection of the existing ROI definitions, preserving their parameter conventions.</summary>
public static class RoiShapeValueAdapter
{
    public static CoordinateFrame ImageFrame {get;}=RoiDeclarations.ImageFrame;
    private static TypeRef PointType=>TypeRef.GeometryOf(GeometryKind.Point,ImageFrame);
    public static TypeRef Declaration {get;}=RoiDeclarations.Shape;
    public static TypeRef SequenceDeclaration {get;}=RoiDeclarations.Sequence;
    public static RecordValue Freeze(RoiShape shape)
    {
        var values=new Dictionary<string,object?>(StringComparer.Ordinal);
        PointGeometry Point(double x,double y)=>new(ImageFrame,new(x,y));
        void Center(System.Windows.Point center)=>values["center"]=Point(center.X,center.Y);
        switch(shape.Geometry)
        {
            case Point2d p:values["kind"]="point";values["center"]=Point(p.X,p.Y);break;
            case Line2d line:
                values["kind"]="line";Center(line.Point);values["direction-x"]=line.Direction.X;values["direction-y"]=line.Direction.Y;break;
            case Circle2d circle:
                values["kind"]="circle";Center(circle.Center);values["radius"]=circle.Radius;break;
            case RotatedRect2d rectangle:
                values["kind"]="rectangle";Center(rectangle.Center);values["width"]=rectangle.Width;values["height"]=rectangle.Height;
                values["angle-degrees"]=rectangle.Angle;break;
            case Arc2d arc:
                values["kind"]="arc";Center(arc.Center);values["radius"]=arc.Radius;values["start-radians"]=arc.StartAngle;
                values["end-radians"]=arc.EndAngle;values["major-arc"]=arc.IsMajorArc;break;
            case Annulus2d ring:
                values["kind"]="annulus";Center(ring.Center);values["inner-radius"]=ring.InnerRadius;values["outer-radius"]=ring.OuterRadius;
                values["start-radians"]=ring.StartAngle;values["end-radians"]=ring.EndAngle;break;
            case Polygon2d polygon:
                values["kind"]="polygon";values["closed"]=polygon.IsClosed;
                values["vertices"]=new SequenceValue(Declaration.Fields.Single(p=>p.Id=="vertices").Type,
                    polygon.Vertices.Select(p=>(object?)Point(p.X,p.Y)));break;
            case null:values["kind"]="none";break;
            default:throw new ArgumentException("This ROI geometry has no declared immutable projection.",nameof(shape));
        }
        return new(Declaration,values);
    }
}
