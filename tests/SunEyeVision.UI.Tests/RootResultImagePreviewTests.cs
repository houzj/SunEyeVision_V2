using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.UI.Shell.Windows;
using Xunit;

namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class RootResultImagePreviewTests
{
 [Fact]
 public void Actual_root_window_requires_explicit_port_reuses_snapshot_and_releases_on_close()
  =>ImageThresholdProductionEditorTests.Sta(()=>
 {
  using var f=new ResultWorkspaceProductionTests.Fixture();
  var window=f.Main.OpenRootConfiguration(f.Main.WorkflowTabViewModel.SelectedTab!,f.Host)!;
  try
  {
   Assert.NotNull(window);var nodes=(ComboBox)window.FindName("ImageNodes");var ports=(ComboBox)window.FindName("ImagePorts");
   nodes.SelectedItem=f.Source;window.RefreshPublishedResults();
   Assert.Null(window.ImagePreviewPane?.CurrentFrame);Assert.True(ports.Items.Count>=2);Assert.Null(ports.SelectedItem);
   ports.SelectedIndex=0;window.RefreshPublishedResults();
   var pane=Assert.IsType<SunEyeVision.UI.Services.Images.ResultImagePane>(window.ImagePreviewPane);
   var receipt=pane.CurrentFrame;Assert.NotNull(receipt);Assert.Equal(new byte[]{21},pane.Viewport.ReadPixel(0,0));
   var table=f.Main.CurrentResultPublication;
   window.RefreshPublishedResults();Assert.Same(receipt,pane.CurrentFrame);
   ports.SelectedIndex=1;window.RefreshPublishedResults();Assert.Equal(receipt!.Frame,pane.CurrentFrame!.Frame);
   nodes.SelectedItem=f.Selected;window.RefreshPublishedResults();Assert.Null(ports.SelectedItem);Assert.Null(pane.CurrentFrame);
   ports.SelectedIndex=0;window.RefreshPublishedResults();Assert.Equal(new byte[]{41},pane.Viewport.ReadPixel(0,0));
   window.Close();Assert.Null(pane.CurrentFrame);Assert.Same(table,f.Main.CurrentResultPublication);Assert.True(table!.IsCurrent());
   f.Dispose();Assert.All(f.Runs,run=>Assert.Equal(1,run.Releases));
  }
  finally{window.Close();}
 });
 [Fact]
 public void Actual_root_window_replaces_new_run_and_rejects_wrong_scope_and_document()
  =>ImageThresholdProductionEditorTests.Sta(()=>
 {
  using var f=new ResultWorkspaceProductionTests.Fixture();
  var window=f.Main.OpenRootConfiguration(f.Main.WorkflowTabViewModel.SelectedTab!,f.Host)!;
  var foreign=new ToolExecutionOwner();
  try
  {
   var nodes=(ComboBox)window.FindName("ImageNodes");var ports=(ComboBox)window.FindName("ImagePorts");
   nodes.SelectedItem=f.Selected;window.RefreshPublishedResults();ports.SelectedIndex=0;window.RefreshPublishedResults();
   var pane=window.ImagePreviewPane!;var old=pane.CurrentFrame!.Frame;
   var next=f.Publish(f.Selected,71);window.RefreshPublishedResults();
   Assert.NotNull(ports.SelectedItem);Assert.Equal(next.Image.Frame,pane.CurrentFrame!.Frame);Assert.NotEqual(old,pane.CurrentFrame.Frame);
   Assert.Equal(new byte[]{71},pane.Viewport.ReadPixel(0,0));
   f.Publish(f.Selected,81,scope:"wrong-root");window.RefreshPublishedResults();Assert.Null(pane.CurrentFrame);Assert.Empty(ports.Items.Cast<object>());
   f.Publish(f.Selected,91,owner:foreign);window.RefreshPublishedResults();Assert.Null(pane.CurrentFrame);Assert.Null(ports.SelectedItem);
  }
  finally{window.Close();foreign.Retire();}
 });
 [Fact]
 public void Actual_node_selection_contract_failure_clears_old_physical_image_and_reports_the_error()
  =>ImageThresholdProductionEditorTests.Sta(()=>
 {
  using var f=new ResultWorkspaceProductionTests.Fixture();
  var window=f.Main.OpenRootConfiguration(f.Main.WorkflowTabViewModel.SelectedTab!,f.Host)!;
  var reader=window.Context.Query.FormalOutputContractReader;
  try
  {
   var nodes=(ComboBox)window.FindName("ImageNodes");var ports=(ComboBox)window.FindName("ImagePorts");
   nodes.SelectedItem=f.Source;window.RefreshPublishedResults();ports.SelectedIndex=0;window.RefreshPublishedResults();
   Assert.NotNull(window.ImagePreviewPane!.CurrentFrame);
   window.Context.Query.FormalOutputContractReader=_=>throw new FormatException("root-contract-read-failed");
   nodes.SelectedItem=f.Selected;
   Assert.Null(window.ImagePreviewPane.CurrentFrame);
   Assert.Null(((SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.ImageViewBox)window.FindName("Preview")).ImageDataSource);
   Assert.Contains("root-contract-read-failed",((TextBlock)window.FindName("Error")).Text);
  }
  finally{window.Context.Query.FormalOutputContractReader=reader;window.Close();}
 });

}
