using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Explicit SDK/data geometry bridge. It neither reflects live result members nor invents frames.</summary>
public static class RoiGeometryValueAdapter
{
    public static TypeRef? Declaration(Type type)=>type==typeof(Circle2d)?TypeRef.GeometryOf(GeometryKind.Circle,RoiDeclarations.ImageFrame):
        type==typeof(RotatedRect2d)||type==typeof(System.Windows.Rect)?TypeRef.GeometryOf(GeometryKind.OrientedRectangle,RoiDeclarations.ImageFrame):
        type==typeof(Point2d)?TypeRef.GeometryOf(GeometryKind.Point,RoiDeclarations.ImageFrame):null;
    public static GeometryValue Freeze(object value)=>value switch
    {
        Circle2d circle=>new CircleGeometry(RoiDeclarations.ImageFrame,new(circle.Center.X,circle.Center.Y),circle.Radius),
        RotatedRect2d rectangle=>new OrientedRectangleGeometry(RoiDeclarations.ImageFrame,new(rectangle.Center.X,rectangle.Center.Y),rectangle.Width,rectangle.Height,rectangle.AngleRadians),
        System.Windows.Rect rectangle=>new OrientedRectangleGeometry(RoiDeclarations.ImageFrame,new(rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2),rectangle.Width,rectangle.Height,0),
        Point2d point=>new PointGeometry(RoiDeclarations.ImageFrame,new(point.X,point.Y)),
        _=>throw new ArgumentException("ROI_GEOMETRY_TYPE: 此SDK几何没有明确的中立映射。")
    };
    public static IReadOnlyList<RoiShape> Materialize(object? value)
    {
        if(value is null)throw new ArgumentException("ROI_SOURCE_NULL: 区域来源是空值，不是成功的空集合。");
        if(value is SequenceValue sequence)return sequence.SelectMany(Materialize).ToArray();
        if(value is Array array)return array.Cast<object?>().SelectMany(Materialize).ToArray();
        if(value is RecordValue record)return new[]{Checked(RoiShapeValueAdapter.Materialize(record))};
        if(value is RoiShape shape)
        {return new[]{Checked(shape.Clone())};}
        object geometry=value switch
        {
            CircleGeometry circle when circle.Frame==RoiDeclarations.ImageFrame=>new Circle2d(circle.Center.X,circle.Center.Y,circle.Radius),
            OrientedRectangleGeometry rectangle when rectangle.Frame==RoiDeclarations.ImageFrame=>new RotatedRect2d(rectangle.Center.X,rectangle.Center.Y,rectangle.Width,rectangle.Height,rectangle.AngleRadians*180/Math.PI),
            PointGeometry point when point.Frame==RoiDeclarations.ImageFrame=>new Point2d(point.Point.X,point.Point.Y),
            System.Windows.Rect rectangle=>new RotatedRect2d(rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,rectangle.Width,rectangle.Height,0),
            Circle2d or RotatedRect2d=>value,
            _=>throw new ArgumentException("ROI_SOURCE_GEOMETRY: 来源几何类型或逻辑坐标系不受支持。")
        };
        return new[]{Checked(new RoiShape{Geometry=geometry})};
    }
    private static RoiShape Checked(RoiShape shape)
    {
        // Freeze validates finite coordinates/numbers through the neutral record contract.
        var record=RoiShapeValueAdapter.Freeze(shape);
        double N(string key)=>record.Get<double>(key);
        void Positive(string key){if(N(key)<=0)throw new ArgumentException("ROI_SOURCE_SIZE: 区域尺寸必须为正数："+key);}
        switch(record.Get<string>("kind"))
        {
            case "none":throw new ArgumentException("ROI_SOURCE_GEOMETRY: 来源含空几何。");
            case "circle":case "arc":Positive("radius");break;
            case "rectangle":Positive("width");Positive("height");break;
            case "line":if(Math.Abs(N("direction-x"))+Math.Abs(N("direction-y"))<1e-12)throw new ArgumentException("ROI_SOURCE_GEOMETRY: 直线方向不能为零。");break;
            case "annulus":Positive("outer-radius");if(N("inner-radius")<0||N("inner-radius")>=N("outer-radius"))throw new ArgumentException("ROI_SOURCE_SIZE: 内半径必须非负且小于外半径。");break;
        }
        return shape;
    }
}
