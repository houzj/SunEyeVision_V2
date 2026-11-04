using System.Text;
using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class GeometryConfigurationTests
{
    private static readonly CoordinateFrame Frame=new("image:input",CoordinateDomain.Image,CoordinateUnit.Pixel);
    private static GeometryValue[] Shapes()=>new GeometryValue[]{
        new PointGeometry(Frame,new(-3,11)),new LineGeometry(Frame,new(1,2),new(7,-4)),
        new CircleGeometry(Frame,new(2,-5),3.5),new ArcGeometry(Frame,new(-1,8),6,0.3,-1.8),
        new OrientedRectangleGeometry(Frame,new(4,9),12,3,0.7),new EllipseGeometry(Frame,new(-4,2),7,2,-0.9),
        new PolygonGeometry(Frame,new[]{new Point2(1,2),new Point2(8,3),new Point2(2,9)})};
    private static FrozenToolDeclarations Declared(TypeRef type,bool nullable=false)
    {
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("geometry",type,Nullable:nullable,EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        return new("geometry-test",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),
            new(null,Array.Empty<ResourceFieldDeclaration>(),"admitted"),Array.Empty<ManualReferenceSource>());
    }
    private static string Encode(object? value)
    {
        using var bytes=new MemoryStream();
        using(var writer=new Utf8JsonWriter(bytes))ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{"geometry",new ConstantSetting(value)}});
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    private static object? Decode(string json,TypeRef type,bool nullable=false)
    {
        using var document=JsonDocument.Parse(json);
        return Assert.IsType<ConstantSetting>(DeclaredFieldJson.Read(Declared(type,nullable),document.RootElement)["geometry"]).Value;
    }
    [Fact]
    public void All_seven_runtime_geometries_keep_their_declared_frame_and_exact_coordinates()
    {
        foreach(var shape in Shapes())
        {
            var json=Encode(shape);
            var decoded=Assert.IsAssignableFrom<GeometryValue>(Decode(json,TypeRef.GeometryOf(shape.Kind,Frame)));
            Assert.Equal(shape.Kind,decoded.Kind);Assert.Equal(Frame,decoded.Frame);Assert.Equal(json,Encode(decoded));
            switch(decoded)
            {
                case PointGeometry point:Assert.Equal(new Point2(-3,11),point.Point);break;
                case LineGeometry line:Assert.Equal(new Point2(1,2),line.Start);Assert.Equal(new Point2(7,-4),line.End);break;
                case CircleGeometry circle:Assert.Equal(3.5,circle.Radius);Assert.Equal(new Point2(2,-5),circle.Center);break;
                case ArcGeometry arc:Assert.Equal(0.3,arc.StartRadians);Assert.Equal(-1.8,arc.SweepRadians);break;
                case OrientedRectangleGeometry box:Assert.Equal(12,box.Width);Assert.Equal(3,box.Height);Assert.Equal(0.7,box.AngleRadians);break;
                case EllipseGeometry ellipse:Assert.Equal(7,ellipse.RadiusX);Assert.Equal(2,ellipse.RadiusY);Assert.Equal(-0.9,ellipse.AngleRadians);break;
                case PolygonGeometry polygon:Assert.Equal(new[]{new Point2(1,2),new Point2(8,3),new Point2(2,9)},polygon.Vertices);break;
            }
        }
    }
    [Fact]
    public void Nested_records_sequences_and_nullable_fields_use_the_same_declared_codec()
    {
        var pointType=TypeRef.GeometryOf(GeometryKind.Point,Frame);
        var recordType=TypeRef.RecordOf("test.geometry.record",new[]{new Port("point",pointType),new Port("points",TypeRef.SequenceOf(pointType))});
        var record=new RecordValue(recordType,new Dictionary<string,object?>{{"point",new PointGeometry(Frame,new(2,7))},{"points",new SequenceValue(TypeRef.SequenceOf(pointType),new object?[]{new PointGeometry(Frame,new(-3,5)),new PointGeometry(Frame,new(8,1))})}});
        var decoded=Assert.IsType<RecordValue>(Decode(Encode(record),recordType));
        Assert.Equal(new Point2(2,7),decoded.Get<PointGeometry>("point").Point);
        Assert.Equal(new Point2(8,1),Assert.IsType<PointGeometry>(decoded.Get<SequenceValue>("points")[1]).Point);
        Assert.Null(Decode(Encode(null),pointType,true));
        Assert.ThrowsAny<Exception>(()=>Decode(Encode(null),pointType));
    }
    [Fact]
    public void Wrong_kind_frame_duplicates_unknown_fields_and_degenerate_geometry_are_rejected()
    {
        var point=Encode(new PointGeometry(Frame,new(2,7)));var pointType=TypeRef.GeometryOf(GeometryKind.Point,Frame);
        Assert.ThrowsAny<Exception>(()=>Decode(point,TypeRef.GeometryOf(GeometryKind.Line,Frame)));
        Assert.ThrowsAny<Exception>(()=>Decode(point,TypeRef.GeometryOf(GeometryKind.Point,new("other",CoordinateDomain.Image,CoordinateUnit.Pixel))));
        foreach(var invalid in new[]{point.Replace("\"x\":2","\"x\":2,\"x\":3"),point.Replace("\"x\":2","\"x\":2,\"extra\":3"),point.Replace("\"x\":2","\"x\":1e309"),point.Replace("\"unit\":\"Pixel\"","\"unit\":\"Millimeter\"")})
            Assert.ThrowsAny<Exception>(()=>Decode(invalid,pointType));
        var line=Encode(new LineGeometry(Frame,new(1,2),new(7,4))).Replace("\"x\":7","\"x\":1").Replace("\"y\":4","\"y\":2");
        Assert.ThrowsAny<Exception>(()=>Decode(line,TypeRef.GeometryOf(GeometryKind.Line,Frame)));
        var arc=Encode(new ArcGeometry(Frame,new(2,3),4,0.2,-1.1)).Replace("\"sweepRadians\":-1.1","\"sweepRadians\":0");
        Assert.ThrowsAny<Exception>(()=>Decode(arc,TypeRef.GeometryOf(GeometryKind.Arc,Frame)));
    }
}