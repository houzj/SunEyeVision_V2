using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>原厂三输出（ModuStatus/ModuRunTime/BranchMatch）的中立投影；分支走向另由
/// <see cref="ToolOutcome{T}.SelectedBranches"/> 报给宿主，不作为数据端口。</summary>
public sealed class IfOutput:IToolOutput
{
    public const string ModuleStatus="output.module-status",ModuleRunTime="output.module-time-ms",
        BranchMatch="output.branch-match";
    public static IReadOnlyList<Port> Ports{get;}=Array.AsReadOnly(new[]
    {
        new Port(ModuleStatus,DataType.Integer,Storage:NumericStorage.Int32),
        new Port(ModuleRunTime,DataType.Number,Storage:NumericStorage.Float32),
        new Port(BranchMatch,TypeRef.ScalarOf(ScalarKind.Text))
    });
    public static IReadOnlyList<OutputDisplayNode> Display{get;}=OutputDisplayNode.Freeze(new[]
    {
        new OutputDisplayNode(BranchMatch,"命中分支"),
        new OutputDisplayNode(ModuleStatus,"模块状态",isModuleExecutionSummary:true),
        new OutputDisplayNode(ModuleRunTime,"模块耗时 (ms)",isModuleExecutionSummary:true)
    });
    private readonly IReadOnlyDictionary<string,object?> _values;
    internal IfOutput(IReadOnlyDictionary<string,object?> values)=>_values=new InputValues(Ports,values).CopyValues();
    public IReadOnlyDictionary<string,object?> ToValues()=>_values;
}
