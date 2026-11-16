using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.If.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>条件分支的选出语义：闭区间命中、多命中按槽序、未命中不产生分支、校验失败不产生分支。</summary>
public sealed class IfBranchSelectionTests
{
    private static readonly IfTool Tool=new();

    [Fact]
    public async Task Value_inside_the_range_selects_that_branch()
    {
        var outcome=await Execute(Slot(0,enabled:true,source:55,minimum:100,maximum:200),
            Slot(1,enabled:true,source:55,minimum:50,maximum:60));
        Assert.True(outcome.Success);
        Assert.Equal(new[]{1},outcome.SelectedBranches);
        Assert.Equal("条件2",outcome.Output!.ToValues()[IfOutput.BranchMatch]);
        Assert.Equal(1L,outcome.Output.ToValues()[IfOutput.ModuleStatus]);
    }

    [Theory]
    [InlineData(50d)]
    [InlineData(60d)]
    public async Task Both_range_endpoints_match(double value)
    {
        var outcome=await Execute(Slot(0,enabled:true,source:value,minimum:50,maximum:60));
        Assert.True(outcome.Success);
        Assert.Equal(new[]{0},outcome.SelectedBranches);
    }

    [Theory]
    [InlineData(49.999)]
    [InlineData(60.001)]
    public async Task Value_outside_the_range_selects_nothing(double value)
    {
        var outcome=await Execute(Slot(0,enabled:true,source:value,minimum:50,maximum:60));
        Assert.True(outcome.Success);
        Assert.Empty(outcome.SelectedBranches);
        Assert.Equal(string.Empty,outcome.Output!.ToValues()[IfOutput.BranchMatch]);
        Assert.Equal(1L,outcome.Output.ToValues()[IfOutput.ModuleStatus]);
    }

    [Fact]
    public async Task Disabled_slot_is_never_evaluated()
    {
        var outcome=await Execute(Slot(0,enabled:false,source:55,minimum:50,maximum:60));
        Assert.True(outcome.Success);
        Assert.Empty(outcome.SelectedBranches);
    }

    [Fact]
    public async Task Multiple_hits_are_reported_in_slot_order()
    {
        var outcome=await Execute(
            Slot(0,enabled:true,source:55,minimum:50,maximum:60),
            Slot(1,enabled:true,source:5,minimum:1000,maximum:2000),
            Slot(2,enabled:true,source:55,minimum:0,maximum:100));
        Assert.True(outcome.Success);
        Assert.Equal(new[]{0,2},outcome.SelectedBranches);
        Assert.Equal("条件1;条件3",outcome.Output!.ToValues()[IfOutput.BranchMatch]);
    }

    [Fact]
    public async Task Validation_failure_returns_diagnostics_and_selects_nothing()
    {
        var outcome=await Execute(Slot(0,enabled:true,source:null));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        Assert.Empty(outcome.SelectedBranches);
        Assert.Equal("IF_CONDITION_SOURCE",Assert.Single(outcome.Diagnostics).Code);
    }

    [Fact]
    public async Task Execution_logs_the_enabled_and_matched_conditions()
    {
        var context=new TestContext();
        var settings=Settings(Slot(0,enabled:true,source:55,minimum:50,maximum:60));
        await using var session=Tool.CreateSession();
        await session.ExecuteAsync(new ToolInvocation<IfSettings>(settings,IfFields.Read(settings),context));
        Assert.Contains("条件1",Assert.Single(context.Logs));
    }

    private static async Task<ToolOutcome<IfOutput>> Execute(params SlotSpec[] slots)
    {
        var settings=Settings(slots);
        await using var session=Tool.CreateSession();
        return await session.ExecuteAsync(new ToolInvocation<IfSettings>(settings,IfFields.Read(settings),new TestContext()));
    }

    private static SlotSpec Slot(int index,bool enabled,double? source,
        double minimum=IfFields.DefaultMinimum,double maximum=IfFields.DefaultMaximum)
        =>new(index,enabled,source,minimum,maximum);

    private static IfSettings Settings(params SlotSpec[] slots)
    {
        var all=IfFields.CreateDefaultConditions().ToArray();
        foreach(var slot in slots)
            all[slot.Index]=new IfConditionSettings
                {Enabled=slot.Enabled,Source=slot.Source,Minimum=slot.Minimum,Maximum=slot.Maximum};
        return new IfSettings{Conditions=Array.AsReadOnly(all)}.Freeze();
    }

    private sealed record SlotSpec(int Index,bool Enabled,double? Source,double Minimum,double Maximum);

    private sealed class TestContext:IToolExecutionContext
    {
        public RunIdentity Identity{get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"if-under-test",Guid.NewGuid());
        public CancellationToken Cancellation=>CancellationToken.None;
        public List<string> Logs{get;}=new();
        public void Log(string message)=>Logs.Add(message);
        public IOutputResources Resources=>throw new NotSupportedException("条件分支不申请输出资源。");
    }
}
