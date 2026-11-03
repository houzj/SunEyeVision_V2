using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;
using SunEyeVision.UI.Features.GlobalVariableManager;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class SourceAndVariablesSessionTests
{
 [Fact] public void Source_strip_start_edit_undo_and_stale_request_use_one_actual_session()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image(11),f.Image(22));f.Main.SelectedNode=node;UiRuntime.Pump();
  var binding=f.Main.ActiveImageSourceBinding;Assert.NotNull(binding);var strip=binding.Strip;Assert.Equal(2,strip.Items.Count);Assert.True(strip.CanEdit);
  var version=strip.Version;var request=new ImageSourceStripRequest(ImageSourceStripRequestKind.SelectStart,version,strip.Items[1].Id);
  UiRuntime.Await(strip.RequestAsync(request,CancellationToken.None));Assert.Equal(1L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  f.Session.Undo();UiRuntime.Pump();Assert.Equal(0L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  Assert.Throws<InvalidOperationException>(()=>UiRuntime.Await(strip.RequestAsync(request,CancellationToken.None)));
  f.Main.SelectedNode=null;UiRuntime.Pump();Assert.Null(f.Main.ActiveImageSourceBinding);Assert.False(strip.CanEdit);Assert.False(strip.CanNavigate);
 });
 [Fact] public void Variable_window_private_candidate_commit_and_temporary_gate_keep_the_same_participant()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();bool current=true,editable=true;
  var vm=new GlobalVariableManagerViewModel(f.Session,()=>current,()=>editable);var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   vm.SelectedGroup=vm.Groups.First();Assert.True(vm.AddVariableCommand.CanExecute(null));vm.AddVariableCommand.Execute(null);var candidate=Assert.Single(vm.CurrentVariables);candidate.Value="正式值";
   Assert.Empty(f.Session.Document.GlobalVariables);Assert.True(vm.HasPendingChanges);
   editable=false;vm.RefreshAvailability();Assert.True(vm.IsValid);Assert.False(vm.CanEdit);Assert.False(vm.AddVariableCommand.CanExecute(null));
   editable=true;vm.RefreshAvailability();f.Session.Editing.Apply(vm.ScopeId);Assert.Equal("正式值",Assert.Single(f.Session.Document.GlobalVariables).Value);Assert.True(vm.IsValid);
   Assert.False(vm.ImportCommand.CanExecute(null));Assert.False(vm.ExportCommand.CanExecute(null));Assert.False(vm.OpenTargetParameterCommand.CanExecute(null));
  }
  finally{window.Close();UiRuntime.Pump();}
  Assert.False(vm.IsValid);current=false;
 });
}
