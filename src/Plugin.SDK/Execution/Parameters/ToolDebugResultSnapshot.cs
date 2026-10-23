using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// Captures the upstream result objects visible to one debug execution and rejects a torn
/// snapshot if any source is replaced while the capture is in progress.
/// </summary>
public static class ToolDebugResultSnapshot
{
    public static ToolResultSnapshot Capture(
        IDataSourceQueryService query,
        IEnumerable<string?> sourceNodeIds,
        bool requireAll = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sourceNodeIds);

        var ids = sourceNodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != "__GlobalVars__")
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var results = new Dictionary<string, ToolResults>(StringComparer.Ordinal);
        var observed = new Dictionary<string, ToolResults?>(StringComparer.Ordinal);
        var leases = new List<IDisposable>();

        try
        {
        foreach (var id in ids)
        {
            var result = query.GetNodeContext(id)?.Result;
            observed.Add(id, result);
            if (result != null && !result.IsSuccess)
            {
                if (requireAll)
                    throw new InvalidOperationException($"调试执行无法使用上游节点 {id} 的失败或未完成结果：{result.ErrorMessage}");
                continue;
            }
            if (result == null && requireAll)
                throw new InvalidOperationException($"调试执行无法建立本轮数据快照：上游节点 {id} 尚无结果，请先执行上游节点或工作流。");
            if (result != null)
            {
                if (result.Resources != null) leases.Add(result.Resources.Retain());
                results.Add(id, result);
            }
        }

        foreach (var pair in observed)
        {
            if (!ReferenceEquals(pair.Value, query.GetNodeContext(pair.Key)?.Result))
                throw new InvalidOperationException($"调试执行期间上游节点 {pair.Key} 的结果已更新，未执行以避免混用不同轮次数据；请重新点击执行。");
        }

        return new ToolResultSnapshot(results, leases);
        }
        catch
        {
            foreach (var lease in leases) lease.Dispose();
            throw;
        }
    }
}
