using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class GroupRunParameterPageTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Solution Model=new();
        public readonly SunEyeVision.Workflow.Workflow Parent=new("parent","主流程"), Child=new("child","Group内部"){IsSubroutine=true};
        public readonly WorkflowNodeBase Owner=new("group","Group1","Group1","Subroutine"){SubroutineId="child",Parameters=new SubroutineParameters{SubroutineId="child"}};
        public readonly NodeConfigurationEditHost Host;
        public readonly ProductionEditorContext Context;
        public readonly GroupRunParameterSession Session;
        public Fixture(Action<SubroutineParameters>? bind=null,string secondMode="Text")
        {
            Parent.Nodes.Add(Owner);
            Child.Nodes.Add(new("first","解析1","解析1","ProtocolParse"){Parameters=new ProtocolParseParameters()});
            Child.Nodes.Add(new("second","解析2","解析2","ProtocolParse"){Parameters=new ProtocolParseParameters{Mode=secondMode,InputText=secondMode=="Bytes"?"01":""}});
            Model.Workflows.Add(Parent);Model.Workflows.Add(Child);Host=new(Model);
            bool Allowed(string id,WorkflowNodeBase node)=>WorkflowOwnership.IsInternalNode(Model,Child,Parent.Id,id,node);
            Context=new(Host,Parent.Id,Owner,()=>true,n=>bind?.Invoke((SubroutineParameters)n.Parameters),allowRelated:Allowed);
            Session=new(Context,Model,Allowed);
        }
        public AvailableGroupParameter Source(string node,string property)=>Session.Available().Single(a=>a.Target.NodeId==node&&a.Target.PropertyName==property);
        public void Dispose()=>Context.Discard();
    }
    [Fact]
    public void Actual_parameter_alias_and_multiple_relations_save_together_with_internal_values_and_roundtrip()
    {
        using var f=new Fixture();var group=f.Session.AddGroup();group.Name="协议设置";
        var item=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Separator)));item.Name="分隔符";item.Description="内部两工具共享";
        f.Session.AddTarget(item,f.Source("second",nameof(ProtocolParseParameters.Separator)));
        f.Session.Write(item,";");
        Assert.All(f.Child.Nodes,n=>Assert.Equal(",",((ProtocolParseParameters)n.Parameters).Separator));
        Assert.Empty(((SubroutineParameters)f.Owner.Parameters).RunParameterGroups);
        Assert.True(f.Context.TryCommitEditor(out var error),error);
        Assert.All(f.Child.Nodes,n=>Assert.Equal(";",((ProtocolParseParameters)n.Parameters).Separator));
        var saved=(SubroutineParameters)ToolParameterSnapshot.Create(f.Owner.Parameters);
        var copied=Assert.Single(Assert.Single(saved.RunParameterGroups).Parameters);
        Assert.Equal(item.Id,copied.Id);Assert.Equal("分隔符",copied.Name);Assert.Equal(2,copied.Targets.Count);
        saved.RunParameterGroups[0].Parameters[0].Targets[0].NodeId="modified-copy";
        Assert.Equal("first",((SubroutineParameters)f.Owner.Parameters).RunParameterGroups[0].Parameters[0].Targets[0].NodeId);
        f.Host.Undo();Assert.Empty(((SubroutineParameters)f.Owner.Parameters).RunParameterGroups);Assert.All(f.Child.Nodes,n=>Assert.Equal(",",((ProtocolParseParameters)n.Parameters).Separator));
        f.Host.Redo();Assert.All(f.Child.Nodes,n=>Assert.Equal(";",((ProtocolParseParameters)n.Parameters).Separator));
    }
    [Fact]
    public void Invalid_relation_duplicate_type_dynamic_binding_and_invalid_values_never_mutate_formal_parameters()
    {
        using var f=new Fixture();var group=f.Session.AddGroup();var item=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.AsciiData)));
        Assert.Throws<InvalidOperationException>(()=>f.Session.AddTarget(item,f.Source("first",nameof(ProtocolParseParameters.AsciiData))));
        Assert.Throws<InvalidOperationException>(()=>f.Session.AddTarget(item,f.Source("second",nameof(ProtocolParseParameters.Separator))));
        Assert.Single(item.Targets);Assert.ThrowsAny<Exception>(()=>f.Session.Write(item,"invalid-bool"));
        var slot=Assert.Single(f.Session.Resolve(item));slot.Node.ParamSettings.SetSetting(ParamSetting.CreateBinding(nameof(ProtocolParseParameters.AsciiData),"source","value"));
        Assert.Throws<InvalidOperationException>(()=>f.Session.Write(item,true));
        Assert.False(((ProtocolParseParameters)slot.Node.Parameters).AsciiData);
        Assert.Throws<InvalidOperationException>(()=>GroupRunParameterSession.ConvertValue("NaN",typeof(double)));
        Assert.Throws<InvalidOperationException>(()=>GroupRunParameterSession.ConvertValue("99",typeof(DayOfWeek)));
        Assert.Throws<InvalidOperationException>(()=>GroupRunParameterSession.ConvertValue(double.NaN,typeof(double)));
        Assert.Throws<InvalidOperationException>(()=>GroupRunParameterSession.ConvertValue((DayOfWeek)99,typeof(DayOfWeek)));
        var mode=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Mode)));
        Assert.Throws<InvalidOperationException>(()=>f.Session.Write(mode,"invalid-mode"));
        Assert.Throws<InvalidOperationException>(()=>f.Session.AddTarget(mode,f.Source("second",nameof(ProtocolParseParameters.StringEncoding))));
        Assert.Single(mode.Targets);
        Assert.Contains(GroupRunParameterSession.Choices(f.Session.Resolve(mode))!,c=>Equals(c.Value,"Text")&&c.DisplayName=="文本解析");
        Assert.Empty(((SubroutineParameters)f.Owner.Parameters).RunParameterGroups);
    }
    [Fact]
    public void Nested_aliases_resolve_typed_targets_and_cyclic_or_missing_aliases_are_rejected()
    {
        using var f=new Fixture();var nested=new SunEyeVision.Workflow.Workflow("nested","nested"){IsSubroutine=true};
        var nestedOwner=new WorkflowNodeBase("nestedOwner","nestedOwner","nestedOwner","Subroutine"){SubroutineId="nested",Parameters=new SubroutineParameters{SubroutineId="nested"}};
        nested.Nodes.Add(new("parse","parse","parse","ProtocolParse"){Parameters=new ProtocolParseParameters()});
        f.Child.Nodes.Add(nestedOwner);f.Model.Workflows.Add(nested);
        var ng=new SubroutineRunParameterGroup();var ni=new SubroutineRunParameter{Name="内部参数"};
        ni.Targets.Add(new(){WorkflowId=nested.Id,NodeId="parse",PropertyName=nameof(ProtocolParseParameters.Separator)});ng.Parameters.Add(ni);((SubroutineParameters)nestedOwner.Parameters).RunParameterGroups.Add(ng);
        var available=f.Session.Available().Single(a=>a.Target.ExposedParameterId==ni.Id);
        var group=f.Session.AddGroup();var item=f.Session.Add(group,available);f.Session.Write(item,"|");
        Assert.True(f.Context.TryCommitEditor(out var error),error);Assert.Equal("|",((ProtocolParseParameters)nested.Nodes[0].Parameters).Separator);
        var nestedCopy=(SubroutineParameters)nestedOwner.Parameters;
        var relations=nestedCopy.RunParameterGroups[0].Parameters[0].Targets;
        relations.Clear();relations.Add(new(){WorkflowId=f.Child.Id,NodeId="first",PropertyName=nameof(ProtocolParseParameters.Separator)});
        Assert.Throws<InvalidOperationException>(()=>f.Session.Resolve(item,false));
        Assert.Equal(",",((ProtocolParseParameters)f.Child.Nodes[0].Parameters).Separator);
        relations.Clear();relations.Add(new(){WorkflowId=f.Child.Id,NodeId=nestedOwner.Id,ExposedParameterId=ni.Id});
        Assert.Throws<InvalidOperationException>(()=>f.Session.Resolve(item,false));
    }
    [Fact]
    public void Copied_Group_relations_remap_to_its_copied_internal_graph_and_preserve_local_alias_identity()
    {
        using var f=new Fixture();var group=f.Session.AddGroup();var item=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Separator)));
        Assert.True(f.Context.TryCommitEditor(out var error),error);
        var counter=0;
        var fragment=WorkflowFragmentCopy.Prepare(f.Parent,new[]{f.Owner.Id},"destination",id=>f.Model.Workflows.SingleOrDefault(w=>w.Id==id),
            (original,_)=>new WorkflowNodeBase("copy"+(++counter),original.Name,original.DispName,original.ToolType));
        var root=Assert.Single(fragment.Nodes);var owned=Assert.Single(fragment.OwnedWorkflows);
        var relation=Assert.Single(Assert.Single(Assert.Single(((SubroutineParameters)root.Parameters).RunParameterGroups).Parameters).Targets);
        Assert.Equal(owned.Id,relation.WorkflowId);Assert.Equal(owned.Nodes[0].Id,relation.NodeId);Assert.NotEqual("first",relation.NodeId);
        Assert.Equal(item.Id,((SubroutineParameters)root.Parameters).RunParameterGroups[0].Parameters[0].Id);
    }
    [Fact]
    public void Exposure_metadata_validates_identities_and_threshold_enum_is_explicitly_available()
    {
        using var f=new Fixture();var group=f.Session.AddGroup();var item=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Separator)));
        var p=f.Session.Parameters;Assert.True(p.Validate().IsValid);
        var second=f.Session.AddGroup();second.Name=group.Name;Assert.False(p.Validate().IsValid);second.Name="独立分组";
        item.Targets.Add(new(){WorkflowId="child",NodeId="first",PropertyName=nameof(ProtocolParseParameters.Separator)});Assert.False(p.Validate().IsValid);item.Targets.RemoveAt(1);
        Assert.True(p.Validate().IsValid);
        var hostRoot=typeof(GroupRunParameterPageTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var assembly=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostRoot,"plugins","SunEyeVision.Tool.ImageThreshold","SunEyeVision.Tool.ImageThreshold.dll"));
        var parameters=(ToolParameters)Activator.CreateInstance(assembly.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters",true)!)!;
        var descriptor=GroupParameterCatalog.Describe(parameters).Single(d=>d.PropertyName=="Method");
        Assert.True(descriptor.ValueType.IsEnum);Assert.Equal("阈值方法",descriptor.DisplayName);Assert.Equal(10,descriptor.Choices!.Count);
        Assert.DoesNotContain(GroupParameterCatalog.Describe(parameters),d=>d.PropertyName==nameof(ToolParameters.Context)||d.PropertyName==nameof(ToolParameters.ResolvedInputImage));
    }
    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    [Fact]
    public void Deployed_Group_control_hosts_fifth_page_selects_real_alias_and_renders_actual_dark_and_light_layouts()=>Sta(()=>
    {
        var hostRoot=typeof(GroupRunParameterPageTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
        var assembly=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(hostRoot,"plugins","SunEyeVision.Tool.Subroutine","SunEyeVision.Tool.Subroutine.dll"));
        ISubroutineParameterEditor? editor=null;
        using var f=new Fixture(p=>editor!.BindParameters(p,null),secondMode:"Bytes");
        var group=f.Session.AddGroup();group.Name="通信设置";var item=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Separator)));item.Name="协议分隔符";
        var mode=f.Session.Add(group,f.Source("first",nameof(ProtocolParseParameters.Mode)));
        f.Session.AddTarget(mode,f.Source("second",nameof(ProtocolParseParameters.Mode)));
        var control=(UserControl)Activator.CreateInstance(assembly.GetType("SunEyeVision.Tool.Subroutine.Views.SubroutineDebugControl",true)!,new object?[]{"child","Group1","group",null,null,f.Context.DraftNode.Parameters,null})!;
        editor=(ISubroutineParameterEditor)control;using var page=new GroupRunParametersControl(f.Session);editor.SetRunParameterEditor(page);
        try
        {
            ToolEditorCommit.AttachDraftContext(control,f.Context);f.Context.PrepareView=()=>ToolEditorCommit.TryPrepare(control,out var error)?null:error;
            var tabs=Descendants<TabControl>(control).FirstOrDefault();
            control.Measure(new Size(1080,580));control.Arrange(new Rect(0,0,1080,580));control.UpdateLayout();
            tabs??=Descendants<TabControl>(control).First();Assert.Equal(5,tabs.Items.Count);tabs.SelectedIndex=2;
            foreach(var theme in new[]{"Dark","Light"})
            {
                control.Resources.MergedDictionaries.Clear();
                control.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Shell/Resources/AppResources.xaml",UriKind.Relative)});
                control.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml",UriKind.Relative)});
                control.Resources.MergedDictionaries.Add(new(){Source=new Uri($"/SunEyeVision.UI;component/Themes/{theme}/{theme}Theme.xaml",UriKind.Relative)});
                control.UpdateLayout();control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);control.UpdateLayout();
                var tree=Descendants<TreeView>(page).Single();var groupRow=(TreeViewItem)tree.Items[0];groupRow.IsExpanded=true;((TreeViewItem)groupRow.Items[0]).IsSelected=true;
                Assert.Single(Descendants<Button>(page),b=>Equals(b.Content,"编辑"));control.UpdateLayout();
                Assert.True(page.ActualWidth>900);Assert.True(page.ActualHeight>450);
                var image=new RenderTargetBitmap(1080,580,96,96,PixelFormats.Pbgra32);image.Render(control);
                var root=Environment.GetEnvironmentVariable("S2_CASE_ROOT")??throw new InvalidOperationException("Missing validation output root");Directory.CreateDirectory(root);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(root,"group-runparameters-"+theme+".png"));encoder.Save(file);
            }
            var parameterTree=Descendants<TreeView>(page).Single();var parameterGroup=(TreeViewItem)parameterTree.Items[0];
            ((TreeViewItem)parameterGroup.Items[1]).IsSelected=true;control.UpdateLayout();
            var optionEditor=Descendants<ComboBox>(page).Single(c=>c.ItemsSource is System.Collections.Generic.IReadOnlyList<GroupParameterChoice>);
            Assert.Null(optionEditor.SelectedValue);
            Assert.Equal("Bytes",((ProtocolParseParameters)f.Session.Resolve(mode)[1].Node.Parameters).Mode);
            Assert.Equal(2,optionEditor.Items.Count);optionEditor.SelectedValue="Text";
            control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.Equal("Text",((ProtocolParseParameters)f.Session.Resolve(mode)[1].Node.Parameters).Mode);
            ((TreeViewItem)parameterGroup.Items[0]).IsSelected=true;
            f.Session.Write(item,";");Assert.True(ToolEditorCommit.TryCommit(control,out var message),message);
            Assert.Equal(";",((ProtocolParseParameters)f.Child.Nodes[0].Parameters).Separator);
            var window=new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(control){Title="Group1",Width=1100,Height=680,Left=-10000,ShowActivated=false};
            try
            {
                control.Resources.MergedDictionaries[2]=new(){Source=new Uri("/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml",UriKind.Relative)};
                window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Shell/Resources/AppResources.xaml",UriKind.Relative)});
                window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Resources/Styles/CommonStyleResources.xaml",UriKind.Relative)});
                window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml",UriKind.Relative)});
                window.Show();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();
                Assert.Equal(1100,window.ActualWidth);Assert.Equal(680,window.ActualHeight);
                var bitmap=new RenderTargetBitmap(1100,680,96,96,PixelFormats.Pbgra32);bitmap.Render((FrameworkElement)window.Content);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"group-runparameters-window-Dark.png"));encoder.Save(file);
            }
            finally{window.Close();}
        }
        finally{editor.Dispose();}
    });
    private static void Sta(Action action)
    {
        Exception? failure=null;var thread=new Thread(()=>{try{action();}catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(35)));
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
