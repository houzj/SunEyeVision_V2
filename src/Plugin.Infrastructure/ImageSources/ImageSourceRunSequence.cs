using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.Infrastructure.ImageSources;

/// <summary>One host-owned local-source sequence; its read receipts are settled only after the run drains.</summary>
public sealed class ImageSourceRunSequence : IDisposable
{
    private readonly LocalImageSequenceService _sequences = new();
    private readonly string _workflowId;
    private readonly long _generation;
    private readonly IReadOnlyDictionary<string, ImageSourcePolicy> _policies;
    private readonly Dictionary<string, LocalImageRunInput> _tickets;
    private int _state;

    public ImageSourceRunSequence(string workflowId, IEnumerable<ImageSourcePolicy> policies, IResourcePathResolver paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(paths);
        _workflowId = workflowId;
        var captured = policies.Select(p => new ImageSourcePolicy
        {
            NodeId = p.NodeId, SourceKind = p.SourceKind, FilePaths = new(p.FilePaths),
            CurrentIndex = p.CurrentIndex, ForRunPaths = new(p.ForRunPaths), RunMode = p.RunMode,
            AutoSwitchEnabled = p.AutoSwitchEnabled, StopAtLastImage = p.StopAtLastImage,
            CameraDeviceId = p.CameraDeviceId, CameraTimeoutMs = p.CameraTimeoutMs,
            OutputFormat = p.OutputFormat, SNInitialValue = p.SNInitialValue
        }).ToArray();
        _policies = captured.ToDictionary(p => p.NodeId, StringComparer.Ordinal);
        var plans = captured.Where(p => p.SourceKind == ImageSourceKind.Local)
            .Select(p => ImageSourcePolicy.CreateLocalPlan(p.NodeId, p, paths)).ToArray();
        _generation = _sequences.Begin(workflowId, plans);
        _tickets = _sequences.GetInputs(workflowId, _generation).ToDictionary(t => t.SourceNodeId, StringComparer.Ordinal);
    }

    public IHostInputPreparer CreatePreparer(string nodeId, ICameraCaptureService? cameraService = null, CameraFrame? borrowedFrame = null, bool advance = true)
    {
        if (Volatile.Read(ref _state) != 0) throw new ObjectDisposedException(nameof(ImageSourceRunSequence));
        if (!_policies.TryGetValue(nodeId, out var policy)) throw new InvalidOperationException("图像源不属于本轮冻结的运行序列。");
        if (policy.SourceKind == ImageSourceKind.Camera)
            return new ImageSourceHostInputPreparer(null, policy.SNInitialValue, cameraService, borrowedFrame);
        var ticket = _tickets[nodeId];
        return new ImageSourceHostInputPreparer(ticket.Entry?.Address ?? string.Empty, policy.SNInitialValue)
        {
            LocalReadAdmission = (identity, cancellation) => _sequences.AcceptRead(ticket, identity.RequestId, advance, cancellation).Entry.Address
        };
    }

    public LocalImageReadRound FinishRound()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0) throw new InvalidOperationException("本轮已结算或释放。");
        try { return _sequences.FinishReadRound(_workflowId, _generation, 0); }
        finally { _sequences.End(_workflowId, _generation); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, 1) == 0) _sequences.End(_workflowId, _generation);
    }
}

/// <summary>Creates private-draft input with an owner-scoped cursor independent of formal runs.</summary>
public static class ImageSourceDebugInputs
{
    public static IHostInputPreparer Create(string workflowId, ImageSourcePolicy policy, ToolExecutionOwner debugOwner,
        IResourcePathResolver paths, ICameraCaptureService? cameraService = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(debugOwner);
        ArgumentNullException.ThrowIfNull(paths);
        return policy.SourceKind == ImageSourceKind.Camera
            ? new ImageSourceHostInputPreparer(null, policy.SNInitialValue, cameraService)
            : ImageSourceDebugCursor.For(debugOwner).Prepare(workflowId, policy, paths);
    }
}
