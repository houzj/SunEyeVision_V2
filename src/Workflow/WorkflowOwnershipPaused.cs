using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Workflow;

/// <summary>Group ownership is protected until its independent migration is resumed.</summary>
public static class WorkflowOwnership
{
    public static bool IsInternalNode(Solution document,Workflow child,string parentId,string workflowId,WorkflowNodeBase node)
        =>throw new InvalidOperationException("Group 归属查询尚未接入。");

    public static IReadOnlyList<Workflow> CollectExclusiveDescendants(IEnumerable<Workflow> source,Workflow target,
        IEnumerable<WorkflowNodeBase> removingOwners,IEnumerable<Workflow>? declaredOwned=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(removingOwners);
        if(target.IsSubroutine||removingOwners.Any(node=>RuntimeNodeAdmission.IsPausedControlFlow(node.ToolType,node.SubroutineId))||
            (declaredOwned??Enumerable.Empty<Workflow>()).Any())
            throw new InvalidOperationException("Group 归属和删除尚未接入。");
        return Array.Empty<Workflow>();
    }
}
