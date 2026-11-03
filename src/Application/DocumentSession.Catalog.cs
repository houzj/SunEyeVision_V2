using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Returns catalog metadata only for an actual node in this active session.</summary>
    public ToolCatalogDescriptor? GetNodeCatalog(string workflowId, NodeDefinition node)
    {
        ArgumentNullException.ThrowIfNull(node);
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            ValidateScopeCore(scope);
            var view = RequireNode(scope, node.Id);
            if (!ReferenceEquals(view.Node, node))
                throw new InvalidOperationException("目录查询的节点已退出或被替换。");
            return _directory.GetCatalog(node.ToolType);
        }
    }
}
