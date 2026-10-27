using System.Text.Json;
using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;

public interface IRootWorkspaceHistoryCommand
{
 string Description{get;}
 bool IsCurrent{get;}
 void Execute();
 void Undo();
}

/// <summary>根定义、独占定义及宿主Tab的同一次变更；运行结果不进入历史。</summary>
public sealed class RootWorkflowCollectionCommand : IRootWorkspaceHistoryCommand
{
 private readonly Solution _solution;private readonly WorkflowEngine _engine;private readonly Flow _root;
 private readonly Flow[] _definitions;private readonly Dictionary<Flow,int> _indices;
 private readonly bool _attachOnExecute;private readonly Action<bool> _refresh;
 private readonly GraphBindingUndoSnapshot _before;private GraphBindingUndoSnapshot? _after;
 private string _expected;
 public string Description {get;}
 public bool IsCurrent {get{try{return Stamp(_solution)==_expected;}catch{return false;}}}
 public RootWorkflowCollectionCommand(Solution solution,WorkflowEngine engine,Flow root,IReadOnlyList<Flow> owned,
  bool attachOnExecute,int index,string description,Action<bool> refresh)
 {
  if(root.IsSubroutine)throw new InvalidOperationException("根流程命令不能直接操作Group定义。");
  _solution=solution;_engine=engine;_root=root;_definitions=new[]{root}.Concat(owned).Distinct().ToArray();_attachOnExecute=attachOnExecute;_refresh=refresh;Description=description;
  _indices=_definitions.ToDictionary(flow=>flow,flow=>solution.Workflows.IndexOf(flow));
  if(attachOnExecute){_indices[root]=index;foreach(var flow in owned)_indices[flow]=solution.Workflows.Count+1+Array.IndexOf(_definitions,flow);}
  _before=GraphBindingUndoSnapshot.Capture(solution.Workflows.Concat(_definitions).Distinct(),solution);_expected=Stamp(solution);
 }
 public void Execute()=>Apply(_attachOnExecute,false);
 public void Undo()=>Apply(!_attachOnExecute,true);
 private void Apply(bool attach,bool undo)
 {
  if(!IsCurrent)throw new InvalidOperationException("根流程历史之后还有画布或参数修改，请先撤销后续修改，避免覆盖其他流程配置。");
  if(attach)
  {
   foreach(var flow in _definitions)
    if(_engine.GetWorkflowById(flow.Id)!=null||_solution.Workflows.Any(old=>old.Id==flow.Id))throw new InvalidOperationException("根流程历史身份已被占用。");
  }
  else foreach(var flow in _definitions)
   if(!ReferenceEquals(_engine.GetWorkflowById(flow.Id),flow)||!_solution.Workflows.Contains(flow))throw new InvalidOperationException("根流程定义已被替换。");
  var rollback=GraphBindingUndoSnapshot.Capture(_solution.Workflows.Concat(_definitions).Distinct(),_solution);
  try
  {
   SetDefinitions(attach);
   if(undo)_before.Restore();else _after?.Restore();
   _refresh(attach);
   if(!undo&&_after==null)_after=GraphBindingUndoSnapshot.Capture(_solution.Workflows.Concat(_definitions).Distinct(),_solution);
   _expected=Stamp(_solution);
  }
  catch
  {
   SetDefinitions(!attach);rollback.Restore();_refresh(!attach);_expected=Stamp(_solution);throw;
  }
 }
 private void SetDefinitions(bool attach)
 {
  if(attach)foreach(var flow in _definitions.OrderBy(f=>_indices[f]))
  {
   _engine.RegisterWorkflow(flow);
   if(!_solution.Workflows.Contains(flow))_solution.Workflows.Insert(Math.Clamp(_indices[flow],0,_solution.Workflows.Count),flow);
  }
  else foreach(var flow in _definitions.AsEnumerable().Reverse()){_solution.Workflows.Remove(flow);_engine.DeleteWorkflow(flow.Id);}
 }
 internal static string Stamp(Solution solution)=>JsonSerializer.Serialize(new
 {
  solution.Workflows,
  Variables=solution.GlobalVariables.Select(v=>new{v.Id,v.ProducerBinding,v.ConsumerBindings,v.RebindingReason,v.BindingValidationPending})
 },WorkflowSerializationOptions.Compact);
}

/// <summary>根流程管理历史独立于各容器画布历史，退出方案时丢弃。</summary>
public sealed class RootWorkflowCollectionHistory
{
 private readonly Stack<IRootWorkspaceHistoryCommand> _undo=new(),_redo=new();
 public event EventHandler? Changed;
 public bool CanUndo=>_undo.TryPeek(out var command)&&command.IsCurrent;
 public bool CanRedo=>_redo.TryPeek(out var command)&&command.IsCurrent;
 public void Execute(IRootWorkspaceHistoryCommand command){command.Execute();_undo.Push(command);_redo.Clear();Changed?.Invoke(this,EventArgs.Empty);}
 public void Undo(){if(!CanUndo)return;var command=_undo.Peek();command.Undo();_undo.Pop();_redo.Push(command);Changed?.Invoke(this,EventArgs.Empty);}
 public void Redo(){if(!CanRedo)return;var command=_redo.Peek();command.Execute();_redo.Pop();_undo.Push(command);Changed?.Invoke(this,EventArgs.Empty);}
}

/// <summary>根名称修改与集合操作属于同一历史；只同步显示元数据，不改变内部画布。</summary>
public sealed class RootWorkflowNameCommand : IRootWorkspaceHistoryCommand
{
 private readonly Solution _solution;private readonly Flow _root;private readonly string _before,_after;private readonly Action _refresh;private string _expected;
 public string Description=>"重命名流程";
 public bool IsCurrent{get{try{return RootWorkflowCollectionCommand.Stamp(_solution)==_expected;}catch{return false;}}}
 public RootWorkflowNameCommand(Solution solution,Flow root,string name,Action refresh)
 {
  if(root.IsSubroutine||!solution.Workflows.Contains(root))throw new InvalidOperationException("名称编辑不是正式根流程。");
  if(string.IsNullOrWhiteSpace(name)||name.Length>64||name.IndexOfAny("?;.[]<>()`".ToCharArray())>=0)throw new ArgumentException("流程名称应为1到64字符且不能包含 ? ; . [ ] < > ( ) `。");
  _solution=solution;_root=root;_before=root.Name;_after=name;_refresh=refresh;_expected=RootWorkflowCollectionCommand.Stamp(solution);
 }
 public void Execute()=>Set(_after);
 public void Undo()=>Set(_before);
 private void Set(string name)
 {
  if(!IsCurrent||!_solution.Workflows.Contains(_root))throw new InvalidOperationException("根流程名称历史已经失效，请先撤销后续编辑。");
  var previous=_root.Name;
  try{_root.Name=name;_refresh();_expected=RootWorkflowCollectionCommand.Stamp(_solution);}
  catch{_root.Name=previous;_refresh();_expected=RootWorkflowCollectionCommand.Stamp(_solution);throw;}
 }
}
