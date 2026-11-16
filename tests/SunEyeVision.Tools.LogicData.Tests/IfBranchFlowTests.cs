using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.If.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>整链：条件命中走对应出边，未命中目标标 Skipped（非失败），越界分支序号判失败。</summary>
public sealed class IfBranchFlowTests
{
    private static RuntimeDirectorySnapshot PrepareDirectory()
        =>(RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
            .GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!
            .Invoke(null,new object[]
            {
                new[]
                {
                    Registration("if-runtime","If",typeof(IfTool)),
                    Registration("constant-source","ConstantSource",typeof(ConstantNumberTool)),
                    Registration("undeclared-branch","UndeclaredBranch",typeof(UndeclaredBranchTool))
                }
            })!;

    private static ToolComponentRegistration Registration(string componentId,string toolId,Type entry)
        =>new(new ToolComponentMetadata(componentId,toolId,ToolComponentRole.Runtime,
            FoundationContract.Major,FoundationContract.Minor),entry);

    private static DocumentRunResult Run(DocumentSession session,string workflowId)
    {
        var context=SynchronizationContext.Current;
        DocumentRunBatch batch;
        try{SynchronizationContext.SetSynchronizationContext(null);batch=session.CaptureRunBatch(new[]{workflowId});}
        finally{SynchronizationContext.SetSynchronizationContext(context);}
        using(batch)return batch.RunAsync(_=>{}).GetAwaiter().GetResult();
    }

    [Fact]
    public void Hit_branch_runs_and_the_other_branch_is_skipped()
    {
        using var session=DocumentSession.CreateNew(PrepareDirectory());
        var (flowId,ifId,firstId,secondId)=BuildBranchGraph(session,minimum:40,maximum:60);
        using var result=Run(session,flowId);
        var workflow=result.Workflows[flowId];
        Assert.True(workflow.Success,string.Join(";",workflow.Errors));
        var byNode=workflow.DeclaredOutputs.ToDictionary(output=>output.Identity.NodeId,StringComparer.Ordinal);
        Assert.True(byNode[ifId].IsSuccess);
        Assert.Equal(new[]{0},byNode[ifId].SelectedBranches);
        Assert.Equal(ExecutionStatus.Skipped,byNode[secondId].Status);
        Assert.True(byNode[firstId].IsSuccess);
    }

    [Fact]
    public void No_hit_skips_every_branch_and_keeps_the_run_successful()
    {
        using var session=DocumentSession.CreateNew(PrepareDirectory());
        var (flowId,ifId,firstId,secondId)=BuildBranchGraph(session,minimum:100,maximum:200);
        using var result=Run(session,flowId);
        var workflow=result.Workflows[flowId];
        Assert.True(workflow.Success,string.Join(";",workflow.Errors));
        Assert.Empty(workflow.Errors);
        var byNode=workflow.DeclaredOutputs.ToDictionary(output=>output.Identity.NodeId,StringComparer.Ordinal);
        Assert.True(byNode[ifId].IsSuccess);
        Assert.Empty(byNode[ifId].SelectedBranches);
        Assert.Equal(ExecutionStatus.Skipped,byNode[firstId].Status);
        Assert.Equal(ExecutionStatus.Skipped,byNode[secondId].Status);
    }

    [Fact]
    public void Undeclared_branch_index_fails_the_node_instead_of_skipping_silently()
    {
        using var session=DocumentSession.CreateNew(PrepareDirectory());
        var flow=session.GetWorkflows().Single();
        var edit=session.PrepareGraphEdit(flow.Id);
        var source=session.CreateNode(edit,"UndeclaredBranch",new CanvasPoint(0,0));
        var target=session.CreateNode(edit,"ConstantSource",new CanvasPoint(0,100));
        edit.AddConnection(new ConnectionDefinition("edge-0",source.Id,target.Id,"RightPort","LeftPort"));
        session.CommitGraphEdit(edit,"越界分支序号");
        using var result=Run(session,flow.Id);
        var workflow=result.Workflows[flow.Id];
        Assert.False(workflow.Success);
        var failed=workflow.DeclaredOutputs.Single(output=>output.Identity.NodeId==source.Id);
        Assert.Equal(ExecutionStatus.Failed,failed.Status);
        Assert.Contains("未声明的分支序号",failed.ErrorMessage);
        Assert.Empty(failed.SelectedBranches);
    }

    /// <summary>建立「数值源 → 条件分支 → 两个目标」的图，并把条件1 绑定到数值源输出。</summary>
    private static (string FlowId,string IfId,string FirstId,string SecondId) BuildBranchGraph(
        DocumentSession session,double minimum,double maximum)
    {
        var flow=session.GetWorkflows().Single();
        var edit=session.PrepareGraphEdit(flow.Id);
        var source=session.CreateNode(edit,"ConstantSource",new CanvasPoint(0,0));
        var ifNode=session.CreateNode(edit,"If",new CanvasPoint(0,100));
        var first=session.CreateNode(edit,"ConstantSource",new CanvasPoint(200,100));
        var second=session.CreateNode(edit,"ConstantSource",new CanvasPoint(200,200));
        edit.AddConnection(new ConnectionDefinition("edge-source",source.Id,ifNode.Id,"RightPort","LeftPort"));
        edit.AddConnection(new ConnectionDefinition("edge-branch-0",ifNode.Id,first.Id,"RightPort","LeftPort"));
        edit.AddConnection(new ConnectionDefinition("edge-branch-1",ifNode.Id,second.Id,"RightPort","LeftPort"));
        session.CommitGraphEdit(edit,"建立条件分支图");
        var draft=session.Editing.Host.BeginEdit(flow.Id,ifNode.Id);
        draft.ApplyFields(new Dictionary<string,ParameterSetting>(StringComparer.Ordinal)
        {
            [IfFields.EnabledField(0)]=new ConstantSetting(true),
            [IfFields.SourceField(0)]=new ReferenceSetting(
                new OutputReference(source.Id,ConstantNumberTool.ValuePort,DataType.Number)),
            [IfFields.MinimumField(0)]=new ConstantSetting(minimum),
            [IfFields.MaximumField(0)]=new ConstantSetting(maximum)
        });
        session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{draft}));
        return(flow.Id,ifNode.Id,first.Id,second.Id);
    }
}

public sealed record ProbeSettings:IToolSettings<ProbeSettings>
{
    public ProbeSettings Freeze()=>this with{};
}

public sealed class ProbeNumberOutput:IToolOutput
{
    private readonly double _value;
    internal ProbeNumberOutput(double value)=>_value=value;
    public IReadOnlyDictionary<string,object?> ToValues()
        =>new Dictionary<string,object?>(StringComparer.Ordinal){[ConstantNumberTool.ValuePort]=_value};
}

/// <summary>测试用数值源：模拟前序节点输出，供条件分支选择取值来源。</summary>
public sealed class ConstantNumberTool:IToolDefinition<ProbeSettings,ProbeNumberOutput>,IToolSettingsFields,
    IToolCatalogProvider,IToolManualReferenceSources
{
    public const string ValuePort="output.value";
    public const double Value=50d;
    private static readonly ToolSchema Declaration=new(Array.Empty<Port>(),
        new[]{new Port(ValuePort,DataType.Number,Storage:NumericStorage.Float64)});
    public string ToolId=>"ConstantSource";
    public ToolCatalogDescriptor Catalog{get;}=new("数值源","测试用固定数值输出","generic","逻辑工具",90);
    public ToolSchema SettingsSchema{get;}=new(Array.Empty<Port>(),Array.Empty<Port>());
    public IReadOnlyList<ManualReferenceSource> ManualReferenceSources{get;}=Array.AsReadOnly(new[]
    {
        new ManualReferenceSource(ValuePort)
    });
    public ToolSchema Describe()=>Declaration;
    public InputValues ReadSettings(object settings)=>new(SettingsSchema.Inputs,new Dictionary<string,object?>());
    public object MaterializeSettings(InputValues values)=>new ProbeSettings();
    public IReadOnlyList<Diagnostic> Validate(ProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<ProbeSettings,ProbeNumberOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<ProbeSettings,ProbeNumberOutput>
    {
        public ValueTask<ToolOutcome<ProbeNumberOutput>> ExecuteAsync(ToolInvocation<ProbeSettings> invocation)
            =>ValueTask.FromResult(ToolOutcome<ProbeNumberOutput>.Completed(new ProbeNumberOutput(Value)));
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}

/// <summary>测试用越界分支工具：声明 1 个分支却报出序号 1，验证宿主不会静默跳过。</summary>
public sealed class UndeclaredBranchTool:IToolDefinition<ProbeSettings,ProbeNumberOutput>,IToolSettingsFields,IToolCatalogProvider
{
    private static readonly ToolSchema Declaration=new(Array.Empty<Port>(),
        new[]{new Port(ConstantNumberTool.ValuePort,DataType.Number,Storage:NumericStorage.Float64)},
        controlOutputs:new[]{new ControlOutput("branch.0","条件1")});
    public string ToolId=>"UndeclaredBranch";
    public ToolCatalogDescriptor Catalog{get;}=new("越界分支","测试用非法分支序号","generic","逻辑工具",91);
    public ToolSchema SettingsSchema{get;}=new(Array.Empty<Port>(),Array.Empty<Port>());
    public ToolSchema Describe()=>Declaration;
    public InputValues ReadSettings(object settings)=>new(SettingsSchema.Inputs,new Dictionary<string,object?>());
    public object MaterializeSettings(InputValues values)=>new ProbeSettings();
    public IReadOnlyList<Diagnostic> Validate(ProbeSettings settings)=>Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities()=>ToolCapabilities.None;
    public IToolSession<ProbeSettings,ProbeNumberOutput> CreateSession()=>new Session();
    private sealed class Session:IToolSession<ProbeSettings,ProbeNumberOutput>
    {
        public ValueTask<ToolOutcome<ProbeNumberOutput>> ExecuteAsync(ToolInvocation<ProbeSettings> invocation)
            =>ValueTask.FromResult(new ToolOutcome<ProbeNumberOutput>(true,new ProbeNumberOutput(ConstantNumberTool.Value),
                BusinessJudgment.NotEvaluated,Array.Empty<Diagnostic>()){SelectedBranches=new[]{1}});
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
