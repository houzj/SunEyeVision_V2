using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Tool;

/// <summary>
/// 宿主可注入的工具目录边界。插件加载器依赖此接口，不再直接依赖静态注册表。
/// </summary>
public interface IToolCatalog
{
    int Count { get; }

    void Register(Type toolType);

    IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> toolTypes);
    IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> toolTypes, IReadOnlyList<ToolComponentRegistration> components)
    {
        if(components.Count!=0)throw new NotSupportedException("This catalog does not support physical tool components.");
        return PrepareRegistration(toolTypes);
    }

    void Clear();

    bool Contains(string toolId);

    IToolPlugin? CreateTool(string toolId);

    ToolParameters CreateParameters(string toolId);

    ToolMetadata? GetMetadata(string toolId);

    IReadOnlyList<ToolMetadata> GetAllMetadata();
}

public sealed record ToolComponentRegistration(SunEyeVision.Plugin.Contracts.ToolComponentMetadata Metadata, Type EntryType);

/// <summary>One complete plugin assembly's prepared, unpublished catalog state.</summary>
public interface IPreparedToolRegistration
{
    IToolCatalogCommitScope EnterCommit();
}

/// <summary>Synchronous scope. Validate all participating scopes before Publish; publication only swaps prepared state.</summary>
public interface IToolCatalogCommitScope : IDisposable
{
    void Publish();
}
