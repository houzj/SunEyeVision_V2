using System.Runtime.InteropServices;
using System.Text.Json;
using OpenCvSharp;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Infrastructure.Persistence;

/// <summary>One scope-bound V5 codec for immutable binary, records and persistent pixel assets.</summary>
public sealed class ComplexVariableValueCodec(DecodedResourcesScope scope) : IVariableValueExtensionCodec
{
    public DecodedResourcesScope Scope { get; }=scope??throw new ArgumentNullException(nameof(scope));
    /// <summary>Creates an owned immutable pixel asset in this candidate's resource scope.</summary>
    public IReadOnlyStoredRaster CreateRaster(StoredRasterLayout layout, ReadOnlySpan<byte> pixels)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var result = new FrozenStoredRaster(layout, pixels.ToArray());
        Scope.RegisterNew(result);
        return result;
    }
    private static readonly TypeRef TransformSequence=TypeRef.SequenceOf(VariableValueDeclarations.StoredTransform);
    public bool SupportsType(string typeName)=>typeName is "Binary" or "Transform" or "ROI" or "Image";
    public object? Read(string typeName,JsonElement value,JsonSerializerOptions options)=>typeName switch {
        "Binary"=>value.ValueKind==JsonValueKind.Array?value.EnumerateArray().Select(ReadBinary).ToArray():ReadBinary(value),
        "Transform"=>value.ValueKind==JsonValueKind.Array?new SequenceValue(TransformSequence,value.EnumerateArray().Select(x=>(object?)ReadRecord(x,VariableValueDeclarations.StoredTransform))):ReadRecord(value,VariableValueDeclarations.StoredTransform),
        "ROI"=>value.ValueKind==JsonValueKind.Array?new SequenceValue(VariableValueDeclarations.StoredRoiSequence,value.EnumerateArray().Select(x=>(object?)ReadRoi(x))):throw new JsonException("ROI requires a sequence."),
        "Image"=>ReadImage(value),_=>throw new JsonException("Unsupported complex variable type: "+typeName)};
    private static BinaryValue ReadBinary(JsonElement value)=>new(Convert.FromBase64String(value.GetString()??throw new JsonException("Binary requires Base64 text.")));
    private static RecordValue ReadRoi(JsonElement value){var record=ReadRecord(value,VariableValueDeclarations.StoredRoiShape);VariableValueDeclarations.ValidateStoredRoi(record);return record;}
    private static RecordValue ReadRecord(JsonElement value,TypeRef type)
    {
        if(value.ValueKind!=JsonValueKind.Object)throw new JsonException("Record requires an object.");
        var fields=new Dictionary<string,object?>(StringComparer.Ordinal);
        foreach(var property in value.EnumerateObject())
        {
            var port=type.Fields.SingleOrDefault(p=>p.Id==property.Name)??throw new JsonException("Unknown record field: "+property.Name);
            if(!fields.TryAdd(property.Name,ReadField(property.Value,port.Type)))throw new JsonException("Duplicate record field: "+property.Name);
        }
        return new RecordValue(type,fields);
    }
    private static object? ReadField(JsonElement value,TypeRef type)=>type.Shape switch {
        ValueShape.Record=>ReadRecord(value,type),
        ValueShape.Sequence when value.ValueKind==JsonValueKind.Array=>new SequenceValue(type,value.EnumerateArray().Select(item=>ReadField(item,type.Element!))),
        ValueShape.Scalar=>type.Scalar switch {ScalarKind.Text=>value.GetString(),ScalarKind.Boolean=>value.GetBoolean(),ScalarKind.Int64=>value.GetInt64(),ScalarKind.Float64=>value.GetDouble(),_=>throw new JsonException("Unsupported scalar.")},
        _=>throw new JsonException("Unsupported field shape.")};
    public bool TryGetToken(object value,out string typeName)
    {
        typeName=value switch {BinaryValue or BinaryValue[]=>"Binary",RecordValue record when record.Declaration==VariableValueDeclarations.StoredTransform=>"Transform",
            SequenceValue sequence when sequence.Declaration==TransformSequence=>"Transform",SequenceValue sequence when sequence.Declaration==VariableValueDeclarations.StoredRoiSequence=>"ROI",
            IReadOnlyStoredRaster=>"Image",_=>string.Empty};
        return typeName.Length!=0;
    }
    public void WriteValue(Utf8JsonWriter writer,string typeName,object value,JsonSerializerOptions options)
    {
        if(!TryGetToken(value,out var actual)||actual!=typeName)throw new JsonException("Complex value differs from its token.");
        switch(value)
        {
            case BinaryValue binary: writer.WriteStringValue(Convert.ToBase64String(binary.ToArray()));break;
            case BinaryValue[] binaries: writer.WriteStartArray();foreach(var binary in binaries)writer.WriteStringValue(Convert.ToBase64String(binary.ToArray()));writer.WriteEndArray();break;
            case IReadOnlyStoredRaster image: writer.WriteStringValue(WriteImage(image));break;
            case RecordValue record: VariableValueDeclarations.ValidateStoredTransform(record);WriteRecord(writer,record);break;
            case SequenceValue sequence: writer.WriteStartArray();foreach(var item in sequence){var record=(RecordValue)item!;if(typeName=="ROI")VariableValueDeclarations.ValidateStoredRoi(record);else VariableValueDeclarations.ValidateStoredTransform(record);WriteRecord(writer,record);}writer.WriteEndArray();break;
            default:throw new JsonException("Unsupported complex value.");
        }
    }
    private static void WriteRecord(Utf8JsonWriter writer,RecordValue record)
    {
        var fields=record.CopyFields();writer.WriteStartObject();foreach(var port in record.Declaration.Fields){if(!fields.TryGetValue(port.Id,out var value))continue;writer.WritePropertyName(port.Id);WriteField(writer,value);}writer.WriteEndObject();
    }
    private static void WriteField(Utf8JsonWriter writer,object? value)
    {
        switch(value){case RecordValue record:WriteRecord(writer,record);break;case SequenceValue sequence:writer.WriteStartArray();foreach(var item in sequence)WriteField(writer,item);writer.WriteEndArray();break;
            case string text:writer.WriteStringValue(text);break;case bool boolean:writer.WriteBooleanValue(boolean);break;case long integer:writer.WriteNumberValue(integer);break;case double number:writer.WriteNumberValue(number);break;case null:writer.WriteNullValue();break;default:throw new JsonException("Unsupported frozen field.");}
    }
    private FrozenStoredRaster ReadImage(JsonElement value)
    {
        var bytes=Convert.FromBase64String(value.GetString()??throw new JsonException("Image requires Base64 PNG."));
        using var image=Mat.FromImageData(bytes,ImreadModes.Unchanged);
        if(image.Empty())throw new JsonException("Image decode returned empty pixels.");
        var format=Format(image.Depth(),image.Channels());var stride=checked(image.Cols*BytesPerPixel(format));
        var layout=new StoredRasterLayout(image.Cols,image.Rows,stride,format);var pixels=new byte[layout.BufferLength];
        for(var row=0;row<image.Rows;row++)Marshal.Copy(image.Ptr(row),pixels,row*stride,stride);
        var result=new FrozenStoredRaster(layout,pixels);Scope.RegisterNew(result);return result;
    }
    private static string WriteImage(IReadOnlyStoredRaster image)
    {
        using var reader=image.AcquireReader();var layout=reader.Layout;
        var type=layout.Format switch {StoredRasterFormat.Gray8=>MatType.CV_8UC1,StoredRasterFormat.Gray16=>MatType.CV_16UC1,
            StoredRasterFormat.Bgr24=>MatType.CV_8UC3,StoredRasterFormat.Bgr48=>MatType.CV_16UC3,StoredRasterFormat.Bgra32=>MatType.CV_8UC4,_=>MatType.CV_16UC4};
        using var mat=new Mat(layout.Height,layout.Width,type);var width=checked(layout.Width*BytesPerPixel(layout.Format));
        for(var row=0;row<layout.Height;row++)Marshal.Copy(reader.GetRow(row)[..width].ToArray(),0,mat.Ptr(row),width);
        return Convert.ToBase64String(mat.ToBytes(".png"));
    }
    private static StoredRasterFormat Format(int depth,int channels)=>(depth,channels) switch {
        (0,1)=>StoredRasterFormat.Gray8,(2,1)=>StoredRasterFormat.Gray16,(0,3)=>StoredRasterFormat.Bgr24,(2,3)=>StoredRasterFormat.Bgr48,
        (0,4)=>StoredRasterFormat.Bgra32,(2,4)=>StoredRasterFormat.Bgra64,_=>throw new JsonException("PNG supports unsigned 8/16-bit, 1/3/4-channel images only.")};
    private static int BytesPerPixel(StoredRasterFormat format)=>format switch {StoredRasterFormat.Gray8=>1,StoredRasterFormat.Gray16=>2,StoredRasterFormat.Bgr24=>3,StoredRasterFormat.Bgr48=>6,StoredRasterFormat.Bgra32=>4,_=>8};
}
