using System.Text.Json;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>The unique JSON value representation for immutable runtime geometry.
/// Logical frames are preserved; this codec never creates physical image provenance.</summary>
public static class GeometryValueCanonical
{
    public static void Write(Utf8JsonWriter writer,GeometryValue value)
    {
        ArgumentNullException.ThrowIfNull(writer);ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();writer.WriteString("kind",value.Kind.ToString());
        writer.WriteStartObject("frame");writer.WriteString("id",value.Frame.Id);
        writer.WriteString("domain",value.Frame.Domain.ToString());writer.WriteString("unit",value.Frame.Unit.ToString());writer.WriteEndObject();
        void Point(string name,Point2 point){writer.WriteStartObject(name);writer.WriteNumber("x",point.X);writer.WriteNumber("y",point.Y);writer.WriteEndObject();}
        switch(value)
        {
            case PointGeometry point:Point("point",point.Point);break;
            case LineGeometry line:Point("start",line.Start);Point("end",line.End);break;
            case CircleGeometry circle:Point("center",circle.Center);writer.WriteNumber("radius",circle.Radius);break;
            case ArcGeometry arc:Point("center",arc.Center);writer.WriteNumber("radius",arc.Radius);writer.WriteNumber("startRadians",arc.StartRadians);writer.WriteNumber("sweepRadians",arc.SweepRadians);break;
            case OrientedRectangleGeometry box:Point("center",box.Center);writer.WriteNumber("width",box.Width);writer.WriteNumber("height",box.Height);writer.WriteNumber("angleRadians",box.AngleRadians);break;
            case EllipseGeometry ellipse:Point("center",ellipse.Center);writer.WriteNumber("radiusX",ellipse.RadiusX);writer.WriteNumber("radiusY",ellipse.RadiusY);writer.WriteNumber("angleRadians",ellipse.AngleRadians);break;
            case PolygonGeometry polygon:
                writer.WriteStartArray("vertices");foreach(var point in polygon.Vertices){writer.WriteStartObject();writer.WriteNumber("x",point.X);writer.WriteNumber("y",point.Y);writer.WriteEndObject();}writer.WriteEndArray();break;
            default:throw new JsonException("Unsupported immutable geometry kind.");
        }
        writer.WriteEndObject();
    }

    public static GeometryValue Read(JsonElement value,TypeRef declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if(declaration.Shape!=ValueShape.Geometry)throw new ArgumentException("A geometry declaration is required.",nameof(declaration));
        try
        {
            var kind=EnumText<GeometryKind>(value.GetProperty("kind"));
            var shapeFields=kind switch
            {
                GeometryKind.Point=>new[]{"point"},GeometryKind.Line=>new[]{"start","end"},
                GeometryKind.Circle=>new[]{"center","radius"},GeometryKind.Arc=>new[]{"center","radius","startRadians","sweepRadians"},
                GeometryKind.OrientedRectangle=>new[]{"center","width","height","angleRadians"},
                GeometryKind.Ellipse=>new[]{"center","radiusX","radiusY","angleRadians"},GeometryKind.Polygon=>new[]{"vertices"},
                _=>throw new JsonException("Unknown immutable geometry kind.")
            };
            Exact(value,shapeFields.Prepend("kind").Append("frame").ToArray());
            var frameValue=value.GetProperty("frame");Exact(frameValue,"id","domain","unit");
            var frame=new CoordinateFrame(frameValue.GetProperty("id").GetString()??throw new JsonException("Missing logical frame identity."),
                EnumText<CoordinateDomain>(frameValue.GetProperty("domain")),EnumText<CoordinateUnit>(frameValue.GetProperty("unit")));
            if(kind!=declaration.Geometry||frame!=declaration.Frame)throw new JsonException("Geometry kind or logical frame differs from its admitted declaration.");
            Point2 Point(string name)=>ReadPoint(value.GetProperty(name));
            double Number(string name)=>value.GetProperty(name).GetDouble();
            return kind switch
            {
                GeometryKind.Point=>new PointGeometry(frame,Point("point")),
                GeometryKind.Line=>new LineGeometry(frame,Point("start"),Point("end")),
                GeometryKind.Circle=>new CircleGeometry(frame,Point("center"),Number("radius")),
                GeometryKind.Arc=>new ArcGeometry(frame,Point("center"),Number("radius"),Number("startRadians"),Number("sweepRadians")),
                GeometryKind.OrientedRectangle=>new OrientedRectangleGeometry(frame,Point("center"),Number("width"),Number("height"),Number("angleRadians")),
                GeometryKind.Ellipse=>new EllipseGeometry(frame,Point("center"),Number("radiusX"),Number("radiusY"),Number("angleRadians")),
                GeometryKind.Polygon=>new PolygonGeometry(frame,value.GetProperty("vertices").EnumerateArray().Select(ReadPoint)),
                _=>throw new JsonException("Unknown immutable geometry kind.")
            };
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        {throw new JsonException("Geometry constant is malformed or violates its declaration.",error);}
    }
    private static Point2 ReadPoint(JsonElement value)
    {Exact(value,"x","y");return new(value.GetProperty("x").GetDouble(),value.GetProperty("y").GetDouble());}
    private static T EnumText<T>(JsonElement value)where T:struct,Enum
    {
        var text=value.GetString();
        return Enum.TryParse<T>(text,out var parsed)&&Enum.IsDefined(parsed)&&text==parsed.ToString()?parsed:throw new JsonException("Invalid canonical geometry enum.");
    }
    private static void Exact(JsonElement value,params string[] names)
    {
        if(value.ValueKind!=JsonValueKind.Object)throw new JsonException("Geometry object is required.");
        var found=new HashSet<string>(StringComparer.Ordinal);
        foreach(var field in value.EnumerateObject())if(!found.Add(field.Name))throw new JsonException("Duplicate geometry property: "+field.Name);
        if(!found.SetEquals(names))throw new JsonException("Geometry requires exactly: "+string.Join(",",names));
    }
}