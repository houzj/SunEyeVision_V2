using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Immutable, admitted host facts captured within the document's run reservation.</summary>
public sealed record HostInputFactoryContext(ScopedNodeIdentity Node, string ToolId, ToolSchema InputSchema,
    ToolSchema SettingsSchema, FieldSettings Fields, IReadOnlyDictionary<string, ConstantSetting> CurrentValues,
    HostInputDeclaration Declaration, IResourcePathResolver ResourcePaths);

/// <summary>Host composition only; returned preparers transfer to a successful run capture.</summary>
public interface IHostInputFactory
{
    IHostInputPreparer? Create(HostInputFactoryContext context);
}
