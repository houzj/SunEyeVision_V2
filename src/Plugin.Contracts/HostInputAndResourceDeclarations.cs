using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>One immutable input port supplied by an explicitly admitted host capability.</summary>
public sealed record HostInputPort(string PortId, TypeRef Type);

/// <summary>Pure, immutable declaration; it does not acquire resources or execute a tool.</summary>
public sealed class HostInputDeclaration
{
    public HostInputDeclaration(string capabilityId, int version, IEnumerable<HostInputPort> ports, IEnumerable<string> services)
    {
        if (string.IsNullOrWhiteSpace(capabilityId) || version <= 0) throw new ArgumentException("Invalid host input capability identity.");
        CapabilityId = capabilityId; Version = version;
        var inputs = (ports ?? throw new ArgumentNullException(nameof(ports))).ToArray();
        var required = (services ?? throw new ArgumentNullException(nameof(services))).ToArray();
        if (inputs.Any(port => port is null || string.IsNullOrWhiteSpace(port.PortId) || port.Type is null) ||
            inputs.Select(port => port.PortId).Distinct(StringComparer.Ordinal).Count() != inputs.Length ||
            required.Any(string.IsNullOrWhiteSpace) || required.Distinct(StringComparer.Ordinal).Count() != required.Length)
            throw new ArgumentException("Host input ports and services must have unique nonempty identities.");
        Ports = Array.AsReadOnly(inputs.OrderBy(port => port.PortId, StringComparer.Ordinal).ToArray());
        Services = Array.AsReadOnly(required.OrderBy(service => service, StringComparer.Ordinal).ToArray());
    }
    public string CapabilityId { get; }
    public int Version { get; }
    public IReadOnlyList<HostInputPort> Ports { get; }
    public IReadOnlyList<string> Services { get; }
}

public enum ResourceReferenceCardinality { Scalar, Sequence }
public sealed record FileEmbeddingDeclaration(string EnabledFieldId,int MaximumFiles,string? DynamicListFieldId=null);
public sealed record ResourceFieldDeclaration(string FieldId, ResourcePurpose Purpose, ResourceReferenceCardinality Cardinality,
    FileEmbeddingDeclaration? Embedding=null);

public interface IToolHostInputDeclaration
{
    HostInputDeclaration HostInputs { get; }
}

public interface IToolResourceFieldDeclarations
{
    IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; }
}

public sealed record ToolHostDeclarationSnapshot(HostInputDeclaration? HostInputs, IReadOnlyList<ResourceFieldDeclaration> ResourceFields, string Fingerprint);

/// <summary>Checks declarations against the admitted schemas and creates a deterministic identity.</summary>
public static class ToolHostDeclarationRules
{
    public static string ValidateAndFingerprint(IToolDefinition definition) => Capture(definition).Fingerprint;

    public static ToolHostDeclarationSnapshot Capture(IToolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var inputs = (definition as IToolHostInputDeclaration)?.HostInputs;
        var suppliedFields = (definition as IToolResourceFieldDeclarations)?.ResourceFields;
        if (definition is IToolResourceFieldDeclarations && suppliedFields is null) throw new InvalidOperationException("Resource declarations are missing.");
        var fields = Array.AsReadOnly((suppliedFields ?? Array.Empty<ResourceFieldDeclaration>()).ToArray());
        if (definition is IToolHostInputDeclaration && inputs is null) throw new InvalidOperationException("Host input declaration is missing.");
        if (fields.Any(field => field is null || string.IsNullOrWhiteSpace(field.FieldId) || !Enum.IsDefined(field.Purpose) || !Enum.IsDefined(field.Cardinality)) ||
            fields.Select(field => field.FieldId).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new InvalidOperationException("Resource field declarations are invalid or duplicated.");
        var schema = definition.Describe();
        foreach (var port in inputs?.Ports ?? Array.Empty<HostInputPort>())
            if (schema.Inputs.SingleOrDefault(candidate => candidate.Id == port.PortId)?.Type != port.Type)
                throw new InvalidOperationException("Host input declaration differs from tool schema: " + port.PortId);
        var settings = (definition as IToolSettingsFields)?.SettingsSchema;
        foreach (var field in fields)
        {
            var type = settings?.Inputs.SingleOrDefault(candidate => candidate.Id == field.FieldId)?.Type;
            var valid = field.Cardinality == ResourceReferenceCardinality.Scalar
                ? type?.Shape == ValueShape.Scalar && type.Scalar == ScalarKind.Text
                : type?.Shape == ValueShape.Sequence && type.Element?.Shape == ValueShape.Scalar && type.Element.Scalar == ScalarKind.Text;
            if (!valid) throw new InvalidOperationException("Resource field declaration differs from settings schema: " + field.FieldId);
        }
        ValidateEmbedding(fields,settings);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(inputs is not null);
            if (inputs is not null)
            {
                writer.Write(inputs.CapabilityId); writer.Write(inputs.Version); writer.Write(inputs.Ports.Count);
                foreach (var port in inputs.Ports) { writer.Write(port.PortId); writer.Write(port.Type.Fingerprint); }
                writer.Write(inputs.Services.Count); foreach (var service in inputs.Services) writer.Write(service);
            }
            writer.Write(fields.Count);
            foreach (var field in fields.OrderBy(field => field.FieldId, StringComparer.Ordinal))
            {
                writer.Write(field.FieldId); writer.Write((int)field.Purpose); writer.Write((int)field.Cardinality);
                writer.Write(field.Embedding is not null);
                if(field.Embedding is {} embedding)
                {writer.Write(embedding.EnabledFieldId);writer.Write(embedding.MaximumFiles);writer.Write(embedding.DynamicListFieldId??string.Empty);}
            }
        }
        return new(inputs, fields, Convert.ToHexString(SHA256.HashData(stream.ToArray())));
    }

    public static void ValidateEmbedding(IReadOnlyList<ResourceFieldDeclaration> fields,ToolSchema? settings)
    {
        if(fields.Count(field=>field.Embedding is not null)>1)throw new InvalidOperationException("A tool declares one file-list embedding policy.");
        foreach(var field in fields.Where(field=>field.Embedding is not null))
        {
            var policy=field.Embedding!;
            bool Boolean(string id)=>settings?.Inputs.SingleOrDefault(port=>port.Id==id) is {Type:{} type,EditModes:FieldEditModes.Constant,Nullable:false,Required:true}
                &&type==TypeRef.ScalarOf(ScalarKind.Boolean);
            var target=settings?.Inputs.SingleOrDefault(port=>port.Id==field.FieldId);
            if(field.Purpose!=ResourcePurpose.FileRead||field.Cardinality!=ResourceReferenceCardinality.Sequence||policy.MaximumFiles is <1 or >10000
                ||target is not {EditModes:FieldEditModes.Constant,Nullable:false,Required:true}
                ||target.Type.Shape!=ValueShape.Sequence||target.Type.Element!=TypeRef.ScalarOf(ScalarKind.Text)
                ||!Boolean(policy.EnabledFieldId)||policy.DynamicListFieldId is {} dynamicField&&!Boolean(dynamicField))
                throw new InvalidOperationException("File embedding policy differs from the admitted settings: "+field.FieldId);
        }
    }
}
