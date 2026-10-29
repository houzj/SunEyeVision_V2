using SunEyeVision.Plugin.Runtime;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Monitoring;

/// <summary>Adapts one admitted SDK snapshot once. All result surfaces share its publication and borrow its owner.</summary>
internal static class ToolResultViews
{
    private sealed class SnapshotIdentity {internal Guid Id {get;}=Guid.NewGuid();}
    private static readonly ConditionalWeakTable<DeclaredOutputSnapshot,SnapshotIdentity> Identities=new();

    public static IResultPublication FromSnapshot(DeclaredOutputSnapshot snapshot,Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(isCurrent);
        if(!snapshot.IsComplete)throw new InvalidOperationException("结果尚未提供完整的正式输出声明，不能展示未准入值。");
        var reader=snapshot.AcquireReader();
        try
        {
            var state=snapshot.Status switch
            {
                ExecutionStatus.NotExecuted=>ResultViewState.NotExecuted,ExecutionStatus.Running=>ResultViewState.Running,
                ExecutionStatus.Success=>ResultViewState.Success,ExecutionStatus.PartialSuccess=>ResultViewState.PartialSuccess,
                ExecutionStatus.Failed=>ResultViewState.Failed,ExecutionStatus.Timeout=>ResultViewState.Timeout,
                ExecutionStatus.Cancelled=>ResultViewState.Cancelled,_=>throw new ArgumentOutOfRangeException(nameof(snapshot))
            };
            return new ResultPublication(snapshot.Schema,
                new(Identities.GetValue(snapshot,_=>new()).Id,snapshot.Identity,snapshot.SchemaRevision,snapshot.Schema.Fingerprint),
                state,reader.Values,reader,isCurrent,snapshot.Diagnostics,
                snapshot.Fields.ToDictionary(field=>field.Key,field=>field.Value.DisplayName,StringComparer.Ordinal),
                snapshot.Fields.Where(field=>field.Value.DisplayInResultPanel).Select(field=>field.Key).ToHashSet(StringComparer.Ordinal));
        }
        catch{reader.Dispose();throw;}
    }

    public static IResultPublication Declaration(ToolSchema schema,Func<bool> isCurrent,
        IReadOnlyDictionary<string,string>? displayNames=null,IReadOnlySet<string>? displayedPorts=null)
        =>new ResultPublication(schema,new(Guid.NewGuid(),null,null,schema.Fingerprint),ResultViewState.Declaration,
            null,null,isCurrent,displayNames:displayNames,displayedPorts:displayedPorts);
}
