using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Windows;

namespace SunEyeVision.Tool.Subroutine.Views;
public sealed class SubroutineIoPicker : SdkDialogWindow
{
    private readonly List<(CheckBox Check,AvailableDataSource Source)> _entries=new();
    private readonly CheckBox _include=new(){Content="包含模块名称",IsChecked=true,Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    private readonly Func<bool> _discard;
    private bool _updating,_forced,_closing,_closed;
    public IReadOnlyList<AvailableDataSource> SelectedSources=>_entries.Where(e=>e.Check.IsChecked==true).Select(e=>e.Source).ToArray();
    public bool IncludeModuleName=>_include.IsChecked==true;
    public SubroutineIoPicker(Window owner,IEnumerable<AvailableDataSource> sources,Func<bool>? discard=null)
    {
        Owner=owner;Resources.MergedDictionaries.Add(owner.Resources);Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.Tool.Subroutine;component/Resources/GroupIo/IoTreeStyle.xaml",UriKind.Relative)});Title="批量选择";Width=370;Height=420;MinWidth=370;MinHeight=420;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty,"ThemePanelBackgroundBrush");SetResourceReference(ForegroundProperty,"ThemeTextPrimaryBrush");
        _discard=discard??(()=>MessageBox.Show(this,"是否放弃已勾选信息直接退出？",Title,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes);
        var root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"ThemePanelBackgroundBrush");root.RowDefinitions.Add(new(){Height=new GridLength(52)});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=new GridLength(32)});
        root.Children.Add(new TextBlock{Text="选择列表",FontSize=14,FontWeight=FontWeights.Bold,Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center});
        var tree=new TreeView{Name="ImportSourcesTree",Margin=new Thickness(18,0,18,10),Padding=new Thickness(10),BorderThickness=new Thickness(1),FontSize=14};tree.Resources[typeof(TreeViewItem)]=Resources["VmIoTreeItem"];tree.SetResourceReference(Control.BorderBrushProperty,"ThemeBorderBrush");tree.SetResourceReference(Control.BackgroundProperty,"ThemeControlBackgroundBrush");tree.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");Grid.SetRow(tree,1);root.Children.Add(tree);
        foreach(var module in sources.Where(s=>s.IsValid&&SdkTypeVocabulary.ToTypeVocabulary(s.PropertyType)!=null).DistinctBy(s=>(s.SourceWorkflowId,s.SourceNodeId,s.SourceFieldId,s.GetBindingPath())).GroupBy(s=>(s.SourceWorkflowId,s.SourceNodeId,s.SourceNodeName)))
        {
            var check=new CheckBox{Content=module.Key.SourceNodeName,IsThreeState=false,IsChecked=false};var parent=new TreeViewItem{Header=check,IsExpanded=false};tree.Items.Add(parent);
            var children=new List<CheckBox>();
            foreach(var source in module)
            {
                var leaf=new CheckBox{Content=string.IsNullOrWhiteSpace(source.DisplayName)?source.PropertyName:source.DisplayName,Tag=source,IsChecked=false};leaf.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");children.Add(leaf);_entries.Add((leaf,source));parent.Items.Add(new TreeViewItem{Header=leaf});
                leaf.Click+=(_,_)=>{if(_updating)return;_updating=true;check.IsChecked=children.All(c=>c.IsChecked==true)?true:children.All(c=>c.IsChecked!=true)?false:null;_updating=false;};
            }
            check.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");check.Click+=(_,_)=>{if(_updating)return;_updating=true;var selected=check.IsChecked==true;foreach(var child in children)child.IsChecked=selected;check.IsChecked=selected;_updating=false;};
        }
        var footer=new DockPanel();footer.SetResourceReference(Panel.BackgroundProperty,"ThemeStatusBarBackgroundBrush");Grid.SetRow(footer,2);root.Children.Add(footer);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);
        var cancel=new Button{Content="取消",Width=66,Height=24,Margin=new Thickness(0,0,15,0)};var confirm=new Button{Content="确定",Width=66,Height=24,Margin=new Thickness(0,0,15,0)};
        buttons.Children.Add(cancel);buttons.Children.Add(confirm);footer.Children.Add(_include);_include.SetResourceReference(Control.ForegroundProperty,"ThemeTextPrimaryBrush");
        cancel.Click+=(_,_)=>Close();confirm.Click+=(_,_)=>DialogResult=true;Content=root;
        owner.Closing+=OwnerClosing;
        Closing+=(_,e)=>{_closing=true;if(!_forced&&DialogResult!=true&&SelectedSources.Count>0){var discard=_discard();if(!_forced&&!discard){e.Cancel=true;_closing=false;}}};
        Closed+=(_,_)=>{_closed=true;owner.Closing-=OwnerClosing;};
    }
    internal void ForceClose(){_forced=true;if(!_closed&&!_closing)Close();}
    private void OwnerClosing(object? sender,CancelEventArgs e){if(!e.Cancel)ForceClose();}
}
