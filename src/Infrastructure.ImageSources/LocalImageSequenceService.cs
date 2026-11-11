using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SunEyeVision.Infrastructure.ImageSources;

public sealed record LocalImageSequenceEntry(string EntryId, string ResourceReference, string Address, int OriginalIndex);
public sealed record LocalImageSourcePlan(string SourceNodeId, IReadOnlyList<LocalImageSequenceEntry> Entries,
    string? SelectedEntryId, bool AutoAdvance, bool StopAtLast);
public sealed record LocalImageRunInput(string WorkflowId, long RunGeneration, string SourceNodeId,
    long IterationVersion, int Position, LocalImageSequenceEntry? Entry);
public enum LocalImageCompletion { Stale, Retained, Advanced, StopAtEnd }
public sealed record LocalImageReadReceipt(LocalImageRunInput Ticket, Guid RequestId,
    LocalImageSequenceEntry Entry, int Position, bool StopAfterCurrent);
public sealed record LocalImageReadRound(IReadOnlyList<LocalImageReadReceipt> Accepted, bool StopAfterCurrent);

/// <summary>
/// Owns frozen per-source sequences for an execution session. It never reads a view,
/// resolves a resource, or schedules execution; the workflow runner controls completion.
/// </summary>
public sealed class LocalImageSequenceService
{
    private sealed class Source
    {
        public required string NodeId { get; init; }
        public required LocalImageSequenceEntry[] Entries { get; init; }
        public bool HasCursor;
        public required bool AutoAdvance { get; init; }
        public required bool StopAtLast { get; init; }
        public int Position { get; set; }
    }
    private sealed record Session(long Generation, Source[] Sources)
    {
        public long IterationVersion { get; set; }
        public Dictionary<string, LocalImageReadReceipt> Accepted { get; } = new(StringComparer.Ordinal);
    }
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private long _generation;

    public long Begin(string workflowId, IEnumerable<LocalImageSourcePlan> plans)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        var sources = plans.Select(plan =>
        {
            var entries = plan.Entries.ToArray();
            if (entries.Select(entry => entry.EntryId).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new ArgumentException("图像运行序列中存在重复条目身份。", nameof(plans));
            var selected = Array.FindIndex(entries, entry => entry.EntryId == plan.SelectedEntryId);
            if (plan.SelectedEntryId is not null && selected < 0)
                throw new InvalidOperationException($"图像来源 {plan.SourceNodeId} 的已选图片不在本次有效运行序列中，请重新选择。");
            return new Source
            {
                NodeId = plan.SourceNodeId, Entries = entries,
                Position = entries.Length == 0 ? -1 : Math.Max(selected, 0), HasCursor=selected>=0,
                AutoAdvance = plan.AutoAdvance, StopAtLast = plan.StopAtLast
            };
        }).ToArray();
        if (sources.Select(source => source.NodeId).Distinct(StringComparer.Ordinal).Count() != sources.Length)
            throw new ArgumentException("图像运行序列中存在重复来源节点。", nameof(plans));
        lock (_gate)
        {
            var generation = ++_generation;
            _sessions[workflowId] = new Session(generation, sources);
            return generation;
        }
    }

    public IReadOnlyList<LocalImageRunInput> GetInputs(string workflowId, long generation)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(workflowId, out var session) || session.Generation != generation)
                return Array.Empty<LocalImageRunInput>();
            return Array.AsReadOnly(session.Sources.Select(source => new LocalImageRunInput(
                workflowId, generation, source.NodeId, session.IterationVersion, source.Position,
                source.Position < 0 ? null : source.Entries[source.Position])).ToArray());
        }
    }

    internal IReadOnlyDictionary<string, LocalImageRunInput> GetSelectedInputs(string workflowId, long generation)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(workflowId, out var session) || session.Generation != generation)
                throw new InvalidOperationException("图像运行序列已失效。");
            return session.Sources.Where(source => source.HasCursor && source.Position >= 0)
                .ToDictionary(source => source.NodeId, source => new LocalImageRunInput(workflowId, generation,
                    source.NodeId, session.IterationVersion, source.Position, source.Entries[source.Position]));
        }
    }

    /// <summary>Accept one frozen source at its actual read boundary. The caller must
    /// validate the execution owner and request before entering this state machine.</summary>
    public LocalImageReadReceipt AcceptRead(LocalImageRunInput ticket, Guid requestId,
        bool advance, CancellationToken cancellation, int navigationDelta = 0, bool holdCurrent = false)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("图像读取缺少请求身份。", nameof(requestId));
        if(navigationDelta is < -1 or > 1)throw new ArgumentOutOfRangeException(nameof(navigationDelta));
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!_sessions.TryGetValue(ticket.WorkflowId, out var session) || session.Generation != ticket.RunGeneration ||
                session.IterationVersion != ticket.IterationVersion)
                throw new InvalidOperationException("图像读取所属运行序列已失效。");
            if (session.Accepted.TryGetValue(ticket.SourceNodeId, out var prior))
            {
                if (prior.RequestId != requestId || prior.Ticket != ticket)
                    throw new InvalidOperationException("图像来源本轮已由另一请求接纳。");
                return prior;
            }
            var source = session.Sources.SingleOrDefault(item => item.NodeId == ticket.SourceNodeId)
                ?? throw new InvalidOperationException("图像读取来源不属于当前序列。");
            if (ticket.Position != source.Position || ticket.Entry != (source.Position < 0 ? null : source.Entries[source.Position]))
                throw new InvalidOperationException("图像读取位置已发生变化。");
            if (source.Position < 0 || source.Entries.Length == 0)
                throw new InvalidOperationException("当前来源没有可运行的图像。");
            var position = !source.HasCursor ? (navigationDelta<0?source.Entries.Length-1:0) :
                navigationDelta != 0 ? (source.Position + navigationDelta + source.Entries.Length) % source.Entries.Length :
                advance && source.AutoAdvance && !holdCurrent ? (source.Position + 1) % source.Entries.Length : source.Position;
            var entry = source.Entries[position];
            var receipt = new LocalImageReadReceipt(ticket, requestId, entry, position,
                navigationDelta == 0 && advance && source.AutoAdvance && source.StopAtLast && position == source.Entries.Length - 1);
            // This assignment is the read-admission linearization point. No I/O or callbacks run under this lock.
            cancellation.ThrowIfCancellationRequested();
            session.Accepted.Add(source.NodeId, receipt);
            source.Position = position;source.HasCursor=true;
            return receipt;
        }
    }
    /// <summary>Close one drained round and return only sources that actually accepted a read.
    /// Completion, downstream failure and cancellation do not undo those receipts.</summary>
    public LocalImageReadRound FinishReadRound(string workflowId, long generation, long iterationVersion)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(workflowId, out var session) || session.Generation != generation ||
                session.IterationVersion != iterationVersion)
                throw new InvalidOperationException("图像读取轮次已失效或已经结算。");
            var nextVersion = checked(session.IterationVersion + 1);
            var receipts = session.Sources.Where(source => session.Accepted.ContainsKey(source.NodeId))
                .Select(source => session.Accepted[source.NodeId]).ToArray();
            var result = new LocalImageReadRound(Array.AsReadOnly(receipts), receipts.Any(item => item.StopAfterCurrent));
            session.Accepted.Clear();
            session.IterationVersion = nextVersion;
            return result;
        }
    }
    public void End(string workflowId, long generation)
    {
        lock (_gate)
            if (_sessions.TryGetValue(workflowId, out var session) && session.Generation == generation)
                _sessions.Remove(workflowId);
    }
}
