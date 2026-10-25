namespace SunEyeVision.Plugin.Contracts;

/// <summary>The existing SDK ROI record expressed once in neutral Contracts; no second geometry store or registry.</summary>
public static class RoiDeclarations
{
    public static CoordinateFrame ImageFrame{get;}=new("image:input",CoordinateDomain.Image,CoordinateUnit.Pixel);
    private static TypeRef PointType=>TypeRef.GeometryOf(GeometryKind.Point,ImageFrame);
    public static TypeRef Shape{get;}=TypeRef.RecordOf("sdk.roi-shape",new[]{
        new Port("kind",DataType.Text),new Port("center",PointType,false),
        new Port("direction-x",DataType.Number,false),new Port("direction-y",DataType.Number,false),
        new Port("radius",DataType.Number,false),new Port("inner-radius",DataType.Number,false),new Port("outer-radius",DataType.Number,false),
        new Port("width",DataType.Number,false),new Port("height",DataType.Number,false),new Port("angle-degrees",DataType.Number,false),
        new Port("start-radians",DataType.Number,false),new Port("end-radians",DataType.Number,false),new Port("major-arc",DataType.Boolean,false),
        new Port("vertices",TypeRef.SequenceOf(PointType),false),new Port("closed",DataType.Boolean,false)});
    public static TypeRef Sequence{get;}=TypeRef.SequenceOf(Shape);
}
