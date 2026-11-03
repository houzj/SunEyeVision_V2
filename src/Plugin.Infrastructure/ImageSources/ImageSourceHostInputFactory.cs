using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.Infrastructure.ImageSources;

public sealed record ImageSourceReadReceipt(Guid SessionId, Guid NodeGeneration, Guid CursorId, long SelectionVersion, LocalImageReadReceipt Read);

/// <summary>Host-owned source cursors, keyed by the actual session and node lifetime.</summary>
public sealed class ImageSourceHostInputFactory : IHostInputFactory, IDisposable
{
    private sealed class Cursor(string signature, ImageSourceRunSequence sequence)
    {
        internal readonly string Signature = signature;
        internal readonly ImageSourceRunSequence Sequence = sequence;
        internal CancellationTokenRegistration Retirement;
        internal readonly Guid CursorId = Guid.NewGuid();
        internal long SelectionVersion;
        internal readonly Queue<ImageSourceReadReceipt> Completed = new();
    }
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Session, Guid Node, Guid? Debug), Cursor> _cursors = new();
    private readonly ICameraCaptureService? _cameraService;
    private bool _disposed;
    public ImageSourceHostInputFactory(ICameraCaptureService? cameraService = null) => _cameraService = cameraService;

    public IHostInputPreparer? Create(HostInputFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Declaration.CapabilityId != "image-source" || context.Declaration.Version != 1) return null;
        var cursor = GetCursor(context);
        long version;
        lock (_gate) version = cursor.SelectionVersion;
        return new OwnedPreparer(cursor.Sequence.CreatePreparer(context.Node.NodeId, _cameraService), cursor.Sequence,
            round => PublishReceipt(context, cursor, version, round));
    }

    public void SelectIndex(HostInputFactoryContext context, int index)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Declaration.CapabilityId != "image-source" || context.Declaration.Version != 1)
            throw new InvalidOperationException("节点不是已准入图像源。");
        var cursor = GetCursor(context);
        lock (_gate)
        {
            cursor.Sequence.SelectIndex(context.Node.NodeId, index);
            cursor.SelectionVersion = checked(cursor.SelectionVersion + 1);
            cursor.Completed.Clear();
        }
    }

    /// <summary>Retire this source cursor; an in-flight round drains without publishing its old receipt.</summary>
    public void Reset(HostInputFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureCurrent();
        Cursor? retired;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceHostInputFactory));
            _cursors.Remove((context.EditSessionId, context.NodeGeneration, context.DebugScopeId), out retired);
        }
        if (retired is not null) { retired.Retirement.Dispose(); retired.Sequence.Dispose(); }
    }
    /// <summary>Drain current-generation receipts on the document owner thread.</summary>
    public IReadOnlyList<ImageSourceReadReceipt> TakeCompletedReads(HostInputFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureCurrent();
        var signature = Describe(context).Value;
        var key = (context.EditSessionId, context.NodeGeneration, context.DebugScopeId);
        lock (_gate)
        {
            if (!_cursors.TryGetValue(key, out var cursor) || cursor.Signature != signature) return Array.Empty<ImageSourceReadReceipt>();
            var result = cursor.Completed.ToArray(); cursor.Completed.Clear();
            return Array.AsReadOnly(result);
        }
    }

    public bool IsReceiptCurrent(HostInputFactoryContext context, ImageSourceReadReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(receipt);
        context.EnsureCurrent();
        var signature = Describe(context).Value;
        lock (_gate)
            return !_disposed && !context.NodeRetirement.IsCancellationRequested &&
                receipt.SessionId == context.EditSessionId && receipt.NodeGeneration == context.NodeGeneration &&
                _cursors.TryGetValue((context.EditSessionId, context.NodeGeneration, context.DebugScopeId), out var cursor) &&
                cursor.Signature == signature && cursor.CursorId == receipt.CursorId && cursor.SelectionVersion == receipt.SelectionVersion;
    }
    private void PublishReceipt(HostInputFactoryContext context, Cursor cursor, long version, LocalImageReadRound round)
    {
        var key = (context.EditSessionId, context.NodeGeneration, context.DebugScopeId);
        lock (_gate)
        {
            if (_disposed || context.NodeRetirement.IsCancellationRequested ||
                !_cursors.TryGetValue(key, out var current) || !ReferenceEquals(current, cursor) || cursor.SelectionVersion != version) return;
            if (round.Accepted.Count == 0) return;
            cursor.SelectionVersion = checked(cursor.SelectionVersion + 1);
            cursor.Completed.Clear();
            foreach (var read in round.Accepted)
                cursor.Completed.Enqueue(new(context.EditSessionId, context.NodeGeneration, cursor.CursorId, cursor.SelectionVersion, read));
        }
    }
    private static (ImageSourcePolicy Policy, string Value) Describe(HostInputFactoryContext context)
    {
        var selected = context.Fields.Select(context.SettingsSchema.Inputs);
        var resolved = selected.Capture().Where(pair => pair.Value is ReferenceSetting or VariableSetting)
            .ToDictionary(pair => pair.Key, pair => context.CurrentValues.TryGetValue(pair.Key, out var value)
                ? value.Value : throw new InvalidOperationException("图像源设置引用尚无已接纳的当前值：" + pair.Key), StringComparer.Ordinal);
        var policy = ImageSourcePolicy.ReadFields(context.Node.NodeId, selected.Materialize(resolved));
        var plan = policy.SourceKind == ImageSourceKind.Local
            ? ImageSourcePolicy.CreateLocalPlan(context.Node.NodeId, policy, context.ResourcePaths) : null;
        return (policy, JsonSerializer.Serialize(new { policy, plan }));
    }
    private Cursor GetCursor(HostInputFactoryContext context)
    {
        context.EnsureCurrent();
        context.NodeRetirement.ThrowIfCancellationRequested();
        var (policy, signature) = Describe(context);
        var key = (context.EditSessionId, context.NodeGeneration, context.DebugScopeId);
        Cursor? replaced = null;
        Cursor cursor;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageSourceHostInputFactory));
            context.NodeRetirement.ThrowIfCancellationRequested();
            if (!_cursors.TryGetValue(key, out cursor!) || cursor.Signature != signature)
            {
                replaced = cursor;
                cursor = new Cursor(signature, new ImageSourceRunSequence(context.Node.WorkflowId, new[] { policy }, context.ResourcePaths));
                _cursors[key] = cursor;
                var captured = cursor;
                cursor.Retirement = (context.DebugScopeId is null ? context.NodeRetirement : context.DebugRetirement).Register(() => Retire(key, captured));
            }
        }
        if (replaced is not null) { replaced.Retirement.Dispose(); replaced.Sequence.Dispose(); }
        context.NodeRetirement.ThrowIfCancellationRequested();
        context.EnsureCurrent();
        return cursor;
    }

    private void Retire((Guid Session, Guid Node, Guid? Debug) key, Cursor cursor)
    {
        lock (_gate)
            if (_cursors.TryGetValue(key, out var current) && ReferenceEquals(current, cursor)) _cursors.Remove(key);
        cursor.Sequence.Dispose();
    }

    public void Dispose()
    {
        Cursor[] cursors;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; cursors = _cursors.Values.ToArray(); _cursors.Clear();
        }
        foreach (var cursor in cursors) { cursor.Retirement.Dispose(); cursor.Sequence.Dispose(); }
    }

    private sealed class OwnedPreparer(IHostInputPreparer inner, ImageSourceRunSequence sequence, Action<LocalImageReadRound> completed) : IHostInputPreparer
    {
        private int _disposed;
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public System.Threading.Tasks.ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context) => inner.PrepareAsync(context);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { inner.Dispose(); }
            finally { completed(sequence.FinishRound()); }
        }
    }
}
