namespace SunEyeVision.UI.Services;

public sealed record LocalImageSequenceEntry(string EntryId, string ResourceReference, string Address, int OriginalIndex);
public sealed record LocalImageSourcePlan(string SourceNodeId, IReadOnlyList<LocalImageSequenceEntry> Entries,
    string? SelectedEntryId, bool AutoAdvance, bool StopAtLast);
public sealed record LocalImageRunInput(string WorkflowId, long RunGeneration, string SourceNodeId,
    long IterationVersion, int Position, LocalImageSequenceEntry? Entry);
public enum LocalImageCompletion { Stale, Retained, Advanced, StopAtEnd }

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
        public required bool AutoAdvance { get; init; }
        public required bool StopAtLast { get; init; }
        public int Position { get; set; }
    }
    private sealed record Session(long Generation, Source[] Sources)
    {
        public long IterationVersion { get; set; }
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
                Position = entries.Length == 0 ? -1 : Math.Max(selected, 0),
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

    public LocalImageCompletion Complete(string workflowId, long generation,
        IReadOnlyList<LocalImageRunInput> consumed, bool success, bool advance = true)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(workflowId, out var session) || session.Generation != generation
                || consumed.Count != session.Sources.Length)
                return LocalImageCompletion.Stale;
            var byNode = new Dictionary<string, LocalImageRunInput>(StringComparer.Ordinal);
            foreach (var input in consumed)
                if (!byNode.TryAdd(input.SourceNodeId, input)) return LocalImageCompletion.Stale;
            foreach (var source in session.Sources)
            {
                if (!byNode.TryGetValue(source.NodeId, out var input) || input.WorkflowId != workflowId
                    || input.RunGeneration != generation || input.Position != source.Position
                    || input.IterationVersion != session.IterationVersion
                    || input.Entry != (source.Position < 0 ? null : source.Entries[source.Position]))
                    return LocalImageCompletion.Stale;
            }
            session.IterationVersion++;
            if (!success || !advance) return LocalImageCompletion.Retained;
            if (session.Sources.Any(source => source.AutoAdvance && source.StopAtLast
                && source.Position >= 0 && source.Position == source.Entries.Length - 1))
                return LocalImageCompletion.StopAtEnd;
            var advanced = false;
            foreach (var source in session.Sources)
            {
                if (!source.AutoAdvance || source.Entries.Length < 2) continue;
                source.Position = (source.Position + 1) % source.Entries.Length;
                advanced = true;
            }
            return advanced ? LocalImageCompletion.Advanced : LocalImageCompletion.Retained;
        }
    }

    public void End(string workflowId, long generation)
    {
        lock (_gate)
            if (_sessions.TryGetValue(workflowId, out var session) && session.Generation == generation)
                _sessions.Remove(workflowId);
    }
}
