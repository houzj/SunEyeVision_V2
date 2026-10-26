using System.Reflection;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class GroupRunParameterConfigTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Solution Model=new();public readonly SunEyeVision.Workflow.Workflow Parent=new("parent","流程"),Child=new("child","内部"){IsSubroutine=true};
        public readonly WorkflowNodeBase Group=new("group","Group1","Group1","Subroutine"){SubroutineId="child",Parameters=new SubroutineParameters{SubroutineId="child"}};
        public readonly ProductionEditorContext Context;public readonly GroupRunParameterSession Editor;
        public Fixture(int maximum=32)
        {
            Parent.Nodes.Add(Group);Child.Nodes.Add(new("a","解析1","解析1","ProtocolParse"){Parameters=new ProtocolParseParameters()});Child.Nodes.Add(new("b","解析2","解析2","ProtocolParse"){Parameters=new ProtocolParseParameters()});Model.Workflows.Add(Parent);Model.Workflows.Add(Child);
            bool Allowed(string id,WorkflowNodeBase node)=>WorkflowOwnership.IsInternalNode(Model,Child,Parent.Id,id,node);
            Context=new(new NodeConfigurationEditHost(Model),Parent.Id,Group,()=>true,_=>{},allowRelated:Allowed);Editor=new(Context,Model,Allowed,maximum);
        }
        public AvailableGroupParameter Source(string node,string name)=>Editor.Available().Single(s=>s.Target.NodeId==node&&s.Target.PropertyName==name);
        public void Dispose()=>Context.Discard();
    }
    [Fact]
    public void Native_relation_policy_matches_module_kind_and_parameter_key_instead_of_only_CLR_type()
    {
        using var f=new Fixture();var group=f.Editor.AddGroup();var item=f.Editor.Add(group,f.Source("a",nameof(ProtocolParseParameters.Mode)));
        Assert.False(f.Editor.CanRelate(item,f.Source("b",nameof(ProtocolParseParameters.InputFormat))));
        Assert.Throws<InvalidOperationException>(()=>f.Editor.AddTarget(item,f.Source("b",nameof(ProtocolParseParameters.InputFormat))));
        Assert.Single(item.Targets);Assert.True(f.Editor.CanRelate(item,f.Source("b",nameof(ProtocolParseParameters.Mode))));
        f.Editor.AddTarget(item,f.Source("b",nameof(ProtocolParseParameters.Mode)));Assert.Equal(2,item.Targets.Count);
        f.Editor.ReplaceTargets(item,Array.Empty<AvailableGroupParameter>());
        Assert.True(f.Editor.CanRelate(item,f.Source("b",nameof(ProtocolParseParameters.Mode))));
        Assert.False(f.Editor.CanRelate(item,f.Source("b",nameof(ProtocolParseParameters.StringEncoding))));
        f.Editor.ReplaceTargets(item,new[]{f.Source("b",nameof(ProtocolParseParameters.Mode))});
        Assert.Equal("ProtocolParse",item.SourceToolType);Assert.Equal(nameof(ProtocolParseParameters.Mode),item.SourceParameterName);
    }
    [Fact]
    public void Module_batch_limit_is_atomic_and_cross_group_moves_preserve_exposure_identity()
    {
        using var f=new Fixture(maximum:2);var group=f.Editor.AddGroup();var sources=f.Editor.Available().Where(a=>a.Target.NodeId=="a").ToArray();
        Assert.Throws<InvalidOperationException>(()=>f.Editor.AddBatch(group,sources));Assert.Empty(group.Parameters);
        f.Editor.AddBatch(group,sources.Take(2).Concat(sources.Take(1)));Assert.Equal(2,group.Parameters.Count);
        var item=group.Parameters[0];var id=item.Id;var second=f.Editor.AddGroup();f.Editor.MoveToGroup(item,second);
        Assert.Same(item,Assert.Single(second.Parameters));Assert.Equal(id,item.Id);Assert.Single(group.Parameters);
        item.Name=new string('名',31);Assert.True(f.Editor.Parameters.Validate().IsValid);
        item.Name=new string('名',32);Assert.False(f.Editor.Parameters.Validate().IsValid);
        Assert.Empty(((SubroutineParameters)f.Group.Parameters).RunParameterGroups);
    }
    [Fact]
    public void Data_sync_uses_first_relation_value_and_never_publishes_without_Group_confirmation()
    {
        using var f=new Fixture();var group=f.Editor.AddGroup();var item=f.Editor.Add(group,f.Source("a",nameof(ProtocolParseParameters.Separator)));
        Assert.Throws<InvalidOperationException>(()=>f.Editor.Synchronize(item));f.Editor.AddTarget(item,f.Source("b",nameof(ProtocolParseParameters.Separator)));
        var slots=f.Editor.Resolve(item);((ProtocolParseParameters)slots[1].Node.Parameters).Separator=";";
        f.Editor.Synchronize(item);Assert.All(slots,s=>Assert.Equal(",",((ProtocolParseParameters)s.Node.Parameters).Separator));
        Assert.Empty(((SubroutineParameters)f.Group.Parameters).RunParameterGroups);
    }
    [Fact]
    public void File_save_reopen_preserves_Group_metadata_and_all_related_values_and_rejects_invalid_draft_before_rewrite()
    {
        using var f=new Fixture();var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing test root"),"group-file",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        var hostRoot=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostRoot,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!);
        // Persistence fixture: register the actual two required tools. Full release admission
        // is tested by the real Release executable; never pretend tests/bin is a release root.
        SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
        var manager=new SolutionManager(Path.Combine(root,"solutions"),Path.Combine(root,"settings.json"),Path.Combine(root,"recovery"),root);manager.SetCurrentSolution(f.Model,registerInCatalog:false);
        var session=manager.Session!;
        using var context=new ProductionEditorContext(session.Editing.Host,f.Parent.Id,f.Group,()=>ReferenceEquals(manager.Session,session),_=>{},documentEdits:session.Editing,
            allowRelated:(id,node)=>WorkflowOwnership.IsInternalNode(f.Model,f.Child,f.Parent.Id,id,node));
        var editor=new GroupRunParameterSession(context,f.Model,(id,node)=>WorkflowOwnership.IsInternalNode(f.Model,f.Child,f.Parent.Id,id,node));
        var group=editor.AddGroup();group.Name="通信";var source=editor.Available().Single(s=>s.Target.NodeId=="a"&&s.Target.PropertyName==nameof(ProtocolParseParameters.Separator));var item=editor.Add(group,source);editor.AddTarget(item,editor.Available().Single(s=>s.Target.NodeId=="b"&&s.Target.PropertyName==nameof(ProtocolParseParameters.Separator)));editor.Write(item,";");
        EventHandler<Solution> prepare=(_,_)=>session.Editing.AcceptAll();manager.SolutionSaving+=prepare;
        var path=Path.Combine(root,"group.solution");var result=manager.SaveCurrentDocument(path);Assert.True(result.FileWritten,result.Error);Assert.False(manager.HasUnsavedChanges);
        var bytes=File.ReadAllBytes(path);((SubroutineParameters)context.DraftNode.Parameters).RunParameterGroups[0].Name="";
        Assert.False(manager.SaveCurrentDocument(path).FileWritten);Assert.Equal(bytes,File.ReadAllBytes(path));
        manager.SolutionSaving-=prepare;manager.CloseSolution(saveChanges:false);
        var reader=new SolutionManager(Path.Combine(root,"reader-solutions"),Path.Combine(root,"reader-settings.json"),Path.Combine(root,"reader-recovery"),root);
        var reopened=reader.OpenSolution(path)!;Assert.NotNull(reopened);Assert.NotSame(f.Model,reopened);
        var saved=(SubroutineParameters)reopened.Workflows.Single(w=>w.Id=="parent").Nodes.Single().Parameters;
        var alias=Assert.Single(Assert.Single(saved.RunParameterGroups).Parameters);Assert.Equal(item.Id,alias.Id);Assert.Equal(2,alias.Targets.Count);Assert.Equal("ProtocolParse",alias.SourceToolType);Assert.Equal(nameof(ProtocolParseParameters.Separator),alias.SourceParameterName);
        Assert.All(reopened.Workflows.Single(w=>w.Id=="child").Nodes,n=>Assert.Equal(";",((ProtocolParseParameters)n.Parameters).Separator));reader.CloseSolution(saveChanges:false);
    }
    private static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Visual<T>(child))yield return nested;}}
    [Fact]
    public void Real_configuration_window_filters_module_checkboxes_moves_groups_and_closes_to_parent_draft_only()=>Sta(()=>
    {
        using var f=new Fixture();var window=new GroupRunParameterConfigWindow(f.Editor){Left=-10000,ShowActivated=false};
        try
        {
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            window.Show();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
            var filter=Visual<ComboBox>(window).Single(c=>c.Name=="ParameterTypeFilter");filter.SelectedIndex=2;window.UpdateLayout();
            var sources=Visual<TreeView>(window).Single(t=>t.Name=="ParameterSourceTree");Assert.Equal(2,sources.Items.Count);
            var module=(CheckBox)((TreeViewItem)sources.Items[0]).Header;module.IsChecked=true;module.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));window.UpdateLayout();
            Assert.Equal(3,f.Editor.Parameters.RunParameterGroups[0].Parameters.Count);
            var second=f.Editor.AddGroup();window.RefreshFromSession(second.Id);window.UpdateLayout();
            var groupPicker=Visual<ComboBox>(window).First(c=>c.Name!="DestinationGroup"&&c.Name!="ParameterTypeFilter"&&c.DataContext?.GetType().GetProperty("Item")?.GetValue(c.DataContext) is SubroutineRunParameter);
            groupPicker.SelectedItem=second;window.UpdateLayout();Assert.Single(second.Parameters);
            Assert.True(window.TryPrepare(out var error),error);
            var bitmap=new RenderTargetBitmap(1100,680,96,96,PixelFormats.Pbgra32);bitmap.Render((FrameworkElement)window.Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-configuration-window-Dark.png")))encoder.Save(file);
            var closed=false;window.Closed+=(_,_)=>closed=true;window.Close();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Assert.True(closed);Assert.False(window.IsVisible);Assert.Empty(((SubroutineParameters)f.Group.Parameters).RunParameterGroups);Assert.True(f.Context.HasPendingChanges);
        }
        finally{window.ForceClose();}
    });
    [Fact]
    public void Modal_relation_cancel_preserves_targets_confirm_updates_draft_and_owner_close_releases_configuration()=>Sta(()=>
    {
        using var f=new Fixture();var group=f.Editor.AddGroup();var item=f.Editor.Add(group,f.Source("a",nameof(ProtocolParseParameters.Mode)));
        var owner=new Window{Width=400,Height=300,Left=-10000,ShowActivated=false};owner.Show();
        var configuration=new GroupRunParameterConfigWindow(f.Editor,owner){Left=-10000,ShowActivated=false};
        var trace=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"modal-149-trace.txt");
        void Trace(string text)=>File.AppendAllText(trace,text+Environment.NewLine);
        Trace("owner shown");
        try
        {
            configuration.Dispatcher.BeginInvoke(new Action(()=>
            {
                Trace("configuration modal callback");
                var cancel=new GroupRelationWindow(f.Editor,item,configuration){Left=-10000,ShowActivated=false};
                cancel.Dispatcher.BeginInvoke(new Action(()=>
                {
                    Trace("cancel callback");var checks=Visual<CheckBox>(cancel).ToArray();Assert.Equal(2,checks.Length);
                    foreach(var check in checks)check.IsChecked=true;
                    cancel.Close();
                }),System.Windows.Threading.DispatcherPriority.ContextIdle);
                cancel.ShowDialog();Trace("cancel returned");Assert.Single(item.Targets);
                var confirm=new GroupRelationWindow(f.Editor,item,configuration){Left=-10000,ShowActivated=false};
                confirm.Dispatcher.BeginInvoke(new Action(()=>
                {
                    Trace("confirm callback");foreach(var check in Visual<CheckBox>(confirm))check.IsChecked=true;
                    Visual<Button>(confirm).Single(b=>Equals(b.Content,"确定")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }),System.Windows.Threading.DispatcherPriority.ContextIdle);
                Assert.True(confirm.ShowDialog());Trace("confirm returned");Assert.Equal(2,item.Targets.Count);
                Trace("closing owner");owner.Close();Trace("owner close returned");
            }),System.Windows.Threading.DispatcherPriority.ContextIdle);
            configuration.ShowDialog();Assert.False(owner.IsVisible);Assert.False(configuration.IsVisible);
            Assert.Empty(((SubroutineParameters)f.Group.Parameters).RunParameterGroups);Assert.True(f.Context.HasPendingChanges);
        }
        finally{configuration.ForceClose();owner.Close();}
    });
    private static void Sta(Action action){Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(45)));if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();}
}
