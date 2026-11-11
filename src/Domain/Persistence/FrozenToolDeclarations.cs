using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Domain.Persistence;

/// <summary>Detached admitted declarations used only for one persistence operation; no runtime object or directory is retained.</summary>
public sealed class FrozenToolDeclarations
{
    public FrozenToolDeclarations(string toolId, ToolSchema schema, ToolSchema settingsSchema, ToolSchema displaySchema,
        IReadOnlyDictionary<string, ParameterReferenceBehavior> referenceBehaviors, ToolHostDeclarationSnapshot hostDeclarations,
        IReadOnlyList<ManualReferenceSource> manualReferenceSources, InputValues? displayDefaults=null, InputValues? settingsDefaults=null)
    {
        if(string.IsNullOrWhiteSpace(toolId))throw new ArgumentException("Tool identity is required.",nameof(toolId));
        ToolId=toolId; Schema=schema ?? throw new ArgumentNullException(nameof(schema));
        SettingsSchema=settingsSchema ?? throw new ArgumentNullException(nameof(settingsSchema));
        DisplaySchema=displaySchema ?? throw new ArgumentNullException(nameof(displaySchema));
        var defaults=displayDefaults?.CopyValues().ToDictionary(
            pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal);
        DisplayDefaults=new FieldSettings(DisplaySchema.Inputs,defaults??new Dictionary<string,ParameterSetting>());
        var recoverable=SettingsSchema.Inputs.Where(port=>port.DefaultPresence==FieldDefaultPresence.UseDeclaredDefault).ToArray();
        SettingsDefaults=new FieldSettings(recoverable,recoverable
            .Where(port=>settingsDefaults?.Contains(port.Id)==true)
            .ToDictionary(port=>port.Id,port=>(ParameterSetting)new ConstantSetting(settingsDefaults!.Get<object>(port.Id)),StringComparer.Ordinal));
        ArgumentNullException.ThrowIfNull(referenceBehaviors); ArgumentNullException.ThrowIfNull(hostDeclarations);
        var ports=Schema.Inputs.Concat(SettingsSchema.Inputs).Concat(DisplaySchema.Inputs).ToArray();
        if(ports.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=ports.Length)
            throw new ArgumentException("Combined input, settings and display field identities must be unique.");
        var copied=new Dictionary<string,ParameterReferenceBehavior>(StringComparer.Ordinal);
        foreach(var pair in referenceBehaviors)
        {
            var target=SettingsSchema.Inputs.SingleOrDefault(p=>p.Id==pair.Key) ?? throw new ArgumentException("Reference behavior requires a settings field.");
            ParameterReferenceRules.ValidateTarget(target,pair.Value);copied.Add(pair.Key,pair.Value);
        }
        ReferenceBehaviors=new ReadOnlyDictionary<string,ParameterReferenceBehavior>(copied);
        if(string.IsNullOrWhiteSpace(hostDeclarations.Fingerprint))throw new ArgumentException("Host declaration fingerprint is required.");
        var resources=hostDeclarations.ResourceFields?.ToArray() ?? throw new ArgumentException("Resource declarations are missing.");
        if(resources.Any(f=>f is null||string.IsNullOrWhiteSpace(f.FieldId)||!Enum.IsDefined(f.Purpose)||!Enum.IsDefined(f.Cardinality)) || resources.Select(f=>f.FieldId).Distinct(StringComparer.Ordinal).Count()!=resources.Length)
            throw new ArgumentException("Resource declaration identities are invalid or duplicated.");
        var input=hostDeclarations.HostInputs;
        ToolHostDeclarationRules.ValidateEmbedding(resources,SettingsSchema);
        var detached=input is null ? null : new HostInputDeclaration(input.CapabilityId,input.Version,input.Ports,input.Services);
        HostDeclarations=new ToolHostDeclarationSnapshot(detached,Array.AsReadOnly(resources),hostDeclarations.Fingerprint);
        ArgumentNullException.ThrowIfNull(manualReferenceSources);
        ManualReferenceSources=Array.AsReadOnly(manualReferenceSources
            .Select(source => new ManualReferenceSource(source.PortId, source.MemberPath)).ToArray());
    }
    public string ToolId { get; }
    public ToolSchema Schema { get; }
    public ToolSchema SettingsSchema { get; }
    public ToolSchema DisplaySchema { get; }
    public FieldSettings DisplayDefaults { get; }
    public FieldSettings SettingsDefaults { get; }
    public IReadOnlyDictionary<string,ParameterReferenceBehavior> ReferenceBehaviors { get; }
    public ToolHostDeclarationSnapshot HostDeclarations { get; }
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources { get; }
}
