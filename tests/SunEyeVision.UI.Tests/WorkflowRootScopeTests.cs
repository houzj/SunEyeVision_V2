using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class WorkflowRootScopeTests
{
    [Fact]
    public void Returning_to_a_tab_preserves_nested_visible_definition_and_root_execution_scope()
    {
        var root = new Flow("root", "根"); var child = new Flow("child", "子") { IsSubroutine = true };
        var nested = new Flow("nested", "孙") { IsSubroutine = true };
        root.Nodes.Add(new("owner", "组", "组", "Subroutine") { SubroutineId = child.Id });
        child.Nodes.Add(new("owner", "内组", "内组", "Subroutine") { SubroutineId = nested.Id });
        var definitions = new[] { root, child, nested }.ToDictionary(f => f.Id);
        Flow? Resolve(string id) => definitions.GetValueOrDefault(id);
        var tab = new WorkflowTabViewModel { Id = root.Id, Name = root.Name };
        tab.SetContent(root.Nodes, root.Connections);
        tab.NavigateToSubroutine(child.Id, "组", null, "owner"); tab.SetContent(child.Nodes, child.Connections);
        tab.NavigateToSubroutine(nested.Id, "内组", null, "owner"); tab.SetContent(nested.Nodes, nested.Connections);
        var nestedHistory = tab.CommandManager; tab.CurrentScale = 1.4; tab.CaptureViewport(140, 280);
        MainWindowViewModel.BindExistingWorkflowTab(tab, root, Resolve);
        Assert.True(tab.IsInSubroutine); Assert.Equal(nested.Id, tab.CurrentWorkflowId);
        Assert.Same(nested.Nodes, tab.WorkflowNodes); Assert.Same(nested.Connections, tab.WorkflowConnections);
        Assert.Same(root.Nodes, tab.RootWorkflowNodes); Assert.Single(tab.RootWorkflowNodes); Assert.Empty(tab.WorkflowNodes);
        Assert.Same(nestedHistory, tab.CommandManager); Assert.Equal(1.4, tab.CurrentScale);
        Assert.Same(root, MainWindowViewModel.ResolveRootExecutionDefinition(tab, Resolve));
        var notifications = new List<string?>(); tab.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        Assert.True(tab.NavigateBack()); MainWindowViewModel.BindExistingWorkflowTab(tab, root, Resolve);
        Assert.Contains(nameof(tab.NavigationAncestorIds), notifications); Assert.Contains(nameof(tab.CurrentOwnerNodeId), notifications);
        Assert.Equal("owner", tab.CurrentOwnerNodeId);
        Assert.Same(child.Nodes, tab.WorkflowNodes); Assert.Same(root.Nodes, tab.RootWorkflowNodes);
        Assert.True(tab.NavigateBack()); MainWindowViewModel.BindExistingWorkflowTab(tab, root, Resolve);
        Assert.Same(root.Nodes, tab.WorkflowNodes); Assert.False(tab.IsInSubroutine);
    }
    [Fact]
    public void Invalid_ancestor_owner_returns_to_root_without_copying_child_nodes_into_it()
    {
        var root = new Flow("root", "根"); var child = new Flow("child", "子") { IsSubroutine = true };
        var nested = new Flow("nested", "孙") { IsSubroutine = true };
        root.Nodes.Add(new("owner", "组", "组", "Subroutine") { SubroutineId = child.Id });
        child.Nodes.Add(new("nested-owner", "内组", "内组", "Subroutine") { SubroutineId = nested.Id });
        nested.Nodes.Add(new("image", "图像", "图像", "ImageLoad"));
        var definitions = new[] { root, child, nested }.ToDictionary(f => f.Id);
        Flow? Resolve(string id) => definitions.GetValueOrDefault(id);
        var tab = new WorkflowTabViewModel { Id = root.Id };
        tab.SetContent(root.Nodes, root.Connections);
        tab.NavigateToSubroutine(child.Id, "组", null, "owner"); tab.SetContent(child.Nodes, child.Connections);
        tab.NavigateToSubroutine(nested.Id, "内组", null, "nested-owner"); tab.SetContent(nested.Nodes, nested.Connections);
        root.Nodes.Clear(); // The entire ancestor link was removed while another tab was selected.
        MainWindowViewModel.BindExistingWorkflowTab(tab, root, Resolve);
        Assert.False(tab.IsInSubroutine); Assert.Equal(root.Id, tab.CurrentWorkflowId); Assert.Empty(root.Nodes);
        Assert.Same(root.Nodes, tab.WorkflowNodes); Assert.Single(nested.Nodes); Assert.Empty(tab.NavigationAncestorIds);
        Assert.Null(tab.CurrentOwnerNodeId);
        Assert.Null(MainWindowViewModel.ResolveRootExecutionDefinition(tab, _ => null));
        Assert.Null(MainWindowViewModel.ResolveRootExecutionDefinition(tab, _ => child));
        Assert.Throws<InvalidOperationException>(() => MainWindowViewModel.BindExistingWorkflowTab(tab, child, Resolve));
    }
    [Fact]
    public void Replacing_root_definition_rebinds_the_single_root_collection()
    {
        var tab = new WorkflowTabViewModel { Id = "root" }; var original = new Flow("root", "旧");
        tab.SetContent(original.Nodes, original.Connections);
        var replacement = new Flow("root", "新"); replacement.Nodes.Add(new("n", "图像", "图像", "ImageLoad"));
        MainWindowViewModel.BindExistingWorkflowTab(tab, replacement, id => id == "root" ? replacement : null);
        Assert.Same(replacement.Nodes, tab.RootWorkflowNodes); Assert.Same(replacement.Nodes, tab.WorkflowNodes);
        Assert.NotSame(original.Nodes, tab.RootWorkflowNodes);
    }
}
