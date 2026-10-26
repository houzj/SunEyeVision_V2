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
    // 显式全量探针按分类加载实际组件，同时保留完整VM目录中的未实现项。
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
        foreach(var g in directory.Groups)
        {
            var c=new ToolCategory(g.Name,g.Icon,g.Name,0,false){HoverIcon=g.HoverIcon};
            c.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ToolCategory.IsExpanded)&&c.IsExpanded)LoadCategory(c);};
            Categories.Add(c);
        }
        foreach(var c in Categories)c.ToolCount=BuildCategory(c.Name,false).Count();
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
    private sealed record CatalogEntry(string Id,string Name,string Icon,string? ToolId);
    private sealed record CatalogGroup(string Name,CatalogEntry[] Items);
    private static readonly CatalogGroup[] Catalog=ReadCatalog();
    private static CatalogGroup[] ReadCatalog()
    {
        using var stream=typeof(ToolboxViewModel).Assembly.GetManifestResourceStream("SunEyeVision.UI.VmToolCatalog.json")
            ??throw new InvalidOperationException("缺少VM工具目录资源");
        return System.Text.Json.JsonSerializer.Deserialize<CatalogGroup[]>(stream)
            ??throw new InvalidOperationException("VM工具目录为空");
    }
    private System.Collections.Generic.IEnumerable<ToolItem> BuildCategory(string name,bool load)
    {
        var deployed=(_directory?.GetCategory(name)??Enumerable.Empty<DirectoryTool>()).Where(t=>t.IsVisible).ToDictionary(t=>t.Id);
        var matched=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in Catalog.FirstOrDefault(g=>g.Name==name)?.Items??Array.Empty<CatalogEntry>())
        {
            ToolItem? actual=null;
            if(entry.ToolId!=null&&deployed.TryGetValue(entry.ToolId,out var declaration))
            {
                matched.Add(entry.ToolId);
                actual=load?LoadItem(declaration):ToItem(declaration);
            }
            yield return new ToolItem(entry.Name,name,"generic",actual?.Description??string.Empty,actual?.ToolId??"vm:"+entry.Id)
            {CatalogIcon=entry.Icon,IsAvailable=actual?.IsAvailable??false,LoadError=actual?.LoadError??(actual==null?"本产品尚未部署该工具":null)};
        }
        foreach(var declaration in deployed.Values.Where(t=>!matched.Contains(t.Id)).OrderBy(t=>t.Order).ThenBy(t=>t.Id,StringComparer.Ordinal))
            yield return load?LoadItem(declaration):ToItem(declaration);
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