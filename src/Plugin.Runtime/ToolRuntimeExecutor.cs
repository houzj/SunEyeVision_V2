using System.Diagnostics;
using System.Collections.Concurrent;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>One admitted tool call. The host owns scheduling, input resource lifetime
/// and publication. The executor owns only the session, including asynchronous cleanup.</summary>
public static class ToolRuntimeExecutor
{
    private static readonly ConcurrentDictionary<string, byte> SeenPluginAssemblies = new(StringComparer.Ordinal);

    public static async ValueTask<ToolOutcome> ExecuteAsync(IToolDefinition definition,object settings,
        InputValues inputs,IToolExecutionContext context,IEnumerable<OutputValueRequest>? outputRequests=null,
        RuntimeDirectorySnapshot? directory=null)
    {
        ArgumentNullException.ThrowIfNull(definition);ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(inputs);
        using var timing=RuntimeExecutionTimingProbe.Begin(definition.ToolId, context.Identity.NodeId.ToString());
        timing.Mark("executor-enter");
        var assembly = definition.GetType().Assembly;
        var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "unknown";
        timing.Mark("plugin-assembly-resolved:" + assemblyName);
        if (SeenPluginAssemblies.TryAdd(assembly.FullName ?? assemblyName, 0))
            timing.Mark("plugin-first-use");
        var frozen=definition.FreezeSettings(settings);
        var errors=definition.Validate(frozen);
        if(errors.Count!=0)return new(false,null,BusinessJudgment.NotEvaluated,Array.AsReadOnly(errors.ToArray()));
        var schema=definition.Describe();
        var invocation=new ToolInvocation(frozen,new InputValues(schema.Inputs,inputs.CopyValues()),context,outputRequests);
        foreach(var request in invocation.OutputRequests)request.Validate(schema);
        if(context.Cancellation.IsCancellationRequested)
            return new(false,null,BusinessJudgment.NotEvaluated,new[]{new Diagnostic("TOOL_CANCELLED","工具执行已取消。")});
        if (definition is IToolExecutionFilter filter && !filter.ShouldExecute(frozen))
            return ToolOutcome.Suppress();
        timing.Mark("invoke-start");
        var sessionClock=Stopwatch.StartNew();
        var sessionLease=directory?.AcquireSession(definition);
        var session=sessionLease?.Session ?? definition.CreateSession();
        sessionClock.Stop();
        timing.Mark("session-create:"+sessionClock.ElapsedMilliseconds+"ms");
        Exception? failure=null;
        ToolOutcome? outcome=null;
        try
        {
            outcome=await session.ExecuteAsync(invocation).ConfigureAwait(false);
            timing.Mark("session-execute");
            return outcome;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                if (sessionLease is not null) await sessionLease.DisposeAsync().ConfigureAwait(false);
                else await session.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception cleanup)when(failure is not null){throw new AggregateException("Tool execution and session cleanup both failed.",failure,cleanup);}
            catch(Exception cleanup)
            {
                // The return value is suppressed by the cleanup exception. The caller still
                // owns context.Resources, including the direct-alias case.
                if(outcome?.Output is IDisposable output&&!ReferenceEquals(output,context.Resources))
                {
                    try{output.Dispose();}
                    catch(Exception release){throw new AggregateException("Session and untransferred output cleanup both failed.",cleanup,release);}
                }
                throw;
            }
        }
    }
}
