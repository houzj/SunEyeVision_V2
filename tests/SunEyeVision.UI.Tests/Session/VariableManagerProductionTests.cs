using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.UI.Features.GlobalVariableManager;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class VariableManagerProductionTests
{
 [Fact] public void Reused_original_group_menu_follows_actual_placement_target_on_every_open()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var vm=new GlobalVariableManagerViewModel(f.Session,()=>true,()=>true);
  var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   var first=vm.SelectedGroup!;vm.AddGroupCommand.Execute(null);var second=vm.SelectedGroup!;UiRuntime.Pump();
   var tabs=UiRuntime.Visuals<TabControl>(window).Single();
   foreach(var group in new[]{first,second})
   {
    var item=Assert.IsType<TabItem>(tabs.ItemContainerGenerator.ContainerFromItem(group));var menu=item.ContextMenu!;Assert.NotNull(menu);menu.PlacementTarget=item;menu.IsOpen=true;UiRuntime.Pump();
    Assert.Same(group,menu.DataContext);
    if(ReferenceEquals(group,second))menu.Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    menu.IsOpen=false;
   }
   Assert.False(first.IsEditing);Assert.True(second.IsEditing);
  }
  finally{window.Close();UiRuntime.Pump();}
 });
 [Fact] public void Original_group_header_enter_renames_actual_draft_and_undo_restores_saved_group()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var vm=new GlobalVariableManagerViewModel(f.Session,()=>true,()=>true);
  var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   vm.AddVariableCommand.Execute(null);f.Session.Editing.Apply(vm.ScopeId);UiRuntime.Pump();
   var group=vm.SelectedGroup!;var original=group.Name;group.BeginEdit();UiRuntime.Pump();
   var editor=UiRuntime.Visuals<GroupRenameTextBox>(window).Single(box=>ReferenceEquals(box.DataContext,group));editor.Text="读码组";
   editor.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(editor),0,System.Windows.Input.Key.Enter){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
   Assert.Equal("读码组",group.Name);Assert.Equal("读码组",vm.DraftVariables.Single().Group);Assert.False(group.IsEditing);
   var prepared=vm.Prepare(vm.Host);group.BeginEdit();editor.Text="回执后的组";
   var receipt=f.Session.CommitEdit(prepared);vm.Acknowledge(prepared,receipt);UiRuntime.Pump();
   Assert.Equal("读码组",f.Session.Document.GlobalVariables.Single().Group);Assert.Equal("回执后的组",vm.SelectedGroup!.EditingText);Assert.True(window.HasRetainedPendingEdits);
   f.Session.Editing.Apply(vm.ScopeId);Assert.Equal("回执后的组",f.Session.Document.GlobalVariables.Single().Group);window.Close();UiRuntime.Pump();
   f.Session.Undo();Assert.Equal("读码组",f.Session.Document.GlobalVariables.Single().Group);f.Session.Undo();Assert.Equal(original,f.Session.Document.GlobalVariables.Single().Group);
   f.Session.Redo();f.Session.Redo();Assert.Equal("回执后的组",f.Session.Document.GlobalVariables.Single().Group);
  }
  finally{window.Close();UiRuntime.Pump();}
 });
 [Fact] public void Top_preserves_original_global_index_shift_across_interleaved_groups()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var vm=new GlobalVariableManagerViewModel(f.Session,()=>true,()=>true);
  try
  {
   var firstGroup=vm.SelectedGroup!;vm.AddVariableCommand.Execute(null);var first=vm.SelectedVariable!;
   vm.AddGroupCommand.Execute(null);vm.AddVariableCommand.Execute(null);var between=vm.SelectedVariable!;
   vm.SelectedGroup=firstGroup;vm.AddVariableCommand.Execute(null);var moved=vm.SelectedVariable!;
   vm.MoveTopCommand.Execute(null);Assert.Equal(1,moved.Index);Assert.Equal(2,first.Index);Assert.Equal(3,between.Index);
   f.Session.Editing.Apply(vm.ScopeId);Assert.Equal(3,f.Session.Document.GlobalVariables.Select(item=>item.Index).Distinct().Count());
  }
  finally{vm.Discard();}
 });
 [Fact] public void Original_manager_composition_pages_orders_and_commits_one_real_variable_draft()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var vm=new GlobalVariableManagerViewModel(f.Session,()=>true,()=>true);
  var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   Assert.IsType<VariableManagerView>(window.Content);
   for(var i=0;i<25;i++)vm.AddVariableCommand.Execute(null);
   Assert.Equal(12,vm.PageCount);Assert.Equal(25,vm.ItemsTotalCount);Assert.Equal(3,vm.CurrentPageNum);Assert.Single(vm.CurrentVariables);
   vm.CurrentPageNum=1;UiRuntime.Pump();Assert.Equal(12,vm.CurrentVariables.Count);
   var moved=vm.CurrentVariables[1];vm.SelectedVariable=moved;Assert.True(vm.MoveTopCommand.CanExecute(null));vm.MoveTopCommand.Execute(null);Assert.Same(moved,vm.CurrentVariables[0]);
   var grid=UiRuntime.Visuals<DataGrid>(window).Single();Assert.Equal(9,grid.Columns.Count);Assert.False(grid.AutoGenerateColumns);
   var history=f.Session.HistoryEntries.Count;f.Session.Editing.Apply(vm.ScopeId);UiRuntime.Pump();Assert.Equal(history+1,f.Session.HistoryEntries.Count);
   Assert.Equal(moved.Id,f.Session.Document.GlobalVariables.OrderBy(item=>item.Index).First().Id);
   Assert.Equal(25,vm.DraftVariables.Count);Assert.True(vm.IsValid);
   f.Save();Assert.False(f.Session.HasUnsavedChanges);
  }
  finally{window.Close();UiRuntime.Pump();}
  Assert.Equal(0,f.Session.Editing.RegisteredScopeCount);
 });
 [Fact] public void Virtualized_raw_text_and_receipt_newer_input_survive_and_array_child_retires()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var vm=new GlobalVariableManagerViewModel(f.Session,()=>true,()=>true);
  var window=new GlobalVariableManagerDialog(vm){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
  try
  {
   vm.AddVariableCommand.Execute(null);var value=vm.CurrentVariables.Single();value.Type="Integer";vm.SetDraftValue(value,new long[]{12});UiRuntime.Pump();
   var box=UiRuntime.Visuals<TextBox>(window).Single(item=>item.Name=="VariableValueEditor");box.Text="bad";UiRuntime.Pump();
   vm.SearchText="no match";UiRuntime.Pump();Assert.False(window.TryCommitPendingEdits(out _));
   vm.SearchText="";UiRuntime.Pump();box=UiRuntime.Visuals<TextBox>(window).Single(item=>item.Name=="VariableValueEditor");Assert.Equal("bad",box.Text);
   box.Text="17";Assert.True(window.TryCommitPendingEdits(out var error),error);
   var proposal=vm.Prepare(vm.Host);box.Text="23";var receipt=f.Session.CommitEdit(proposal);vm.Acknowledge(proposal,receipt);UiRuntime.Pump();
   Assert.Equal(17L,Assert.IsType<long[]>(f.Session.Document.GlobalVariables.Single().Value).Single());
   Assert.Contains("23",window.PendingEditStamp);f.Session.Editing.Apply(vm.ScopeId);Assert.Equal(23L,Assert.IsType<long[]>(f.Session.Document.GlobalVariables.Single().Value).Single());
   var current=vm.DraftVariables.Single();current.Type="Integer[]";vm.SetDraftValue(current,new long[]{1,2,3});UiRuntime.Pump();
   Exception? childFailure=null;bool inspected=false;
   window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{try{var child=System.Windows.Application.Current.Windows.OfType<VariableArrayDialog>().Single();Assert.Same(window,child.Owner);Assert.Equal(3,child.ItemsTotalCount);Assert.Equal(40,child.PageCount);inspected=true;}catch(Exception error){childFailure=error;}finally{window.Close();}}));
   var details=UiRuntime.Visuals<Button>(window).Single(button=>button.IsVisible&&button.DataContext==current&&button.Name=="ArrayDetails");details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UiRuntime.Pump();Assert.Null(childFailure);Assert.True(inspected);
  }
  finally{window.Close();UiRuntime.Pump();}
  Assert.Empty(System.Windows.Application.Current.Windows.OfType<VariableArrayDialog>());Assert.False(vm.IsValid);
 });
}
