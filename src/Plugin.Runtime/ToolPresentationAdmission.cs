using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Application-issued authority for presenting one actual document node.
/// Tool components receive public field contracts, never this host capability or its directory.</summary>
public sealed class ToolPresentationAdmission
{
    private readonly Func<bool> _isCurrent;
    internal RuntimeDirectorySnapshot Directory { get; }
    internal IToolDefinition Definition { get; }
    internal Type? EditorEntryType { get; }
    public string ToolId => Definition.ToolId;
    public CancellationToken RetirementToken { get; }

    internal ToolPresentationAdmission(RuntimeDirectorySnapshot directory, IToolDefinition definition,
        Func<bool> isCurrent, CancellationToken retirement)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        RetirementToken = retirement;
        EditorEntryType = directory.ResolveEditor(definition.ToolId)?.EntryType;
        EnsureCurrent();
    }

    public bool IsCurrent => !RetirementToken.IsCancellationRequested && Directory.IsActive && _isCurrent();
    internal void EnsureCurrent()
    {
        if (!IsCurrent) throw new InvalidOperationException("呈现来源节点、方案或组件目录已退出。");
    }
    internal RuntimeGenerationLease AcquireInvocation()
        =>AcquireInvocation("工具呈现");
    internal RuntimeGenerationLease AcquireInvocation(string owner)
    {
        var generation = Directory.AcquireInvocation(owner);
        try { EnsureCurrent(); return generation; }
        catch { generation.Dispose(); throw; }
    }
}
