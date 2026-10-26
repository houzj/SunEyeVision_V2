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
    private readonly TextBlock _error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,2,8,4)};
    private readonly Button _edit=new(){Content="编辑",Width=80,Height=28};
    private bool _rebuilding;
    private GroupRunParameterConfigWindow? _configurationWindow;
    private TreeViewItem? _previous;
    private object? Selection => (_tree.SelectedItem as TreeViewItem)?.Tag;
    internal GroupRunParametersControl(GroupRunParameterSession session)
    {
        Session=session;
        SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        _tree.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        var root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"ThemePanelBackgroundBrush");
        root.RowDefinitions.Add(new(){Height=new GridLength(44)});
        root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel{Margin=new Thickness(10,0,10,0)};
        DockPanel.SetDock(_edit,Dock.Right);header.Children.Add(_edit);
        header.Children.Add(new TextBlock{Text="运行参数",FontSize=14,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
        _edit.Click+=(_,_)=>OpenConfiguration();
        var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(300)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        _tree.BorderThickness=new Thickness(0);_tree.SetResourceReference(Control.BackgroundProperty,"ThemePanelBackgroundBrush");content.Children.Add(_tree);
        var scroll=new ScrollViewer{Content=_details,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);content.Children.Add(scroll);Grid.SetRow(content,1);root.Children.Add(content);
        Grid.SetRow(_error,2);root.Children.Add(_error);Content=root;
        _tree.SelectedItemChanged+=(_,_)=>
        {
            if(_rebuilding)return;
            if(HasErrors(_details)&&_previous is not null)
            { _rebuilding=true;_previous.IsSelected=true;_rebuilding=false;_error.Text="请先修正当前参数的输入错误。";return; }
            _previous=_tree.SelectedItem as TreeViewItem;ShowDetails();
        };
        BindParameters(Session.Parameters);
    }
    internal void OpenConfiguration()
    {
        if(HasErrors(_details)){_error.Text="请先修正当前参数输入。";return;}
        var dialog=new GroupRunParameterConfigWindow(Session,Window.GetWindow(this));_configurationWindow=dialog;
        try{dialog.ShowDialog();}finally{_configurationWindow=null;Rebuild();}
    }
    internal bool TryPrepare(out string error)
    {
        if(_configurationWindow is {} dialog)return dialog.TryPrepare(out error);
        error="";return true;
    }
    internal bool HasPendingConfigurationEdits=>_configurationWindow?.HasPendingEdits==true;
    internal void Deactivate()=>_configurationWindow?.ForceClose();
    private static bool HasErrors(DependencyObject parent)
    {
        if(Validation.GetHasError(parent))return true;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)if(HasErrors(VisualTreeHelper.GetChild(parent,i)))return true;
        return false;
    }
    public void BindParameters(SubroutineParameters parameters)
    {
        if(!ReferenceEquals(parameters,Session.Parameters))throw new InvalidOperationException("运行参数页与Group候选不一致。");
        _error.Text="";Rebuild();_configurationWindow?.RefreshFromSession();
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
    private void ShowDetails()
    {
        _details.Children.Clear();
        if(Selection is SubroutineRunParameterGroup group){_details.Children.Add(new TextBlock{Text=group.Name,FontWeight=FontWeights.Bold});return;}
        if(Selection is not SubroutineRunParameter item)
        { _details.Children.Add(new TextBlock{Text="选择运行参数；点击编辑可配置内部模块参数。",TextWrapping=TextWrapping.Wrap});return; }
        var description=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
        description.SetBinding(TextBlock.TextProperty,new Binding(nameof(item.Description)){Source=item});_details.Children.Add(description);
        try
        {
            var slots=Session.Resolve(item);var type=slots[0].Property.PropertyType;
            var title=new TextBlock{FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,10)};title.SetBinding(TextBlock.TextProperty,new Binding(nameof(item.Name)){Source=item});_details.Children.Add(title);
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
            _details.Children.Add(new TextBlock{Text=Session.TargetLabel(target),ToolTip=target.WorkflowId+" / "+target.NodeId+" / "+(target.ExposedParameterId?.ToString()??target.PropertyName),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
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
    public void Dispose(){Deactivate();_tree.Items.Clear();_details.Children.Clear();Content=null;}
}
