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
    private readonly Dictionary<string, LocalImagePathCache> _caches;
    private readonly Dictionary<string, (SubscribedImageFolder Folder, long AcceptedRevision)> _folders = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _iteration;
    private bool _active, _disposed, _pendingSelection;
    private (string NodeId, int Index)? _queuedSelection;

    public ImageSourceRunSequence(string workflowId, IEnumerable<ImageSourcePolicy> policies, IResourcePathResolver paths,
        Action? requestFolderRun = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(paths);
        _workflowId = workflowId;
        var captured = policies.Select(p => new ImageSourcePolicy
        {
            NodeId = p.NodeId, SourceKind = p.SourceKind, FilePaths = new(p.FilePaths),
            CurrentIndex = p.CurrentIndex, ForRunPaths = new(p.ForRunPaths), RunMode = p.RunMode,
            AutoSwitchEnabled = p.AutoSwitchEnabled, StopAtLastImage = p.StopAtLastImage, PathCache = p.PathCache,
            SubscribeFolder = p.SubscribeFolder, FolderPath = p.FolderPath, RefreshFolder = p.RefreshFolder,
            TriggerFolderRun = p.TriggerFolderRun,
            CameraDeviceId = p.CameraDeviceId, CameraTimeoutMs = p.CameraTimeoutMs,
            OutputFormat = p.OutputFormat, SNInitialValue = p.SNInitialValue
        }).ToArray();
        _policies = captured.ToDictionary(p => p.NodeId, StringComparer.Ordinal);
        _caches = captured.Where(p=>p.SourceKind==ImageSourceKind.Local&&p.PathCache>0)
            .ToDictionary(p=>p.NodeId,p=>new LocalImagePathCache(p.PathCache),StringComparer.Ordinal);
        try
        {
            _plans = captured.Where(p => p.SourceKind == ImageSourceKind.Local).Select(policy =>
            {
                if (!policy.UsesFolder) return ImageSourcePolicy.CreateLocalPlan(policy.NodeId, policy, paths);
                var folder = new SubscribedImageFolder(policy.ResolveFolder(paths),
                    policy.TriggerFolderRun ? requestFolderRun : null);
                _folders.Add(policy.NodeId, (folder, 0));
                return new LocalImageSourcePlan(policy.NodeId, Array.Empty<LocalImageSequenceEntry>(), null,
                    policy.AutoSwitchEnabled, policy.StopAtLastImage);
            }).ToArray();
            _generation = _sequences.Begin(workflowId, _plans);
        }
        catch
        {
            Release();
            throw;
        }
    }

    public IHostInputPreparer CreatePreparer(string nodeId, ICameraFrameProvider? cameraService = null, bool advance = true, int navigationDelta = 0)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceRunSequence));
            if (_active) throw new InvalidOperationException("上一图像读取轮次尚未排空。");
            if (!_policies.TryGetValue(nodeId, out var policy)) throw new InvalidOperationException("图像源不属于本轮冻结的运行序列。");
            if(policy.SourceKind==ImageSourceKind.Sdk)throw new InvalidOperationException("SDK 图像源需要本轮外部文件，不能使用运行游标。");
            IHostInputPreparer result;
            if (policy.SourceKind == ImageSourceKind.Camera)
                result = new ImageSourceHostInputPreparer(null, policy.SNInitialValue, cameraService);
            else
            {
                RefreshFolders();
                var ticket = _sequences.GetInputs(_workflowId, _generation).Single(t => t.SourceNodeId == nodeId);
                result = new ImageSourceHostInputPreparer(ticket.Entry?.Address ?? string.Empty, policy.SNInitialValue)
                {
                    PathCache = _caches.GetValueOrDefault(nodeId),
                    LocalReadAdmission = (identity, cancellation) =>
                    {
                        lock (_gate)
                        {
                            var receipt = _sequences.AcceptRead(ticket, identity.RequestId, advance, cancellation, navigationDelta,
                                holdCurrent: _pendingSelection);
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

    private void RefreshFolders()
    {
        if (!_folders.Any(pair => pair.Value.Folder.Revision != pair.Value.AcceptedRevision)) return;
        var prior = _sequences.GetSelectedInputs(_workflowId, _generation);
        var replacements = new Dictionary<string, (LocalImageSourcePlan Plan, long Revision)>(StringComparer.Ordinal);
        foreach (var (nodeId, watched) in _folders)
        {
            var revision = watched.Folder.Revision;
            if (revision != watched.AcceptedRevision)
                replacements.Add(nodeId, (watched.Folder.Capture(_policies[nodeId], prior.GetValueOrDefault(nodeId)?.Position ?? -1), revision));
        }
        var updated = _plans.Select(plan => replacements.TryGetValue(plan.SourceNodeId, out var replacement)
            ? replacement.Plan : plan with { SelectedEntryId = prior.GetValueOrDefault(plan.SourceNodeId)?.Entry?.EntryId }).ToArray();
        foreach (var nodeId in replacements.Keys) _caches.GetValueOrDefault(nodeId)?.Dispose();
        _generation = _sequences.Begin(_workflowId, updated);
        _plans = updated;
        _iteration = 0;
        foreach (var (nodeId, replacement) in replacements)
            _folders[nodeId] = (_folders[nodeId].Folder, replacement.Revision);
    }

    internal LocalImageSourcePlan? CaptureFolderPlan(string nodeId)
    {
        lock (_gate)
            return !_disposed && _folders.ContainsKey(nodeId)
                ? _plans.Single(plan => plan.SourceNodeId == nodeId) : null;
    }

    /// <summary>Changes the next local read without editing the document configuration.</summary>
    public void SelectIndex(string nodeId, int index)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceRunSequence));
            if (_folders.ContainsKey(nodeId)) throw new InvalidOperationException("订阅文件夹使用独立运行列表。");
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
                if (_disposed) Release();
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
            if (!_active) Release();
        }
    }

    private void Release()
    {
        _sequences.End(_workflowId, _generation);
        var caches = _caches.Values.ToArray();
        _caches.Clear();
        var errors = new List<Exception>();
        foreach (var watched in _folders.Values)
            try { watched.Folder.Dispose(); } catch (Exception error) { errors.Add(error); }
        _folders.Clear();
        foreach (var cache in caches)
            try { cache.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("本地图像缓存释放失败。", errors);
    }
}

// Access is serialized by the source's admitted read round. The round retains
// the cache even when its configuration is retired before it drains.
internal sealed class LocalImagePathCache(int capacity) : IDisposable
{
    private readonly Dictionary<string, IDisposable> _images = new(StringComparer.Ordinal);

    internal T Open<T>(string address, Func<T> load, Func<T,T> clone) where T : class, IDisposable
    {
        if (_images.TryGetValue(address, out var cached)) return clone((T)cached);
        var image = load();
        if (_images.Count >= capacity) return image;
        try { _images.Add(address, clone(image)); }
        catch (Exception failure)
        {
            try { image.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("图像缓存副本创建及源图释放均失败。", failure, cleanup); }
            throw;
        }
        return image;
    }

    public void Dispose()
    {
        var images = _images.Values.ToArray();
        _images.Clear();
        var errors = new List<Exception>();
        foreach (var image in images)
            try { image.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException(errors);
    }
}

