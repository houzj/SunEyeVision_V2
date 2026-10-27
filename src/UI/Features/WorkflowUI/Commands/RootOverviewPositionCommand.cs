using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>总览位置属于方案根管理历史，不写入节点画布视口。</summary>
public sealed class RootOverviewPositionCommand:IRootWorkspaceHistoryCommand
{
 private readonly Solution _document;private readonly Flow _root;private readonly RootWorkspaceSettings _before,_after;private readonly Action _refresh;private string _expected;
 public string Description=>"移动总览流程";
 public bool IsCurrent=>_document.Workflows.Contains(_root)&&!_root.IsSubroutine&&RootWorkflowCollectionCommand.Stamp(_document)==_expected;
 public RootOverviewPositionCommand(Solution document,Flow root,RootWorkspaceSettings after,Action refresh)
 {_document=document;_root=root;_before=root.WorkspaceSettings;after.Validate();_after=after;_refresh=refresh;_expected=RootWorkflowCollectionCommand.Stamp(document);}
 public void Execute()=>Set(_after);public void Undo()=>Set(_before);
 private void Set(RootWorkspaceSettings value)
 {
  if(!IsCurrent)throw new InvalidOperationException("总览位置历史已变化，请先撤销后续编辑。");
  var prior=_root.WorkspaceSettings;try{_root.WorkspaceSettings=value;_refresh();_expected=RootWorkflowCollectionCommand.Stamp(_document);}catch{_root.WorkspaceSettings=prior;_refresh();_expected=RootWorkflowCollectionCommand.Stamp(_document);throw;}
 }
}