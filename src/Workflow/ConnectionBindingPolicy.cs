using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>Design-time ownership only. Removing a control edge never chooses another data source.</summary>
public static class ConnectionBindingPolicy
{
    public static bool RemoveAutomaticImageBinding(WorkflowNodeBase node, string connectionId)
    {
        if(node.RuntimeConfiguration?.Schema.WorkflowImageInputId is { } port)
        {
            if(node.RuntimeConfiguration.Fields.Read(port) is not SunEyeVision.Plugin.Contracts.ReferenceSetting setting||setting.OriginId!="connection:"+connectionId)return false;
            node.RuntimeConfiguration=node.RuntimeConfiguration.WithFieldSettings(new System.Collections.Generic.Dictionary<string,SunEyeVision.Plugin.Contracts.ParameterSetting>{
                [port]=setting with {OriginId=null,RebindingReason="自动图像来源对应的连线已删除，请重新选择图像来源。"}});
            node.LastResult=null;node.OutputCache=null;return true;
        }
        if(node.RuntimeConfiguration is null)throw new System.InvalidOperationException("节点缺少已准入 Runtime 配置。");
        return false;
    }
}
