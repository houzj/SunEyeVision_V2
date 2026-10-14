using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>Design-time ownership only. Removing a control edge never chooses another data source.</summary>
public static class ConnectionBindingPolicy
{
    public static bool RemoveAutomaticImageBinding(WorkflowNodeBase node, string connectionId)
    {
        var property = node.Parameters.GetType().GetProperty("ImageSource");
        var savedProperty = node.Parameters.GetType().GetProperty("ImageSourceRef");
        var source = property?.GetValue(node.Parameters) as AvailableDataSource;
        var saved = savedProperty?.GetValue(node.Parameters) as ImageSourceReference;
        if (property?.CanWrite != true || (source?.AutomaticConnectionId ?? saved?.AutomaticConnectionId) != connectionId) return false;
        property.SetValue(node.Parameters, null);
        if (savedProperty?.CanWrite == true) savedProperty.SetValue(node.Parameters, null);
        node.Parameters.RebindingRequired["ImageSource"] = "自动图像来源对应的连线已删除，请重新选择图像来源";
        node.LastResult = null; node.OutputCache = null; return true;
    }
}
