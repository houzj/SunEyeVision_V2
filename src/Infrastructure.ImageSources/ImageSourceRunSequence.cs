using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts;

using SunEyeVision.Plugin.Contracts.Acquisition;


namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>One host-owned local-source sequence; its read receipts are settled only after the run drains.</summary>
public sealed class ImageSourceRunSequence : IDisposable
{
    private readonly LocalImageSequenceService _sequences = new();
    private readonly string _workflowId;
    private long _generation;
    private LocalImageSourcePlan[] _plans;
    private readonly IReadOnlyDictionary<string, ImageSourcePolicy> _policies;
    private readonly object _gate = new();
    private long _iteration;
    private bool _active, _disposed, _pendingSelection;
    private (string NodeId, int Index)? _queuedSelection;

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
        _plans = captured.Where(p => p.SourceKind == ImageSourceKind.Local)
            .Select(p => ImageSourcePolicy.CreateLocalPlan(p.NodeId, p, paths)).ToArray();
        _generation = _sequences.Begin(workflowId, _plans);

    }

    public IHostInputPreparer CreatePreparer(string nodeId, ICameraFrameProvider? cameraService = null, bool advance = true)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceRunSequence));
            if (_active) throw new InvalidOperationException("上一图像读取轮次尚未排空。");
            if (!_policies.TryGetValue(nodeId, out var policy)) throw new InvalidOperationException("图像源不属于本轮冻结的运行序列。");
            IHostInputPreparer result;
            if (policy.SourceKind == ImageSourceKind.Camera)
                result = new ImageSourceHostInputPreparer(null, policy.SNInitialValue, cameraService);
            else
            {
                var ticket = _sequences.GetInputs(_workflowId, _generation).Single(t => t.SourceNodeId == nodeId);
                result = new ImageSourceHostInputPreparer(ticket.Entry?.Address ?? string.Empty, policy.SNInitialValue)
                {
                    LocalReadAdmission = (identity, cancellation) =>
                    {
                        lock (_gate)
                        {
                            var receipt = _sequences.AcceptRead(ticket, identity.RequestId, advance && !_pendingSelection, cancellation);
                            _pendingSelection = false;
                            return receipt.Entry.Address;
                        }
                    }
                };
            }
            _active = true;
            return result;
        }
    }

    /// <summary>Changes the next local read without editing the document configuration.</summary>
    public void SelectIndex(string nodeId, int index)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceRunSequence));
            var plan = _plans.SingleOrDefault(item => item.SourceNodeId == nodeId)
                ?? throw new InvalidOperationException("节点不是本地图像来源。");
            var entry = plan.Entries.SingleOrDefault(item => item.OriginalIndex == index)
                ?? throw new InvalidOperationException("所选图像不属于本次可运行列表。");
            if (_active) { _queuedSelection = (nodeId, index); return; }
            var replacement = _plans.Select(item => ReferenceEquals(item, plan) ? item with { SelectedEntryId = entry.EntryId } : item).ToArray();
            _generation = _sequences.Begin(_workflowId, replacement);
            _plans = replacement; _iteration = 0; _pendingSelection = true;
        }
    }
    public LocalImageReadRound FinishRound()
    {
        lock (_gate)
        {
            if (!_active) throw new InvalidOperationException("本轮未开始或已结算。");
            try { return _sequences.FinishReadRound(_workflowId, _generation, _iteration++); }
            finally
            {
                _active = false;
                if (_disposed) _sequences.End(_workflowId, _generation);
                else if (_queuedSelection is { } selection)
                {
                    _queuedSelection = null;
                    SelectIndex(selection.NodeId, selection.Index);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (!_active) _sequences.End(_workflowId, _generation);
        }
    }
}

