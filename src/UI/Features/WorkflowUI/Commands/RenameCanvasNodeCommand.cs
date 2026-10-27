using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>名称与独占子流程显示名共用历史；保留局部序号供撤销，不把其提前放回空洞池。</summary>
public sealed class RenameCanvasNodeCommand : IUndoableCommand
{
    private readonly WorkflowNodeBase _node;
    private readonly Flow? _owned;
    private readonly string _beforeName,_beforeDisplay,_afterDisplay;
    private readonly string? _beforeOwnedName,_beforeSubroutineName;
    private readonly Action _refresh;
    public RenameCanvasNodeCommand(WorkflowNodeBase node,string name,Flow? owned,Action refresh)
    {
        var trimmed=name.Trim();if(trimmed.Length==0||trimmed.Length>64)throw new ArgumentException("节点名称须为1至64个字符。",nameof(name));
        _node=node;_beforeName=node.Name;_beforeDisplay=node.DispName;_afterDisplay=trimmed;_owned=owned;_beforeOwnedName=owned?.Name;_refresh=refresh;_beforeSubroutineName=(node.Parameters as SubroutineParameters)?.SubroutineName;
    }
    public event EventHandler? CanExecuteChanged{add{}remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)
    {
        try
        {
            _node.DispName=_afterDisplay;_node.Name=$"{_node.GlobalIndex} {_afterDisplay}";
            if(_owned!=null)_owned.Name=_afterDisplay;
            if(_node.Parameters is SubroutineParameters parameters)parameters.SubroutineName=_afterDisplay;
            _refresh();
        }
        catch { Restore(); throw; }
    }
    private void Restore()
    {
        _node.DispName=_beforeDisplay;_node.Name=_beforeName;
        if(_owned!=null)_owned.Name=_beforeOwnedName!;
        if(_node.Parameters is SubroutineParameters parameters)parameters.SubroutineName=_beforeSubroutineName??_beforeDisplay;
    }
    public void Undo() { Restore();_refresh(); }
}