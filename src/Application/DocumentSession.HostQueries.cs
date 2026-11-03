using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Read the admitted capability of this actual node, never infer it from a tool name.</summary>
    public HostInputDeclaration? GetNodeHostInputDeclaration(string workflowId, NodeDefinition node)
    {
        ArgumentNullException.ThrowIfNull(node);
        lock (_gate)
        {
            var view = RequireNode(OpenScope(workflowId), node.Id);
            if (!ReferenceEquals(view.Node, node)) throw new InvalidOperationException("宿主能力查询的节点已退出或被替换。");
            return view.Configuration.HostDeclarations.HostInputs;
        }
    }
}