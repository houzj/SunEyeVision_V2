using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using Xunit;

namespace SunEyeVision.Tool.Subroutine.Tests;
public sealed class SubroutineLoopMigrationTests
{
    private static GenericToolResults Values(params (string Key,object Value)[] fields)
    {var result=new GenericToolResults{Status=ExecutionStatus.Success};foreach(var field in fields)result.AddResultItem(field.Key,field.Value);return result;}
    private static SubroutineParameters Parameters(string type,object limit,Func<int,object> value,Action<int>? onIteration=null)
    {
        var count=0;var p=new SubroutineParameters{SubroutineId="child",SubroutineName="循环",LoopConfig=new(){EnableLoop=true,EnableBreak=true,DataType=type,
            StartValueBinding="parent.Start",EndValueBinding="parent.End",SourceCompareBinding="inside.Value",TargetCompareBinding="parent.Limit",ComparisonOperator="=="}};
        p.Inputs.Add(new(){Name="input"});p.Outputs.Add(new(){Name="output"});
        p.NodeResults=new Dictionary<string,ToolResults>{["parent"]=Values(("Start",0L),("End",5L),("Limit",limit))};
        p.ExecuteSubWorkflowAsync=(_,_,_)=>{count++;onIteration?.Invoke(count);return Task.FromResult(new ExecutionResult{Success=true,NodeResults=new Dictionary<string,NodeExecutionResult>{["inside"]=new(){Result=Values(("Value",value(count)))}}});};
        return p;
    }
    [Theory]
    [InlineData("int",3)]
    [InlineData("float",2)]
    [InlineData("string",2)]
    public void Actual_tool_breaks_using_selected_type_and_sources_from_parent_and_current_iteration(string type,int expected)
    {
        var count=0;object limit=type=="string"?"stop":type=="float"?0.5:3L;
        var p=Parameters(type,limit,i=>type=="string"?(i==2?"stop":"go"):type=="float"?i*0.25:(object)(long)i,i=>count=i);
        var result=new SubroutineTool().Run(p);Assert.True(result.IsSuccess,result.ErrorMessage);Assert.Equal(expected,count);
    }
    [Fact]
    public void Typed_comparison_reference_distinguishes_identical_node_ids_across_scopes()
    {
        var count=0;var p=Parameters("int",3L,i=>(long)i,i=>count=i);
        p.LoopConfig.SourceCompareReference=new("inside","Value","内部","值"){SourceWorkflowId="child"};
        p.LoopConfig.TargetCompareReference=new("inside","Value","父流程","值"){SourceWorkflowId="parent-flow"};
        p.NodeResults!["inside"]=Values(("Value",3L));
        Assert.True(new SubroutineTool().Run(p).IsSuccess);Assert.Equal(3,count);
    }
    [Fact]
    public void Missing_active_comparison_fails_instead_of_silently_running_every_iteration()
    {
        var count=0;var p=Parameters("string","stop",_=>"go",i=>count=i);p.LoopConfig.TargetCompareBinding="parent.Missing";
        var result=new SubroutineTool().Run(p);Assert.False(result.IsSuccess);Assert.Equal(1,count);Assert.Contains("目的比较值",result.ErrorMessage);
    }
    [Fact]
    public void Cancellation_after_child_prevents_another_iteration_and_is_not_swallowed()
    {
        using var cancellation=new CancellationTokenSource();var count=0;
        var p=Parameters("int",99L,i=>(long)i,i=>{count=i;cancellation.Cancel();});
        p.SetExecutionContext(new SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext(cancellationToken:cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(()=>new SubroutineTool().Run(p));Assert.Equal(1,count);
    }
    [Fact]
    public void Interval_and_type_validation_match_native_declared_limits_and_type_change_clears_old_comparisons()
    {
        var p=Parameters("int",1L,_=>1L);p.LoopConfig.LoopInterval=10001;Assert.False(p.Validate().IsValid);
        p.LoopConfig.LoopInterval=10000;Assert.True(p.Validate().IsValid);
        p.LoopConfig.DataType="float";Assert.Empty(p.LoopConfig.SourceCompareBinding);Assert.Empty(p.LoopConfig.TargetCompareBinding);
        p.LoopConfig.DataType="double";Assert.False(p.Validate().IsValid);
    }
    [Fact]
    public async Task Cancellation_interrupts_native_maximum_interval_without_waiting_ten_seconds()
    {
        using var cancellation=new CancellationTokenSource();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p=Parameters("int",99L,i=>(long)i,_=>entered.TrySetResult());p.LoopConfig.LoopInterval=10000;
        p.SetExecutionContext(new SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext(cancellationToken:cancellation.Token));
        var run=Task.Run(()=>new SubroutineTool().Run(p));await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));await Task.Delay(80);cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await run.WaitAsync(TimeSpan.FromSeconds(3)));
    }
    [Fact]
    public void Viewmodel_type_filters_and_binding_catalog_share_declared_scope_and_field_identity()
    {
        var p=Parameters("int",3L,i=>(long)i);var query=new DataSourceQueryService();
        var parent=new AvailableDataSource{SourceWorkflowId="parent-flow",SourceNodeId="parent",PropertyName="Limit",PropertyType=typeof(long)};
        var inside=new AvailableDataSource{SourceWorkflowId="child",SourceNodeId="inside",PropertyName="Value",PropertyType=typeof(long)};
        query.AddExtraDataSourceProvider(()=>new(){parent});
        var vm=new SunEyeVision.Tool.Subroutine.ViewModels.SubroutineParameterDialogViewModel("child","Group","owner",query,new(){inside},p);
        try
        {
            Assert.Equal(new[]{"int","float","string"},vm.AvailableLoopDataTypes);Assert.Equal(2,vm.LoopComparisonSources.Count);
            p.LoopConfig.SourceCompareReference=ImageSourceReference.FromDataSource(inside);p.LoopConfig.TargetCompareReference=ImageSourceReference.FromDataSource(parent);
            var fields=BindingInputCatalog.Describe(p);var source=Assert.Single(fields,f=>f.Key=="Loop:SourceCompareBinding");var target=Assert.Single(fields,f=>f.Key=="Loop:TargetCompareBinding");
            Assert.True(source.InternalProjection);Assert.Equal("child",source.ReadWorkflowId!());Assert.False(target.InternalProjection);Assert.Equal("parent-flow",target.ReadWorkflowId!());
            p.LoopConfig.DataType="float";Assert.Equal(typeof(double),vm.LoopComparisonType);Assert.Null(p.LoopConfig.SourceCompareReference);Assert.Null(p.LoopConfig.TargetCompareReference);
            Assert.Equal(typeof(double),Assert.Single(BindingInputCatalog.Describe(p),f=>f.Key=="Loop:SourceCompareBinding").AllowedTypes[0]);
        }
        finally{vm.Dispose();}
    }

}
