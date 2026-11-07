using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.Editing.Wpf.Models.Communication;

/// <summary>Consumer scope routing only. The host's admitted run service owns variable values and identity validation.</summary>
public sealed class RuntimeVariableReceiveService(ICommunicationReceiveService inner, string? workflowId = null) : ICommunicationReceiveService
{
    public string? WorkflowId { get; } = workflowId;
    public string ConsumerScope { get; init; } = workflowId ?? "";
    public Task ConsumeAsync(string deviceId, string targetId, string consumerId, Action<CommunicationReceiveSample> commit,
        CancellationToken token = default) => inner.ConsumeAsync(deviceId, targetId, ConsumerScope + "/" + consumerId, commit, token);
    public IReadOnlyList<CommunicationReceiveSource> GetSources() => inner.GetSources();
    public IReadOnlyList<ReceiveVariableSource> GetVariables() => inner.GetVariables();
    public Task<CommunicationReceiveSample> ReadAsync(string deviceId, string targetId, CancellationToken token = default)
        => inner.ReadAsync(deviceId, targetId, token);
    public IReadOnlyDictionary<string, ReceiveVariableValue> ReadVariables(IReadOnlyList<string> ids) => inner.ReadVariables(ids);
}
