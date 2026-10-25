using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Regions;

/// <summary>Local geometry list and numeric editors. Gestures are delegated to a restricted host scope.</summary>
public sealed class RegionGeometryEditor : UserControl,IDisposable
{
    private readonly string _group;
    private readonly Func<IReadOnlyList<RoiEditItem>> _read;
    private readonly Func<Guid?,RecordValue?,Guid?> _write;
    private readonly Action<Guid,bool> _enable;
    private readonly ListBox _list=new(){DisplayMemberPath=nameof(RoiEditItem.Name),MaxHeight=130};
    private readonly StackPanel _numbers=new();
    private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap,Opacity=.8};
    private readonly CheckBox _enabled=new(){Content="启用所选区域"};
    private readonly Dictionary<string,TextBox> _inputs=new();
    private IRoiEditViewport? _viewport;
    private bool _refreshing;
    private bool _writing;
    public bool HasInvalidText {get;private set;}
    public string Error=>HasInvalidText?_status.Text:"";
    public Guid? SelectedId=>(_list.SelectedItem as RoiEditItem)?.Id;
    public event Action? ModelChanged;
    public RegionGeometryEditor(string group,string label,Func<IReadOnlyList<RoiEditItem>> read,Func<Guid?,RecordValue?,Guid?> write,Action<Guid,bool> enable)
    {
        _group=group;_read=read;_write=write;_enable=enable;
        var body=new StackPanel();var shapes=new WrapPanel();
        foreach(var kind in RoiGeometryEditing.Kinds)
        {
            var button=new Button{Content=RoiGeometryEditing.Label(kind),Margin=new Thickness(2),Padding=new Thickness(5,3,5,3)};
            button.Click+=(_,_)=>{if(HasInvalidText){_status.Text="请先修正区域数值或恢复有效值。";return;}if(_viewport is null)_status.Text="图像编辑视口尚未就绪。";else if(!_viewport.BeginDraw(_group,kind,out var error))_status.Text=error;};shapes.Children.Add(button);
        }
        body.Children.Add(shapes);var actions=new WrapPanel();
        foreach(var (labelText,action) in new[]{("选择",RoiEditAction.Select),("移动",RoiEditAction.Move),("缩放",RoiEditAction.Resize),("旋转",RoiEditAction.Rotate)})
        {var button=new Button{Content=labelText,Margin=new Thickness(2),Padding=new Thickness(5,2,5,2)};button.Click+=(_,_)=>{if(HasInvalidText){_status.Text="请先修正区域数值或恢复有效值。";return;}_viewport?.Select(_group,SelectedId);_viewport?.SetAction(action);};actions.Children.Add(button);}
        var delete=new Button{Content="删除",Margin=new Thickness(2)};delete.Click+=(_,_)=>{if(SelectedId is { } id){_write(id,null);Refresh();ModelChanged?.Invoke();}};actions.Children.Add(delete);
        var cancel=new Button{Content="取消绘制",Margin=new Thickness(2)};cancel.Click+=(_,_)=>_viewport?.CancelGesture();actions.Children.Add(cancel);
        var restore=new Button{Content="恢复有效值",Margin=new Thickness(2)};restore.Click+=(_,_)=>RenderNumbers();actions.Children.Add(restore);
        body.Children.Add(actions);body.Children.Add(_list);body.Children.Add(_enabled);body.Children.Add(_numbers);body.Children.Add(_status);
        _list.SelectionChanged+=(_,args)=>{if(_refreshing)return;if(HasInvalidText){_refreshing=true;_list.SelectedItem=args.RemovedItems.Cast<object>().FirstOrDefault();_refreshing=false;return;}RenderNumbers();if(_viewport is not null)_viewport.Select(_group,SelectedId);};
        _enabled.Click+=(_,_)=>{if(!_refreshing&&SelectedId is { } id){_enable(id,_enabled.IsChecked==true);ModelChanged?.Invoke();}};
        Content=new GroupBox{Header=label,Content=body,Padding=new Thickness(8),Margin=new Thickness(0,5,0,8)};Refresh();
    }
    public void SetViewport(IRoiEditViewport? viewport)
    {if(_viewport is not null){_viewport.GeometryChanged-=OnGeometry;_viewport.SelectionChanged-=OnSelection;_viewport.StatusChanged-=OnStatus;}_viewport=viewport;if(viewport is not null){viewport.GeometryChanged+=OnGeometry;viewport.SelectionChanged+=OnSelection;viewport.StatusChanged+=OnStatus;}}
    private void OnStatus(string message){if(!HasInvalidText)_status.Text=message;}
    public void Refresh(Guid? selected=null)
    {
        var id=selected??SelectedId;_refreshing=true;
        try{var rows=_read();_list.ItemsSource=rows;_list.SelectedItem=rows.SingleOrDefault(row=>row.Id==id);_enabled.IsChecked=(_list.SelectedItem as RoiEditItem)?.Enabled;}
        finally{_refreshing=false;}
        if(!_writing)RenderNumbers();
    }
    private void OnSelection(string group,Guid? id)
    {if(group==_group&&SelectedId!=id){if(HasInvalidText){_viewport?.Select(_group,SelectedId);return;}Refresh(id);}}
    private void OnGeometry(RoiGeometryChange change)
    {
        if(change.Group!=_group)return;
        try{var id=_write(change.Id,change.Geometry);Refresh(id);_viewport?.Select(_group,id);ModelChanged?.Invoke();}
        catch(ArgumentException error){_status.Text=error.Message;}
    }
    private void RenderNumbers()
    {
        _numbers.Children.Clear();_inputs.Clear();HasInvalidText=false;_status.Text="";
        if(_list.SelectedItem is not RoiEditItem item)return;
        var shape=item.Geometry;var kind=shape.Get<string>("kind");
        void Add(string key,string label,double value)
        {
            var row=new Grid{Margin=new Thickness(0,2,0,2)};row.ColumnDefinitions.Add(new(){Width=new GridLength(125)});row.ColumnDefinitions.Add(new());
            row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});var box=new TextBox{Text=value.ToString("G17",CultureInfo.InvariantCulture)};
            box.Tag=key;Grid.SetColumn(box,1);row.Children.Add(box);_inputs.Add(key,box);box.TextChanged+=(_,_)=>ApplyNumbers();_numbers.Children.Add(row);
        }
        if(kind=="polygon")
        {var vertices=RoiGeometryEditing.Vertices(shape);for(var i=0;i<vertices.Length;i++){Add("x:"+i,"顶点"+(i+1)+" X",vertices[i].X);Add("y:"+i,"顶点"+(i+1)+" Y",vertices[i].Y);}}
        else if(shape.Contains("center")){var p=RoiGeometryEditing.Center(shape);Add("x","中心X",p.X);Add("y","中心Y",p.Y);}
        foreach(var (key,label) in new[]{("direction-x","方向X"),("direction-y","方向Y"),("radius","半径"),("inner-radius","内半径"),("outer-radius","外半径"),("width","宽"),("height","高"),("angle-degrees","旋转角度"),("start-radians","起始角度"),("end-radians","结束角度")})
            if(shape.Contains(key))Add(key,label,shape.Get<double>(key)*(key.EndsWith("radians")?180/Math.PI:1));
        if(kind=="arc")
        {
            var major=new CheckBox{Content="大弧",IsChecked=shape.Contains("major-arc")&&shape.Get<bool>("major-arc")};
            major.Click+=(_,_)=>{if(HasInvalidText){major.IsChecked=!major.IsChecked;return;}var values=RoiGeometryEditing.Values((_list.SelectedItem as RoiEditItem)!.Geometry);values["major-arc"]=major.IsChecked==true;_write(SelectedId,RoiGeometryEditing.Checked(values));Refresh();ModelChanged?.Invoke();};
            _numbers.Children.Add(major);
        }
    }
    private void ApplyNumbers()
    {
        if(_list.SelectedItem is not RoiEditItem item)return;
        try
        {
            var parsed=_inputs.ToDictionary(pair=>pair.Key,pair=>double.Parse(pair.Value.Text,CultureInfo.InvariantCulture));
            if(parsed.Values.Any(value=>!double.IsFinite(value)))throw new ArgumentException("请输入有限数值。");
            var values=RoiGeometryEditing.Values(item.Geometry);
            if(item.Geometry.Get<string>("kind")=="polygon")
                values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,
                    Enumerable.Range(0,parsed.Count/2).Select(index=>(object?)RoiGeometryEditing.Point(new(parsed["x:"+index],parsed["y:"+index]))));
            else if(parsed.ContainsKey("x"))values["center"]=RoiGeometryEditing.Point(new(parsed["x"],parsed["y"]));
            foreach(var pair in parsed.Where(pair=>pair.Key!="x"&&pair.Key!="y"&&!pair.Key.Contains(':')))
                values[pair.Key]=pair.Value*(pair.Key.EndsWith("radians")?Math.PI/180:1);
            var geometry=RoiGeometryEditing.Checked(values);_writing=true;
            try{_write(item.Id,geometry);Refresh(item.Id);ModelChanged?.Invoke();}finally{_writing=false;}
            HasInvalidText=false;_status.Foreground=Brushes.Gray;_status.Text="数值已写入当前草稿。";
        }
        catch(Exception error) when(error is ArgumentException or FormatException or OverflowException)
        {HasInvalidText=true;_status.Foreground=Brushes.OrangeRed;_status.Text=error.Message;}
    }
    public void Dispose(){SetViewport(null);ModelChanged=null;}
}
