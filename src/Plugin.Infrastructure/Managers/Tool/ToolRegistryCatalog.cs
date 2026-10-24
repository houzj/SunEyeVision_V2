using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Tool;

/// <summary>
/// 迁移期目录适配器。把新实例边界映射到现有 <see cref="ToolRegistry"/>，
/// 使宿主调用点可先完成依赖注入，再替换内部存储。
/// </summary>
public sealed class ToolRegistryCatalog : IToolCatalog
{
    public int Count => ToolRegistry.GetToolCount();

    public void Register(Type toolType) => ToolRegistry.RegisterTool(toolType);

    public IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> toolTypes)=>ToolRegistry.PrepareRegistration(toolTypes);

    public void Clear() => ToolRegistry.ClearAll();

    public bool Contains(string toolId) => ToolRegistry.ToolExists(toolId);

    public IToolPlugin? CreateTool(string toolId) => ToolRegistry.CreateToolInstance(toolId);

    public ToolParameters CreateParameters(string toolId) => ToolRegistry.CreateParameters(toolId);

    public ToolMetadata? GetMetadata(string toolId) => ToolRegistry.GetToolMetadata(toolId);

    public IReadOnlyList<ToolMetadata> GetAllMetadata() => ToolRegistry.GetAllToolMetadata();
}
