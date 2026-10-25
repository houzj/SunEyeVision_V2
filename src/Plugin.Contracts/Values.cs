using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
namespace SunEyeVision.Plugin.Contracts;
public enum DataType { Boolean, Integer, Number, Text, NumberArray, Image }
public sealed record Port(string Id, TypeRef Type, bool Required=true, bool Nullable=false, NumericStorage? Storage=null);
public sealed class ToolSchema
{
    public ToolSchema(IEnumerable<Port> inputs, IEnumerable<Port> outputs)
    {
        Inputs=Array.AsReadOnly(inputs.ToArray()); Outputs=Array.AsReadOnly(outputs.ToArray());
        foreach(var group in new[]{Inputs,Outputs})
            if(group.Any(p=>p is null || string.IsNullOrWhiteSpace(p.Id)) || group.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=group.Count)
                throw new ArgumentException("Port identities must be nonempty and unique.");
        if (Inputs.Concat(Outputs).Any(p => p.Type is null))
            throw new ArgumentException("Ports require a declared type.");
        foreach(var port in Inputs.Concat(Outputs))NumericStorageRules.ValidateDeclaration(port);
        // IDs may contain punctuation. Length-prefixed fields avoid delimiter collisions
        // between one compound ID and several different ports.
        using var buffer=new MemoryStream();
        using(var writer=new BinaryWriter(buffer,Encoding.UTF8,true))
        {
            foreach(var group in new[]{Inputs,Outputs})
            {
                writer.Write(group.Count);
                foreach(var port in group)
                {
                    writer.Write(port.Id);writer.Write(port.Type.Fingerprint);
                    writer.Write(port.Required);writer.Write(port.Nullable);
                    writer.Write(port.Storage is null?-1:(int)port.Storage.Value);
                }
            }
        }
        Fingerprint=Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }
    public IReadOnlyList<Port> Inputs { get; }
    public IReadOnlyList<Port> Outputs { get; }
    public string Fingerprint { get; }
}
public sealed class InputValues
{
    private readonly Dictionary<string,object?> _values;
    private readonly IReadOnlyDictionary<string,Port> _ports;
    public InputValues(IEnumerable<Port> ports,IReadOnlyDictionary<string,object?> values)
    {
        var specs=ports.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        _ports=new ReadOnlyDictionary<string,Port>(specs);
        if(values.Keys.Any(k=>!specs.ContainsKey(k)))throw new ArgumentException("Undeclared input/output identity.");
        _values=new(StringComparer.Ordinal);
        foreach(var p in specs.Values)
        {
            if(!values.TryGetValue(p.Id,out var v)){ if(p.Required)throw new ArgumentException($"Missing required port {p.Id}"); continue; }
            _values[p.Id]=ValueRules.Freeze(p,v);
        }
    }
    public bool Contains(string id)=>_values.ContainsKey(id);
    public DeclaredValueReader Read(string id)
    {
        if(!_ports.TryGetValue(id,out var port))throw new KeyNotFoundException(id);
        return _values.TryGetValue(id,out var value)?new(port.Type,value):new(port.Type,null,true);
    }
    public T Get<T>(string id)
    {
        if(!_values.TryGetValue(id,out var v))throw new KeyNotFoundException(id);
        if(v is T t)return (T)ValueRules.Copy(t)!;
        if(v is null && default(T) is null)return default!;
        throw new InvalidOperationException($"Port {id} is not {typeof(T).Name}");
    }
    public IReadOnlyDictionary<string,object?> CopyValues()=>new ReadOnlyDictionary<string,object?>(_values.ToDictionary(p=>p.Key,p=>ValueRules.Copy(p.Value),StringComparer.Ordinal));
    public IEnumerable<IReadOnlyImage> Images()=>_values.Values.SelectMany(ValueRules.Images).Distinct(ReferenceEqualityComparer.Instance).Cast<IReadOnlyImage>();
    public IEnumerable<IReadOnlyRegion> Regions()=>_values.Values.SelectMany(ValueRules.Regions).Distinct(ReferenceEqualityComparer.Instance).Cast<IReadOnlyRegion>();
}
public static class ValueRules
{
    public static object? Freeze(Port port,object? value)
    {
        NumericStorageRules.ValidateValue(port,value);
        if(value is null)return port.Nullable?null:throw new ArgumentException($"Port {port.Id} disallows null");
        if(port.Type.Shape==ValueShape.Record)
        {
            if(value is RecordValue record && record.Declaration==port.Type)return record;
            if(value is IReadOnlyDictionary<string,object?> fields)return new RecordValue(port.Type,fields);
            throw new ArgumentException($"Invalid record for {port.Id}");
        }
        if(port.Type.Shape==ValueShape.Sequence && !(port.Type.Element==(TypeRef)DataType.Number && value is double[]))
        {
            if(value is SequenceValue sequence && sequence.Declaration==port.Type)return sequence;
            if(value is Array sourceArray && sourceArray.Rank==1)return new SequenceValue(port.Type,sourceArray.Cast<object?>());
            throw new ArgumentException($"Invalid sequence for {port.Id}");
        }
        bool valid = port.Type.Shape switch {
            ValueShape.Scalar => port.Type.Scalar switch {
                ScalarKind.Boolean => value is bool, ScalarKind.Int64 => value is long,
                ScalarKind.Float64 => value is double d && double.IsFinite(d), ScalarKind.Text => value is string,
                _ => false },
            ValueShape.Sequence => port.Type.Element == (TypeRef)DataType.Number &&
                value is double[] a && port.Type.Count.Accepts(a.Length) && a.All(double.IsFinite),
            ValueShape.Resource => port.Type.Resource switch {
                ResourceKind.Image => value is IReadOnlyImage,
                ResourceKind.Region => value is IReadOnlyRegion,
                _ => false },
            ValueShape.Geometry => value is GeometryValue geometry && geometry.Kind==port.Type.Geometry && geometry.Frame==port.Type.Frame,
            _ => false };
        if(!valid)throw new ArgumentException($"Invalid value type for {port.Id} ({port.Type})");
        if(value is IReadOnlyImage image)ResourceContractRules.ValidateImage(image);
        if(value is IReadOnlyRegion region)ResourceContractRules.ValidateRegion(region);
        return value is double[] array?array.Clone():value;
    }
    internal static object? Copy(object? value)=>value is double[] array?array.Clone():value;
    internal static IEnumerable<IReadOnlyImage> Images(object? value)
    {
        if(value is IReadOnlyImage image)yield return image;
        else if(value is RecordValue record){foreach(var item in record.Images())yield return item;}
        else if(value is SequenceValue sequence){foreach(var element in sequence)foreach(var item in Images(element))yield return item;}
    }
    internal static IEnumerable<IReadOnlyRegion> Regions(object? value)
    {
        if(value is IReadOnlyRegion region)yield return region;
        else if(value is IReadOnlyImage image)yield return image.EffectiveDomain;
        else if(value is RecordValue record){foreach(var item in record.Regions())yield return item;}
        else if(value is SequenceValue sequence){foreach(var element in sequence)foreach(var item in Regions(element))yield return item;}
    }
}
public sealed class OutputReference
{
    public OutputReference(string nodeId,string portId,TypeRef type,IEnumerable<string>? memberFieldPath=null,
        BindingSelection? selection=null,string? scopeId=null,NumericStorage? storage=null)
    {
        if(string.IsNullOrWhiteSpace(nodeId)||string.IsNullOrWhiteSpace(portId))throw new ArgumentException("Stable node and field identities are required.");
        if(scopeId is not null && string.IsNullOrWhiteSpace(scopeId))throw new ArgumentException("An explicit scope identity cannot be empty.",nameof(scopeId));
        NodeId=nodeId;PortId=portId;Type=type??throw new ArgumentNullException(nameof(type));
        NumericStorageRules.ValidateDeclaration(new Port(portId,type,Storage:storage));
        Storage=NumericStorageRules.Effective(type,storage);
        MemberFieldPath=Array.AsReadOnly((memberFieldPath??Array.Empty<string>()).ToArray());
        if(MemberFieldPath.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Member field identities must be nonempty.");
        Selection=selection??BindingSelection.All;ScopeId=scopeId;
    }
    public string NodeId {get;}
    public string PortId {get;}
    public TypeRef Type {get;}
    public NumericStorage? Storage {get;}
    public string? ScopeId {get;}
    public IReadOnlyList<string> MemberFieldPath {get;}
    public BindingSelection Selection {get;}
}
public static class BindingValidator
{
    public static Diagnostic? Validate(OutputReference reference,ToolSchema source)
    {
        var p=source.Outputs.SingleOrDefault(p=>p.Id==reference.PortId);
        return p is null?new("missing-output","Output removed",reference.PortId):p.Type!=reference.Type?new("type-changed","Output type changed",reference.PortId):null;
    }
}
