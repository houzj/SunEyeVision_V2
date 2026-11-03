using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Immutable host facts issued by an admitted document session.</summary>
public sealed class HostInputFactoryContext
{
    internal HostInputFactoryContext(ScopedNodeIdentity node, Guid editSessionId, Guid nodeGeneration,
        CancellationToken nodeRetirement, string toolId, ToolSchema inputSchema, ToolSchema settingsSchema,
        FieldSettings fields, IReadOnlyDictionary<string, ConstantSetting> currentValues,
        HostInputDeclaration declaration, IResourcePathResolver resourcePaths, Action validate)
    {
        Node = node; EditSessionId = editSessionId; NodeGeneration = nodeGeneration; NodeRetirement = nodeRetirement;
        ToolId = toolId; InputSchema = inputSchema; SettingsSchema = settingsSchema; Fields = fields;
        CurrentValues = currentValues; Declaration = declaration; ResourcePaths = resourcePaths; _validate = validate;
    }
    private readonly Action _validate;
    public void EnsureCurrent() => _validate();
    public ScopedNodeIdentity Node { get; }
    public Guid EditSessionId { get; }
    public Guid NodeGeneration { get; }
    public CancellationToken NodeRetirement { get; }
    public string ToolId { get; }
    public ToolSchema InputSchema { get; }
    public ToolSchema SettingsSchema { get; }
    public FieldSettings Fields { get; }
    public IReadOnlyDictionary<string, ConstantSetting> CurrentValues { get; }
    public HostInputDeclaration Declaration { get; }
    public IResourcePathResolver ResourcePaths { get; }
}

/// <summary>Host composition only; returned preparers transfer to a successful run capture.</summary>
public interface IHostInputFactory
{
    IHostInputPreparer? Create(HostInputFactoryContext context);
}
