namespace SunEyeVision.Plugin.Contracts;

/// <summary>Non-visual discovery information owned by the required Runtime.
/// IconKey is a semantic key resolved by the host, never a WPF resource or URI.</summary>
public sealed record ToolCatalogDescriptor
{
    public ToolCatalogDescriptor(string displayName,string description,string iconKey,string category,int order)
    {
        if(string.IsNullOrWhiteSpace(displayName)||string.IsNullOrWhiteSpace(description)
            ||string.IsNullOrWhiteSpace(iconKey)||string.IsNullOrWhiteSpace(category)||order<0)
            throw new ArgumentException("Runtime catalog information must be explicit and complete.");
        DisplayName=displayName;Description=description;IconKey=iconKey;Category=category;Order=order;
    }
    public string DisplayName{get;}
    public string Description{get;}
    public string IconKey{get;}
    public string Category{get;}
    public int Order{get;}
}

public interface IToolCatalogProvider
{
    ToolCatalogDescriptor Catalog {get;}
}
