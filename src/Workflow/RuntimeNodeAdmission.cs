using System;
using System.Linq;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

/// <summary>同一准入代次上的正式运行节点；暂停的控制流不进入工具执行链。</summary>
public static class RuntimeNodeAdmission
{
    public static bool IsPausedControlFlow(string? toolType, string? subroutineId) =>
        !string.IsNullOrWhiteSpace(subroutineId) ||
        string.Equals(toolType, "Subroutine", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(toolType, "Condition", StringComparison.OrdinalIgnoreCase);

    public static void Require(RuntimeDirectorySnapshot directory, WorkflowNodeBase node)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(node);
        RequireShape(node);
        if (!directory.IsActive) throw new InvalidOperationException("工具运行目录已退役。");
        if (string.IsNullOrWhiteSpace(node.ToolType) || !directory.Contains(node.ToolType))
            throw new InvalidOperationException("工具未准入：" + node.ToolType);
        var configuration = node.RuntimeConfiguration ?? throw new InvalidOperationException("节点缺少已准入 Runtime 配置：" + node.Id);
        if (!ReferenceEquals(configuration.Directory, directory) || !string.Equals(configuration.ToolId, node.ToolType, StringComparison.Ordinal))
            throw new InvalidOperationException("节点工具身份或目录代次不一致：" + node.Id);

    }
    /// <summary>快照复制前拒绝旧参数与暂停控制流，避免先反序列化未经准入的旧工具。</summary>
    public static void RequireShape(WorkflowNodeBase node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (IsPausedControlFlow(node.ToolType, node.SubroutineId))
            throw new InvalidOperationException("Group / 控制流当前尚未接入正式运行：" + node.ToolType);
        var configuration = node.RuntimeConfiguration ?? throw new InvalidOperationException("节点缺少已准入 Runtime 配置：" + node.Id);
        if (!string.Equals(configuration.ToolId, node.ToolType, StringComparison.Ordinal))
            throw new InvalidOperationException("节点工具身份不一致：" + node.Id);
    }
}
