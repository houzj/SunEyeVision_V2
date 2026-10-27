using System.Windows;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
    public static readonly DependencyProperty WorkflowEditingAllowedProperty = DependencyProperty.Register(
        nameof(WorkflowEditingAllowed), typeof(bool), typeof(ShellWorkspaceControl), new PropertyMetadata(true));
    public bool WorkflowEditingAllowed
    {
        get => (bool)GetValue(WorkflowEditingAllowedProperty);
        set => SetValue(WorkflowEditingAllowedProperty, value);
    }
    public static readonly DependencyProperty RootWorkspaceEditingAllowedProperty = DependencyProperty.Register(
        nameof(RootWorkspaceEditingAllowed), typeof(bool), typeof(ShellWorkspaceControl), new PropertyMetadata(true));
    public bool RootWorkspaceEditingAllowed
    {
        get => (bool)GetValue(RootWorkspaceEditingAllowedProperty);
        set => SetValue(RootWorkspaceEditingAllowedProperty, value);
    }
    public static readonly DependencyProperty RootUndoAllowedProperty = DependencyProperty.Register(nameof(RootUndoAllowed),typeof(bool),typeof(ShellWorkspaceControl),new PropertyMetadata(false));
    public bool RootUndoAllowed {get=>(bool)GetValue(RootUndoAllowedProperty);set=>SetValue(RootUndoAllowedProperty,value);}
    public static readonly DependencyProperty RootRedoAllowedProperty = DependencyProperty.Register(nameof(RootRedoAllowed),typeof(bool),typeof(ShellWorkspaceControl),new PropertyMetadata(false));
    public bool RootRedoAllowed {get=>(bool)GetValue(RootRedoAllowedProperty);set=>SetValue(RootRedoAllowedProperty,value);}
    public static readonly DependencyProperty SingleExecutionBusyProperty = DependencyProperty.Register(
        nameof(SingleExecutionBusy), typeof(bool), typeof(ShellWorkspaceControl), new PropertyMetadata(false));
    public bool SingleExecutionBusy
    {
        get => (bool)GetValue(SingleExecutionBusyProperty);
        set => SetValue(SingleExecutionBusyProperty, value);
    }
}
