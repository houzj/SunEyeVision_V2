using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services.Editing;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class RootCollectionMainWindowTests
{
    [Fact] public void Root_navigation_preserves_session_owned_variable_window_and_its_private_input()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var original=f.Tab;Assert.True(Add(f));var other=f.Tab;f.Main.SetSelectedTab(original);
        var model=new SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerViewModel(f.Session,()=>true,()=>!f.Main.IsRunning);
        var tracked=UiRuntime.Field<HashSet<SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerViewModel>>(f.Main,"_variableEditors")!;tracked.Add(model);
        var window=new SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerDialog(model){Owner=f.Host,ShowInTaskbar=false};window.Show();UiRuntime.Pump();
        try{model.SelectedGroup=model.Groups.First();model.AddVariableCommand.Execute(null);Assert.Single(model.CurrentVariables).Value="保留未提交输入";f.Main.SetSelectedTab(other);UiRuntime.Pump();Assert.True(model.IsValid);Assert.True(window.IsVisible);Assert.True(model.HasPendingChanges);Assert.Equal("保留未提交输入",Assert.Single(model.CurrentVariables).Value);Assert.Empty(f.Session.Document.GlobalVariables);}
        finally{window.Close();tracked.Remove(model);UiRuntime.Pump();}
    });
    [Fact] public void Removing_selected_root_retires_its_actual_parameter_window_before_receipt_acknowledgement()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());var original=f.Tab;Assert.True(Add(f));f.Main.SetSelectedTab(original);var window=Open(f,node);Sequence(window).Text="17";
        Assert.True(Delete(f,original));Assert.False(window.IsCurrent);Assert.Null(f.Session.LastObserverError);Assert.DoesNotContain("未刷新",f.Main.StatusText);Assert.True(f.Session.Undo());UiRuntime.Pump();Assert.False(window.IsCurrent);Assert.NotSame(original,f.Main.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==f.Root.Id));
    });

    [Fact] public void Root_configuration_window_is_cancelled_on_remove_and_cannot_revive_after_immediate_undo()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var original=f.Tab;Assert.True(Add(f));f.Main.SetSelectedTab(original);var window=f.Main.OpenRootConfiguration(original,f.Host)!;Assert.NotNull(window);UiRuntime.Pump();var context=window.Context;
        using var removal=f.Session.Editing.Host.BeginRootCollectionEdit();removal.Remove(f.Root);f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,collection:removal));Assert.False(context.IsValid);Assert.False(window.IsVisible);Assert.Null(f.Main.WorkflowTabViewModel.SelectedTab);
        Assert.True(f.Session.Undo());Assert.False(context.IsValid);UiRuntime.Pump();Assert.NotSame(original,f.Tab);Assert.False(context.IsValid);Assert.Null(f.Session.LastObserverError);
    });
    [Fact] public void Remove_then_immediate_undo_retires_old_tab_and_clears_source_clipboard_before_dispatcher_refresh()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();var original=f.Tab;Assert.True(Add(f));var other=f.Tab;f.Main.SetSelectedTab(original);f.Projection(node).IsSelected=true;Assert.True(f.Main.CopyCanvasSelection());f.Main.SetSelectedTab(other);
        using var remove=f.Session.Editing.Host.BeginRootCollectionEdit();remove.Remove(f.Root);f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,collection:remove));Assert.False(f.Main.CanPasteCanvasSelection);f.Main.SetSelectedTab(original);Assert.Same(other,f.Tab);
        Assert.True(f.Session.Undo());Assert.Same(f.Root,f.Session.GetWorkflows().Single(root=>root.Id==f.Root.Id));Assert.False(Copy(f,original));UiRuntime.Pump();
        var restored=f.Main.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==f.Root.Id);Assert.NotSame(original,restored);Assert.Null(original.DocumentCanvas);Assert.False(f.Main.CanPasteCanvasSelection);Assert.Null(f.Session.LastObserverError);
    });
    [Fact] public void Observer_failure_is_reported_without_undoing_the_already_committed_collection()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var history=f.Session.HistoryEntries.Count;EventHandler<DocumentChangeSet> failure=(_,_)=>throw new InvalidOperationException("受控观察者刷新失败");f.Session.Changed+=failure;
        try{Assert.True(Add(f));Assert.Equal(2,f.Session.GetWorkflows().Count);Assert.Equal(history+1,f.Session.HistoryEntries.Count);Assert.NotNull(f.Session.LastObserverError);Assert.Contains("已提交",f.Main.StatusText);Assert.Contains("未刷新",f.Main.StatusText);}
        finally{f.Session.Changed-=failure;}
    });
    [Fact] public void Confirming_unchanged_name_does_not_publish_unrelated_private_parameter_text()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();var window=Open(f,node);var text=Sequence(window);var opening=SequenceOf(node);text.Text="17";var history=f.Session.HistoryEntries.Count;
        f.Tab.BeginRename();f.Tab.RenameBuffer="  "+f.Tab.Name+"  ";Assert.True(f.Tab.ConfirmRename());Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Equal(opening,SequenceOf(node));Assert.True(window.IsCurrent);Assert.Equal("17",text.Text);
        text.Text="尚未输入完";f.Tab.BeginRename();Assert.True(f.Tab.ConfirmRename());Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Equal("尚未输入完",text.Text);
    });
    [Fact] public void Root_rename_preserves_current_full_name_rule_for_existing_node_clipboard()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();f.Projection(node).IsSelected=true;Assert.True(f.Main.CopyCanvasSelection());var tab=f.Tab;var original=tab.Name;
        tab.BeginRename();tab.RenameBuffer="来源已改名";Assert.True(tab.ConfirmRename());var history=f.Session.HistoryEntries.Count;Assert.False(f.Main.PasteCanvasSelection());Assert.Single(f.Canvas.Nodes);Assert.Equal(history,f.Session.HistoryEntries.Count);
        Assert.True(f.Session.Undo());UiRuntime.Pump();Assert.Equal(original,tab.Name);Assert.True(f.Main.PasteCanvasSelection());Assert.Equal(2,f.Canvas.Nodes.Count);
    });
    [Fact] public void Copy_first_free_name_preserves_valid_unicode_at_the_sixty_four_character_limit()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Tab;var name=new string('长',59)+"🚀末尾字";Assert.Equal(64,name.Length);source.BeginRename();source.RenameBuffer=name;Assert.True(source.ConfirmRename());Assert.True(Copy(f,source));var first=f.Tab.Name;
        Assert.Equal(new string('长',59)+"_副本1",first);Assert.DoesNotContain(first,char.IsSurrogate);Assert.True(Copy(f,source));Assert.Equal(new string('长',59)+"_副本2",f.Tab.Name);Assert.Equal(name,f.Root.Name);
    });
    [Fact] public void Overview_copy_uses_the_same_history_and_rejects_a_stale_drag_capture()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());var capture=f.Main.CaptureRootOverview();var history=f.Session.HistoryEntries.Count;
        var request=new SunEyeVision.UI.Shell.Controls.Shell.RootOverviewPositionEventArgs(f.Root.Id,312,217,capture.Identity,capture.Revision);
        Assert.True(f.Main.CommitOverviewPosition(request,true));var copy=f.Tab.ActualRoot!;Assert.NotSame(f.Root,copy);Assert.Equal(310,copy.WorkspaceSettings.OverviewX);Assert.Equal(215,copy.WorkspaceSettings.OverviewY);Assert.Equal(history+1,f.Session.HistoryEntries.Count);Assert.Single(copy.Nodes);
        Assert.False(f.Main.CommitOverviewPosition(request,true));Assert.Equal(2,f.Session.GetWorkflows().Count);Assert.Equal(history+1,f.Session.HistoryEntries.Count);
    });
    [Fact] public void Continuous_waiting_and_pause_reserve_collection_until_real_stop_drains()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());f.Fields(source,("local.auto-switch",new ConstantSetting(false)));var tab=f.Tab;
        var interval=f.Session.Editing.Host.BeginRootEdit(f.Root.Id);interval.WorkspaceSettings=interval.WorkspaceSettings with{RunIntervalMs=700};f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,roots:new[]{interval}));Assert.True(Add(f));f.Main.SetSelectedTab(tab);
        f.Main.ToggleContinuousAllCommand.Execute(null);var task=UiRuntime.Field<Task>(f.Main,"_formalRunTask")!;var run=UiRuntime.Field<DocumentContinuousRun>(f.Main,"_formalContinuousRun")!;var end=DateTime.UtcNow.AddSeconds(20);while(run.State.Phase!=ContinuousPhase.Waiting){Assert.True(DateTime.UtcNow<end);UiRuntime.Pump();Thread.Sleep(1);}f.Main.PauseCommand.Execute(null);
        Assert.True(f.Main.IsExecutionPaused);var history=f.Session.HistoryEntries.Count;Assert.False(f.Main.CanEditRootCollection);Assert.False(Add(f));Assert.False(Copy(f,tab));Assert.False(Delete(f,tab));tab.BeginRename();tab.RenameBuffer="运行中不可改名";Assert.False(tab.ConfirmRename());tab.CancelRename();Assert.Equal(history,f.Session.HistoryEntries.Count);
        f.Main.StopWorkflowCommand.Execute(null);Assert.False(f.Main.CanEditRootCollection);UiRuntime.Await(task);Assert.True(f.Main.CanEditRootCollection);Assert.True(Add(f));
    });

    [Fact] public void Acknowledgement_failure_keeps_committed_collection_and_reports_refresh_failure()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());var tab=f.Tab;var history=f.Session.HistoryEntries.Count;var observer=new FailingAcknowledgement(f.Session,source);f.Session.Editing.Register(observer);
        try{Assert.True(Copy(f,tab));Assert.Equal(2,f.Session.GetWorkflows().Count);Assert.Equal(history+1,f.Session.HistoryEntries.Count);Assert.Equal(41,SequenceOf(source));Assert.Contains("已提交",f.Main.StatusText);Assert.Contains("未刷新",f.Main.StatusText);}
        finally{f.Session.Editing.Unregister(observer.ScopeId);}
    });

    private sealed class FailingAcknowledgement(DocumentSession session,NodeDefinition source):IDocumentEditParticipant
    {
        public Guid ScopeId{get;}=Guid.NewGuid();public string ScopeKey=>"controlled-view-ack-failure";public bool HasPendingChanges=>false;public bool IsValid=>true;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host){var draft=host.BeginEdit(session.GetWorkflows().Single().Id,source.Id);draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(41L)}});return DocumentEditProposal.Prepare(host,nodes:new[]{draft});}
        public void Acknowledge(DocumentEditProposal prepared,DocumentEditReceipt receipt)=>throw new InvalidOperationException("受控视图回执刷新失败");public void Cancel(){}
    }
    private static bool Add(DocumentUiFixture f) => (bool)UiRuntime.Call(f.Main,"AddRootWorkspace")!;
    private static bool Copy(DocumentUiFixture f, WorkflowTabViewModel tab, Point? point=null) => (bool)UiRuntime.Call(f.Main,"CopyRootWorkspace",tab,point)!;
    private static bool Delete(DocumentUiFixture f, WorkflowTabViewModel tab) => (bool)UiRuntime.Call(f.Main,"DeleteRootWorkspace",tab)!;
    private static RuntimeToolEditorWindow Open(DocumentUiFixture f,NodeDefinition node)
    {
        f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
        var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();UiRuntime.AwaitEditorReady(window);return window;
    }
    private static TextBox Sequence(RuntimeToolEditorWindow window)
    {
        var settings=UiRuntime.Visuals<DeclaredSettingsView>(window.Editor).Single();
        var fields=(DeclaredFieldsView)settings.Content;
        return Assert.IsType<TextBox>(fields.Fields.Single(item=>item.FieldId=="sequence.initial").InputElement);
    }
    private static long SequenceOf(NodeDefinition node)=>Assert.IsType<long>(Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);

    [Fact] public void Add_rename_cancel_undo_and_last_root_protection_keep_actual_tab_and_history()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var original=f.Tab;var canvas=f.Canvas;var before=f.Session.HistoryEntries.Count;
        Assert.True(Add(f));var added=f.Tab;Assert.Equal("流程2",added.Name);Assert.Equal(before+1,f.Session.HistoryEntries.Count);Assert.Same(canvas,original.DocumentCanvas);
        added.BeginRename();added.RenameBuffer="  生产根  ";Assert.True(added.ConfirmRename());Assert.Equal("生产根",added.Name);Assert.Equal("生产根",added.ActualRoot!.Name);
        before=f.Session.HistoryEntries.Count;added.BeginRename();added.RenameBuffer="取消的名字";added.CancelRename();Assert.Equal("生产根",added.Name);Assert.Equal(before,f.Session.HistoryEntries.Count);
        added.BeginRename();added.RenameBuffer=original.Name;Assert.False(added.ConfirmRename());Assert.Equal("生产根",added.Name);Assert.Equal(original.Name,added.RenameBuffer);Assert.True(added.IsEditingName);Assert.Equal(before,f.Session.HistoryEntries.Count);
        added.RenameBuffer="bad.name";Assert.False(added.ConfirmRename());Assert.Equal("生产根",added.Name);added.CancelRename();
        f.Session.Undo();UiRuntime.Pump();Assert.Same(added,f.Tab);Assert.Equal("流程2",added.Name);f.Session.Redo();UiRuntime.Pump();Assert.Equal("生产根",added.Name);
        Assert.True(Delete(f,original));UiRuntime.Pump();before=f.Session.HistoryEntries.Count;Assert.False(Delete(f,added));Assert.Single(f.Main.WorkflowTabViewModel.Tabs);Assert.Equal(before,f.Session.HistoryEntries.Count);
    });

    [Fact] public void Copy_flushes_actual_parameter_window_into_one_history_and_remaps_real_executable_graph()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,210);f.ConfigureImage(source,f.Image());f.Connect(source,blob);var original=f.Tab;
        var window=Open(f,source);Sequence(window).Text="17";var history=f.Session.HistoryEntries.Count;
        Assert.True(f.Main.CanEditRootCollection);Assert.True(Copy(f,original,new Point(400,350)),f.Main.StatusText);UiRuntime.Pump();
        var copied=f.Tab.ActualRoot!;Assert.NotSame(f.Root,copied);Assert.Equal(history+1,f.Session.HistoryEntries.Count);Assert.Equal(17,SequenceOf(source));var copiedImage=copied.Nodes.Single(node=>node.ToolType=="ImageLoad");var copiedBlob=copied.Nodes.Single(node=>node.ToolType=="BlobAnalysis");
        Assert.Equal(17,SequenceOf(copiedImage));Assert.Equal(400,copied.WorkspaceSettings.OverviewX);Assert.Single(copied.Connections);Assert.Equal(copiedImage.Id,copied.Connections.Single().SourceNodeId);
        var reference=Assert.IsType<ReferenceSetting>(copiedBlob.Configuration.Fields.Read("input.image")).Reference;Assert.Equal(copied.Id,reference.ScopeId);Assert.Equal(copiedImage.Id,reference.NodeId);Assert.False(window.IsCurrent);
        f.Run();using var reader=f.Session.AcquirePublishedResult(copied.Id,copiedBlob.Id);Assert.True(reader?.Snapshot.IsSuccess,f.Main.StatusText);
        f.Save();var file=System.IO.Path.Combine(f.DirectoryPath,"reopened.solution");System.IO.File.Copy(System.IO.Path.Combine(f.DirectoryPath,"document.solution"),file);var open=(Task<bool>)UiRuntime.Call(f.Main,"OpenExternalSolutionAsync",file,false)!;UiRuntime.Await(open);Assert.True(open.Result);
        var current=SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSession!;Assert.NotSame(f.Session,current);Assert.Equal(2,current.GetWorkflows().Count);var reopened=current.GetWorkflows().Single(root=>root.Id==copied.Id);f.Main.SetSelectedTab(f.Main.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==reopened.Id));f.Run();using var again=current.AcquirePublishedResult(reopened.Id,copiedBlob.Id);Assert.True(again?.Snapshot.IsSuccess);
    });

    [Fact] public void Invalid_raw_parameter_text_rejects_whole_copy_without_losing_view_selection_or_number()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());var tab=f.Tab;var window=Open(f,source);var text=Sequence(window);text.Text="尚未输入完";var history=f.Session.HistoryEntries.Count;
        Assert.False(Copy(f,tab));Assert.Single(f.Session.GetWorkflows());Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Same(tab,f.Tab);Assert.Same(source,f.Main.SelectedNode);Assert.Equal("尚未输入完",text.Text);Assert.True(window.IsCurrent);
        text.Text="4";Assert.True(Copy(f,tab));Assert.Equal("流程1_副本1",f.Tab.Name);Assert.Equal(history+1,f.Session.HistoryEntries.Count);
    });

    [Fact] public void New_raw_text_between_prepare_and_receipt_is_preserved_in_same_window_and_keeps_original_selection()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();f.ConfigureImage(source,f.Image());var tab=f.Tab;var window=Open(f,source);var text=Sequence(window);text.Text="7";var body=window.Editor.Content;var history=f.Session.HistoryEntries.Count;
        EventHandler<DocumentChangeSet> editing=(_,change)=>{if(!change.ChangedRoots.IsDefaultOrEmpty&&change.ChangedRoots.Any(root=>root.Kind==RootCollectionChangeKind.Added))text.Text="回执期间的新输入";};
        f.Session.Changed+=editing;try{Assert.True(Copy(f,tab));}finally{f.Session.Changed-=editing;}UiRuntime.Pump();
        Assert.Equal(history+1,f.Session.HistoryEntries.Count);Assert.Same(tab,f.Tab);Assert.Same(body,window.Editor.Content);Assert.Equal("回执期间的新输入",text.Text);Assert.True(window.IsCurrent);Assert.Equal(7,SequenceOf(source));Assert.Equal(7,SequenceOf(f.Session.GetWorkflows().Single(root=>!ReferenceEquals(root,f.Root)).Nodes.Single()));
        text.Text="11";var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));Assert.True(context.TryCommitEditor(out var error),error);Assert.Equal(11,SequenceOf(source));
    });

    [Fact] public void Rename_receipt_does_not_clear_a_later_inline_text_revision()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var tab=f.Tab;tab.BeginRename();tab.RenameBuffer="已准备名称";
        EventHandler<DocumentChangeSet> later=(_,change)=>{if(!change.ChangedRoots.IsDefaultOrEmpty)tab.RenameBuffer="用户继续输入";};f.Session.Changed+=later;
        try{Assert.True(tab.ConfirmRename());}finally{f.Session.Changed-=later;}
        Assert.Equal("已准备名称",f.Root.Name);Assert.Equal("已准备名称",tab.Name);Assert.Equal("用户继续输入",tab.RenameBuffer);Assert.True(tab.IsEditingName);tab.CancelRename();
    });

    [Fact] public void Delete_undo_recreates_tab_but_does_not_revive_old_presentation_capability_or_stale_tab()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var image=f.Add();f.ConfigureImage(image,f.Image());var first=f.Tab;Assert.True(Copy(f,first));var copy=f.Tab;var root=copy.ActualRoot!;var copiedImage=root.Nodes.Single();f.Run();using var reader=f.Session.AcquirePublishedResult(root.Id,copiedImage.Id);Assert.NotNull(reader);var cap=f.Session.AdmitPresentation(root.Id,copiedImage.Id);
        f.Main.SetSelectedTab(first);var firstCanvas=first.DocumentCanvas;Assert.True(Delete(f,copy));UiRuntime.Pump();Assert.Same(first,f.Tab);Assert.Same(firstCanvas,first.DocumentCanvas);Assert.Null(copy.DocumentCanvas);Assert.False(cap.IsCurrent);Assert.True(reader.Snapshot.IsSuccess);Assert.Null(f.Session.LastObserverError);
        Assert.True(f.Session.Undo());UiRuntime.Pump();var restored=f.Main.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==root.Id);Assert.Same(root,restored.ActualRoot);Assert.NotSame(copy,restored);Assert.False(cap.IsCurrent);Assert.Null(f.Session.AcquirePublishedResult(root.Id,copiedImage.Id));
        var history=f.Session.HistoryEntries.Count;Assert.False(Copy(f,copy));Assert.False(Delete(f,copy));Assert.Equal(history,f.Session.HistoryEntries.Count);
        var impostor=new WorkflowTabViewModel{Id=root.Id,Name=root.Name};Assert.False(Copy(f,impostor));Assert.False(Delete(f,impostor));
    });

    [Fact] public void Producer_display_follows_committed_name_and_dependent_delete_rejects_without_mutation()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var image=f.Add();f.ConfigureImage(image,f.Image());var first=f.Tab;Assert.True(Add(f));
        var port=f.Session.GetNodeSchema(f.Root.Id,image.Id)!.Outputs.Single(port=>port.Id=="output.image");using var variables=f.Session.Editing.Host.BeginVariableEdit();var reference=new OutputReference(image.Id,port.Id,port.Type,new[]{"width"},scopeId:f.Root.Id);
        variables.Variables.Add(new GlobalVariable("宽度",0L,"Integer"){ProducerBinding=new ProducerBinding{ReferenceExpression=ParameterSettingCanonical.Serialize(reference)}});f.Session.CommitEdit(DocumentEditProposal.Prepare(f.Session.Editing.Host,variables:variables));UiRuntime.Pump();var actual=Assert.Single(f.Session.Document.GlobalVariables);
        first.BeginRename();first.RenameBuffer="生产来源";Assert.True(first.ConfirmRename());Assert.Same(actual,Assert.Single(f.Session.Document.GlobalVariables));Assert.Equal("生产来源",actual.ProducerBinding!.WorkflowName);
        var history=f.Session.HistoryEntries.Count;var selected=f.Tab;Assert.False(Delete(f,first));Assert.Equal(history,f.Session.HistoryEntries.Count);Assert.Same(selected,f.Tab);Assert.NotNull(actual.ProducerBinding);Assert.Contains(f.Root,f.Session.GetWorkflows());
    });
}
