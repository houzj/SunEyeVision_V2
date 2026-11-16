using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;


namespace SunEyeVision.Infrastructure.ImageSources;

public sealed record ImageSourceReadReceipt(Guid SessionId, Guid NodeGeneration, Guid CursorId, long SelectionVersion, LocalImageReadReceipt Read);

/// <summary>Host-owned source cursors, keyed by the actual session and node lifetime.</summary>
public sealed class ImageSourceHostInputFactory : IHostInputFactory, IDisposable
{
    private sealed class Cursor(string signature, ImageSourceRunSequence sequence, HostInputFactoryContext owner, bool folder)
    {
        internal readonly string Signature = signature;
        internal readonly ImageSourceRunSequence Sequence = sequence;
        internal CancellationTokenRegistration Retirement;
        internal readonly Guid CursorId = Guid.NewGuid();
        internal readonly SunEyeVision.Domain.Workflows.ScopedNodeIdentity Node = owner.Node;
        internal readonly bool IsFolder = folder;
        internal HostInputFactoryContext Owner = owner;
        internal long SelectionVersion;
        internal readonly Queue<ImageSourceReadReceipt> Completed = new();
    }
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Session, Guid Node, Guid? Debug), Cursor> _cursors = new();
    private readonly Dictionary<(Guid Session, Guid Node, Guid? Debug), ImageSourceProcessingState> _processing = new();
    private readonly ICameraFrameProvider? _cameraService;
    private readonly Action<HostInputFactoryContext>? _requestFolderRun;
    private bool _disposed;
    public ImageSourceHostInputFactory(ICameraFrameProvider? cameraService = null, Action<HostInputFactoryContext>? requestFolderRun = null)
    {
        _cameraService = cameraService;
        _requestFolderRun = requestFolderRun;
    }

    public IHostInputPreparer? Create(HostInputFactoryContext context) => Create(context, 0, null);

    public IHostInputFactory ForSdkFiles(string workflowId,IReadOnlyDictionary<string,string> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(files);
        var paths=files.ToDictionary(pair=>pair.Key,pair=>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            if(!Path.IsPathFullyQualified(pair.Value))
                throw new ArgumentException("SDK 供图要求绝对文件路径。",nameof(files));
            return Path.GetFullPath(pair.Value);
        },StringComparer.Ordinal);
        return new SdkInputFactory(this,workflowId,paths,null);
    }
    public IHostInputFactory ForSdkImages(string workflowId,IReadOnlyDictionary<string,SdkImageInput> images)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(images);
        var captured=images.ToDictionary(pair=>pair.Key,pair=>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            return pair.Value??throw new ArgumentException("SDK 图像不能为空。",nameof(images));
        },StringComparer.Ordinal);
        return new SdkInputFactory(this,workflowId,null,captured);
    }

    private sealed class SdkInputFactory(ImageSourceHostInputFactory owner,string workflowId,
        IReadOnlyDictionary<string,string>? paths,IReadOnlyDictionary<string,SdkImageInput>? images):IHostInputFactory
    {
        public IHostInputPreparer? Create(HostInputFactoryContext context)=>
            context.Node.WorkflowId==workflowId
                ? owner.Create(context,0,paths?.GetValueOrDefault(context.Node.NodeId),images?.GetValueOrDefault(context.Node.NodeId))
                : owner.Create(context);
    }

    /// <summary>Applies a navigation intent only to preparers captured for this run.</summary>
    public IHostInputFactory ForNavigation(string workflowId, int direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        return new NavigationInputFactory(this, workflowId, direction);
    }

    private sealed class NavigationInputFactory(ImageSourceHostInputFactory owner, string workflowId, int direction) : IHostInputFactory
    {
        public IHostInputPreparer? Create(HostInputFactoryContext context) =>
            owner.Create(context, context.Node.WorkflowId == workflowId ? direction : 0, null);
    }

    private IHostInputPreparer? Create(HostInputFactoryContext context, int navigationDelta, string? sdkAddress,SdkImageInput? sdkImage=null)
    {
        if (context.Declaration is not { CapabilityId: "image-source", Version: 1 }) return null;
        context.EnsureCurrent();
        var key = (context.EditSessionId, context.NodeGeneration, context.DebugScopeId);
        ImageSourceProcessingState state;
        IDisposable reader;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_processing.TryGetValue(key, out state!))
            {
                state = new ImageSourceProcessingState();
                _processing.Add(key, state);
                var captured = state;
                state.Retirement = (context.DebugScopeId is null ? context.NodeRetirement : context.DebugRetirement).Register(() =>
                {
                    lock (_gate)
                        if (_processing.TryGetValue(key, out var current) && ReferenceEquals(current, captured)) _processing.Remove(key);
                    captured.Dispose();
                });
            }
            reader = state.Acquire();
        }
        try
        {
            var input = CreateCore(context, navigationDelta, sdkAddress, sdkImage)
                ?? throw new InvalidOperationException("图像源输入准备器缺失。");
            return new ProcessingPreparer(input, state, reader,context);
        }
        catch { reader.Dispose(); throw; }
    }

    private IHostInputPreparer? CreateCore(HostInputFactoryContext context, int navigationDelta, string? sdkAddress,SdkImageInput? sdkImage=null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Declaration.CapabilityId != "image-source" || context.Declaration.Version != 1) return null;
        if(context.Fields.Read("source.kind") is ConstantSetting {Value:"sdk"})
        {
            context.EnsureCurrent();
            context.NodeRetirement.ThrowIfCancellationRequested();
            lock(_gate)ObjectDisposedException.ThrowIf(_disposed,this);
            return new ImageSourceHostInputPreparer(null,0,sdkAddress:sdkAddress??string.Empty,sdkImage:sdkImage);
        }
        if(sdkAddress is not null||sdkImage is not null)throw new InvalidOperationException("SDK 输入所绑定的图像源已切换模式。");
        var cursor = GetCursor(context);
        long version;
        lock (_gate) version = cursor.SelectionVersion;
        return new OwnedPreparer(context, cursor.Sequence.CreatePreparer(context.Node.NodeId, _cameraService, navigationDelta: navigationDelta), cursor.Sequence,
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
        if (retired is not null) ReleaseCursors(new[] { retired });
    }

    /// <summary>Release watchers when a committed source changes, even if it is never run again.</summary>
    public void RetireFolderCursor(Guid sessionId, string workflowId, string nodeId)
    {
        Cursor[] retired;
        lock (_gate)
        {
            var matches = _cursors.Where(pair => pair.Key.Session == sessionId && pair.Key.Debug is null &&
                pair.Value.IsFolder && pair.Value.Node.WorkflowId == workflowId && pair.Value.Node.NodeId == nodeId).ToArray();
            foreach (var pair in matches) _cursors.Remove(pair.Key);
            retired = matches.Select(pair => pair.Value).ToArray();
        }
        ReleaseCursors(retired);
    }

    /// <summary>Start watching a committed folder before the first execution when it can request a run.</summary>
    public void TrackFolderTrigger(HostInputFactoryContext context)
    {
        if (_requestFolderRun is not null && context.DebugScopeId is null &&
            context.Declaration is { CapabilityId: "image-source", Version: 1 } &&
            context.Fields.Read("source.kind") is ConstantSetting { Value: "local" } &&
            context.Fields.Read("local.subscribe-folder") is ConstantSetting { Value: true } &&
            context.Fields.Read("local.trigger-folder-run") is ConstantSetting { Value: true })
            _ = GetCursor(context);
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
    public LocalImageSourcePlan? CaptureFolderPlan(HostInputFactoryContext context, ImageSourceReadReceipt receipt)
    {
        if (!IsReceiptCurrent(context, receipt)) return null;
        lock (_gate)
            return _cursors.TryGetValue((context.EditSessionId, context.NodeGeneration, context.DebugScopeId), out var cursor)
                && cursor.CursorId == receipt.CursorId ? cursor.Sequence.CaptureFolderPlan(context.Node.NodeId) : null;
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
        // Exposure and gain are evaluated against this invocation's upstream
        // results, not against the editor's last published parameter values.
        var perFrame = selected.Declarations.Keys.Where(id=>id is "camera.exposure-time" or "camera.gain" or "stitch.clear-value"
            or "trigger.text" or "trigger.filter-text")
            .ToDictionary(id=>id,_=>(ParameterSetting)new UnsetSetting(),StringComparer.Ordinal);
        selected=selected.Apply(perFrame);
        var resolved = selected.Capture().Where(pair => pair.Value is ReferenceSetting or VariableSetting)
            .ToDictionary(pair => pair.Key, pair => context.CurrentValues.TryGetValue(pair.Key, out var value)
                ? value.Value : throw new InvalidOperationException("图像源设置引用尚无已接纳的当前值：" + pair.Key), StringComparer.Ordinal);
        var policy = ImageSourcePolicy.ReadFields(context.Node.NodeId, selected.Materialize(resolved));
        var plan = policy.SourceKind == ImageSourceKind.Local && !policy.UsesFolder
            ? ImageSourcePolicy.CreateLocalPlan(context.Node.NodeId, policy, context.ResourcePaths) : null;
        var folder = policy.UsesFolder ? policy.ResolveFolder(context.ResourcePaths) : null;
        return (policy, JsonSerializer.Serialize(new { policy, plan, folder }));
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
                Cursor? subscribed = null;
                void Request()
                {
                    HostInputFactoryContext owner;
                    lock (_gate)
                    {
                        if (_disposed || subscribed is null || !_cursors.TryGetValue(key, out var active) ||
                            !ReferenceEquals(active, subscribed)) return;
                        owner = active.Owner;
                    }
                    _requestFolderRun!(owner);
                }
                Action? request = context.DebugScopeId is null && _requestFolderRun is not null ? Request : null;
                cursor = subscribed = new Cursor(signature,
                    new ImageSourceRunSequence(context.Node.WorkflowId, new[] { policy }, context.ResourcePaths, request),
                    context, policy.UsesFolder);
                _cursors[key] = cursor;
                var captured = cursor;
                cursor.Retirement = (context.DebugScopeId is null ? context.NodeRetirement : context.DebugRetirement).Register(() => Retire(key, captured));
            }
            else cursor.Owner = context;
        }
        if (replaced is not null) ReleaseCursors(new[] { replaced });
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
        ImageSourceProcessingState[] states;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; cursors = _cursors.Values.ToArray(); _cursors.Clear();
            states = _processing.Values.ToArray(); _processing.Clear();
        }
        var failures = new List<Exception>();
        try { ReleaseCursors(cursors); } catch (Exception error) { failures.Add(error); }
        foreach (var state in states)
        {
            try { state.Retirement.Dispose(); } catch (Exception error) { failures.Add(error); }
            try { state.Dispose(); } catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count > 0) throw new AggregateException("图像源资源释放失败。", failures);
    }

    private static void ReleaseCursors(IEnumerable<Cursor> cursors)
    {
        var failures = new List<Exception>();
        foreach (var cursor in cursors)
        {
            try { cursor.Retirement.Dispose(); } catch (Exception error) { failures.Add(error); }
            try { cursor.Sequence.Dispose(); } catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count > 0) throw new AggregateException("图像源游标释放失败。", failures);
    }

    private sealed class ProcessingPreparer(IHostInputPreparer inner, ImageSourceProcessingState state, IDisposable reader,HostInputFactoryContext captured)
        : IHostInputPreparer, IHostInputRoundObserver
    {
        private int _disposed;
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return new ProcessingInputs(await inner.PrepareAsync(context).ConfigureAwait(false), state, captured,context.Gate,
                () => Volatile.Read(ref _disposed) == 0);
        }
        public IReadOnlyList<HostReadCompletion> CaptureCompletedReads() =>
            inner is IHostInputRoundObserver observer ? observer.CaptureCompletedReads() : Array.Empty<HostReadCompletion>();
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            var failures = new List<Exception>();
            try { inner.Dispose(); } catch (Exception error) { failures.Add(error); }
            try { reader.Dispose(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count > 0) throw new AggregateException("图像源输入与处理状态释放失败。", failures);
        }
    }
    private sealed class ProcessingInputs(IPreparedHostInputs inner, ImageSourceProcessingState state,HostInputFactoryContext captured,
        IHostInvocationGate gate, Func<bool> ownerCurrent) : IPreparedHostInputs
    {
        private int _disposed;
        public IReadOnlyDictionary<string, object?> Values => inner.Values;
        public IToolExecutionContext BindContext(IToolExecutionContext context) =>
            new ProcessingContext(inner.BindContext(context), state,captured, gate, () => Volatile.Read(ref _disposed) == 0 && ownerCurrent());
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) await inner.DisposeAsync().ConfigureAwait(false);
        }
    }
    private sealed class ProcessingContext(IToolExecutionContext inner, ImageSourceProcessingState state,HostInputFactoryContext captured,
        IHostInvocationGate gate, Func<bool> current)
        : IToolExecutionContext, IImageSourceProcessingContext, ILocalImageReadContext, ILocalImageCacheContext
    {
        public RunIdentity Identity => inner.Identity;
        public CancellationToken Cancellation => inner.Cancellation;
        public IOutputResources Resources => inner.Resources;
        public void Log(string message) => inner.Log(message);
        public Guid GetConfigurationRevision(string fieldId)
        {
            Cancellation.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(!current(),this);
            if(!captured.SettingsSchema.Inputs.Any(port=>port.Id==fieldId))throw new ArgumentException("配置版本只允许读取已声明设置。",nameof(fieldId));
            return captured.ConfigurationRevisions.GetValueOrDefault(fieldId);
        }
        public string AcceptLocalImage() => ((ILocalImageReadContext)inner).AcceptLocalImage();
        public T OpenLocalImage<T>(string address, Func<T> load, Func<T,T> clone) where T : class, IDisposable =>
            ((ILocalImageCacheContext)inner).OpenLocalImage(address, load, clone);
        public T GetProcessingState<T>(Func<T> create) where T : class, IDisposable
        {
            Cancellation.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(!current(), this);
            T? value = null;
            if (!gate.TryInvoke(() => value = state.Get(create)))
            {
                Cancellation.ThrowIfCancellationRequested();
                throw new InvalidOperationException("处理状态所属调用已退役。");
            }
            ObjectDisposedException.ThrowIf(!current(), this);
            return value ?? throw new InvalidOperationException("图像源处理状态未接纳。");
        }
    }

    private sealed class OwnedPreparer(HostInputFactoryContext owner, IHostInputPreparer inner, ImageSourceRunSequence sequence, Action<LocalImageReadRound> completed) : IHostInputPreparer, IHostInputRoundObserver
    {
        private int _disposed;
        private readonly object _gate = new();
        private RunIdentity? _run;
        private IReadOnlyList<HostReadCompletion>? _reads;
        public string CapabilityId => inner.CapabilityId;
        public int CapabilityVersion => inner.CapabilityVersion;
        public IReadOnlyList<string> Services => inner.Services;
        public System.Threading.Tasks.ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
        {
            lock (_gate)
            {
                if (_run is not null) throw new InvalidOperationException("宿主输入准备器只能接纳一次运行。");
                _run = context.Identity;
            }
            return inner.PrepareAsync(context);
        }
        public IReadOnlyList<HostReadCompletion> CaptureCompletedReads()
        {
            lock (_gate) return _reads ?? throw new InvalidOperationException("图像输入尚未结算。");
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            var errors = new List<Exception>();
            try { inner.Dispose(); } catch (Exception error) { errors.Add(error); }
            try
            {
                var round = sequence.FinishRound();
                lock (_gate)
                {
                    if (round.Accepted.Count > 0 && _run is null) throw new InvalidOperationException("图像回执缺少本轮运行身份。");
                    _reads = Array.AsReadOnly(round.Accepted.Select(read =>
                    {
                        var run = _run!.Value;
                        if (read.RequestId != run.RequestId) throw new InvalidOperationException("图像回执请求身份与准备器不一致。");
                        return new HostReadCompletion(owner.EditSessionId, owner.NodeGeneration, owner.Node, run, read.StopAfterCurrent);
                    }).ToArray());
                }
                completed(round);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("图像输入结算失败。", errors);
        }
    }
}
