using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Original variable-selection layout, group/search/page/confirmation behavior over a private frozen host snapshot.</summary>
public partial class VariableSelectionDialog:BaseWindow
{
    private readonly IReadOnlyList<VariableSelectionItem> _snapshot;
    private VariableGroup? _selectedGroup;
    private int _page=1,_pageCount=10;
    private bool _closed;
    public IReadOnlyList<VariableGroup> EnumEntrysA {get;}
    public ObservableCollection<VariableSelectionItem> BindItems {get;}=new();
    public int ItemsTotalCount {get;private set;}
    public string EmptyStatus=>ItemsTotalCount==0?"Visible":"Collapsed";
    public string PageNumShowStatus=>ItemsTotalCount==0?"Collapsed":"Visible";
    public int CurrentPageNum {get=>_page;set{if(_closed||_page==value)return;_page=value;RefreshPage();OnPropertyChanged(nameof(CurrentPageNum));}}
    public int PageCount {get=>_pageCount;set{if(_closed||value<=0||value==_pageCount)return;_pageCount=value;_page=1;RefreshPage();OnPropertyChanged(nameof(PageCount));OnPropertyChanged(nameof(CurrentPageNum));}}
    public VariableGroup? TriggerSelectedObject
    {
        get=>_selectedGroup;
        set
        {
            if(_closed||ReferenceEquals(_selectedGroup,value)||value is null||!Groups(EnumEntrysA).Any(group=>ReferenceEquals(group,value)))return;
            _selectedGroup=value;_page=1;RefreshPage();OnPropertyChanged(nameof(TriggerSelectedObject));OnPropertyChanged(nameof(CurrentPageNum));
        }
    }
    public bool IsConfirm {get;private set;}
    public VariableSelectionItem? SelectedVarItem {get;private set;}
    public VariableSelectionDialog():this(DesignerProperties.GetIsInDesignMode(new DependencyObject())
        ?Design.VariableSelectionDesignData.Items:Array.Empty<VariableSelectionItem>()){}
    public VariableSelectionDialog(IEnumerable<VariableSelectionItem> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var rows=snapshot.ToArray();
        if(rows.Any(row=>row is null||row.Reference is null||string.IsNullOrWhiteSpace(row.Name))
            ||rows.Select(row=>(row.Reference.SolutionId,row.Reference.VariableId)).Distinct().Count()!=rows.Length)
            throw new ArgumentException("变量候选缺少身份、显示名称或包含重复身份。",nameof(snapshot));
        _snapshot=Array.AsReadOnly(rows);
        var rootName=AppLocalizationService.GetLang("Lang_GlobalVariable","全局变量");
        var groups=rows.GroupBy(row=>string.IsNullOrEmpty(row.Group)?AppLocalizationService.GetLang("Lang_VarGroup","变量组")+"0":row.Group)
            .Select(group=>new VariableGroup(group.Key,rootName+"."+group.Key,Array.Empty<VariableGroup>(),group.OrderBy(row=>row.Index))).ToArray();
        EnumEntrysA=groups.Length==0?Array.Empty<VariableGroup>():new[]{new VariableGroup(rootName,rootName,groups,Array.Empty<VariableSelectionItem>())};
        InitializeComponent();SetResourceReference(StyleProperty,typeof(BaseWindow));
        Closed+=OnClosed;
        if(groups.FirstOrDefault() is {} initial)TriggerSelectedObject=initial;else RefreshPage();
    }
    private static IEnumerable<VariableGroup> Groups(IEnumerable<VariableGroup> roots)
    {foreach(var group in roots){yield return group;foreach(var child in Groups(group.EnumEntrysA))yield return child;}}
    private void OnLoaded(object sender,RoutedEventArgs args)
    {if(!_closed)RefreshPage();}
    private void RefreshPage()
    {
        if(_closed||globalVarList is null)return;
        var search=SearchText.Text;
        var items=(_selectedGroup?.Items??Array.Empty<VariableSelectionItem>()).Where(row=>string.IsNullOrEmpty(search)||row.Name.Contains(search,StringComparison.Ordinal)).ToArray();
        ItemsTotalCount=items.Length;
        var pages=Math.Max(1,(ItemsTotalCount+_pageCount-1)/_pageCount);_page=Math.Clamp(_page,1,pages);
        globalVarList.SelectedItem=null;BindItems.Clear();
        foreach(var row in items.Skip((_page-1)*_pageCount).Take(_pageCount))BindItems.Add(row);
        OnPropertyChanged(nameof(ItemsTotalCount));OnPropertyChanged(nameof(EmptyStatus));OnPropertyChanged(nameof(PageNumShowStatus));
    }
    private void SearchItem_OnClick(object sender,RoutedEventArgs args)=>Search();
    private void SearchText_TextChanged(object sender,TextChangedEventArgs args)=>Search();
    private void Search(){if(_closed)return;_page=1;RefreshPage();OnPropertyChanged(nameof(CurrentPageNum));}
    private void Confirm_Click(object sender,RoutedEventArgs args)=>ConfirmSelection();
    private void ConfirmSelection()
    {
        if(_closed)return;
        if(globalVarList.SelectedItem is VariableSelectionItem item&&BindItems.Any(row=>ReferenceEquals(row,item)))
        {SelectedVarItem=item;IsConfirm=true;}
        Close();
    }
    private void globalVarList_MouseDoubleClick(object sender,MouseButtonEventArgs args)
    {if(!_closed&&globalVarList.SelectedItem is VariableSelectionItem)ConfirmSelection();}
    public void Retire()
    {
        if(_closed)return;IsConfirm=false;SelectedVarItem=null;Close();
    }
    private void OnClosed(object? sender,EventArgs args)
    {
        if(_closed)return;_closed=true;Closed-=OnClosed;Loaded-=OnLoaded;
        BindItems.Clear();globalVarList.ItemsSource=null;globalVarList.SelectedItem=null;DataContext=null;
    }
}
