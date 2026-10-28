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
    // 显式全量探针按分类加载实际组件，仅显示当前产品实际准入项。
    public ObservableCollection<ToolItem> AllTools => new(Categories.SelectMany(c=>BuildCategory(c.Name,true)));
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
        foreach(var g in directory.Groups.Where(g=>directory.CountCategory(g.Name)>0))
        {
            var c=new ToolCategory(g.Name,g.Icon,g.Name,0,false){HoverIcon=g.HoverIcon};
            c.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ToolCategory.IsExpanded)&&c.IsExpanded)LoadCategory(c);};
            Categories.Add(c);
        }
        foreach(var c in Categories)c.ToolCount=BuildCategory(c.Name,false).Count();
    }
    /// <summary>工具箱与画布右键共用分类加载及可用态，不另维护工具清单。</summary>
    public System.Collections.Generic.IReadOnlyList<ToolItem> GetCategoryTools(string name)
    {
        var category=Categories.FirstOrDefault(c=>c.Name==name);
        if(category==null)return Array.Empty<ToolItem>();
        LoadCategory(category);
        return category.Tools?.ToArray()??Array.Empty<ToolItem>();
    }
    private void LoadCategory(ToolCategory category)
    {
        if(category.Tools!=null)return;
        try
        {
            category.Tools=new ObservableCollection<ToolItem>(BuildCategory(category.Name,true));
            StatusMessage=string.Empty;
        }
        catch(Exception ex)
        {
            StatusMessage="分类读取失败："+ex.Message;
            SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,StatusMessage,"Toolbox",ex);
        }
    }
    private System.Collections.Generic.IEnumerable<ToolItem> BuildCategory(string name,bool load)
    {
        foreach(var declaration in _directory?.GetCategory(name) ?? Array.Empty<DirectoryTool>())
            yield return load ? LoadItem(declaration) : ToItem(declaration);
    }
    private static ToolItem LoadItem(DirectoryTool declaration)
    {
        try
        {
            var metadata=ToolRegistry.GetToolMetadata(declaration.Id);
            if(metadata==null)throw new InvalidOperationException("工具未成功注册");
            return ToItem(declaration);
        }
        catch(Exception error)
        {
            SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(SunEyeVision.Plugin.SDK.Logging.LogLevel.Error,"工具加载失败："+declaration.Id,"Toolbox",error);
            return new ToolItem(declaration.DisplayName,declaration.Category,declaration.Icon,declaration.Description,declaration.Id)
            {IsAvailable=false,LoadError=error.Message};
        }
    }
    private static ToolItem ToItem(DirectoryTool t)=>new(t.DisplayName,t.Category,t.Icon,t.Description,t.Id);
    public void ClosePopup(){IsCompactModePopupOpen=false;SelectedCategory=null;}
}