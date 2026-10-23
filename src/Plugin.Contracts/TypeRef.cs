using System.Security.Cryptography;
using System.Text;

namespace SunEyeVision.Plugin.Contracts;

public enum ValueShape { Scalar, Sequence, Record, Geometry, Resource }
public enum ScalarKind { Boolean, Int64, Float64, Text }
public enum GeometryKind { Point, Line, Circle, Arc, OrientedRectangle, Ellipse, Polygon }
public enum ResourceKind { Image, Model, Contour }

public readonly record struct CountConstraint
{
    public CountConstraint(int minimum, int? maximum = null)
    {
        if (minimum < 0 || maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Invalid declared count range.");
        Minimum = minimum;
        Maximum = maximum;
    }
    public int Minimum { get; }
    public int? Maximum { get; }
    public bool Accepts(int count) => count >= Minimum && (Maximum is null || count <= Maximum);
    public static CountConstraint Exactly(int count) => new(count, count);
    public static CountConstraint Any => new(0);
}

/// <summary>Immutable semantic declaration. A runtime value never changes this descriptor.</summary>
public sealed class TypeRef : IEquatable<TypeRef>
{
    private TypeRef(ValueShape shape, ScalarKind? scalar = null, TypeRef? element = null,
        CountConstraint? count = null, string? recordId = null, IEnumerable<Port>? fields = null,
        GeometryKind? geometry = null, ResourceKind? resource = null)
    {
        Shape = shape;
        Scalar = scalar;
        Element = element;
        Count = count ?? CountConstraint.Exactly(1);
        RecordId = recordId;
        Fields = Array.AsReadOnly((fields ?? Array.Empty<Port>()).ToArray());
        if (Fields.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id)) ||
            Fields.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Fields.Count)
            throw new ArgumentException("Record fields require unique nonempty identities.");
        Geometry = geometry;
        Resource = resource;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write((int)Shape);
            writer.Write(Scalar is null ? -1 : (int)Scalar.Value);
            writer.Write(Element?.Fingerprint ?? string.Empty);
            writer.Write(Count.Minimum);
            writer.Write(Count.Maximum ?? -1);
            writer.Write(RecordId ?? string.Empty);
            writer.Write(Geometry is null ? -1 : (int)Geometry.Value);
            writer.Write(Resource is null ? -1 : (int)Resource.Value);
            writer.Write(Fields.Count);
            foreach (var field in Fields)
            {
                writer.Write(field.Id);
                writer.Write(field.Type.Fingerprint);
                writer.Write(field.Required);
                writer.Write(field.Nullable);
            }
        }
        Fingerprint = Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public ValueShape Shape { get; }
    public ScalarKind? Scalar { get; }
    public TypeRef? Element { get; }
    public CountConstraint Count { get; }
    public string? RecordId { get; }
    public IReadOnlyList<Port> Fields { get; }
    public GeometryKind? Geometry { get; }
    public ResourceKind? Resource { get; }
    public string Fingerprint { get; }

    public static TypeRef ScalarOf(ScalarKind kind) => Enum.IsDefined(kind)
        ? new(ValueShape.Scalar, scalar: kind) : throw new ArgumentOutOfRangeException(nameof(kind));
    public static TypeRef SequenceOf(TypeRef element, CountConstraint count = default) =>
        new(ValueShape.Sequence, element: element ?? throw new ArgumentNullException(nameof(element)), count: count);
    public static TypeRef RecordOf(string id, IEnumerable<Port> fields) =>
        new(ValueShape.Record, recordId: !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentException("Record identity is required.", nameof(id)),
            fields: fields ?? throw new ArgumentNullException(nameof(fields)));
    public static TypeRef GeometryOf(GeometryKind kind) => Enum.IsDefined(kind)
        ? new(ValueShape.Geometry, geometry: kind) : throw new ArgumentOutOfRangeException(nameof(kind));
    public static TypeRef ResourceOf(ResourceKind kind) => Enum.IsDefined(kind)
        ? new(ValueShape.Resource, resource: kind) : throw new ArgumentOutOfRangeException(nameof(kind));

    // Source conveniences for existing foundation consumers; these map into this one declaration.
    // They are not a second runtime type catalog or legacy binary compatibility.
    public static implicit operator TypeRef(DataType type) => type switch
    {
        DataType.Boolean => ScalarOf(ScalarKind.Boolean),
        DataType.Integer => ScalarOf(ScalarKind.Int64),
        DataType.Number => ScalarOf(ScalarKind.Float64),
        DataType.Text => ScalarOf(ScalarKind.Text),
        DataType.NumberArray => SequenceOf(ScalarOf(ScalarKind.Float64)),
        DataType.Image => ResourceOf(ResourceKind.Image),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public bool Equals(TypeRef? other) => other is not null && Fingerprint == other.Fingerprint;
    public override bool Equals(object? obj) => obj is TypeRef other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint);
    public static bool operator ==(TypeRef? left, TypeRef? right) => ReferenceEquals(left, right) || left?.Equals(right) == true;
    public static bool operator !=(TypeRef? left, TypeRef? right) => !(left == right);
    public override string ToString() => Shape switch
    {
        ValueShape.Scalar => Scalar.ToString()!,
        ValueShape.Sequence => $"Sequence<{Element}>[{Count.Minimum}..{Count.Maximum?.ToString() ?? "*"}]",
        ValueShape.Record => $"Record<{RecordId}>",
        ValueShape.Geometry => $"Geometry<{Geometry}>",
        _ => $"Resource<{Resource}>"
    };
}
