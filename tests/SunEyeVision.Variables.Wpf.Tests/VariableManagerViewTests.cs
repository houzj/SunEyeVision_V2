using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using Xunit;
namespace SunEyeVision.Variables.Wpf.Tests;
[Collection("正式WPF owner")]
public sealed class VariableManagerViewTests
{
 private static IEnumerable<T> Visuals<T>(DependencyObject node)where T:DependencyObject
 {if(node is T value)yield return value;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
 [Fact] public void Same_public_view_renders_independently_with_original_labels_and_forwards_raw_input()
 {
  SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
  {
   var design=new VariableManagerDesignData();var view=new VariableManagerView{DataContext=design};
   var window=new Window{Content=view,Width=1170,Height=683,Left=-32000,Top=-32000,ShowInTaskbar=false,ShowActivated=false};
   try
   {
    window.Show();window.UpdateLayout();var grid=Visuals<DataGrid>(view).Single();Assert.Equal(9,grid.Columns.Count);
    foreach(var column in grid.Columns)Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<Label>(column.Header).Content?.ToString()));
    var row=design.CurrentVariables.First();var editor=Visuals<TextBox>(view).Single(box=>box.Name=="VariableNameEditor"&&ReferenceEquals(box.DataContext,row));
    bool received=false;view.EditorAction+=(kind,sender,_)=>{if(kind=="NameChanged"&&ReferenceEquals(sender,editor))received=true;};
    editor.Text="原始输入";Assert.True(received);Assert.Equal("产品编号",row.Name);
    Assert.Contains(Visuals<Button>(view),button=>button.Name=="btnImport"&&!button.IsEnabled);
    Assert.Contains(Visuals<Button>(view),button=>button.Name=="btnExport"&&!button.IsEnabled);
   }
   finally{window.Close();}
  },loadShellResources:false);
 }
}
