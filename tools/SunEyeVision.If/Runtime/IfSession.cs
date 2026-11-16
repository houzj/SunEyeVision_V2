using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>逐槽判定取值是否落在闭区间，命中槽位序号即分支序号。工具不持有节点身份或图结构。</summary>
internal sealed class IfSession:IToolSession<IfSettings,IfOutput>
{
    private int _disposed;

    public ValueTask<ToolOutcome<IfOutput>> ExecuteAsync(ToolInvocation<IfSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        ArgumentNullException.ThrowIfNull(invocation);
        var cancellation=invocation.Context.Cancellation;
        cancellation.ThrowIfCancellationRequested();
        var settings=invocation.Settings??throw new ArgumentException("条件分支设置不能为空。");
        var errors=settings.Validate();
        if(errors.Count!=0)return ValueTask.FromResult(new ToolOutcome<IfOutput>(false,null,BusinessJudgment.NotEvaluated,errors));
        var started=Stopwatch.GetTimestamp();
        var matched=new List<int>(IfFields.ConditionCount);
        var enabled=0;
        for(var index=0;index<IfFields.ConditionCount;index++)
        {
            var condition=settings.Conditions[index];
            if(!condition.Enabled||condition.Source is not {} value)continue;
            enabled++;
            // 含端点：原厂判定函数体未取证，本方明确取闭区间（见 B 线交接单 1.4）。
            if(value<condition.Minimum||value>condition.Maximum)continue;
            matched.Add(index);
        }
        cancellation.ThrowIfCancellationRequested();
        var match=string.Join(";",matched.Select(IfFields.ConditionName));
        var elapsed=(double)(float)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var output=new IfOutput(new Dictionary<string,object?>(StringComparer.Ordinal)
        {
            [IfOutput.ModuleStatus]=1L,
            [IfOutput.ModuleRunTime]=elapsed,
            [IfOutput.BranchMatch]=match
        });
        invocation.Context.Log($"条件分支：启用 {enabled} 条，命中 [{match}]，耗时 {elapsed}ms");
        return ValueTask.FromResult(new ToolOutcome<IfOutput>(true,output,BusinessJudgment.NotEvaluated,Array.Empty<Diagnostic>())
        {
            SelectedBranches=Array.AsReadOnly(matched.ToArray())
        });
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed,1);
        return ValueTask.CompletedTask;
    }
}
