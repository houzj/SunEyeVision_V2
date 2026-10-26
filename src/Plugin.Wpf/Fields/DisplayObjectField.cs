using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>One declared display object: visibility inline, style in a private modal draft.</summary>
public sealed class DisplayObjectField : Grid
{
    private readonly IReadOnlyList<(DeclaredFieldEditor Editor,string Label)> _style;
    private readonly string _title;
    public DisplayObjectField(string title,DeclaredFieldEditor visibility,IReadOnlyList<(DeclaredFieldEditor Editor,string Label)> style)
    {
        _title=title;_style=style;Margin=new Thickness(0,1,0,1);
        ColumnDefinitions.Add(new(){Width=new GridLength(138)});ColumnDefinitions.Add(new(){Width=GridLength.Auto});ColumnDefinitions.Add(new());
        visibility.SetLabel(title);Children.Add(visibility);
        var edit=new Button{Content=EditorIcons.Pencil(),ToolTip="编辑"+title+"样式",Width=29,Height=29,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(8,0,0,0)};
        edit.Click+=(_,_)=>OpenStyleEditor();SetColumn(edit,1);Children.Add(edit);
    }
    public void OpenStyleEditor()
    {
        var content=new StackPanel{Margin=new Thickness(16)};
        var staged=_style.Select(item=>(item.Editor,Clone:item.Editor.CreateStagedCopy(item.Label))).ToArray();
        foreach(var item in staged)content.Children.Add(item.Clone);
        var error=new TextBlock{Foreground=Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(16,4,16,4)};
        var apply=new Button{Content="确定",MinWidth=75,Padding=new Thickness(12,4,12,4),Margin=new Thickness(4)};
        var cancel=new Button{Content="取消",MinWidth=75,Padding=new Thickness(12,4,12,4),Margin=new Thickness(4),IsCancel=true};
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(8)};buttons.Children.Add(apply);buttons.Children.Add(cancel);
        var root=new DockPanel();DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);DockPanel.SetDock(error,Dock.Bottom);root.Children.Add(error);root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var window=new Window{Title=_title+"显示样式",Content=root,Width=380,Height=440,MinWidth=330,MinHeight=280,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(242,243,245)),Foreground=new SolidColorBrush(Color.FromRgb(102,102,102)),FontSize=14};
        window.Resources.MergedDictionaries.Add(EditorResources.Create());
        if(Window.GetWindow(this) is {} owner)window.Owner=owner;
        apply.Click+=(_,_)=>
        {
            error.Text=staged.FirstOrDefault(item=>item.Clone.HasInvalidText).Clone?.Error??"";
            if(error.Text.Length>0)return;
            var backups=staged.Select(item=>(item.Editor,item.Editor.Setting)).ToArray();
            foreach(var item in staged)
                if(!item.Editor.TryApplySetting(item.Clone.Setting))
                {
                    error.Text=item.Editor.Error;
                    foreach(var backup in backups)if(!backup.Editor.TryApplySetting(backup.Setting))error.Text+="；恢复失败："+backup.Editor.Error;
                    return;
                }
            window.DialogResult=true;
        };
        cancel.Click+=(_,_)=>window.DialogResult=false;
        window.ShowDialog();
    }
}
