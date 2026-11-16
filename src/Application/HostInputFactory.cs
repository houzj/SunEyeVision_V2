using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Immutable host facts issued by an admitted document session.</summary>
public sealed class HostInputFactoryContext
{
    internal HostInputFactoryContext(ScopedNodeIdentity node, Guid editSessionId, Guid nodeGeneration,
        CancellationToken nodeRetirement, string toolId, ToolSchema inputSchema, ToolSchema settingsSchema,
        FieldSettings fields, IReadOnlyDictionary<string, ConstantSetting> currentValues,
        HostInputDeclaration declaration, IResourcePathResolver resourcePaths, Action validate, Guid? debugScopeId = null, CancellationToken debugRetirement = default,
        IReadOnlyDictionary<string,Guid>? configurationRevisions=null)
    {
        Node = node; EditSessionId = editSessionId; NodeGeneration = nodeGeneration; NodeRetirement = nodeRetirement;
        ToolId = toolId; InputSchema = inputSchema; SettingsSchema = settingsSchema; Fields = fields;
        CurrentValues = currentValues; Declaration = declaration; ResourcePaths = resourcePaths; _validate = validate;
        DebugScopeId = debugScopeId; DebugRetirement = debugRetirement;
        ConfigurationRevisions=new System.Collections.ObjectModel.ReadOnlyDictionary<string,Guid>(
            configurationRevisions?.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal));
    }
    private readonly Action _validate;
    public void EnsureCurrent() => _validate();
    public ScopedNodeIdentity Node { get; }
    public Guid EditSessionId { get; }
    public Guid? DebugScopeId { get; }
    public CancellationToken DebugRetirement { get; }
    public Guid NodeGeneration { get; }
    public CancellationToken NodeRetirement { get; }
    public string ToolId { get; }
    public ToolSchema InputSchema { get; }
    public ToolSchema SettingsSchema { get; }
    public FieldSettings Fields { get; }
    public IReadOnlyDictionary<string, ConstantSetting> CurrentValues { get; }
    public HostInputDeclaration Declaration { get; }
    public IResourcePathResolver ResourcePaths { get; }
    public IReadOnlyDictionary<string,Guid> ConfigurationRevisions {get;}
}

/// <summary>Host composition only; returned preparers transfer to a successful run capture.</summary>
public interface IHostInputFactory
{
    IHostInputPreparer? Create(HostInputFactoryContext context);
}

/// <summary>Read-only facts from one fully settled host preparer; not a plugin execution contract.</summary>
public sealed record HostReadCompletion(Guid EditSessionId, Guid NodeGeneration, ScopedNodeIdentity Node, RunIdentity Run, bool StopAfterCurrent);
public interface IHostInputRoundObserver
{
    IReadOnlyList<HostReadCompletion> CaptureCompletedReads();
}
