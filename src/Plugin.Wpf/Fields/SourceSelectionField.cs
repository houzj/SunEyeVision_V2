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
        var panel=new StackPanel{Margin=new Thickness(0,6,0,10)};
        panel.Children.Add(new TextBlock{Text=title,FontWeight=FontWeights.SemiBold});panel.Children.Add(_current);
        var row=new WrapPanel();row.Children.Add(_sources);row.Children.Add(_index);
        var apply=new Button{Content="应用来源",Margin=new Thickness(4)};var clear=new Button{Content="清除来源",Margin=new Thickness(4)};
        var restore=new Button{Content="撤销选择",Margin=new Thickness(4)};
        row.Children.Add(apply);row.Children.Add(clear);row.Children.Add(restore);panel.Children.Add(row);panel.Children.Add(_error);Content=panel;
        _sources.DropDownOpened+=(_,_)=>RefreshCatalog();
        _sources.SelectionChanged+=(_,_)=>{_index.Visibility=(_sources.SelectedItem as SourceChoice)?.RequiresIndex==true?Visibility.Visible:Visibility.Collapsed;if(!_refreshing)_pending=true;};
        _index.TextChanged+=(_,_)=>{if(!_refreshing)_pending=true;};
        apply.Click+=(_,_)=>ApplySelection();clear.Click+=(_,_)=>ClearSource();restore.Click+=(_,_)=>Reset();Reset();
    }
    public void Select(string identity,string index="0")
    {RefreshCatalog();_sources.SelectedItem=((IReadOnlyList<SourceChoice>)_sources.ItemsSource).SingleOrDefault(source=>source.Identity==identity);_index.Text=index;_pending=true;}
    public bool ApplySelection()
    {
        if(_sources.SelectedItem is not SourceChoice choice){_error.Text="请选择已声明的来源。";_pending=true;return false;}
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
    public void Reset(){_refreshing=true;try{_sources.SelectedItem=null;_index.Text="0";_current.Text=_read();_error.Text="";_pending=false;}finally{_refreshing=false;}}
    private void RefreshCatalog()
    {
        var selected=(_sources.SelectedItem as SourceChoice)?.Identity;_refreshing=true;
        try{var catalog=_catalog();_sources.ItemsSource=catalog;_sources.SelectedItem=catalog.SingleOrDefault(item=>item.Identity==selected);}
        finally{_refreshing=false;}
    }
}
