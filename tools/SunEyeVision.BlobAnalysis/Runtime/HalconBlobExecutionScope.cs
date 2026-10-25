using System;
using HalconDotNet;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>算法同步执行期间固定8邻域，并在原线程恢复调用者设置；不改HALCON全局上限。</summary>
public sealed class HalconBlobExecutionScope : IDisposable
{
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly HTuple _previousNeighborhood;
    private bool _disposed;

    public HalconBlobExecutionScope()
    {
        RequireUnlimitedConnection();
        HOperatorSet.GetSystem("tsp_neighborhood", out _previousNeighborhood);
        try { HOperatorSet.SetSystem("tsp_neighborhood", 8); }
        catch { _previousNeighborhood.Dispose(); throw; }
    }

    public static void RequireUnlimitedConnection()
    {
        HOperatorSet.GetSystem("max_connection", out var limit);
        using (limit)
            if (limit.I != 0)
                throw new InvalidOperationException("BLOB_HALCON_MAX_CONNECTION：HALCON连通区域返回上限必须为0（不截断）。Blob数量限制在特征筛选和排序之后执行，不能提前丢失候选区域。");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("Blob的HALCON执行作用域必须在原同步线程退出。");
        try { HOperatorSet.SetSystem("tsp_neighborhood", _previousNeighborhood); }
        finally { _previousNeighborhood.Dispose(); _disposed = true; }
    }
}
