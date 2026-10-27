using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>The original configuration window keeps edits on close; only the owning Group publishes.</summary>
internal sealed class GroupRunParameterConfigWindow : Window
{
    private sealed record Filter(GroupParameterEditorKind? Kind,string Label){public override string ToString()=>Label;}
    private sealed record Row(SubroutineRunParameterGroup Group,SubroutineRunParameter? Item);
    private readonly GroupRunParameterSession _session;
    private readonly Grid _body=new();
    private readonly TreeView _sources=new(){BorderThickness=new Thickness(0)};
    private readonly ListView _rows=new(){BorderThickness=new Thickness(0)};
    private readonly ComboBox _types=new(){Width=130,Margin=new Thickness(4)};
    private readonly TextBox _search=new(){MinWidth=150,Margin=new Thickness(4)};
    private readonly ComboBox _destination=new(){Width=130,DisplayMemberPath="Name"};
    private readonly TextBox _description=new(){MinWidth=180};
    private readonly TextBlock _error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6)};
    private readonly Dictionary<Guid,bool> _expanded=new();
    private bool _refreshing,_forceClose,_closing,_closed;
    private Window? _registeredOwner;
    private SubroutineRunParameterGroup? ActiveGroup=>_destination.SelectedItem as SubroutineRunParameterGroup;
    internal GroupRunParameterConfigWindow(GroupRunParameterSession session,Window? owner=null)
    {
        _types.Name="ParameterTypeFilter";_sources.Name="ParameterSourceTree";_destination.Name="DestinationGroup";
        _session=session;Title="设置";Width=1100;Height=680;MinWidth=900;MinHeight=540;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,DefaultDebugWindow.DebugWindowChrome);
        SetResourceReference(BackgroundProperty,"ThemePanelBackgroundBrush");SetResourceReference(ForegroundProperty,"ThemeTextPrimaryBrush");
        if(owner is not null){Owner=owner;Resources.MergedDictionaries.Add(owner.Resources);_registeredOwner=owner;owner.Closing+=OwnerClosing;}
        _body.SetResourceReference(Panel.BackgroundProperty,"ThemePanelBackgroundBrush");
        _sources.SetResourceReference(Control.BackgroundProperty,"ThemePanelBackgroundBrush");_sources.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        _rows.SetResourceReference(Control.BackgroundProperty,"ThemePanelBackgroundBrush");_rows.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        _body.RowDefinitions.Add(new(){Height=new GridLength(32)});_body.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});_body.RowDefinitions.Add(new(){Height=GridLength.Auto});_body.RowDefinitions.Add(new(){Height=new GridLength(42)});
        var title=new DockPanel{Margin=new Thickness(10,0,6,0)};var close=ActionButton("×",()=>Close());close.Width=32;DockPanel.SetDock(close,Dock.Right);System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close,true);title.Children.Add(close);title.Children.Add(new TextBlock{Text=Title,VerticalAlignment=VerticalAlignment.Center});_body.Children.Add(title);
        var panels=new Grid{Margin=new Thickness(6,6,6,0)};panels.ColumnDefinitions.Add(new(){Width=new GridLength(370)});panels.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(panels,1);_body.Children.Add(panels);
        var left=new Grid();left.RowDefinitions.Add(new(){Height=new GridLength(37)});left.RowDefinitions.Add(new(){Height=new GridLength(37)});left.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        left.Children.Add(new TextBlock{Text="选择列表",FontWeight=FontWeights.Bold,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});
        var filters=new DockPanel();DockPanel.SetDock(_types,Dock.Left);filters.Children.Add(_types);filters.Children.Add(_search);Grid.SetRow(filters,1);left.Children.Add(filters);Grid.SetRow(_sources,2);left.Children.Add(_sources);panels.Children.Add(left);
        _types.ItemsSource=new[]{new Filter(null,"全部"),new Filter(GroupParameterEditorKind.Numeric,"数值"),new Filter(GroupParameterEditorKind.Enumeration,"枚举"),new Filter(GroupParameterEditorKind.Boolean,"开关"),new Filter(GroupParameterEditorKind.SingleChoice,"单选"),new Filter(GroupParameterEditorKind.Region,"ROI设置"),new Filter(GroupParameterEditorKind.FilePath,"文件路径"),new Filter(GroupParameterEditorKind.Model,"训练模型设置"),new Filter(GroupParameterEditorKind.Text,"文本"),new Filter(GroupParameterEditorKind.Composite,"复合参数")};_types.SelectedIndex=0;
        _search.ToolTip="搜索模块或参数";_types.SelectionChanged+=(_,_)=>RefreshSources();_search.TextChanged+=(_,_)=>RefreshSources();
        var right=new Grid{Margin=new Thickness(8,0,0,0)};right.RowDefinitions.Add(new(){Height=new GridLength(37)});right.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});right.RowDefinitions.Add(new(){Height=new GridLength(40)});Grid.SetColumn(right,1);panels.Children.Add(right);
        var toolbar=new DockPanel();toolbar.Children.Add(new TextBlock{Text="已选参数",FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
        var commands=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};commands.Children.Add(_destination);commands.Children.Add(ActionButton("新增分组",()=>{var group=_session.AddGroup();RefreshFromSession(group.Id);}));commands.Children.Add(ActionButton("全部删除",()=>{if(Confirm("删除全部分组和曝光参数？")){_session.Parameters.RunParameterGroups.Clear();RefreshFromSession();}}));toolbar.Children.Add(commands);right.Children.Add(toolbar);
        var columns=new GridView();columns.Columns.Add(Column("名称",150,TextCell("Item.Name")));columns.Columns.Add(Column("类型",70,LabelCell()));columns.Columns.Add(Column("分组",105,GroupCell()));columns.Columns.Add(Column("关联",135,ButtonCell("关联设置",row=>OpenRelations(row.Item!))));columns.Columns.Add(Column("同步",55,ButtonCell("同步",row=>_session.Synchronize(row.Item!))));columns.Columns.Add(Column("操作",95,OperationsCell()));var columnStyle=new Style(typeof(GridViewColumnHeader));columnStyle.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension("ThemeControlBackgroundBrush")));columnStyle.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("ThemeTextPrimaryBrush")));columnStyle.Setters.Add(new Setter(Control.BorderBrushProperty,new DynamicResourceExtension("ThemeBorderBrush")));columns.ColumnHeaderContainerStyle=columnStyle;_rows.View=columns;
        var container=new Style(typeof(ListViewItem));container.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("ThemeTextPrimaryBrush")));container.Setters.Add(new Setter(HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));var hidden=new DataTrigger{Binding=new Binding("Item"),Value=null};hidden.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));container.Triggers.Add(hidden);_rows.ItemContainerStyle=container;
        var headerTemplate=new DataTemplate();var header=new FrameworkElementFactory(typeof(DockPanel));var groupName=new FrameworkElementFactory(typeof(TextBox));groupName.SetValue(FrameworkElement.WidthProperty,160d);groupName.SetValue(TextBox.MaxLengthProperty,SubroutineRunParameterGroup.MaximumNameLength);groupName.SetBinding(TextBox.TextProperty,new Binding("Name.Name"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});header.AppendChild(groupName);
        foreach(var command in new[]{("↑",-1),("↓",1),("删除分组",0)})
        {
            var captured=command;var button=new FrameworkElementFactory(typeof(Button));button.SetValue(ContentControl.ContentProperty,captured.Item1);button.SetValue(FrameworkElement.MarginProperty,new Thickness(3,0,0,0));button.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,_)=>Run(()=>
            {
                if(((FrameworkElement)sender).DataContext is not CollectionViewGroup {Name:SubroutineRunParameterGroup group})return;
                if(captured.Item2==0){if(!Confirm("删除分组及其中的曝光参数？"))return;_session.Parameters.RunParameterGroups.Remove(group);}
                else{var groups=_session.Parameters.RunParameterGroups;var at=groups.IndexOf(group);if(at+captured.Item2>=0&&at+captured.Item2<groups.Count)groups.Move(at,at+captured.Item2);}
                RefreshFromSession(group.Id);
            })));header.AppendChild(button);
        }
        headerTemplate.VisualTree=header;
        var groupStyle=new GroupStyle();var groupContainer=new Style(typeof(GroupItem));var groupTemplate=new ControlTemplate(typeof(GroupItem));var expander=new FrameworkElementFactory(typeof(Expander));expander.SetBinding(HeaderedContentControl.HeaderProperty,new Binding());expander.SetValue(HeaderedContentControl.HeaderTemplateProperty,headerTemplate);expander.SetValue(Expander.IsExpandedProperty,true);
        expander.AddHandler(FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>{var element=(Expander)sender;if(element.DataContext is CollectionViewGroup{Name:SubroutineRunParameterGroup group})element.IsExpanded=!_expanded.TryGetValue(group.Id,out var open)||open;}));
        expander.AddHandler(Expander.ExpandedEvent,new RoutedEventHandler((sender,_)=>Remember((Expander)sender,true)));expander.AddHandler(Expander.CollapsedEvent,new RoutedEventHandler((sender,_)=>Remember((Expander)sender,false)));expander.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));groupTemplate.VisualTree=expander;groupContainer.Setters.Add(new Setter(TemplateProperty,groupTemplate));groupStyle.ContainerStyle=groupContainer;_rows.GroupStyle.Add(groupStyle);Grid.SetRow(_rows,1);right.Children.Add(_rows);
        var descriptionPanel=new DockPanel{Margin=new Thickness(4)};descriptionPanel.Children.Add(new TextBlock{Text="描述",Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center});descriptionPanel.Children.Add(_description);Grid.SetRow(descriptionPanel,2);right.Children.Add(descriptionPanel);
        _rows.SelectionChanged+=(_,_)=>{if(_rows.SelectedItem is Row{Item:{} item} row){_destination.SelectedItem=row.Group;_description.SetBinding(TextBox.TextProperty,new Binding(nameof(item.Description)){Source=item,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});}else{BindingOperations.ClearBinding(_description,TextBox.TextProperty);_description.Text="";}};
        Grid.SetRow(_error,2);_body.Children.Add(_error);var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(8)};footer.Children.Add(ActionButton("确定",()=>{if(TryPrepare(out _))Close();}));Grid.SetRow(footer,3);_body.Children.Add(footer);Content=_body;
        Closing+=(sender,e)=>{_closing=true;if(!_forceClose&&_session.IsValid&&!TryPrepare(out _)){e.Cancel=true;_closing=false;}};Closed+=(_,_)=>{_closed=true;_closing=false;if(_registeredOwner is {} previous)previous.Closing-=OwnerClosing;_registeredOwner=null;};
        RefreshFromSession();
    }
    private void OwnerClosing(object? sender,CancelEventArgs e){if(!e.Cancel)ForceClose();}
    private void Remember(Expander sender,bool open){if(!_refreshing&&sender.DataContext is CollectionViewGroup{Name:SubroutineRunParameterGroup group})_expanded[group.Id]=open;}
    private static GridViewColumn Column(string title,double width,DataTemplate template)=>new(){Header=title,Width=width,CellTemplate=template};
    private DataTemplate TextCell(string path)
    {var template=new DataTemplate();var text=new FrameworkElementFactory(typeof(TextBox));text.SetValue(TextBox.MaxLengthProperty,SubroutineRunParameterGroup.MaximumNameLength);text.SetBinding(TextBox.TextProperty,new Binding(path){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});template.VisualTree=text;return template;}
    private DataTemplate LabelCell()
    {var template=new DataTemplate();var label=new FrameworkElementFactory(typeof(TextBlock));label.SetResourceReference(TextBlock.ForegroundProperty,"ThemeTextPrimaryBrush");label.SetBinding(TextBlock.TextProperty,new Binding("Item"){Converter=new KindConverter(_session)});template.VisualTree=label;return template;}
    private DataTemplate GroupCell()
    {var template=new DataTemplate();var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetBinding(ItemsControl.ItemsSourceProperty,new Binding("ItemsSource"){Source=_destination});combo.SetValue(ItemsControl.DisplayMemberPathProperty,"Name");combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,new Binding("Group"){Mode=BindingMode.OneWay});combo.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,new SelectionChangedEventHandler((sender,_)=>{if(!_refreshing&&sender is ComboBox{DataContext:Row{Item:{} item} row,SelectedItem:SubroutineRunParameterGroup target}&&target!=row.Group)Run(()=>{_session.MoveToGroup(item,target);RefreshFromSession(target.Id);});}));template.VisualTree=combo;return template;}
    private DataTemplate ButtonCell(string title,Action<Row> action)
    {var template=new DataTemplate();var button=new FrameworkElementFactory(typeof(Button));button.SetValue(ContentControl.ContentProperty,title);button.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,_)=>{if(((FrameworkElement)sender).DataContext is Row{Item:not null} row)Run(()=>action(row));}));template.VisualTree=button;return template;}
    private DataTemplate OperationsCell()
    {var template=new DataTemplate();var panel=new FrameworkElementFactory(typeof(StackPanel));panel.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);foreach(var spec in new[]{("↑",-1),("↓",1),("×",0)}){var command=spec;var button=new FrameworkElementFactory(typeof(Button));button.SetValue(ContentControl.ContentProperty,command.Item1);button.SetValue(FrameworkElement.WidthProperty,25d);button.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,_)=>{if(((FrameworkElement)sender).DataContext is not Row{Item:{} item} row)return;Run(()=>{if(command.Item2==0)_session.RemoveExposure(item);else{var at=row.Group.Parameters.IndexOf(item);if(at+command.Item2>=0&&at+command.Item2<row.Group.Parameters.Count)row.Group.Parameters.Move(at,at+command.Item2);}RefreshFromSession(row.Group.Id);});}));panel.AppendChild(button);}template.VisualTree=panel;return template;}
    private Button ActionButton(string title,Action action){var button=new Button{Content=title,Margin=new Thickness(3,0,3,0),MinWidth=56,Height=28};button.Click+=(_,_)=>Run(action);return button;}
    private bool Confirm(string text)=>ThemeMessageDialog.Show(text,"设置",MessageBoxButton.YesNo,ThemeMessageDialog.MessageIcon.Question,this)==MessageBoxResult.Yes;
    private void Run(Action action){try{_error.Text="";if(!_session.IsValid)throw new InvalidOperationException("Group编辑作用域已失效。");action();}catch(Exception ex){var error=ex.GetBaseException().Message;RefreshSources();_error.Text=error;}}
    internal bool TryPrepare(out string error)
    {
        if(!ToolEditorCommit.TryPrepare(_body,out error)){_error.Text=error;return false;}
        var validation=_session.Parameters.Validate();error=validation.IsValid?"":string.Join("；",validation.Errors);_error.Text=error;return validation.IsValid;
    }
    internal bool HasPendingEdits=>ToolEditorCommit.HasPendingEdits(_body);
    internal void ForceClose(){_forceClose=true;if(!_closing&&!_closed)Close();}
    internal void RefreshFromSession(Guid? active=null)
    {
        _refreshing=true;
        try
        {
            active??=ActiveGroup?.Id;_destination.ItemsSource=_session.Parameters.RunParameterGroups;_destination.SelectedItem=_session.Parameters.RunParameterGroups.FirstOrDefault(g=>g.Id==active)??_session.Parameters.RunParameterGroups.FirstOrDefault();
            var data=new List<Row>();foreach(var group in _session.Parameters.RunParameterGroups){data.Add(new(group,null));data.AddRange(group.Parameters.Select(p=>new Row(group,p)));}
            var view=new ListCollectionView(data);view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Row.Group)));_rows.ItemsSource=view;
        }
        finally{_refreshing=false;}
        RefreshSources();
    }
    private void RefreshSources()
    {
        if(_refreshing)return;_refreshing=true;
        try
        {
            _sources.Items.Clear();var filter=(_types.SelectedItem as Filter)?.Kind;
            var available=_session.Available().Where(a=>(filter is null||a.EditorKind==filter)&&a.Label.Contains(_search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();
            foreach(var module in available.GroupBy(a=>(a.Target.WorkflowId,a.Target.NodeId)))
            {
                var entries=module.ToArray();var all=entries.Count(a=>_session.FindExposure(a)!=null);
                var moduleCheck=new CheckBox{Content=_session.ModuleLabel(entries[0]),IsThreeState=false,IsChecked=all==0?false:all==entries.Length?true:null};var parent=new TreeViewItem{Header=moduleCheck,IsExpanded=true};moduleCheck.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
                moduleCheck.Click+=(_,_)=>Run(()=>{Toggle(entries,moduleCheck.IsChecked==true);RefreshFromSession(ActiveGroup?.Id);});
                foreach(var entry in entries)
                {var source=entry;var check=new CheckBox{Content=source.Label.Split(" / ").Last(),IsChecked=_session.FindExposure(source)!=null};check.Click+=(_,_)=>Run(()=>{Toggle(new[]{source},check.IsChecked==true);RefreshFromSession(ActiveGroup?.Id);});check.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");parent.Items.Add(new TreeViewItem{Header=check,Tag=source});}
                _sources.Items.Add(parent);
            }
        }
        catch(Exception ex){_error.Text=ex.GetBaseException().Message;}
        finally{_refreshing=false;}
    }
    internal void Toggle(IEnumerable<AvailableGroupParameter> sources,bool selected)
    {
        var entries=sources.ToArray();
        if(selected)
        {
            var group=ActiveGroup;var created=false;if(group is null){group=_session.AddGroup();created=true;}
            try{_session.AddBatch(group,entries);}catch{if(created&&group.Parameters.Count==0)_session.Parameters.RunParameterGroups.Remove(group);throw;}
        }
        else foreach(var source in entries)if(_session.FindExposure(source) is {} item)_session.RemoveExposure(item);
    }
    private void OpenRelations(SubroutineRunParameter item)
    {var dialog=new GroupRelationWindow(_session,item,this);dialog.ShowDialog();RefreshFromSession(ActiveGroup?.Id);}
    private sealed class KindConverter(GroupRunParameterSession session) : IValueConverter
    {
        public object Convert(object value,Type target,object parameter,System.Globalization.CultureInfo culture)
        {if(value is not SubroutineRunParameter item)return "";try{return KindLabel(GroupParameterCatalog.Classify(GroupParameterCatalog.Describe(session.Resolve(item,false)[0].Node.Parameters).Single(d=>d.PropertyName==session.Resolve(item,false)[0].Property.Name)));}catch{return item.SourceEditorKind!=GroupParameterEditorKind.Auto?KindLabel(item.SourceEditorKind):"未关联";}}
        public object ConvertBack(object value,Type target,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
    }
    internal static string KindLabel(GroupParameterEditorKind kind)=>kind switch{GroupParameterEditorKind.Numeric=>"数值",GroupParameterEditorKind.Enumeration=>"枚举",GroupParameterEditorKind.Boolean=>"开关",GroupParameterEditorKind.SingleChoice=>"单选",GroupParameterEditorKind.Region=>"ROI设置",GroupParameterEditorKind.FilePath=>"文件路径",GroupParameterEditorKind.Model=>"模型",GroupParameterEditorKind.Text=>"文本",_=>"复合参数"};
}
internal sealed class GroupRelationWindow : Window
{
    private readonly GroupRunParameterSession _session;private readonly SubroutineRunParameter _item;private readonly List<(CheckBox Check,AvailableGroupParameter Source)> _entries=new();
    internal GroupRelationWindow(GroupRunParameterSession session,SubroutineRunParameter item,Window owner)
    {
        _session=session;_item=item;Title="关联模块";Width=500;Height=600;Owner=owner;WindowStartupLocation=WindowStartupLocation.CenterOwner;Resources.MergedDictionaries.Add(owner.Resources);
        SetResourceReference(BackgroundProperty,"ThemePanelBackgroundBrush");SetResourceReference(ForegroundProperty,"ThemeTextPrimaryBrush");var panel=new DockPanel{Margin=new Thickness(10)};
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var ok=new Button{Content="确定",Width=80,Margin=new Thickness(4)};var cancel=new Button{Content="取消",Width=80,Margin=new Thickness(4)};buttons.Children.Add(ok);buttons.Children.Add(cancel);DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
        var choices=new StackPanel();foreach(var source in session.Available().Where(s=>session.CanRelate(item,s))){var check=new CheckBox{Content=source.Label,IsChecked=item.Targets.Any(t=>GroupRunParameterSession.SameTarget(t,source.Target)),Margin=new Thickness(4)};_entries.Add((check,source));choices.Children.Add(check);}panel.Children.Add(new ScrollViewer{Content=choices,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=panel;
        cancel.Click+=(_,_)=>Close();ok.Click+=(_,_)=>{try{ConfirmSelection();DialogResult=true;}catch(Exception ex){ThemeMessageDialog.Show(ex.Message,"关联模块",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning,this);}};
    }
    internal void ConfirmSelection()=>_session.ReplaceTargets(_item,_entries.Where(e=>e.Check.IsChecked==true).Select(e=>e.Source));
}
