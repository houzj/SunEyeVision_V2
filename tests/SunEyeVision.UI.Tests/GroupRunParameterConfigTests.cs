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
        public Fixture(int maximum=32,Action<SunEyeVision.Workflow.Workflow>? configure=null,Func<string,string,IDataSourceQueryService>? queryFactory=null)
        {
            Parent.Nodes.Add(Group);Child.Nodes.Add(new("a","解析1","解析1","ProtocolParse"){Parameters=new ProtocolParseParameters()});Child.Nodes.Add(new("b","解析2","解析2","ProtocolParse"){Parameters=new ProtocolParseParameters()});Model.Workflows.Add(Parent);Model.Workflows.Add(Child);
            configure?.Invoke(Child);
            bool Allowed(string id,WorkflowNodeBase node)=>WorkflowOwnership.IsInternalNode(Model,Child,Parent.Id,id,node);
            Context=new(new NodeConfigurationEditHost(Model),Parent.Id,Group,()=>true,_=>{},allowRelated:Allowed);Editor=new(Context,Model,Allowed,maximum,queryFactory);
        }
        public AvailableGroupParameter Source(string node,string name)=>Editor.Available().Single(s=>s.Target.NodeId==node&&s.Target.PropertyName==name);
        public void Dispose()=>Context.Discard();
    }
    private sealed class RegionQuery : IDataSourceQueryService
    {
        public List<AvailableDataSource> Sources=new();public object? Matrix;
        public string? CurrentNodeId{get;set;}
        public List<ParentNodeInfo> GetParentNodes(string nodeId)=>new();
        public List<ParentNodeInfo> GetChildNodes(string nodeId)=>new();
        public List<AvailableDataSource> GetAvailableDataSources(string nodeId,Type? targetType=null)=>Sources;
        public List<AvailableDataSource> GetNodeOutputProperties(string nodeId)=>Sources;
        public List<AvailableDataSource> GetWorkflowNodeDataSources()=>Sources;
        public NodeExecutionContext? GetNodeContext(string nodeId)=>null;
        public object? GetPropertyValue(string nodeId,string property)=>Matrix;
        public bool HasNodeExecuted(string nodeId)=>Matrix!=null;
        public bool IsNodeRegistered(string nodeId)=>true;
    }
    [Fact]
    public void Region_context_intersects_complete_source_identity_and_resolves_live_correction_in_internal_scope()
    {
        var calls=new List<(string,string)>();var first=new RegionQuery();var second=new RegionQuery();
        AvailableDataSource Source(string flow,string node)=>new(){SourceWorkflowId=flow,SourceNodeId=node,PropertyName="geometry",DisplayName=node};
        first.Sources.AddRange(new[]{Source("child","common"),Source("child","only-a"),Source("other","same-name")});
        second.Sources.AddRange(new[]{Source("child","common"),Source("child","only-b"),Source("child","same-name")});
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.ImageThreshold","SunEyeVision.Tool.ImageThreshold.dll"));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool",true)!);
        using var f=new Fixture(configure:child=>{foreach(var node in child.Nodes){node.ToolType="ImageThreshold";node.Parameters=(ToolParameters)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters",true)!)!;}},
            queryFactory:(flow,node)=>{calls.Add((flow,node));return node=="a"?first:second;});
        var item=f.Editor.Add(f.Editor.AddGroup(),f.Source("a",nameof(RoiParameters.InspectionRois)));f.Editor.AddTarget(item,f.Source("b",nameof(RoiParameters.InspectionRois)));
        var parameters=f.Editor.Resolve(item)[0].Node.Parameters;parameters.GetType().GetProperty("IsPositionCorrectionEnabled")!.SetValue(parameters,true);
        parameters.GetType().GetProperty("PositionCorrectionBindingSource")!.SetValue(parameters,new AvailableDataSource{SourceWorkflowId="child",SourceNodeId="correction",PropertyName="matrix",CurrentValue=new SunEyeVision.Plugin.SDK.Execution.Results.AffineMatrix(1,0,0,1,999,999)});
        first.Matrix=new SunEyeVision.Plugin.SDK.Execution.Results.AffineMatrix(1,0,0,1,12,24);
        var context=f.Editor.RegionContext(item);Assert.Equal("common",Assert.Single(context.Sources).SourceNodeId);
        Assert.Equal(new[]{("child","a"),("child","b")},calls);Assert.True(context.Correction.CanUse);Assert.Equal(12,context.Correction.Matrix!.OffsetX);
        first.Matrix=null;Assert.False(f.Editor.RegionContext(item).Correction.CanUse);
        Assert.Equal(SunEyeVision.Plugin.SDK.Execution.Helpers.PositionCorrectionState.WaitingForSource,f.Editor.RegionContext(item).Correction.State);
    }
    [Fact]
    public void Actual_Group_loop_page_filters_types_and_keeps_selected_workflow_identity_for_same_path()=>Sta(()=>
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        var p=new SubroutineParameters{SubroutineId="child",LoopConfig=new(){EnableLoop=true,EnableBreak=true,DataType="string"}};
        AvailableDataSource Source(string scope)=>new(){SourceWorkflowId=scope,SourceNodeId="same",SourceNodeName=scope,PropertyName="Text",DisplayName="文本",PropertyType=typeof(string),VariableType="String"};
        var parent=Source("parent");var child=Source("child");var query=new RegionQuery{Sources=new(){parent}};
        var control=(UserControl)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl",true)!,new object?[]{"child","Group","owner",query,new List<AvailableDataSource>{child},p,null})!;
        var window=new Window{Content=control,Width=1100,Height=680,Left=-10000,ShowActivated=false};
        try
        {
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            window.Show();Visual<TabControl>(control).First().SelectedIndex=4;window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
            var comparisons=Visual<SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding>(control).Where(b=>b.Tag is string).ToArray();Assert.Equal(2,comparisons.Length);
            Assert.All(comparisons,b=>Assert.Equal(typeof(string),b.DataType));
            var source=comparisons.Single(b=>Equals(b.Tag,"SourceCompareBinding"));source.BindingSource="same.Text";
            source.RaiseEvent(new SunEyeVision.Plugin.SDK.UI.Controls.ParamBindingSourceEventArgs(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceSelectedEvent,child));
            Assert.Equal("child",p.LoopConfig.SourceCompareReference!.SourceWorkflowId);
            p.LoopConfig.DataType="float";window.UpdateLayout();Assert.All(comparisons,b=>Assert.Equal(typeof(double),b.DataType));Assert.Null(p.LoopConfig.SourceCompareReference);Assert.Empty(p.LoopConfig.SourceCompareBinding);Assert.All(comparisons,b=>Assert.Empty(b.BindingSource));Assert.All(comparisons,b=>Assert.True(System.Windows.Data.BindingOperations.IsDataBound(b,SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingDisplayTextProperty)));Assert.All(comparisons,b=>Assert.Empty(b.BindingDisplayText));
            var bitmap=new RenderTargetBitmap(1100,680,96,96,PixelFormats.Pbgra32);bitmap.Render(control);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-loop-Dark.png")))encoder.Save(file);
        }
        finally{window.Close();((ISubroutineParameterEditor)control).Dispose();}
    });
    [Fact]
    public void Actual_Group_IO_pages_do_not_add_hidden_rows_and_bind_name_limits_and_last_row_deletion()=>Sta(()=>
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        var p=new SubroutineParameters{SubroutineId="child"};
        var control=(UserControl)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl",true)!,new object?[]{"child","Group","owner",null,null,p,null})!;
        Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);
        var window=new Window{Content=control,Width=1100,Height=680,Left=-10000,ShowActivated=false};
        try
        {
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            window.Show();var tabs=Visual<TabControl>(control).First();
            for(var tab=0;tab<2;tab++)
            {
                tabs.SelectedIndex=tab;window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
                var command=(System.Windows.Input.ICommand)control.DataContext.GetType().GetProperty(tab==0?"AddInputCommand":"AddOutputCommand")!.GetValue(control.DataContext)!;
                var add=Assert.Single(Visual<Button>(control),b=>ReferenceEquals(b.Command,command));Assert.True(add.Command.CanExecute(add.CommandParameter));add.Command.Execute(add.CommandParameter);
                window.UpdateLayout();Assert.Single(Visual<TextBox>(control),t=>t.MaxLength==SubroutineIoRules.MaximumNameLength);
                Assert.True(p.Validate().IsValid);
                if(tab==1)
                {
                    var bitmap=new RenderTargetBitmap(1100,680,96,96,PixelFormats.Pbgra32);bitmap.Render(control);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-output-Dark.png")))encoder.Save(file);
                }
                var remove=Assert.Single(Visual<Button>(control),b=>Equals(b.ToolTip,"删除此项"));remove.Command.Execute(remove.CommandParameter);window.UpdateLayout();
                Assert.Empty(p.Inputs);Assert.Empty(p.Outputs);
            }
        }
        finally{window.Close();((ISubroutineParameterEditor)control).Dispose();}
    });
    [Fact]
    public void Actual_batch_entry_imports_selected_sources_and_manual_rebinding_replaces_imported_metadata()=>Sta(()=>
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        AvailableDataSource Source(string node)=>new(){SourceWorkflowId="parent",SourceNodeId=node,SourceNodeName="模块",PropertyName="Values",DisplayName="数值",PropertyType=typeof(long[]),VariableType="Integer"};
        var source=Source("first");var replacement=Source("second");var query=new RegionQuery{Sources=new(){source,replacement}};
        var p=new SubroutineParameters{SubroutineId="child"};var control=(UserControl)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl",true)!,new object?[]{"child","Group","owner",query,null,p,null})!;
        var window=new Window{Content=control,Width=1100,Height=680,Left=-10000,ShowActivated=false};
        try
        {
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            window.Show();control.DataContext.GetType().GetProperty("SelectedInputTypeName")!.SetValue(control.DataContext,"SourceImport");
            window.Dispatcher.BeginInvoke(new Action(()=>
            {
                var picker=window.OwnedWindows.Cast<Window>().Single(w=>w.IsVisible);Assert.Equal(370,picker.Width);Assert.Equal(420,picker.Height);
                foreach(var item in Visual<TreeView>(picker).Single().Items.OfType<TreeViewItem>()){Assert.False(item.IsExpanded);item.IsExpanded=true;}picker.UpdateLayout();
                var leaf=Visual<CheckBox>(picker).Single(c=>ReferenceEquals(c.Tag,source));
                foreach(var item in Visual<TreeViewItem>(picker).Where(t=>!t.HasItems))Assert.Equal(Visibility.Hidden,((FrameworkElement)item.Template.FindName("Expander",item)!).Visibility);
                leaf.IsChecked=true;leaf.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.True(((ISubroutineParameterEditor)control).HasPendingEdits);
                Assert.False(((ISubroutineParameterEditor)control).TryPrepare(out var pendingError));
                Assert.Contains("批量选择",pendingError);Assert.Empty(p.Inputs);
                Assert.Throws<InvalidOperationException>(()=>((ISubroutineParameterEditor)control).BindParameters(p,query));
                Assert.True(Visual<CheckBox>(picker).Single(c=>Equals(c.Content,"包含模块名称")).IsChecked);
                var bitmap=new RenderTargetBitmap(370,420,96,96,PixelFormats.Pbgra32);bitmap.Render((FrameworkElement)picker.Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-io-picker-Dark.png")))encoder.Save(file);
                Visual<Button>(picker).Single(b=>Equals(b.Content,"确定")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }),System.Windows.Threading.DispatcherPriority.ContextIdle);
            var add=(System.Windows.Input.ICommand)control.DataContext.GetType().GetProperty("AddInputCommand")!.GetValue(control.DataContext)!;add.Execute(null);
            Assert.True(((ISubroutineParameterEditor)control).TryPrepare(out _));
            Assert.False(((ISubroutineParameterEditor)control).HasPendingEdits);
            var input=Assert.Single(p.Inputs);Assert.Equal("模块_数值",input.Name);Assert.Equal(typeof(long[]),input.ParamType);Assert.Equal("first",input.Source!.SourceNodeId);
            window.UpdateLayout();var binding=Assert.Single(Visual<SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding>(control));binding.BindingSource=replacement.GetBindingPath();binding.RaiseEvent(new SunEyeVision.Plugin.SDK.UI.Controls.ParamBindingSourceEventArgs(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceSelectedEvent,replacement));
            Assert.Equal("second",input.Source!.SourceNodeId);Assert.Equal(replacement.GetBindingPath(),input.BindingSource);
            binding.BindingSource="";binding.RaiseEvent(new RoutedEventArgs(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceSelectedEvent));Assert.Null(input.Source);Assert.Empty(input.BindingSource);
            var display=new SubroutineDisplayItem{DisplayName="文本",DisplayType=SubroutineDisplayType.Text,TextStyle=new()};p.DisplayConfigs.Add(display);
            Visual<TabControl>(control).First().SelectedIndex=3;window.UpdateLayout();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
            var displayGrid=Visual<DataGrid>(control).Single(g=>ReferenceEquals(g.ItemsSource,p.DisplayConfigs));
            Assert.Equal(70,displayGrid.Columns[0].ActualWidth);Assert.True(displayGrid.Columns[2].ActualWidth>400);Assert.Equal(70,displayGrid.Columns[0].Width.Value);Assert.Equal(68,displayGrid.Columns[1].Width.Value);Assert.Equal("累积显示",displayGrid.Columns[^2].Header);Assert.Equal(42,displayGrid.Columns[^1].Width.Value);
            Assert.Equal(32,Visual<TextBox>(displayGrid).Single(t=>t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="DisplayName").MaxLength);
            var displayBitmap=new RenderTargetBitmap(1100,680,96,96,PixelFormats.Pbgra32);displayBitmap.Render((FrameworkElement)window.Content);var displayEncoder=new PngBitmapEncoder();displayEncoder.Frames.Add(BitmapFrame.Create(displayBitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-display-Dark.png")))displayEncoder.Save(file);
            var displayBinding=Visual<SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding>(control).Single(b=>ReferenceEquals(b.DataContext,display));
            displayBinding.BindingSource=replacement.GetBindingPath();displayBinding.RaiseEvent(new SunEyeVision.Plugin.SDK.UI.Controls.ParamBindingSourceEventArgs(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceSelectedEvent,replacement));
            Assert.Equal("second",display.BoundDataSource!.SourceNodeId);Assert.NotSame(replacement,display.BoundDataSource);Assert.Equal("parent",display.BoundDataSource.SourceWorkflowId);
            displayBinding.BindingSource="";displayBinding.RaiseEvent(new RoutedEventArgs(SunEyeVision.Plugin.SDK.UI.Controls.ParamBinding.BindingSourceSelectedEvent));Assert.Null(display.BoundDataSource);Assert.Empty(display.BindingSource);Assert.Empty(display.BindingDisplayText);

        }
        finally{window.Close();((ISubroutineParameterEditor)control).Dispose();}
    });
    [Fact]
    public void Actual_picker_cancel_can_be_refused_and_owner_close_releases_the_modal_without_another_prompt()=>Sta(()=>
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        var type=plugin.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineIoPicker",true)!;
        var sources=new[]{new AvailableDataSource{SourceNodeId="a",SourceWorkflowId="parent",SourceNodeName="模块",PropertyName="Value",PropertyType=typeof(long)}};
        var owner=new Window{Width=500,Height=400,Left=-10000,ShowActivated=false};owner.Show();var decisions=0;
        var trace=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"io-picker-close-trace.txt");
        void Trace(string text)=>File.AppendAllText(trace,text+Environment.NewLine);Trace("owner opened");
        try
        {
            var picker=(Window)Activator.CreateInstance(type,new object?[]{owner,sources,(Func<bool>)(()=>++decisions>1)})!;
            picker.Dispatcher.BeginInvoke(new Action(()=>
            {
                foreach(var item in Visual<TreeView>(picker).Single().Items.OfType<TreeViewItem>())item.IsExpanded=true;picker.UpdateLayout();
                var leaf=Visual<CheckBox>(picker).Single(c=>c.Tag is AvailableDataSource);leaf.IsChecked=true;leaf.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var cancel=Visual<Button>(picker).Single(b=>Equals(b.Content,"取消"));cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.True(picker.IsVisible);Assert.Equal(1,decisions);Trace("discard refused");
                cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }),System.Windows.Threading.DispatcherPriority.ContextIdle);
            Assert.False(picker.ShowDialog());Assert.Equal(2,decisions);Trace("cancel returned");
            var closing=(Window)Activator.CreateInstance(type,new object?[]{owner,sources,(Func<bool>)(()=>throw new InvalidOperationException("已关闭拥有者不应再次询问"))})!;
            closing.Dispatcher.BeginInvoke(new Action(()=>
            {
                foreach(var item in Visual<TreeView>(closing).Single().Items.OfType<TreeViewItem>())item.IsExpanded=true;closing.UpdateLayout();
                var leaf=Visual<CheckBox>(closing).Single(c=>c.Tag is AvailableDataSource);leaf.IsChecked=true;Trace("closing owner");owner.Close();Trace("owner close returned");
            }),System.Windows.Threading.DispatcherPriority.ContextIdle);
            Assert.False(closing.ShowDialog());Assert.False(closing.IsVisible);Trace("owned modal returned");
        }
        finally{owner.Close();}
    });
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
    public void Roi_sync_clones_each_target_and_binding_graph_and_commits_with_Group_without_overwriting_subscriptions()
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.ImageThreshold","SunEyeVision.Tool.ImageThreshold.dll"));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool",true)!);
        using var f=new Fixture(configure:child=>{foreach(var node in child.Nodes){node.ToolType="ImageThreshold";node.Parameters=(ToolParameters)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters",true)!)!;}});
        var group=f.Editor.AddGroup();var source=f.Source("a",nameof(RoiParameters.InspectionRois));
        Assert.Equal(GroupParameterEditorKind.Region,source.EditorKind);
        var item=f.Editor.Add(group,source);f.Editor.AddTarget(item,f.Source("b",nameof(RoiParameters.InspectionRois)));
        var slots=f.Editor.Resolve(item);var original=((RoiParameters)slots[0].Node.Parameters).InspectionRois;
        original.Rois.Add(new Roi{Name="原区域",LocalGeometry=new RoiShape{Geometry=new SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d(10,20,5)},
            ParameterBindings=new(){["Radius"]=ParamSetting.CreateBinding("Radius","source","radius")}});
        f.Editor.Synchronize(item);
        var first=((RoiParameters)slots[0].Node.Parameters).InspectionRois;var second=((RoiParameters)slots[1].Node.Parameters).InspectionRois;
        Assert.NotSame(original,first);Assert.NotSame(first,second);Assert.NotSame(first.Rois[0],second.Rois[0]);
        Assert.NotSame(first.Rois[0].ParameterBindings,second.Rois[0].ParameterBindings);
        Assert.NotSame(first.Rois[0].ParameterBindings!["Radius"],second.Rois[0].ParameterBindings!["Radius"]);
        Assert.Equal(original.Rois[0].Id,second.Rois[0].Id);Assert.Equal(5,second.Rois[0].LocalGeometry!.Radius);
        first.Rois[0].Name="独立区域";Assert.Equal("原区域",second.Rois[0].Name);Assert.Equal("原区域",original.Rois[0].Name);
        Assert.All(f.Child.Nodes,n=>Assert.Empty(((RoiParameters)n.Parameters).InspectionRois.Rois));
        Assert.True(f.Context.TryCommitEditor(out var error),error);
        Assert.Equal("独立区域",((RoiParameters)f.Child.Nodes[0].Parameters).InspectionRois.Rois[0].Name);
        var secondSlot=f.Editor.Resolve(((SubroutineParameters)f.Context.DraftNode.Parameters).RunParameterGroups[0].Parameters[0])[1];
        ((RoiParameters)secondSlot.Node.Parameters).InspectionRois.BindingConfig=ParamSetting.CreateBinding("InspectionRois","upstream","roi");
        Assert.Throws<InvalidOperationException>(()=>f.Editor.Synchronize(((SubroutineParameters)f.Context.DraftNode.Parameters).RunParameterGroups[0].Parameters[0]));
        Assert.Equal("原区域",((RoiParameters)f.Child.Nodes[1].Parameters).InspectionRois.Rois[0].Name);
    }
    [Fact]
    public void Real_roi_editor_prepares_private_geometry_and_releases_only_its_shared_image_overlay()=>Sta(()=>
    {
        var root=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,"plugins","SunEyeVision.Tool.ImageThreshold","SunEyeVision.Tool.ImageThreshold.dll"));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool",true)!);
        using var f=new Fixture(configure:child=>{foreach(var node in child.Nodes){node.ToolType="ImageThreshold";node.Parameters=(ToolParameters)Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters",true)!)!;}});
        var group=f.Editor.AddGroup();var item=f.Editor.Add(group,f.Source("a",nameof(RoiParameters.InspectionRois)));
        var image=new SunEyeVision.Plugin.SDK.UI.Controls.ImageControl();var sentinel=new System.Windows.Shapes.Rectangle{Width=2,Height=2};image.InteractionOverlayCanvasControl.Children.Add(sentinel);
        using var editor=new GroupRegionParameterControl(f.Editor,item,()=>image);
        var panel=new Grid();panel.ColumnDefinitions.Add(new(){Width=new GridLength(400)});panel.ColumnDefinitions.Add(new());panel.Children.Add(image);Grid.SetColumn(editor,1);panel.Children.Add(editor);
        var window=new Window{Content=panel,Width=1000,Height=680,Left=-10000,ShowActivated=false};
        try
        {
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/"+resource,UriKind.Relative)});
            window.Show();window.UpdateLayout();Assert.Equal(2,image.InteractionOverlayCanvasControl.Children.Count);
            Assert.Single(Visual<SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views.RoiEditorControl>(editor),c=>c.IsVisible);
            editor.Value.Rois.Add(new Roi{Name="候选区域",LocalGeometry=new RoiShape{Geometry=new SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d(10,20,5)}});
            Assert.True(editor.HasPendingEdits);Assert.True(editor.TryPrepare(out var error),error);Assert.False(editor.HasPendingEdits);
            Assert.Single(((RoiParameters)f.Editor.Resolve(item)[0].Node.Parameters).InspectionRois.Rois);
            Assert.Empty(((RoiParameters)f.Child.Nodes[0].Parameters).InspectionRois.Rois);
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();var bitmap=new RenderTargetBitmap(1000,680,96,96,PixelFormats.Pbgra32);bitmap.Render(panel);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-roi-editor-Dark.png")))encoder.Save(file);
            editor.Visibility=Visibility.Collapsed;Assert.Single(image.InteractionOverlayCanvasControl.Children);Assert.Same(sentinel,image.InteractionOverlayCanvasControl.Children[0]);
            editor.Visibility=Visibility.Visible;window.UpdateLayout();Assert.Equal(2,image.InteractionOverlayCanvasControl.Children.Count);
            editor.Value.Rois[0].Name="尚未确认";editor.Dispose();Assert.Single(image.InteractionOverlayCanvasControl.Children);
            Assert.Equal("候选区域",((RoiParameters)f.Editor.Resolve(item)[0].Node.Parameters).InspectionRois.Rois[0].Name);
            panel.Children.Remove(editor);
            using var page=new GroupRunParametersControl(f.Editor,()=>image);Grid.SetColumn(page,1);panel.Children.Add(page);window.UpdateLayout();
            var tree=Visual<TreeView>(page).First();((TreeViewItem)((TreeViewItem)tree.Items[0]).Items[0]).IsSelected=true;window.UpdateLayout();
            var active=Assert.Single(Visual<GroupRegionParameterControl>(page));active.Value.Rois[0].Name="重绑定后保留";
            page.BindParameters(f.Editor.Parameters);window.UpdateLayout();Assert.Same(active,Assert.Single(Visual<GroupRegionParameterControl>(page)));Assert.True(active.HasPendingEdits);
            Assert.True(page.TryPrepare(out error),error);Assert.Equal("重绑定后保留",((RoiParameters)f.Editor.Resolve(item)[0].Node.Parameters).InspectionRois.Rois[0].Name);
            Assert.Empty(((RoiParameters)f.Child.Nodes[0].Parameters).InspectionRois.Rois);
        }
        finally{window.Close();}
    });
    [Fact]
    public void File_save_reopen_preserves_Group_metadata_and_all_related_values_and_rejects_invalid_draft_before_rewrite()
    {
        using var f=new Fixture(configure:flow=>{var output=(ProtocolParseParameters)flow.Nodes.Single(n=>n.Id=="a").Parameters;output.Outputs[0].Name="Value";output.Outputs[0].DataType="float";output.InputText="7";});var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing test root"),"group-file",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(ProtocolParseTool));
        var hostRoot=typeof(GroupRunParameterConfigTests).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var plugin=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostRoot,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!);
        // Persistence fixture: register the actual two required tools. Full release admission
        // is tested by the real Release executable; never pretend tests/bin is a release root.
        SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
        var producer=(ProtocolParseParameters)f.Child.Nodes.Single(n=>n.Id=="a").Parameters;
        var manager=new SolutionManager(Path.Combine(root,"solutions"),Path.Combine(root,"settings.json"),Path.Combine(root,"recovery"),root);manager.SetCurrentSolution(f.Model,registerInCatalog:false);
        var session=manager.Session!;
        using var context=new ProductionEditorContext(session.Editing.Host,f.Parent.Id,f.Group,()=>ReferenceEquals(manager.Session,session),_=>{},documentEdits:session.Editing,
            allowRelated:(id,node)=>WorkflowOwnership.IsInternalNode(f.Model,f.Child,f.Parent.Id,id,node));
        var editor=new GroupRunParameterSession(context,f.Model,(id,node)=>WorkflowOwnership.IsInternalNode(f.Model,f.Child,f.Parent.Id,id,node));
        var group=editor.AddGroup();group.Name="通信";var source=editor.Available().Single(s=>s.Target.NodeId=="a"&&s.Target.PropertyName==nameof(ProtocolParseParameters.Separator));var item=editor.Add(group,source);editor.AddTarget(item,editor.Available().Single(s=>s.Target.NodeId=="b"&&s.Target.PropertyName==nameof(ProtocolParseParameters.Separator)));editor.Write(item,";");
        var fieldId=producer.Outputs[0].FieldId;var fieldKey=producer.Outputs[0].OutputDefinition().BindingKey;
        var display=new SubroutineDisplayItem{DisplayName="数值",DisplayType=SubroutineDisplayType.Text,TextStyle=new(),BindingSource="a."+fieldKey,IsAccumulated=true,
            BoundDataSource=new(){SourceWorkflowId="child",SourceNodeId="a",SourceFieldId=fieldId,PropertyName=fieldKey,PropertyType=typeof(double),VariableType="Double"}};
        display.GraphicsStyle.LineWidth=5;display.GraphicsStyle.Opacity=0.6;
        ((SubroutineParameters)context.DraftNode.Parameters).DisplayConfigs.Add(display);
        ((SubroutineParameters)context.DraftNode.Parameters).DisplayConfigs.Add(new(){DisplayName="圆弧",DisplayType=SubroutineDisplayType.Arc});
        EventHandler<Solution> prepare=(_,_)=>session.Editing.AcceptAll();manager.SolutionSaving+=prepare;
        var path=Path.Combine(root,"group.solution");var result=manager.SaveCurrentDocument(path);Assert.True(result.FileWritten,result.Error);Assert.False(manager.HasUnsavedChanges);
        var bytes=File.ReadAllBytes(path);
        var invalidDisplay=((SubroutineParameters)context.DraftNode.Parameters).DisplayConfigs[0];invalidDisplay.DisplayName="";
        Assert.False(manager.SaveCurrentDocument(path).FileWritten);Assert.Equal(bytes,File.ReadAllBytes(path));invalidDisplay.DisplayName="数值";
        ((SubroutineParameters)context.DraftNode.Parameters).RunParameterGroups[0].Name="";
        Assert.False(manager.SaveCurrentDocument(path).FileWritten);Assert.Equal(bytes,File.ReadAllBytes(path));
        manager.SolutionSaving-=prepare;manager.CloseSolution(saveChanges:false);
        var reader=new SolutionManager(Path.Combine(root,"reader-solutions"),Path.Combine(root,"reader-settings.json"),Path.Combine(root,"reader-recovery"),root);
        var reopened=reader.OpenSolution(path)!;Assert.NotNull(reopened);Assert.NotSame(f.Model,reopened);
        var saved=(SubroutineParameters)reopened.Workflows.Single(w=>w.Id=="parent").Nodes.Single().Parameters;
        Assert.Equal(2,saved.DisplayConfigs.Count);Assert.Equal(typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Arc2d[]),saved.DisplayConfigs.Single(d=>d.DisplayType==SubroutineDisplayType.Arc).ParamType);var restoredDisplay=saved.DisplayConfigs.Single(d=>d.DisplayType==SubroutineDisplayType.Text);Assert.Equal(display.FieldId,restoredDisplay.FieldId);Assert.Equal(SubroutineDisplayType.Text,restoredDisplay.DisplayType);Assert.Equal("a."+fieldKey,restoredDisplay.BindingSource);Assert.True(restoredDisplay.IsAccumulated);
        Assert.Equal("child",restoredDisplay.BoundDataSource!.SourceWorkflowId);Assert.Equal("a",restoredDisplay.BoundDataSource.SourceNodeId);Assert.Equal(fieldId,restoredDisplay.BoundDataSource.SourceFieldId);Assert.Equal(typeof(double),restoredDisplay.BoundDataSource.PropertyType);Assert.Null(restoredDisplay.BoundDataSource.CurrentValue);
        Assert.NotSame(display.GraphicsStyle,restoredDisplay.GraphicsStyle);Assert.Equal(5,restoredDisplay.GraphicsStyle.LineWidth);Assert.Equal(0.6,restoredDisplay.GraphicsStyle.Opacity);
        var alias=Assert.Single(Assert.Single(saved.RunParameterGroups).Parameters);Assert.Equal(item.Id,alias.Id);Assert.Equal(2,alias.Targets.Count);Assert.Equal("ProtocolParse",alias.SourceToolType);Assert.Equal(nameof(ProtocolParseParameters.Separator),alias.SourceParameterName);
        Assert.All(reopened.Workflows.Single(w=>w.Id=="child").Nodes,n=>Assert.Equal(";",((ProtocolParseParameters)n.Parameters).Separator));
        var actualChild=new ProtocolParseTool().Run((ProtocolParseParameters)reopened.Workflows.Single(w=>w.Id=="child").Nodes.Single(n=>n.Id=="a").Parameters);Assert.True(actualChild.IsSuccess,actualChild.ErrorMessage);
        saved.ExecuteSubWorkflowAsync=(_,_,_)=>System.Threading.Tasks.Task.FromResult(new SunEyeVision.Plugin.SDK.Execution.Results.ExecutionResult{Success=true,NodeResults=new Dictionary<string,SunEyeVision.Plugin.SDK.Execution.Results.NodeExecutionResult>{["a"]=new(){Result=actualChild}}});
        var actualGroup=(SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!.GetMethod("Run",new[]{typeof(SubroutineParameters)})!.Invoke(Activator.CreateInstance(plugin.GetType("SunEyeVision.Tool.Subroutine.SubroutineTool",true)!),new object[]{saved})!;
        try{Assert.True(actualGroup.IsSuccess,actualGroup.ErrorMessage);var text=Assert.IsType<SunEyeVision.Plugin.SDK.Models.Visualization.TextDisplayItem>(Assert.Single(actualGroup.Display.Items));Assert.Equal("7",Assert.Single(text.Texts).Content);}
        finally{actualGroup.Resources?.Dispose();actualChild.Resources?.Dispose();reader.CloseSolution(saveChanges:false);}

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
