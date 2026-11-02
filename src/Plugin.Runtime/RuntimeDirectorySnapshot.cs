using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>A fully validated generation of admitted runtime components. No UI types or mutable global registration.</summary>
public sealed class RuntimeDirectorySnapshot
{
    private readonly IReadOnlyDictionary<string,ToolComponentRegistration> _runtimes;
    private RuntimeDirectorySnapshot(Dictionary<string,ToolComponentRegistration> runtimes)
    {
        _runtimes=new ReadOnlyDictionary<string,ToolComponentRegistration>(runtimes);
        Generation=Guid.NewGuid();
    }
    public Guid Generation { get; }
    public IReadOnlyCollection<string> ToolIds => _runtimes.Keys.ToArray();
    public bool Contains(string toolId)=>_runtimes.ContainsKey(toolId);
    public IToolDefinition CreateDefinition(string toolId)
    {
        if(!_runtimes.TryGetValue(toolId,out var registration))throw new InvalidOperationException("Required runtime is not registered: "+toolId);
        return (IToolDefinition)Activator.CreateInstance(registration.EntryType)!;
    }
    public static RuntimeDirectorySnapshot Prepare(IEnumerable<ToolComponentRegistration> candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var all=candidate.ToArray();
        _=ToolComponentCatalog.Prepare(all.Select(c=>c.Metadata));
        var runtimes=new Dictionary<string,ToolComponentRegistration>(StringComparer.Ordinal);
        foreach(var component in all.Where(c=>c.Metadata.Role==ToolComponentRole.Runtime))
        {
            RuntimeComponentAdmission.Validate(component);
            if(!runtimes.TryAdd(component.Metadata.ToolId,component))throw new InvalidOperationException("Duplicate runtime tool identity.");
        }
        if(runtimes.Count==0)throw new InvalidOperationException("The admitted directory contains no runtime tools.");
        return new RuntimeDirectorySnapshot(runtimes);
    }
}
