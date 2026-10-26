using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>Finite presentation of ROI source choices. The host owns all values and execution.</summary>
public sealed class RegionSourceModeSelector : UserControl
{
    private string _mode;
    private readonly Func<string,bool> _write;
    private readonly List<(RadioButton Button,Func<bool> Selected)> _buttons=new();
    private readonly StackPanel _creation=new(),_inheritance=new();
    private bool _updating;
    private readonly string _groupPrefix=Guid.NewGuid().ToString("N");
    private string _graphic= nameof(RegionFieldMode.LocalGeometry);
    private string _inherited= nameof(RegionFieldMode.InheritedRectangle);
    public RegionSourceModeSelector(string mode,Func<string,bool> write)
    {
        _mode=mode;_write=write;
        if(mode!=nameof(RegionFieldMode.InheritedRegion))_graphic=mode;
        if(mode!=nameof(RegionFieldMode.LocalGeometry)&&mode!=nameof(RegionFieldMode.InheritedRegion)&&mode!=nameof(RegionFieldMode.InheritedGeometry))_inherited=mode;
        var body=new StackPanel();Content=body;
        var type=Row(body,"ROI区域类型");
        Radio(type,"图形类型",()=>_mode!=nameof(RegionFieldMode.InheritedRegion),()=>Select(_graphic),"type");
        Radio(type,"图像类型",()=>_mode==nameof(RegionFieldMode.InheritedRegion),()=>Select(nameof(RegionFieldMode.InheritedRegion)),"type");
        body.Children.Add(_creation);
        var create=Row(_creation,"ROI创建");
        Radio(create,"绘制",()=>_mode==nameof(RegionFieldMode.LocalGeometry),()=>Select(nameof(RegionFieldMode.LocalGeometry)),"creation");
        Radio(create,"继承",()=>_mode!=nameof(RegionFieldMode.LocalGeometry)&&_mode!=nameof(RegionFieldMode.InheritedRegion),()=>Select(_inherited),"creation");
        body.Children.Add(_inheritance);
        var rectangle=Row(_inheritance,"继承方式");
        Option(rectangle,"按矩形区域",RegionFieldMode.InheritedRectangle);Option(rectangle,"按矩形参数",RegionFieldMode.InheritedRectangleParameters);
        var circle=Row(_inheritance,"");
        Option(circle,"按圆形区域",RegionFieldMode.InheritedCircle);Option(circle,"按圆形参数",RegionFieldMode.InheritedCircleParameters);
        Update();
    }
    private static Panel Row(Panel parent,string title)
    {
        var grid=new Grid{MinHeight=32,Margin=new Thickness(0,2,0,2)};
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(89)});grid.ColumnDefinitions.Add(new());
        grid.Children.Add(new TextBlock{Text=title,VerticalAlignment=VerticalAlignment.Center});
        var choices=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(choices,1);grid.Children.Add(choices);parent.Children.Add(grid);return choices;
    }
    private void Radio(Panel panel,string label,Func<bool> selected,Action click,string group)
    {
        var button=new RadioButton{Content=label,Focusable=true,MinWidth=88,Margin=new Thickness(0,0,6,0),GroupName=_groupPrefix+group,VerticalContentAlignment=VerticalAlignment.Center};
        button.SetResourceReference(StyleProperty,"NormalRadioButton");
        button.Click+=(_,_)=>{if(!_updating)click();};panel.Children.Add(button);_buttons.Add((button,selected));
    }
    private void Option(Panel panel,string label,RegionFieldMode mode)=>Radio(panel,label,()=>_mode==mode.ToString(),()=>Select(mode.ToString()),"inheritance");
    private void Select(string value)
    {
        if(!_write(value)){Update();return;}_mode=value;
        if(value!=nameof(RegionFieldMode.InheritedRegion))_graphic=value;
        if(value!=nameof(RegionFieldMode.InheritedRegion)&&value!=nameof(RegionFieldMode.LocalGeometry))_inherited=value;
        Update();
    }
    private void Update()
    {
        _updating=true;
        try
        {
            foreach(var (button,selected) in _buttons)button.IsChecked=selected();
            _creation.Visibility=_mode==nameof(RegionFieldMode.InheritedRegion)?Visibility.Collapsed:Visibility.Visible;
            _inheritance.Visibility=_mode is nameof(RegionFieldMode.LocalGeometry) or nameof(RegionFieldMode.InheritedRegion)?Visibility.Collapsed:Visibility.Visible;
        }
        finally{_updating=false;}
    }
}
