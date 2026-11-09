namespace SunEyeVision.Plugin.Contracts;

/// <summary>Non-visual discovery information owned by the required Runtime.
/// IconKey is a semantic key resolved by the host, never a WPF resource or URI.</summary>
public sealed record ToolCatalogDescriptor
{
    public ToolCatalogDescriptor(string displayName,string description,string iconKey,string category,int order,string nodeDisplayType="DEFAULT")
    {
        if(string.IsNullOrWhiteSpace(displayName)||string.IsNullOrWhiteSpace(description)
            ||string.IsNullOrWhiteSpace(iconKey)||string.IsNullOrWhiteSpace(category)||order<0)
            throw new ArgumentException("Runtime catalog information must be explicit and complete.");
        if(Array.IndexOf(NodeDisplayTypes,nodeDisplayType)<0)
            throw new ArgumentException($"Unknown node display type: {nodeDisplayType}");
        DisplayName=displayName;Description=description;IconKey=iconKey;Category=category;Order=order;NodeDisplayType=nodeDisplayType;
    }
    public string DisplayName{get;}
    public string Description{get;}
    public string IconKey{get;}
    public string Category{get;}
    public int Order{get;}
    /// <summary>节点外观类型，取值与原件显示类型一致；由工具声明，宿主不再按工具标识分支。</summary>
    public string NodeDisplayType{get;}
    private static readonly string[] NodeDisplayTypes =
    {
        "DEFAULT","IMAGE","IFBRANCH","FUNCTION","TIMER","SHELL","COMMUNCATION"
    };
}

public interface IToolCatalogProvider
{
    ToolCatalogDescriptor Catalog {get;}
}
