using SunEyeVision.UI.Features.WorkflowUI;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    internal static Flow? ResolveRootExecutionDefinition(WorkflowTabViewModel tab, Func<string, Flow?> resolve)
    {
        var root = resolve(tab.RootWorkflowId);
        return root is { IsSubroutine: false } && root.Id == tab.RootWorkflowId ? root : null;
    }
    internal static void BindExistingWorkflowTab(WorkflowTabViewModel tab, Flow root, Func<string, Flow?> resolve)
    {
        if (root.Id != tab.RootWorkflowId || root.IsSubroutine)
            throw new InvalidOperationException("标签页根流程身份不一致。");
        tab.BindRootWorkflowNodes(root.Nodes);
        if (!tab.IsNavigationCurrent(resolve)) tab.ResetNavigation();
        var visible = tab.IsInSubroutine ? resolve(tab.CurrentWorkflowId)! : root;
        tab.SetContent(visible.Nodes, visible.Connections);
    }
}
