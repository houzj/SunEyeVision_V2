using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One fully validated generation. Hosts retain this instance for the whole configuration and invocation lifetime.</summary>
public sealed class RuntimeDirectorySnapshot
{
    private sealed record Entry(ToolComponentRegistration Registration,Type SettingsType,Type OutputType,string Schema,string? SettingsSchema,ToolHostDeclarationSnapshot HostDeclarations,ToolCatalogDescriptor Catalog);
    private readonly IReadOnlyDictionary<string,Entry> _runtimes;
    private readonly Func<string,ToolComponentRegistration?>? _editorResolver;
    private readonly object _lifetimeGate=new();
    private bool _retiring;
    private int _leases;
    private readonly Dictionary<Guid,string> _leaseOwners=new();
    private readonly TaskCompletionSource _drained=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _retirementCancellation=new();
    private RuntimeDirectorySnapshot(Dictionary<string,Entry> runtimes, ToolComponentRegistration[] components,
        IReadOnlyList<ToolComponentMetadata> componentMetadata,Func<string,ToolComponentRegistration?>? editorResolver)
    {
        _runtimes=new ReadOnlyDictionary<string,Entry>(runtimes);
        Components=Array.AsReadOnly(components);
        ComponentMetadata=Array.AsReadOnly(componentMetadata.ToArray());
        _editorResolver=editorResolver;
        Generation=Guid.NewGuid();
    }
    public Guid Generation { get; }
    public bool IsActive => !Volatile.Read(ref _retiring);
    internal IReadOnlyList<ToolComponentRegistration> Components { get; }
    internal IReadOnlyList<ToolComponentMetadata> ComponentMetadata { get; }
    internal ToolComponentRegistration? ResolveEditor(string toolId)
        =>_editorResolver is not null
            ? _editorResolver(toolId)
            : Components.SingleOrDefault(component=>component.Metadata.Role==ToolComponentRole.Editor&&component.Metadata.ToolId==toolId);
    public IReadOnlyCollection<string> ToolIds => _runtimes.Keys.ToArray();
    public bool Contains(string toolId)=>_runtimes.ContainsKey(toolId);
    public ToolCatalogDescriptor? GetCatalog(string toolId)=>_runtimes.TryGetValue(toolId,out var entry)?entry.Catalog:null;
    internal IToolDefinition CreateDefinition(string toolId)
    {
        using var creation=AcquireInvocation();
        if(!_runtimes.TryGetValue(toolId,out var entry))throw new InvalidOperationException("Required runtime is not registered: "+toolId);
        var definition=(IToolDefinition)Activator.CreateInstance(entry.Registration.EntryType)!;
        var settingsSchema=(definition as IToolSettingsFields)?.SettingsSchema.Fingerprint;
        if(definition.ToolId!=toolId || definition.SettingsType!=entry.SettingsType || definition.OutputType!=entry.OutputType ||
            definition.Describe().Fingerprint!=entry.Schema || settingsSchema!=entry.SettingsSchema || ToolHostDeclarationRules.ValidateAndFingerprint(definition)!=entry.HostDeclarations.Fingerprint)
            throw new InvalidOperationException("Runtime definition differs from its admitted directory generation: "+toolId);
        return definition;
    }
    /// <summary>Returns validated declarations from the active generation, never a UI or tool-name lookup.</summary>
    public IReadOnlyList<ResourceFieldDeclaration> GetResourceFields(string toolId)
    {
        using var lease=AcquireInvocation();
        return _runtimes.TryGetValue(toolId,out var entry) ? entry.HostDeclarations.ResourceFields : throw new InvalidOperationException("Required runtime is not registered: "+toolId);
    }
    public HostInputDeclaration? GetHostInputs(string toolId)
    {
        using var lease=AcquireInvocation();
        return _runtimes.TryGetValue(toolId,out var entry) ? entry.HostDeclarations.HostInputs : throw new InvalidOperationException("Required runtime is not registered: "+toolId);
    }
    /// <summary>Hold one accepted invocation or retained result until its final reader releases it.</summary>
    public RuntimeGenerationLease AcquireInvocation()=>AcquireInvocation("未标记租约");
    public RuntimeGenerationLease AcquireInvocation(string owner)
    {
        if(string.IsNullOrWhiteSpace(owner))owner="未标记租约";
        lock(_lifetimeGate)
        {
            if(_retiring)throw new InvalidOperationException("Runtime directory is retiring.");
            _leases++;
            var id=Guid.NewGuid();_leaseOwners.Add(id,owner);
            return new RuntimeGenerationLease(this,id);
        }
    }
    internal string DescribeLiveLeases()
    {
        lock(_lifetimeGate)
        {
            if(_leaseOwners.Count==0)return "无活动租约";
            return string.Join("；",_leaseOwners.Values.GroupBy(value=>value,StringComparer.Ordinal)
                .OrderBy(group=>group.Key,StringComparer.Ordinal)
                .Select(group=>$"{group.Key}×{group.Count()}"));
        }
    }
    internal Task BeginRetirement()
    {
        lock(_lifetimeGate)
        {
            Volatile.Write(ref _retiring,true);
            if(_leases==0)_drained.TrySetResult();
            return _drained.Task;
        }
    }
    internal void SignalRetirement()=>_retirementCancellation.Cancel();
    internal CancellationToken RetirementToken=>_retirementCancellation.Token;
    internal void ReleaseInvocation(Guid id)
    {
        lock(_lifetimeGate)
        {
            _leaseOwners.Remove(id);
            if(--_leases<0)throw new InvalidOperationException("Runtime generation lease count underflow.");
            if(_retiring&&_leases==0)_drained.TrySetResult();
        }
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
                (definition as IToolSettingsFields)?.SettingsSchema.Fingerprint,ToolHostDeclarationRules.Capture(definition),((IToolCatalogProvider)definition).Catalog);
            if(!runtimes.TryAdd(component.Metadata.ToolId,entry))throw new InvalidOperationException("Duplicate runtime tool identity.");
        }
        if(runtimes.Count==0)throw new InvalidOperationException("The admitted directory contains no runtime tools.");
        return new RuntimeDirectorySnapshot(runtimes,all,all.Select(component=>component.Metadata).ToArray(),null);
    }
    internal static RuntimeDirectorySnapshot PrepareRuntimeWithEditorResolver(IEnumerable<ToolComponentRegistration> runtimeCandidate,
        IEnumerable<ToolComponentMetadata> componentMetadata,Func<string,ToolComponentRegistration?> editorResolver)
    {
        ArgumentNullException.ThrowIfNull(runtimeCandidate);
        ArgumentNullException.ThrowIfNull(componentMetadata);
        ArgumentNullException.ThrowIfNull(editorResolver);
        var all=runtimeCandidate.ToArray();
        _=ToolComponentCatalog.Prepare(componentMetadata);
        var runtimes=new Dictionary<string,Entry>(StringComparer.Ordinal);
        foreach(var component in all)
        {
            RuntimeComponentAdmission.Validate(component);
            var definition=(IToolDefinition)Activator.CreateInstance(component.EntryType)!;
            var entry=new Entry(component,definition.SettingsType,definition.OutputType,definition.Describe().Fingerprint,
                (definition as IToolSettingsFields)?.SettingsSchema.Fingerprint,ToolHostDeclarationRules.Capture(definition),((IToolCatalogProvider)definition).Catalog);
            if(!runtimes.TryAdd(component.Metadata.ToolId,entry))throw new InvalidOperationException("Duplicate runtime tool identity.");
        }
        if(runtimes.Count==0)throw new InvalidOperationException("The admitted directory contains no runtime tools.");
        return new RuntimeDirectorySnapshot(runtimes,all,componentMetadata.ToArray(),editorResolver);
    }
}

/// <summary>A single accepted generation reference. Dispose exactly once when all owners have transferred or released it.</summary>
public sealed class RuntimeGenerationLease : IDisposable
{
    private RuntimeDirectorySnapshot? _directory;
    private readonly Guid _id;
    internal RuntimeGenerationLease(RuntimeDirectorySnapshot directory,Guid id){_directory=directory;_id=id;}
    public RuntimeDirectorySnapshot Directory=>Volatile.Read(ref _directory)??throw new ObjectDisposedException(nameof(RuntimeGenerationLease));
    public CancellationToken RetirementToken=>Directory.RetirementToken;
    public void Dispose()=>Interlocked.Exchange(ref _directory,null)?.ReleaseInvocation(_id);
}
