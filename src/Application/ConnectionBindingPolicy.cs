using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Detach only the data binding owned by the removed control connection.</summary>
internal static class ConnectionBindingPolicy
{
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
