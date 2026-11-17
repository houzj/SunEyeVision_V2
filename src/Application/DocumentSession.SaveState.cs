using System.Text;
using System.Text.Json;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private sealed record PersistenceStateStamp(Guid HistoryState, string Navigation, long ExplicitValueEdit);
    private PersistenceStateStamp? _writtenState;
    private long _explicitValueEdit;

    /// <summary>Supported document edits and persistent navigation relative to the successful save.
    /// Runtime result publication is not a user edit. Pending registered editors remain unsaved.</summary>
    public bool HasUnsavedChanges
    {
        get
        {
            if (Environment.CurrentManagedThreadId != _ownerThread)
                throw new InvalidOperationException("保存状态只能由方案所属线程读取。");
            lock (_gate) EnsureOpen();
            // This is a read of already installed state, also valid during change/retirement
            // notifications. Do not apply the edit admission gate to a UI title query.
            if (Editing.HasPendingChanges) return true;
            lock (_gate)
            {
                EnsureOpen();
                return _writtenState is null || CapturePersistenceState() != _writtenState;
            }
        }
    }

    /// <summary>
    /// 尚未保存过、且没有任何节点与视图导航的初始空白方案。
    /// 启动时预建的空白工作区属于这种情况：用户从引导页打开别的方案、
    /// 或直接关闭程序时，都不应弹出「是否保存」。
    /// </summary>
    public bool IsPristineBlank
    {
        get
        {
            if (Environment.CurrentManagedThreadId != _ownerThread)
                throw new InvalidOperationException("保存状态只能由方案所属线程读取。");
            lock (_gate)
            {
                EnsureOpen();
                if (_writtenState is not null) return false;
                if (Editing.HasPendingChanges) return false;
                foreach (var flow in Document.Workflows)
                {
                    if (flow.Nodes.Count != 0) return false;
                    if (flow.WorkspaceSettings.OverviewX is not null || flow.WorkspaceSettings.OverviewY is not null) return false;
                }
                return true;
            }
        }
    }

    private PersistenceStateStamp CapturePersistenceState()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var flow in Document.Workflows)
            {
                writer.WriteStartObject(); writer.WriteString("flow", flow.Id);
                if (flow.WorkspaceSettings.OverviewX is {} x) writer.WriteNumber("x", x); else writer.WriteNull("x");
                if (flow.WorkspaceSettings.OverviewY is {} y) writer.WriteNumber("y", y); else writer.WriteNull("y");
                writer.WriteStartArray("display");
                foreach (var node in flow.Nodes)
                {
                    writer.WriteStartObject(); writer.WriteString("node", node.Id); writer.WriteString("source", node.SelectedDisplayReference);
                    if(node.DisplayOverlays is {} overlays)
                    {
                        writer.WriteString("background",overlays.Background);
                        writer.WriteStartArray("slots");
                        foreach(var slot in overlays.Slots)writer.WriteStringValue(slot);
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return new(History.CurrentStateId, Encoding.UTF8.GetString(stream.ToArray()), _explicitValueEdit);
    }
}
