using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Images;

/// <summary>One original viewport, borrowing only the explicitly selected frozen publication.</summary>
internal sealed class ResultImagePane : IDisposable
{
    private sealed class Presentation(IResultPublication publication, IResultValueLease lease, IReadOnlyImage image,
        Func<bool> current, long generation, ResultPublicationStamp stamp, string scopeId, string nodeId)
    {
        internal readonly IResultPublication Publication = publication;
        internal readonly IResultValueLease Lease = lease;
        internal readonly IReadOnlyImage Image = image;
        internal readonly Func<bool> Current = current;
        internal readonly long Generation = generation;
        internal readonly ResultPublicationStamp Stamp = stamp;
        internal NativeResultFrameReceipt? Receipt;
        internal readonly string ScopeId=scopeId, NodeId=nodeId;
        internal bool DisplayOnly;
        internal Func<bool>? DisplayCurrent;
    }
    private sealed class Preview(Func<bool> current, long generation)
    {
        internal readonly Func<bool> Current = current;
        internal readonly long Generation = generation;
        internal NativeResultFrameReceipt? Receipt;
    }
    private Presentation? _presentation;
    private Preview? _preview;
    private long _generation;
    private bool _disposed;
    internal NativeResultViewport Viewport { get; }
    internal ResultImagePane(FrameworkElement originalView) => Viewport = NativeResultViewport.Attach(originalView);
    internal NativeResultFrameReceipt? CurrentFrame
    {
        get
        {
            Viewport.View.Dispatcher.VerifyAccess();
            var active = _presentation;
            if (active is not null) return IsCurrent(active) && active.Receipt == Viewport.CurrentFrame ? active.Receipt : null;
            var preview = _preview;
            return preview is not null && IsCurrent(preview) && preview.Receipt == Viewport.CurrentFrame ? preview.Receipt : null;
        }
    }

    internal bool TryPresent(IResultPublication publication, OutputReference reference, string actualWorkflowId,
        Func<bool> current, out string error)
    {
        Viewport.View.Dispatcher.VerifyAccess();
        error = "";
        if (_disposed) { error = "结果图像面板已关闭。"; return false; }
        var generation = ++_generation;
        var previous = _presentation; _presentation = null; _preview = null;
        IResultValueLease? candidate = null;
        Presentation? active = null;
        try
        {
            Drain(Viewport.Clear, () => previous?.Lease.Dispose());
            bool AttemptCurrent()
            {
                if (_disposed || generation != _generation) return false;
                var valid = current();
                return valid && !_disposed && generation == _generation && publication.IsCurrent()
                    && !_disposed && generation == _generation;
            }
            ArgumentNullException.ThrowIfNull(publication); ArgumentNullException.ThrowIfNull(reference);
            ArgumentNullException.ThrowIfNull(current);
            if (string.IsNullOrWhiteSpace(actualWorkflowId)) throw new ArgumentException("缺少实际工作流身份。");
            if (reference.ScopeId is not null && reference.ScopeId != actualWorkflowId)
                throw new ArgumentException("图像来源不属于当前工作流。");
            var stamp = publication.Stamp;
            if (publication.State is not (ResultViewState.Success or ResultViewState.PartialSuccess)
                || stamp.PublicationId == Guid.Empty || stamp.SchemaRevision is null or < 0)
                throw new InvalidOperationException("来源没有实际完成的成功发布。");
            if (stamp.Invocation is not { } invocation || invocation.NodeId != reference.NodeId)
                throw new ArgumentException("图像来源节点与发布调用身份不一致。");
            if (stamp.SchemaFingerprint != publication.Schema.Fingerprint)
                throw new ArgumentException("发布声明指纹不一致。");
            var port = publication.Schema.Outputs.SingleOrDefault(value => value.Id == reference.PortId)
                ?? throw new ArgumentException("图像来源端口已不存在。");
            if (port.Type != reference.Type || NumericStorageRules.Effective(port.Type, port.Storage) != reference.Storage)
                throw new ArgumentException("图像来源根声明已经变化。");
            if (reference.Selection is BindingSelection.AtField)
                throw new NotSupportedException("图像显示不支持动态索引来源；请选择明确的图像元素。");
            var selectedType = reference.Type;
            foreach (var member in reference.MemberFieldPath) selectedType = ProjectType(selectedType, member);
            if (reference.Selection is BindingSelection.AtConstant)
                selectedType = selectedType.Shape == ValueShape.Sequence ? selectedType.Element!
                    : throw new ArgumentException("元素索引要求序列声明。");
            if (selectedType != TypeRef.ResourceOf(ResourceKind.Image))
                throw new ArgumentException("请选择单一图像；图像序列必须指定元素索引。");
            if (!AttemptCurrent()) throw new InvalidOperationException("图像来源已失效。");
            candidate = publication.Borrow();
            if (candidate.Stamp != stamp || !AttemptCurrent()) throw new InvalidOperationException("图像借阅身份已失效。");
            var values = candidate.Values ?? throw new InvalidOperationException("来源没有已完成的图像值。");
            if (!values.Declarations.TryGetValue(port.Id, out var actualPort) || actualPort != port)
                throw new InvalidOperationException("图像值与冻结端口声明不一致。");
            object? value = values.Get<object?>(port.Id);
            value = SelectValue(value, reference.MemberFieldPath, 0,
                reference.Selection is BindingSelection.AtConstant index ? index.Index : null);
            var image = value as IReadOnlyImage ?? throw new InvalidOperationException("冻结来源没有单一图像值。");
            ResourceContractRules.ValidateImage(image);
            if (!AttemptCurrent()) throw new InvalidOperationException("图像读取期间来源已失效。");
            active = new(publication, candidate, image, current, generation, stamp, actualWorkflowId, reference.NodeId); candidate = null;
            _presentation = active;
            if (!Viewport.BindImage(image, () => CanDisplay(active)))
                throw new InvalidOperationException("图像绑定期间来源已失效。");
            active.Receipt = Viewport.CurrentFrame;
            if (!IsCurrent(active) || active.Receipt is null || active.Receipt.Frame != image.Frame
                || active.Receipt.Coordinates != image.EffectiveDomain.Frame)
                throw new InvalidOperationException("图像安装回执与实际Frame/Domain不一致。");
            return true;
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            if (active is not null && ReferenceEquals(_presentation, active))
            {
                _presentation = null;
                try { Viewport.Clear(); } catch (Exception cleanup) { failures.Add(cleanup); }
                try { active.Lease.Dispose(); } catch (Exception cleanup) { failures.Add(cleanup); }
            }
            // A reentrant newer presentation already drained active; never clear its viewport here.
            try { candidate?.Dispose(); } catch (Exception cleanup) { failures.Add(cleanup); }
            error = string.Join("；", failures.Select(item => item.Message));
            return false;
        }
    }

    /// <summary>Display-only decoded file preview. It does not invent a run or authorize a node-input ROI edit.</summary>
    internal bool TryPresentPreview(IReadOnlyImage image, Func<bool> current, out string error)
    {
        Viewport.View.Dispatcher.VerifyAccess();
        error = "";
        if (_disposed) { error = "结果图像面板已关闭。"; return false; }
        var generation = ++_generation;
        var previous = _presentation; _presentation = null; _preview = null;
        Preview? preview = null;
        try
        {
            Drain(Viewport.Clear, () => previous?.Lease.Dispose());
            ArgumentNullException.ThrowIfNull(image); ArgumentNullException.ThrowIfNull(current);
            ResourceContractRules.ValidateImage(image);
            var frame = image.Frame; var coordinates = image.EffectiveDomain.Frame;
            if (_disposed || generation != _generation) throw new InvalidOperationException("文件预览已被替换。");
            preview = new(current, generation); _preview = preview;
            if (!Viewport.BindImage(image, () => IsCurrent(preview))) throw new InvalidOperationException("文件预览已失效。");
            preview.Receipt = Viewport.CurrentFrame;
            if (!IsCurrent(preview) || preview.Receipt is null || preview.Receipt.Frame != frame || preview.Receipt.Coordinates != coordinates)
                throw new InvalidOperationException("文件预览回执与真实Frame/Domain不一致。");
            return true;
        }
        catch (Exception failure)
        {
            error = failure.Message;
            if (preview is not null && ReferenceEquals(_preview, preview))
            {
                _preview = null;
                try { Viewport.Clear(); } catch (Exception cleanup) { error += "；" + cleanup.Message; }
            }
            return false;
        }
    }
    private bool IsCurrent(Preview preview)
    {
        if (_disposed || preview.Generation != _generation || !ReferenceEquals(_preview, preview)) return false;
        var valid = preview.Current();
        return valid && !_disposed && preview.Generation == _generation && ReferenceEquals(_preview, preview);
    }

    private bool IsCurrent(Presentation active)
    {
        if (_disposed || active.DisplayOnly || active.Generation != _generation || !ReferenceEquals(_presentation, active)) return false;
        var valid = active.Current() && active.Publication.IsCurrent() && active.Publication.Stamp == active.Stamp
            && active.Lease.Stamp == active.Stamp;
        return valid && !_disposed && active.Generation == _generation && ReferenceEquals(_presentation, active);
    }

    private bool CanDisplay(Presentation active)
    {
        if(!active.DisplayOnly)return IsCurrent(active);
        if(_disposed||active.Generation!=_generation||!ReferenceEquals(_presentation,active))return false;
        var valid=active.DisplayCurrent?.Invoke()==true;
        return valid&&!_disposed&&active.Generation==_generation&&ReferenceEquals(_presentation,active);
    }
    internal Action<IReadOnlyList<SunEyeVision.Workflow.ScopedNodeIdentity>?>? PrepareDisplayRetention(Func<bool> current)
    {
        Viewport.View.Dispatcher.VerifyAccess();
        var active=_presentation;
        if(active is null||!CanDisplay(active)||active.Receipt!=Viewport.CurrentFrame)return null;
        return affected=>
        {
            if(affected is null||!affected.Any(node=>node.WorkflowId==active.ScopeId&&node.NodeId==active.NodeId)
                ||_disposed||active.Generation!=_generation||!ReferenceEquals(_presentation,active)
                ||active.Receipt!=Viewport.CurrentFrame||!current())return;
            active.DisplayOnly=true;active.DisplayCurrent=current;
            // The native frame already owns an independent pixel reader. Retire result
            // authority now; its reader is released by the normal viewport clear/replace path.
            active.Lease.Dispose();
            Viewport.RefreshCurrentness();
        };
    }
    internal void ClearUnlessRetained()
    {
        if(_presentation is {DisplayOnly:true} retained&&CanDisplay(retained))RefreshCurrentness();
        else Clear();
    }
    internal void ClearRetained()
    {if(_presentation is {DisplayOnly:true})Clear();}
    internal ResultViewportImageLease? BorrowCurrentImage()
    {
        Viewport.View.Dispatcher.VerifyAccess();
        var active = _presentation;
        if (active?.Receipt is not { } receipt || !IsCurrent(active) || receipt != Viewport.CurrentFrame) return null;
        IResultValueLease? borrowed = null;
        try
        {
            borrowed = active.Publication.Borrow();
            if (borrowed.Stamp != active.Lease.Stamp || !IsCurrent(active) || receipt != Viewport.CurrentFrame)
            { return null; }
            var lease = new ResultViewportImageLease(active.Image, Viewport, receipt, borrowed,
                () => IsCurrent(active) && receipt == Viewport.CurrentFrame);
            borrowed = null;
            return lease;
        }
        catch (ObjectDisposedException) { return null; }
        finally { borrowed?.Dispose(); }
    }

    internal void Clear()
    {
        Viewport.View.Dispatcher.VerifyAccess();
        if (_disposed) return;
        ++_generation;
        var active = _presentation; _presentation = null; _preview = null;
        Drain(Viewport.Clear, () => active?.Lease.Dispose());
    }

    internal void RefreshCurrentness()
    {
        Viewport.View.Dispatcher.VerifyAccess();
        var active = _presentation; var preview = _preview;
        if (_disposed || active is null && preview is null) return;
        var valid = false;
        try
        {
            if (active is not null ? !CanDisplay(active) : !IsCurrent(preview!)) return;
            Viewport.RefreshCurrentness();
            valid = active is not null ? CanDisplay(active) && active.Receipt == Viewport.CurrentFrame
                : IsCurrent(preview!) && preview!.Receipt == Viewport.CurrentFrame;
        }
        finally
        {
            if (!valid && (active is not null ? ReferenceEquals(_presentation, active) : ReferenceEquals(_preview, preview))) Clear();
        }
    }

    private static TypeRef ProjectType(TypeRef type, string member)
    {
        if (type.Shape == ValueShape.Sequence) return TypeRef.SequenceOf(ProjectType(type.Element!, member), type.Count);
        if (type.Shape != ValueShape.Record) throw new ArgumentException("图像路径只能经过声明的组合字段。");
        return type.Fields.SingleOrDefault(field => field.Id == member)?.Type
            ?? throw new ArgumentException("图像组合字段已不存在。");
    }
    // Member projection preserves sequence positions, so constant selection can be pushed down
    // to the single sequence before reading its record fields. Never materialize every image.
    private static object? SelectValue(object? value, IReadOnlyList<string> members, int position, int? index)
    {
        if (value is SequenceValue sequence)
        {
            if (index is not { } selected || selected >= sequence.Count)
                throw new ArgumentException("图像元素索引越界或来源不是单一元素。");
            value = sequence[selected]; index = null;
        }
        if (position == members.Count)
        {
            if (index.HasValue) throw new ArgumentException("元素索引没有对应的序列值。");
            return value;
        }
        if (value is not RecordValue record) throw new InvalidOperationException("冻结图像路径没有声明组合值。");
        return SelectValue(record.Get<object?>(members[position]), members, position + 1, index);
    }
    private static void Drain(params Action[] actions)
    {
        var failures = new List<Exception>();
        foreach (var action in actions) try { action(); } catch (Exception failure) { failures.Add(failure); }
        if (failures.Count != 0) throw new AggregateException("结果图像清理失败。", failures);
    }
    public void Dispose()
    {
        Viewport.View.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true; ++_generation;
        var active = _presentation; _presentation = null; _preview = null;
        Drain(Viewport.Dispose, () => active?.Lease.Dispose());
    }
}

/// <summary>An independent publication borrow for an edit scope. A retired receipt cannot authorize edits.</summary>
internal sealed class ResultViewportImageLease : IDisposable
{
    private IResultValueLease? _lease;
    private readonly IReadOnlyImage _image;
    private readonly Func<bool> _current;
    internal ResultViewportImageLease(IReadOnlyImage image, NativeResultViewport viewport,
        NativeResultFrameReceipt receipt, IResultValueLease lease, Func<bool> current)
    { _image = image; Viewport = viewport; Receipt = receipt; _lease = lease; _current = current; }
    internal IReadOnlyImage Image { get { ObjectDisposedException.ThrowIf(_lease is null, this); return _image; } }
    internal NativeResultViewport Viewport { get; }
    internal ResultPublicationStamp Stamp=>_lease?.Stamp??throw new ObjectDisposedException(nameof(ResultViewportImageLease));
    internal NativeResultFrameReceipt Receipt { get; }
    internal bool IsCurrent
    {
        get
        {
            Viewport.View.Dispatcher.VerifyAccess();
            if (_lease is null) return false;
            var valid = _current();
            return valid && _lease is not null && Receipt == Viewport.CurrentFrame;
        }
    }
    public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
}
