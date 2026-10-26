using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
namespace SunEyeVision.UI.Shell.Controls.Toolbox;
/// <summary>仅由d:DataContext使用；读取同一嵌入分类资源，不扫描目录或装载插件。</summary>
public sealed class ToolboxDesignData
{
    public double PreferredWidth=>230;
    public bool IsCompactMode=>false;
    public bool IsCompactModePopupOpen=>false;
    public string StatusMessage=>"设计时布局示例，非插件运行状态";
    public ObservableCollection<ToolCategory> Categories {get;}=new();
    public ObservableCollection<ToolItem> SelectedCategoryTools {get;}=new();
    public ToolboxDesignData()
    {
        var resource=Application.GetResourceStream(new Uri("pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/ToolGroups.xml"));
        if(resource==null)return;
        using var stream=resource.Stream;
        foreach(var g in XDocument.Load(stream).Root!.Elements("Group"))
            Categories.Add(new ToolCategory((string)g.Attribute("Name")!,(string)g.Attribute("Icon")!,"设计预览",0,false){HoverIcon=(string)g.Attribute("HoverIcon")!});
        var sample=Categories.FirstOrDefault();
        if(sample!=null){sample.Tools=new(){new ToolItem("图像源","采集","image","设计时示意","ImageLoad")};sample.ToolCount=1;sample.IsExpanded=true;}
    }
}