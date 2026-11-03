using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

internal static class RootConfigurationValidation
{
    internal static void Validate(RootWorkflowConfiguration configuration, GraphState graph,
        Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        foreach (var proxy in configuration.RunParameters)
        {
            var nodes = graph.Nodes.Where(item => item.Node.Id == proxy.NodeId).ToArray();
            if (nodes.Length != 1 || nodes[0].Node.ToolType != proxy.ToolType)
                throw new InvalidOperationException("代理来源节点或工具已变化。");
            var node = nodes[0]; var declaration = admit(node.Node, node.Configuration);
            if (proxy.FieldId != proxy.StoragePath) throw new InvalidOperationException("正式字段存储路径已变化。");
            var ports = declaration.Schema.Inputs.Concat(declaration.SettingsSchema.Inputs)
                .Where(port => port.Id == proxy.FieldId && port.EditModes != FieldEditModes.None).ToArray();
            if (ports.Length != 1) throw new InvalidOperationException("正式字段已移除或不可编辑。");
            var port = ports[0];
            if (!Supports(port.Type) && (port.EditModes & FieldEditModes.Reference) == 0)
                throw new InvalidOperationException("需要专用集合或资源编辑器。");
        }
    }

    private static bool Supports(TypeRef type) => type.Shape == ValueShape.Scalar ||
        type.Shape == ValueShape.Record && type.Fields.All(field => Supports(field.Type));
}
