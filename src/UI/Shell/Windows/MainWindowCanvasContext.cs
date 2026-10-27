using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal static WorkflowContext? ResolveCanvasContext(IWorkflowContextService service,WorkflowTabViewModel? tab)
  =>tab==null?null:service.GetOrCreate(tab.CurrentWorkflowId,tab.EffectiveDisplayName);
}
