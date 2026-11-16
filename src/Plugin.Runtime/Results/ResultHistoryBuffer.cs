using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

public sealed record ResultHistoryRow(long RowIndex, string AddTime, string Content);

/// <summary>Bounded text records owned by one node or private debug scope.</summary>
public sealed class ResultHistoryBuffer
{
    private const int Capacity = 500;
    private readonly List<ResultHistoryRow> _rows = new();
    private long _index;

    internal static IReadOnlyList<string> Freeze(DeclaredOutputSnapshot result)
    {
        if (result.Status == ExecutionStatus.Failed)
            return Array.AsReadOnly(new[] { $"Status:0 {result.ErrorMessage}" });
        if (!result.IsSuccess || result.Output is not IToolResultRecordOutput output)
            return Array.Empty<string>();
        try
        {
            return Array.AsReadOnly(output.DescribeResultRecords()
                .Where(text => !string.IsNullOrEmpty(text)).Take(Capacity).ToArray());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Array.AsReadOnly(new[] { $"历史记录生成失败：{error.Message}" });
        }
    }

    public void Append(IReadOnlyList<string> records, DateTimeOffset completed)
    {
        if (records.Count == 0) return;
        var index = ++_index;
        var timestamp = completed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss:fff", System.Globalization.CultureInfo.InvariantCulture);
        _rows.InsertRange(0, records.Select(text => new ResultHistoryRow(index, timestamp, text)));
        if (_rows.Count > Capacity) _rows.RemoveRange(Capacity, _rows.Count - Capacity);
    }

    public IReadOnlyList<ResultHistoryRow> Read() => _rows.ToArray();

    public void Clear() => _rows.Clear();
}
