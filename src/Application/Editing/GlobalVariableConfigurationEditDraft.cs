using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application.Editing;

/// <summary>Private variable definitions; complex runtime values are borrowed read-only.</summary>
public sealed class GlobalVariableConfigurationEditDraft : IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly DocumentSession _session;
    private readonly VariableDraftCapture _opening;
    private readonly Dictionary<string, object?> _explicitValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _valueRevisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GlobalVariable> _valueOwners = new(StringComparer.Ordinal);
    private readonly HashSet<GlobalVariable> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<GlobalVariable, GlobalVariable?> _origins = new(ReferenceEqualityComparer.Instance);
    private bool _consumed;
    private long _revision;
    private readonly string _initialIdentity;

    internal GlobalVariableConfigurationEditDraft(DocumentSession session, IEnumerable<GlobalVariable>? candidate = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _opening = session.CaptureVariablesForEdit();
        Variables = new ObservableCollection<GlobalVariable>(GlobalVariable.CaptureDefinitions((candidate ??
            _opening.Entries.Select(entry => entry.Opening)).ToArray()));
        if (candidate is null)
            for (var i = 0; i < Variables.Count; i++) _origins.Add(Variables[i], _opening.Entries[i].Actual);
        Variables.CollectionChanged += CandidatesChanged;
        WatchCandidates();
        _initialIdentity = CandidateIdentity(_opening.Entries.Select(entry => entry.Opening));
    }

    public Guid EditSessionId => _session.EditSessionId;
    internal DocumentSession Session => _session;
    internal bool HasPendingChanges => _explicitValues.Any(pair => _valueOwners.TryGetValue(pair.Key, out var owner) && Variables.Any(item => ReferenceEquals(item, owner) && item.Id == pair.Key)) ||
        CandidateIdentity(Variables) != _initialIdentity;
    /// <summary>Only candidate copies belong here; adding/removing them does not change the document.</summary>
    public ObservableCollection<GlobalVariable> Variables { get; }

    public void SetValue(string variableId, object? value)
    {
        EnsureCurrent();
        var variable = Variables.SingleOrDefault(item => item.Id == variableId)
            ?? throw new InvalidOperationException("变量不属于当前草稿。");
        SunEyeVision.Plugin.Contracts.VariableValueDeclarations.ValidateEditableValue(variable.Type, value);
        var frozen = GlobalVariable.CopyEditableValue(value);
        _explicitValues[variableId] = frozen;
        _valueRevisions[variableId] = checked(_valueRevisions.GetValueOrDefault(variableId) + 1);
        _valueOwners[variableId] = variable;
        variable.Value = GlobalVariable.CopyEditableValue(frozen);
        _revision = checked(_revision + 1);
    }

    internal GlobalVariableConfigurationEditProposal Freeze()
    {
        EnsureCurrent();
        var values = Variables.ToArray();
        if (values.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id)) ||
            values.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException("变量草稿身份缺失或重复。");
        bool IsCurrentWrite(string id) => _valueOwners.TryGetValue(id, out var owner) && values.Any(item => ReferenceEquals(item, owner) && item.Id == id);
        foreach (var (id, value) in _explicitValues.Where(item => IsCurrentWrite(item.Key)))
            SunEyeVision.Plugin.Contracts.VariableValueDeclarations.ValidateEditableValue(values.Single(item => item.Id == id).Type, value);
        var copies = GlobalVariable.CaptureDefinitions(values);
        var writes = new ReadOnlyDictionary<string, object?>(_explicitValues.Where(pair => IsCurrentWrite(pair.Key)).ToDictionary(
            pair => pair.Key, pair => GlobalVariable.CopyEditableValue(pair.Value), StringComparer.Ordinal));
        var origins = values.Select(variable => new VariableCandidateOrigin(variable.Id, variable, _origins.GetValueOrDefault(variable))).ToArray();
        return new(_session, _opening, copies, Array.AsReadOnly(origins), writes,
            new ReadOnlyDictionary<string, long>(_valueRevisions.Where(pair => IsCurrentWrite(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
            _revision, CandidateIdentity(Variables));
    }

    internal bool Matches(GlobalVariableConfigurationEditProposal frozen) => ReferenceEquals(frozen.Session, _session) &&
        frozen.DraftRevision == _revision && frozen.DraftIdentity == CandidateIdentity(Variables);

    // Nested producer metadata, extension dictionaries and editable arrays can change in
    // place without PropertyChanged. Do not serialize or take ownership of native values.
    private static string CandidateIdentity(IEnumerable<GlobalVariable> variables) => System.Text.Json.JsonSerializer.Serialize(variables.Select(variable => new
    {
        variable.Id, variable.Name, variable.Group, variable.Index, variable.Type, variable.Description,
        variable.ProducerBinding, variable.RebindingReason, variable.BindingValidationPending, variable.AdditionalData,
        EditableValue = GlobalVariable.IsPureValueDeclaration(variable.Type) ? variable.Value : null
    }).ToArray());

    internal GlobalVariableConfigurationEditDraft Rebase(GlobalVariableConfigurationEditProposal frozen,
        IReadOnlyDictionary<GlobalVariable, GlobalVariable> acceptedInstances)
    {
        EnsureCurrent();
        if (!ReferenceEquals(frozen.Session, _session)) throw new InvalidOperationException("变量回执属于另一个方案。");
        var next = new GlobalVariableConfigurationEditDraft(_session, Matches(frozen) ? null : Variables);
        try
        {
        if (!Matches(frozen))
        {
            for (var index = 0; index < next.Variables.Count; index++)
            {
                var variable = next.Variables[index];
                var source = Variables[index];
                var origin = _origins.GetValueOrDefault(source);
                if (origin is null && frozen.Origins.Any(item => ReferenceEquals(item.DraftInstance, source) && item.Actual is null && item.CandidateId == variable.Id))
                    origin = acceptedInstances.SingleOrDefault(item => ReferenceEquals(item.Key, source)).Value
                        ?? throw new InvalidOperationException("已接受的新变量缺少实例回执。");
                if (origin is not null && origin.Id != variable.Id)
                    throw new InvalidOperationException("变量回执身份与草稿不一致。");
                if (origin is not null && next._opening.Entries.Any(entry => ReferenceEquals(entry.Actual, origin)))
                    next._origins[variable] = origin;
                else if (origin is not null)
                    throw new InvalidOperationException("变量回执实例不属于当前方案会话。");
                var revision = _valueRevisions.GetValueOrDefault(variable.Id);
                var newer = ReferenceEquals(_valueOwners.GetValueOrDefault(variable.Id), source) && _valueRevisions.ContainsKey(variable.Id) &&
                    revision != frozen.ValueRevisions.GetValueOrDefault(variable.Id);
                var priorIndex = -1;
                for (var i = 0; i < frozen.Origins.Count; i++)
                    if (ReferenceEquals(frozen.Origins[i].DraftInstance, source)) { priorIndex = i; break; }
                var prior = priorIndex < 0 ? null : frozen.Candidates[priorIndex];
                var changedInPlace = prior is not null && GlobalVariable.IsPureValueDeclaration(variable.Type) &&
                    System.Text.Json.JsonSerializer.Serialize(new { variable.Type, variable.Value }) !=
                    System.Text.Json.JsonSerializer.Serialize(new { prior.Type, prior.Value });
                if (newer && _explicitValues.TryGetValue(variable.Id, out var value))
                {
                    next._explicitValues[variable.Id] = GlobalVariable.CopyEditableValue(value);
                    next._valueRevisions[variable.Id] = revision;
                    next._valueOwners[variable.Id] = variable;
                }
                else if (changedInPlace)
                {
                    next._explicitValues[variable.Id] = GlobalVariable.CopyEditableValue(variable.Value);
                    next._valueRevisions[variable.Id] = checked(frozen.ValueRevisions.GetValueOrDefault(variable.Id) + 1);
                    next._valueOwners[variable.Id] = variable;
                }
                else if (next._opening.Entries.SingleOrDefault(entry => entry.Opening.Id == variable.Id)?.Opening is { } live &&
                    live.Type == variable.Type && GlobalVariable.IsPureValueDeclaration(variable.Type))
                    variable.Value = GlobalVariable.CopyEditableValue(live.Value);
            }
        }
        MarkConsumed();
        return next;
        }
        catch
        {
            next.Dispose();
            throw;
        }
    }

    private void WatchCandidates()
    {
        foreach (var old in _observed.Where(item => !Variables.Any(current => ReferenceEquals(current, item))).ToArray())
        { old.PropertyChanged -= CandidateChanged; _observed.Remove(old); }
        foreach (var item in Variables)
            if (_observed.Add(item)) item.PropertyChanged += CandidateChanged;
    }
    private void CandidateChanged(object? sender, PropertyChangedEventArgs e)
    { EnsureOwner(); _revision = checked(_revision + 1); }
    private void CandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    { EnsureOwner(); WatchCandidates(); _revision = checked(_revision + 1); }

    internal void MarkConsumed() => Dispose();
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("变量草稿只能在方案所属线程释放。");
        if (_consumed) return;
        _consumed = true;
        Variables.CollectionChanged -= CandidatesChanged;
        foreach (var item in _observed) item.PropertyChanged -= CandidateChanged;
        _observed.Clear();
    }
    internal void EnsureCanConsume() => EnsureOwner();
    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("变量草稿只能在方案所属线程使用。");
        if (_consumed) throw new InvalidOperationException("变量草稿已接受。");
    }
    private void EnsureCurrent() { EnsureOwner(); _ = _session.GetWorkflows(); }

}

internal sealed record VariableCandidateOrigin(string CandidateId, GlobalVariable DraftInstance, GlobalVariable? Actual);
internal sealed record GlobalVariableConfigurationEditProposal(DocumentSession Session,
    VariableDraftCapture Opening, IReadOnlyList<GlobalVariable> Candidates, IReadOnlyList<VariableCandidateOrigin> Origins,
    IReadOnlyDictionary<string, object?> ExplicitValues, IReadOnlyDictionary<string, long> ValueRevisions,
    long DraftRevision, string DraftIdentity);
