using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;

/// <summary>分类/条目只消费部署目录，类型装载由ToolRegistry使用入口负责。</summary>
public sealed class ToolboxViewModel : ViewModelBase
{
    private readonly ToolDirectoryCatalog? _directory;
    private bool _compact=true, _popup;
    private string? _selected;
    private string _status=string.Empty;
    public string StatusMessage {get=>_status;private set=>SetProperty(ref _status,value);}
    public ObservableCollection<ToolCategory> Categories {get;}=new();
    // 发布探针可枚举全部目录声明；这不会运行工具类型或加载DLL。
    public ObservableCollection<ToolItem> AllTools => new(_directory?.Tools.Where(t=>t.IsVisible).Select(ToItem)??Enumerable.Empty<ToolItem>());
    public ObservableCollection<ToolItem> SelectedCategoryTools {get;}=new();
    public bool IsCompactMode
    {
        get=>_compact;
        set { if(SetProperty(ref _compact,value)) { ClosePopup(); OnPropertyChanged(nameof(PreferredWidth));
            OnPropertyChanged(nameof(DisplayModeIcon)); OnPropertyChanged(nameof(DisplayModeTooltip)); } }
    }
    public double PreferredWidth=>IsCompactMode?52:230;
    public string DisplayModeIcon=>IsCompactMode?"»":"«";
    public string DisplayModeTooltip=>IsCompactMode?"展开工具箱":"收起工具箱";
    public bool IsCompactModePopupOpen {get=>_popup;set=>SetProperty(ref _popup,value);}
    public string? SelectedCategory
    {
        get=>_selected;
        set
        {
            if(!SetProperty(ref _selected,value))return;
            SelectedCategoryTools.Clear();
            var category=Categories.FirstOrDefault(c=>c.Name==value);
            if(category!=null) { LoadCategory(category); foreach(var t in category.Tools??new ObservableCollection<ToolItem>())SelectedCategoryTools.Add(t); }
            foreach(var c in Categories)c.IsSelected=c.Name==value;
            IsCompactModePopupOpen=IsCompactMode&&category!=null;
        }
    }
    public ICommand ToggleDisplayModeCommand {get;}
    public ToolboxViewModel() : this(ToolRegistry.DirectoryCatalog) { }
    public ToolboxViewModel(ToolDirectoryCatalog? directory)
    {
        _directory=directory;
        ToggleDisplayModeCommand=new RelayCommand(()=>IsCompactMode=!IsCompactMode);
        if(directory==null)return; // VS设计器没有部署目录，不扫描工程或启动插件。
        foreach(var g in directory.Groups)
        {
            var c=new ToolCategory(g.Name,g.Icon,g.Name,0,false){HoverIcon=g.HoverIcon};
            c.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ToolCategory.IsExpanded)&&c.IsExpanded)LoadCategory(c);};
            Categories.Add(c);
        }
        foreach(var c in Categories)c.ToolCount=directory.CountCategory(c.Name);
    }
    private void LoadCategory(ToolCategory category)
    {
        if(category.Tools!=null)return;
        try
        {
            category.Tools=new ObservableCollection<ToolItem>(_directory?.GetCategory(category.Name).Select(ToItem)??Enumerable.Empty<ToolItem>());
            StatusMessage=string.Empty;
        }
        catch(Exception ex)
        {
            StatusMessage="分类读取失败："+ex.Message;
            SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,StatusMessage,"Toolbox",ex);
        }
    }
    private static ToolItem ToItem(DirectoryTool t)=>new(t.DisplayName,t.Category,t.Icon,t.Description,t.Id);
    public void ClosePopup(){IsCompactModePopupOpen=false;SelectedCategory=null;}
}