using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed record SourceChoice(string Identity,string Label,bool RequiresIndex)
{public override string ToString()=>Label;}
public sealed record SourceChoiceSelection(string Identity,int? Index);

/// <summary>A host-supplied source catalog, without SDK/native objects or an invented value declaration.</summary>
public sealed class SourceSelectionField : UserControl
{
    private readonly ComboBox _sources=new(){MinWidth=160};
    private readonly TextBox _index=new(){Text="0",MinWidth=45,Visibility=Visibility.Collapsed};
    private readonly TextBlock _current=new(){TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock _error=new(){Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap};
    private readonly Func<IReadOnlyList<SourceChoice>> _catalog;
    private readonly Func<SourceChoiceSelection?,string?> _write;
    private readonly Func<string> _read;
    private bool _refreshing;
    private bool _pending;
    public bool HasPendingEdits=>_pending;
    public string Error=>_error.Text;
    public SourceSelectionField(string title,Func<IReadOnlyList<SourceChoice>> catalog,Func<string> read,Func<SourceChoiceSelection?,string?> write)
    {
        (_catalog,_read,_write)=(catalog,read,write);
        Resources.MergedDictionaries.Add(EditorResources.Create());
        _error.Style=EditorResources.ErrorStyle();
        var panel=new StackPanel{Margin=new Thickness(0,3,0,3)};
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(89)});row.ColumnDefinitions.Add(new());
        row.Children.Add(new TextBlock{Text=title,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=title});
        _sources.MinWidth=0;_sources.Height=30;_sources.DisplayMemberPath=nameof(SourceChoice.Label);Grid.SetColumn(_sources,1);row.Children.Add(_sources);panel.Children.Add(row);
        var options=new StackPanel{Margin=new Thickness(89,3,0,0)};options.Children.Add(_index);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};
        var apply=new Button{Content="应用序号",Margin=new Thickness(3)};var restore=new Button{Content="撤销",Margin=new Thickness(3)};actions.Children.Add(apply);actions.Children.Add(restore);options.Children.Add(actions);panel.Children.Add(options);
        _current.IsHitTestVisible=false;_current.FontSize=14;_current.Margin=new Thickness(11,0,18,0);_current.VerticalAlignment=VerticalAlignment.Center;_current.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(_current,1);row.Children.Add(_current);_error.Margin=new Thickness(89,2,0,0);panel.Children.Add(_error);Content=panel;
        _sources.DropDownOpened+=(_,_)=>RefreshCatalog();
        _sources.SelectionChanged+=(_,_)=>
        {
            var indexed=(_sources.SelectedItem as SourceChoice)?.RequiresIndex==true;
            _index.Visibility=indexed?Visibility.Visible:Visibility.Collapsed;options.Visibility=indexed?Visibility.Visible:Visibility.Collapsed;
            _current.Visibility=_sources.SelectedItem is SourceChoice?Visibility.Collapsed:Visibility.Visible;
            if(_refreshing)return;
            _pending=true;
            if(_sources.SelectedItem is SourceChoice selected&&!selected.RequiresIndex)
            {if(selected.Identity.Length==0)ClearSource();else ApplySelection();}
        };
        _index.TextChanged+=(_,_)=>{if(!_refreshing)_pending=true;};
        apply.Click+=(_,_)=>ApplySelection();restore.Click+=(_,_)=>Reset();Reset();options.Visibility=Visibility.Collapsed;
    }
    public void Select(string identity,string index="0")
    {RefreshCatalog();_refreshing=true;try{_sources.SelectedItem=((IReadOnlyList<SourceChoice>)_sources.ItemsSource).SingleOrDefault(source=>source.Identity==identity);_index.Text=index;}finally{_refreshing=false;}_pending=true;}
    public bool ApplySelection()
    {
        if(_sources.SelectedItem is not SourceChoice choice){_error.Text="请选择已声明的来源。";_pending=true;return false;}
        if(choice.Identity.Length==0)return ClearSource();
        int? index=null;
        if(choice.RequiresIndex)
        {
            if(!int.TryParse(_index.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var number)||number<0)
            {_error.Text="集合来源必须明确选择非负整数序号。";_pending=true;return false;}index=number;
        }
        return Write(new(choice.Identity,index));
    }
    public bool ClearSource()=>Write(null);
    private bool Write(SourceChoiceSelection? choice)
    {
        try{_error.Text=_write(choice)??"";}
        catch(Exception error) when(error is ArgumentException or InvalidOperationException){_error.Text=error.Message;}
        _pending=_error.Text.Length>0;if(!_pending)Reset();return !_pending;
    }
    public void Reset(){_refreshing=true;try{_sources.SelectedItem=null;_current.Visibility=Visibility.Visible;_index.Text="0";_current.Text=_read();_sources.Text=_current.Text;_error.Text="";_pending=false;}finally{_refreshing=false;}}
    private void RefreshCatalog()
    {
        var selected=(_sources.SelectedItem as SourceChoice)?.Identity;_refreshing=true;
        try{var catalog=new[]{new SourceChoice("","未设置",false)}.Concat(_catalog()).ToArray();_sources.ItemsSource=catalog;_sources.SelectedItem=catalog.SingleOrDefault(item=>item.Identity==selected);if(_sources.SelectedItem is null)_sources.Text=_read();}
        finally{_refreshing=false;}
    }
}
