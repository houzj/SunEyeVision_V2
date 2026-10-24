using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class NodeEditorRoutingTests
{
    [Fact]
    public void Editor_factories_and_WPF_context_are_owned_by_UI_assembly()
    {
        Assert.Same(typeof(MainWindowViewModel).Assembly, typeof(NodeOpenDecisionChain).Assembly);
        Assert.Same(typeof(MainWindowViewModel).Assembly, typeof(NodeOpenContext).Assembly);
        Assert.Null(typeof(WorkflowNodeBase).Assembly.GetType("SunEyeVision.Workflow.Nodes.NodeOpenContext"));
        Assert.Null(typeof(WorkflowNodeBase).Assembly.GetType("SunEyeVision.Workflow.Nodes.NodeOpenDecisionChain"));
    }

    [Theory]
    [InlineData(DebugWindowStyle.Default, false)]
    [InlineData(DebugWindowStyle.Custom, true)]
    public void Formal_node_style_selects_real_standard_or_custom_shell(DebugWindowStyle style, bool custom) => ImageThresholdProductionEditorTests.Sta(() =>
    {
        ToolRegistry.RegisterTool(typeof(DefaultTool));
        Window? window=null;
        try
        {
            var node=new WorkflowNodeBase("node", "node", "node", "Editor.Open.Default") { DebugWindowStyle=style };
            var context=new NodeOpenContext();
            var editor=new UserControl();
            Assert.True(new NodeOpenDecisionChain().Execute(node,context,()=>editor));
            Assert.Same(node,context.Node); Assert.Same(editor,context.DebugControl);
            window=context.CreatedWindow;
            if(custom)Assert.IsType<CustomDebugWindow>(window); else Assert.IsType<DefaultDebugWindow>(window);
            Assert.Equal(style,context.WindowStyle);
        }
        finally { window?.Close(); ToolRegistry.UnregisterTool("Editor.Open.Default"); }
    });

    [Fact]
    public void None_overrides_tool_custom_style_and_never_invokes_editor_factory() => ImageThresholdProductionEditorTests.Sta(() =>
    {
        ToolRegistry.RegisterTool(typeof(CustomTool));
        try
        {
            var node=new WorkflowNodeBase("node", "node", "node", "Editor.Open.Custom") { DebugWindowStyle=DebugWindowStyle.None };
            var context=new NodeOpenContext(); var called=0;
            Assert.True(new NodeOpenDecisionChain().Execute(node,context,()=>{called++;return new UserControl();}));
            Assert.Same(node,context.Node); Assert.Equal(0,called);
            Assert.Equal(DebugWindowStyle.None,context.WindowStyle); Assert.Null(context.DebugControl); Assert.Null(context.CreatedWindow);
        }
        finally { ToolRegistry.UnregisterTool("Editor.Open.Custom"); }
    });

    [Fact]
    public void Tool_custom_default_and_plugin_Window_owner_are_preserved() => ImageThresholdProductionEditorTests.Sta(() =>
    {
        ToolRegistry.RegisterTool(typeof(CustomTool));
        var owner=new Window { ShowActivated=false, Left=-10000, Top=-10000 }; var supplied=new Window();
        owner.Show();
        try
        {
            var node=new WorkflowNodeBase("node", "node", "node", "Editor.Open.Custom");
            var context=new NodeOpenContext(owner,null);
            Assert.True(new NodeOpenDecisionChain().Execute(node,context,()=>supplied));
            Assert.Equal(DebugWindowStyle.Custom,context.WindowStyle);
            Assert.Same(supplied,context.CreatedWindow); Assert.Same(owner,supplied.Owner);
        }
        finally { supplied.Close();owner.Close();ToolRegistry.UnregisterTool("Editor.Open.Custom"); }
    });

    [Fact]
    public void Subroutine_navigation_uses_formal_identity_without_creating_an_editor() => ImageThresholdProductionEditorTests.Sta(() =>
    {
        var node=new WorkflowNodeBase("node", "node", "node", "Subroutine") { SubroutineId="sub" };
        WorkflowNodeBase? navigated=null; var called=0;
        void Navigate(WorkflowNodeBase current)=>navigated=current;
        NodeOpenDecisionChain.OnControlFlowNodeOpen+=Navigate;
        try
        {
            var context=new NodeOpenContext();
            Assert.True(new NodeOpenDecisionChain().Execute(node,context,()=>{called++;return new UserControl();}));
            Assert.Same(node,navigated); Assert.Same(node,context.Node); Assert.Equal(0,called); Assert.Null(context.CreatedWindow);
        }
        finally { NodeOpenDecisionChain.OnControlFlowNodeOpen-=Navigate; }
    });

    public class Probe : IToolPlugin
    {
        public Type ParamsType=>typeof(GenericToolParameters);
        public Type ResultType=>typeof(GenericToolResults);
        public IReadOnlyList<OutputVariable> OutputVariables=>Array.Empty<OutputVariable>();
        public ToolResults Run(ToolParameters parameters)=>new GenericToolResults();
    }
    [Tool("Editor.Open.Default","default")]
    public sealed class DefaultTool : Probe { }
    [Tool("Editor.Open.Custom","custom", DebugWindowStyle=DebugWindowStyle.Custom)]
    public sealed class CustomTool : Probe { }
}