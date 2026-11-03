using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Detach only the data binding owned by the removed control connection.</summary>
internal static class ConnectionBindingPolicy
{
    internal static ToolConfiguration? AddAutomaticImageBinding(AdmittedToolConfiguration target,
        AdmittedToolConfiguration source, string workflowId, string sourceNodeId, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId); ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        if (target.Schema.WorkflowImageInputId is not { } fieldId ||
            target.Configuration.Fields.Read(fieldId) is not UnsetSetting) return null;
        foreach (var port in source.Schema.Outputs)
        {
            var members = ImageMembers(port.Type, Array.Empty<string>()).FirstOrDefault();
            if (members is null) continue;
            var reference = new OutputReference(sourceNodeId, port.Id, port.Type, members,
                scopeId: workflowId, storage: port.Storage);
            return target.WithFieldSettings(new Dictionary<string, ParameterSetting>
            {
                [fieldId] = new ReferenceSetting(reference, OriginId: "connection:" + connectionId)
            }).Configuration;
        }
        return null;
    }

    private static IEnumerable<string[]> ImageMembers(TypeRef type, string[] path)
    {
        if (type == TypeRef.ResourceOf(ResourceKind.Image)) { yield return path; yield break; }
        if (type.Shape != ValueShape.Record) yield break;
        foreach (var field in type.Fields)
            foreach (var child in ImageMembers(field.Type, path.Append(field.Id).ToArray())) yield return child;
    }

    internal static ToolConfiguration? RemoveAutomaticImageBinding(AdmittedToolConfiguration admitted, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        if (admitted.Schema.WorkflowImageInputId is not { } port ||
            admitted.Configuration.Fields.Read(port) is not ReferenceSetting setting || setting.OriginId != "connection:" + connectionId)
            return null;
        return admitted.WithFieldSettings(new Dictionary<string, ParameterSetting>
        {
            [port] = setting with { OriginId = null, RebindingReason = "自动图像来源对应的连线已删除，请重新选择图像来源。" }
        }).Configuration;
    }
}
