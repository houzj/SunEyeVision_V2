using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Models.Communication;

namespace SunEyeVision.Workflow;

/// <summary>Communication reads the same frozen definitions and produced values as workflow inputs.</summary>
public sealed class WorkflowGlobalVariableReceiveService : ICommunicationReceiveService
{
    private readonly ICommunicationReceiveService _inner;
    private readonly WorkflowGlobalRunValues _values;
    private readonly IReadOnlyDictionary<string, ReceiveVariableSource> _sources;
    private readonly IReadOnlyDictionary<string, string> _invalid;

    public WorkflowGlobalVariableReceiveService(ICommunicationReceiveService inner, WorkflowGlobalRunValues values,
        IReadOnlyList<GlobalVariable> definitions)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _values = values ?? throw new ArgumentNullException(nameof(values));
        var frozen = GlobalVariable.CaptureDefinitions(definitions.ToArray());
        _sources = frozen.ToDictionary(v => v.Id, Describe, StringComparer.Ordinal);
        _invalid = frozen.Where(v => !string.IsNullOrEmpty(v.RebindingReason) || !string.IsNullOrEmpty(v.BindingValidationPending))
            .ToDictionary(v => v.Id, v => v.RebindingReason + v.BindingValidationPending, StringComparer.Ordinal);
    }

    public static ReceiveVariableSource Describe(GlobalVariable variable)
    {
        var source = variable.ProducerBinding?.SourceReference;
        return new(variable.Id, variable.Name, variable.Group, variable.Type,
            variable.ProducerBinding != null || !string.IsNullOrEmpty(variable.RebindingReason) || !string.IsNullOrEmpty(variable.BindingValidationPending),
            source?.NodeId ?? "", source?.PortId ?? "", source?.ScopeId ?? "");
    }

    public IReadOnlyList<CommunicationReceiveSource> GetSources() => _inner.GetSources();
    public Task<CommunicationReceiveSample> ReadAsync(string deviceId, string targetId, CancellationToken token = default)
        => _inner.ReadAsync(deviceId, targetId, token);
    public Task ConsumeAsync(string deviceId, string targetId, string consumerId, Action<CommunicationReceiveSample> commit,
        CancellationToken token = default) => _inner.ConsumeAsync(deviceId, targetId, consumerId, commit, token);
    public IReadOnlyList<ReceiveVariableSource> GetVariables() => _sources.Values.ToArray();
    public IReadOnlyDictionary<string, ReceiveVariableValue> ReadVariables(IReadOnlyList<string> ids)
    {
        var state = _values.CaptureState();
        var snapshot = state.Values;
        var issues = state.Issues;
        var result = new Dictionary<string, ReceiveVariableValue>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (!_sources.TryGetValue(id, out var source)) throw new InvalidOperationException("所选变量不属于本次运行快照。");
            if (_invalid.TryGetValue(id, out var invalid)) throw new InvalidOperationException(invalid);
            if (issues.TryGetValue(id, out var issue)) throw new InvalidOperationException(issue.Message);
            if (!snapshot.TryGetValue(id, out var value) || value == null)
                throw new InvalidOperationException("变量在本次运行尚无有效值：" + source.Name);
            if (source.DataType is not ("String" or "Integer" or "Double" or "Boolean"))
                throw new NotSupportedException("通信接收目前只支持已声明的单项文本、整数、浮点及布尔变量；序列和其它存储尚未接入，不能隐式展平。");
            var normalized = CommunicationReceiveService.Normalize(value);
            if (CommunicationReceiveService.ValueType(normalized) != source.DataType)
                throw new InvalidOperationException("通信变量声明与本次运行值不一致：" + source.Name);
            result.Add(id, new(source, normalized));
        }
        return result;
    }
}
