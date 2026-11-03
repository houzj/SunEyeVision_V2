using SunEyeVision.Plugin.Runtime;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.UI.Services.Monitoring;

/// <summary>Adapts one admitted SDK snapshot once. All result surfaces share its publication and borrow its owner.</summary>
internal static class ToolResultViews
{
    private sealed class SnapshotIdentity {internal Guid Id {get;}=Guid.NewGuid();}
    private static readonly ConditionalWeakTable<DeclaredOutputSnapshot,SnapshotIdentity> Identities=new();

    public static IReadOnlyDictionary<string,string> DisplayNames(ToolSchema schema,IReadOnlyDictionary<string,string> fallback,
        ToolSchema? declaredSchema,ResultProjectionDescriptor? projection)
    {
        var names=new Dictionary<string,string>(fallback,StringComparer.Ordinal);
        if(declaredSchema is null||projection is null||
            new ToolSchema(Array.Empty<Port>(),declaredSchema.Outputs).Fingerprint!=new ToolSchema(Array.Empty<Port>(),schema.Outputs).Fingerprint||
            projection.Validate(schema).Count!=0)return names;
        foreach(var group in projection.Columns.Where(column=>column.Value is not null&&column.Value.MemberFieldPath.Count==0&&!string.IsNullOrWhiteSpace(column.DisplayName))
            .GroupBy(column=>column.Value!.PortId,StringComparer.Ordinal))
        {
            var labels=group.Select(column=>column.DisplayName).Distinct(StringComparer.Ordinal).ToArray();
            if(labels.Length==1&&names.ContainsKey(group.Key))names[group.Key]=labels[0];
        }
        return names;
    }

    public static IResultPublication FromSnapshot(DeclaredOutputSnapshot snapshot,Func<bool> isCurrent,
        ToolSchema? declaredSchema=null,ResultProjectionDescriptor? projection=null)
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
                DisplayNames(snapshot.Schema,snapshot.Fields.ToDictionary(field=>field.Key,field=>field.Value.DisplayName,StringComparer.Ordinal),declaredSchema,projection),
                snapshot.Fields.Where(field=>field.Value.DisplayInResultPanel).Select(field=>field.Key).ToHashSet(StringComparer.Ordinal));
        }
        catch{reader.Dispose();throw;}
    }

    public static IResultPublication Declaration(ToolSchema schema,Func<bool> isCurrent,
        IReadOnlyDictionary<string,string>? displayNames=null,IReadOnlySet<string>? displayedPorts=null)
    {
        var outputs=new ToolSchema(Array.Empty<Port>(),schema.Outputs);
        return new ResultPublication(outputs,new(Guid.NewGuid(),null,null,outputs.Fingerprint),ResultViewState.Declaration,
            null,null,isCurrent,displayNames:displayNames,displayedPorts:displayedPorts);
    }
}
