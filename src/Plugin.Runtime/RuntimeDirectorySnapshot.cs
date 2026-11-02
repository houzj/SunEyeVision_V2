using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One fully validated generation. Hosts retain this instance for the whole configuration and invocation lifetime.</summary>
public sealed class RuntimeDirectorySnapshot
{
    private sealed record Entry(ToolComponentRegistration Registration,Type SettingsType,Type OutputType,string Schema,string? SettingsSchema);
    private readonly IReadOnlyDictionary<string,Entry> _runtimes;
    private RuntimeDirectorySnapshot(Dictionary<string,Entry> runtimes, ToolComponentRegistration[] components)
    {
        _runtimes=new ReadOnlyDictionary<string,Entry>(runtimes);
        Components=Array.AsReadOnly(components);
        Generation=Guid.NewGuid();
    }
    public Guid Generation { get; }
    public IReadOnlyList<ToolComponentRegistration> Components { get; }
    public IReadOnlyCollection<string> ToolIds => _runtimes.Keys.ToArray();
    public bool Contains(string toolId)=>_runtimes.ContainsKey(toolId);
    public IToolDefinition CreateDefinition(string toolId)
    {
        if(!_runtimes.TryGetValue(toolId,out var entry))throw new InvalidOperationException("Required runtime is not registered: "+toolId);
        var definition=(IToolDefinition)Activator.CreateInstance(entry.Registration.EntryType)!;
        var settingsSchema=(definition as IToolSettingsFields)?.SettingsSchema.Fingerprint;
        if(definition.ToolId!=toolId || definition.SettingsType!=entry.SettingsType || definition.OutputType!=entry.OutputType ||
            definition.Describe().Fingerprint!=entry.Schema || settingsSchema!=entry.SettingsSchema)
            throw new InvalidOperationException("Runtime definition differs from its admitted directory generation: "+toolId);
        return definition;
    }
    internal static RuntimeDirectorySnapshot Prepare(IEnumerable<ToolComponentRegistration> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var all=candidate.ToArray();
        _=ToolComponentCatalog.Prepare(all.Select(c=>c.Metadata));
        var runtimes=new Dictionary<string,Entry>(StringComparer.Ordinal);
        foreach(var component in all.Where(c=>c.Metadata.Role==ToolComponentRole.Runtime))
        {
            RuntimeComponentAdmission.Validate(component);
            var definition=(IToolDefinition)Activator.CreateInstance(component.EntryType)!;
            var entry=new Entry(component,definition.SettingsType,definition.OutputType,definition.Describe().Fingerprint,
                (definition as IToolSettingsFields)?.SettingsSchema.Fingerprint);
            if(!runtimes.TryAdd(component.Metadata.ToolId,entry))throw new InvalidOperationException("Duplicate runtime tool identity.");
        }
        if(runtimes.Count==0)throw new InvalidOperationException("The admitted directory contains no runtime tools.");
        return new RuntimeDirectorySnapshot(runtimes,all);
    }
}
