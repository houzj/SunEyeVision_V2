using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Workflow;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class VmRenameTests
{
    [Fact] public void Rename_and_owned_group_title_restore_together_without_releasing_node_identity()
    {
        var node=new WorkflowNodeBase{Id="stable",Name="7 原名",DispName="原名",GlobalIndex=7,LocalIndex=2,Parameters=new SunEyeVision.Plugin.SDK.Execution.Parameters.SubroutineParameters{SubroutineName="原参数名"}};var child=new SunEyeVision.Workflow.Workflow("child","原名");
        var command=new RenameCanvasNodeCommand(node," 新名称 ",child,()=>{});command.Execute(null);
        Assert.Equal("新名称",node.DispName);Assert.Equal("7 新名称",node.Name);Assert.Equal("新名称",child.Name);Assert.Equal("新名称",((SunEyeVision.Plugin.SDK.Execution.Parameters.SubroutineParameters)node.Parameters).SubroutineName);Assert.Equal(2,node.LocalIndex);
        command.Undo();Assert.Equal("7 原名",node.Name);Assert.Equal("原名",node.DispName);Assert.Equal("原名",child.Name);Assert.Equal("原参数名",((SunEyeVision.Plugin.SDK.Execution.Parameters.SubroutineParameters)node.Parameters).SubroutineName);Assert.Equal(2,node.LocalIndex);
        command.Execute(null);Assert.Equal("stable",node.Id);Assert.Equal(7,node.GlobalIndex);
    }
    [Fact] public void Empty_overlong_and_failed_refresh_never_leave_partial_rename()
    {
        var node=new WorkflowNodeBase{Name="1 原名",DispName="原名",GlobalIndex=1};var child=new SunEyeVision.Workflow.Workflow("child","原名");
        Assert.Throws<ArgumentException>(()=>new RenameCanvasNodeCommand(node," ",null,()=>{}));
        Assert.Throws<ArgumentException>(()=>new RenameCanvasNodeCommand(node,new string('字',65),null,()=>{}));
        var command=new RenameCanvasNodeCommand(node,"新名称",child,()=>throw new InvalidOperationException("refresh"));
        Assert.Throws<InvalidOperationException>(()=>command.Execute(null));Assert.Equal("1 原名",node.Name);Assert.Equal("原名",node.DispName);Assert.Equal("原名",child.Name);
    }
}