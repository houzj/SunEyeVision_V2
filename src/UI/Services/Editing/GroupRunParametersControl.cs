using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Actual fifth Group page. Configuration remains in the formal editor's private candidate.</summary>
internal sealed class GroupRunParametersControl : UserControl, ISubroutineRunParameterEditor, IDisposable
{
    internal GroupRunParameterSession Session { get; }
    private readonly TreeView _tree=new();
    private readonly StackPanel _details=new(){Margin=new Thickness(12)};
    private readonly StackPanel _configuration=new(){Margin=new Thickness(6)};
    private readonly ComboBox _catalog=new(){MinWidth=200,DisplayMemberPath="Label",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBox _search=new(){MinWidth=120,Margin=new Thickness(0,0,4,4)};
    private readonly TextBlock _error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,2,8,4)};
    private readonly ToggleButton _edit=new(){Content="编辑",Width=80,Height=28};
    private bool _rebuilding;
    private TreeViewItem? _previous;
    private object? Selection => (_tree.SelectedItem as TreeViewItem)?.Tag;
    private SubroutineRunParameterGroup? SelectedGroup => Selection switch
    {
        SubroutineRunParameterGroup group=>group,
        SubroutineRunParameter item=>Session.Parameters.RunParameterGroups.FirstOrDefault(g=>g.Parameters.Contains(item)),
        _=>null
    };
    internal GroupRunParametersControl(GroupRunParameterSession session)
    {
        Session=session;
        SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        _tree.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        var root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"ThemePanelBackgroundBrush");
        root.RowDefinitions.Add(new(){Height=new GridLength(44)});
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel{Margin=new Thickness(10,0,10,0)};
        DockPanel.SetDock(_edit,Dock.Right);header.Children.Add(_edit);
        header.Children.Add(new TextBlock{Text="运行参数",FontSize=14,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
        _edit.Checked+=(_,_)=>_configuration.Visibility=Visibility.Visible;
        _edit.Unchecked+=(_,_)=>_configuration.Visibility=Visibility.Collapsed;
        var actions=new WrapPanel();
        actions.Children.Add(Button("新建分组",()=>{var g=Session.AddGroup();Rebuild(g.Id);}));
        actions.Children.Add(Button("删除",()=>
        {
            if(Selection is SubroutineRunParameter item)SelectedGroup!.Parameters.Remove(item);
            else if(SelectedGroup is {} group)
            {
                if(ThemeMessageDialog.Show("删除分组会移除其中的曝光参数，是否继续？","删除分组",MessageBoxButton.YesNo,ThemeMessageDialog.MessageIcon.Question,Window.GetWindow(this))!=MessageBoxResult.Yes)return;
                Session.Parameters.RunParameterGroups.Remove(group);
            }
            Rebuild();
        }));
        actions.Children.Add(Button("上移",()=>Move(-1)));actions.Children.Add(Button("下移",()=>Move(1)));
        _configuration.Children.Add(actions);
        var picker=new Grid();picker.ColumnDefinitions.Add(new(){Width=new GridLength(150)});picker.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});picker.ColumnDefinitions.Add(new(){Width=GridLength.Auto});picker.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        _search.ToolTip="按模块或参数名称搜索";
        var searchPanel=new DockPanel();searchPanel.Children.Add(new TextBlock{Text="搜索",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,4,0)});searchPanel.Children.Add(_search);picker.Children.Add(searchPanel);Grid.SetColumn(_catalog,1);picker.Children.Add(_catalog);
        var add=Button("添加参数",()=>
        {
            if(_catalog.SelectedItem is not AvailableGroupParameter source)return;
            var group=SelectedGroup??Session.Parameters.RunParameterGroups.FirstOrDefault()??Session.AddGroup();
            var item=Session.Add(group,source);Rebuild(item.Id);
        });Grid.SetColumn(add,2);picker.Children.Add(add);
        var relate=Button("添加关联",()=>
        {
            if(Selection is SubroutineRunParameter item&&_catalog.SelectedItem is AvailableGroupParameter source)
            {Session.AddTarget(item,source);ShowDetails();}
        });Grid.SetColumn(relate,3);picker.Children.Add(relate);
        _configuration.Children.Add(picker);_configuration.Visibility=Visibility.Collapsed;Grid.SetRow(_configuration,1);root.Children.Add(_configuration);
        _search.TextChanged+=(_,_)=>RefreshCatalog();
        var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(300)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        _tree.BorderThickness=new Thickness(0);_tree.SetResourceReference(Control.BackgroundProperty,"ThemePanelBackgroundBrush");content.Children.Add(_tree);
        var scroll=new ScrollViewer{Content=_details,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);content.Children.Add(scroll);Grid.SetRow(content,2);root.Children.Add(content);
        Grid.SetRow(_error,3);root.Children.Add(_error);Content=root;
        _tree.SelectedItemChanged+=(_,_)=>
        {
            if(_rebuilding)return;
            if(HasErrors(_details)&&_previous is not null)
            { _rebuilding=true;_previous.IsSelected=true;_rebuilding=false;_error.Text="请先修正当前参数的输入错误。";return; }
            _previous=_tree.SelectedItem as TreeViewItem;ShowDetails();
        };
        BindParameters(Session.Parameters);
    }
    private Button Button(string text,Action action)
    {
        var button=new Button{Content=text,Margin=new Thickness(2),MinWidth=56,Height=28};
        button.Click+=(_,_)=>Run(action);return button;
    }
    private void Run(Action action)
    {
        if(HasErrors(_details)){_error.Text="请先修正当前参数的输入错误。";return;}
        try{_error.Text="";action();}catch(Exception ex){_error.Text=ex.GetBaseException().Message;}
    }
    private static bool HasErrors(DependencyObject parent)
    {
        if(Validation.GetHasError(parent))return true;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)if(HasErrors(VisualTreeHelper.GetChild(parent,i)))return true;
        return false;
    }
    public void BindParameters(SubroutineParameters parameters)
    {
        if(!ReferenceEquals(parameters,Session.Parameters))throw new InvalidOperationException("运行参数页与Group候选不一致。");
        _error.Text="";Rebuild();RefreshCatalog();
    }
    private void RefreshCatalog()
    {
        try
        {
            var selected=_catalog.SelectedItem as AvailableGroupParameter;
            var entries=Session.Available().Where(p=>p.Label.Contains(_search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();
            _catalog.ItemsSource=entries;
            _catalog.SelectedItem=entries.FirstOrDefault(p=>selected is not null&&p.Target.WorkflowId==selected.Target.WorkflowId&&p.Target.NodeId==selected.Target.NodeId&&p.Target.PropertyName==selected.Target.PropertyName&&p.Target.ExposedParameterId==selected.Target.ExposedParameterId)??entries.FirstOrDefault();
        }
        catch(Exception ex){_catalog.ItemsSource=null;_error.Text=ex.GetBaseException().Message;}
    }
    private void Rebuild(Guid? selected=null)
    {
        selected??=Selection switch{SubroutineRunParameter p=>p.Id,SubroutineRunParameterGroup g=>g.Id,_=>null};
        _rebuilding=true;_tree.Items.Clear();_previous=null;
        TreeViewItem? desired=null;
        foreach(var group in Session.Parameters.RunParameterGroups)
        {
            var groupItem=Item(group,nameof(group.Name));groupItem.IsExpanded=true;
            foreach(var item in group.Parameters)
            {
                var row=Item(item,nameof(item.Name));groupItem.Items.Add(row);
                if(item.Id==selected)desired=row;
            }
            _tree.Items.Add(groupItem);
            if(group.Id==selected)desired=groupItem;
        }
        desired??=_tree.Items.OfType<TreeViewItem>().FirstOrDefault();
        if(desired is not null){desired.IsSelected=true;_previous=desired;}
        _rebuilding=false;ShowDetails();
    }
    private static TreeViewItem Item(object value,string property)
    {
        var label=new TextBlock();label.SetBinding(TextBlock.TextProperty,new Binding(property){Source=value});
        return new(){Header=label,Tag=value,IsExpanded=true};
    }
    private void Move(int delta)
    {
        var group=SelectedGroup;if(group is null)return;
        if(Selection is SubroutineRunParameter item)
        {var index=group.Parameters.IndexOf(item);if(index+delta>=0&&index+delta<group.Parameters.Count)group.Parameters.Move(index,index+delta);Rebuild(item.Id);}
        else
        {var groups=Session.Parameters.RunParameterGroups;var index=groups.IndexOf(group);if(index+delta>=0&&index+delta<groups.Count)groups.Move(index,index+delta);Rebuild(group.Id);}
    }
    private void TextEditor(string caption,object source,string property,bool configureOnly=false)
    {
        var panel=new StackPanel{Margin=new Thickness(0,0,0,10)};
        if(configureOnly)panel.SetBinding(VisibilityProperty,new Binding("IsChecked"){Source=_edit,Converter=new BooleanToVisibilityConverter()});
        panel.Children.Add(new TextBlock{Text=caption,Margin=new Thickness(0,0,0,4)});
        var edit=new TextBox{MinWidth=150};edit.SetBinding(TextBox.TextProperty,new Binding(property){Source=source,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnExceptions=true});
        panel.Children.Add(edit);_details.Children.Add(panel);
    }
    private void ShowDetails()
    {
        _details.Children.Clear();
        if(Selection is SubroutineRunParameterGroup group){TextEditor("分组名称",group,nameof(group.Name),true);return;}
        if(Selection is not SubroutineRunParameter item)
        { _details.Children.Add(new TextBlock{Text="选择运行参数；点击编辑可配置内部模块参数。",TextWrapping=TextWrapping.Wrap});return; }
        TextEditor("参数名称",item,nameof(item.Name),true);TextEditor("描述",item,nameof(item.Description),true);
        try
        {
            var slots=Session.Resolve(item);var type=slots[0].Property.PropertyType;
            _details.Children.Add(new TextBlock{Text=item.Name,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,10)});
            if(!GroupRunParameterSession.Scalar(type))
                _details.Children.Add(new TextBlock{Text="此复合参数的专用编辑器尚未接入。",TextWrapping=TextWrapping.Wrap});
            else
            {
                var value=new GroupValue(Session,item);FrameworkElement editor;
                var choices=GroupRunParameterSession.Choices(slots);
                if(choices is not null){editor=new ComboBox{ItemsSource=choices,DisplayMemberPath="DisplayName",SelectedValuePath="Value"};editor.SetBinding(Selector.SelectedValueProperty,ValueBinding(value));}
                else if(type==typeof(bool)){editor=new CheckBox{Content="启用"};editor.SetBinding(ToggleButton.IsCheckedProperty,ValueBinding(value));}
                else if(type.IsEnum){editor=new ComboBox{ItemsSource=Enum.GetValues(type)};editor.SetBinding(Selector.SelectedItemProperty,ValueBinding(value));}
                else {editor=new TextBox();editor.SetBinding(TextBox.TextProperty,ValueBinding(value));}
                editor.IsEnabled=(choices is null||choices.Count>0)&&!slots.Any(s=>s.Node.ParamSettings.GetSetting(s.Property.Name) is {BindingType:not BindingType.Constant});
                _details.Children.Add(editor);
                var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,10)};status.SetBinding(TextBlock.TextProperty,new Binding(nameof(GroupValue.Status)){Source=value});_details.Children.Add(status);
            }
        }
        catch(Exception ex){_details.Children.Add(new TextBlock{Text=ex.GetBaseException().Message,Foreground=Brushes.Gray,TextWrapping=TextWrapping.Wrap});}
        _details.Children.Add(new TextBlock{Text="关联参数",Margin=new Thickness(0,8,0,4)});
        foreach(var target in item.Targets.ToArray())
        {
            var row=new DockPanel();var remove=Button("移除关联",()=>{item.Targets.Remove(target);ShowDetails();});
            remove.SetBinding(VisibilityProperty,new Binding("IsChecked"){Source=_edit,Converter=new BooleanToVisibilityConverter()});DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            row.Children.Add(new TextBlock{Text=Session.TargetLabel(target),ToolTip=target.WorkflowId+" / "+target.NodeId+" / "+(target.ExposedParameterId?.ToString()??target.PropertyName),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});_details.Children.Add(row);
        }
    }
    private static Binding ValueBinding(GroupValue value)=>new(nameof(GroupValue.Value)){Source=value,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnExceptions=true};
    private sealed class GroupValue(GroupRunParameterSession session,SubroutineRunParameter item) : ObservableObject
    {
        public object? Value
        {
            get
            {
                var values=session.Resolve(item).Select(GroupRunParameterSession.Read).Distinct().Take(2).ToArray();
                return values.Length==1?values[0]:null;
            }
            set{session.Write(item,value);OnPropertyChanged();OnPropertyChanged(nameof(Status));}
        }
        public string Status
        {
            get
            {
                var slots=session.Resolve(item);
                if(slots.Any(s=>s.Node.ParamSettings.GetSetting(s.Property.Name) is {BindingType:not BindingType.Constant}))return "关联参数已有动态绑定，需在原工具中调整绑定。";
                if(GroupRunParameterSession.Choices(slots) is {Count:0})return "关联参数没有共同可选值，请调整关联。";
                var first=GroupRunParameterSession.Read(slots[0]);
                return slots.Any(s=>!Equals(first,GroupRunParameterSession.Read(s)))?"多个关联值不同；编辑后将统一这些关联值。":"";
            }
        }
    }
    public void Dispose(){_tree.Items.Clear();_catalog.ItemsSource=null;_details.Children.Clear();Content=null;}
}
