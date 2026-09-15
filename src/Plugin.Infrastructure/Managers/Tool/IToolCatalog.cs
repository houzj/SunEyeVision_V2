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

    void Clear();

    bool Contains(string toolId);

    IToolPlugin? CreateTool(string toolId);

    ToolParameters CreateParameters(string toolId);

    ToolMetadata? GetMetadata(string toolId);

    IReadOnlyList<ToolMetadata> GetAllMetadata();
}
