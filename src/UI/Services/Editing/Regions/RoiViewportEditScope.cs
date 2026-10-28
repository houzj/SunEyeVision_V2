using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.UI.Services.Images;

namespace SunEyeVision.UI.Services.Editing.Regions;

/// <summary>One borrowed, proven input frame and the original ROI session. No mouse or drawing implementation.</summary>
public sealed class RoiViewportEditScope : IRoiEditViewport, IDisposable
{
    private readonly Func<ResultViewportImageLease?> _input;
    private readonly Func<bool> _current;
    private readonly Func<IReadOnlyList<RoiEditItem>> _read;
    private readonly Func<RoiEditProposal, (RoiEditSelection Selection, IReadOnlyList<RoiEditItem> Items)> _accept;
    private readonly Action _publish;
    private readonly Guid _scope = Guid.NewGuid();
    private ResultViewportImageLease? _image;
    private INativeRoiEditLease? _interaction;
    private NativeRoiSession? _session;
    private IReadOnlyList<RoiEditItem> _scene = Array.Empty<RoiEditItem>();
    private HashSet<string> _groups = new(StringComparer.Ordinal);
    private long _revision;
    private bool _disposed, _accepting;
    private string _group = "inspection";
    public bool IsActive => !_disposed && _current() && _image?.IsCurrent == true && _interaction?.IsCurrent == true;
    public bool HasPendingGesture => IsActive && _interaction!.HasPendingGesture;
    // Kept only as the existing restricted numeric editor's notification contract. Native proposals never use it for writes.
    public event Action<RoiGeometryChange>? GeometryChanged { add { } remove { } }
    public event Action<string, Guid?>? SelectionChanged;
    public event Action<string>? StatusChanged;
    internal RoiViewportEditScope(Func<ResultViewportImageLease?> input, Func<bool> current,
        Func<IReadOnlyList<RoiEditItem>> read, Func<RoiEditProposal, (RoiEditSelection Selection, IReadOnlyList<RoiEditItem> Items)> accept, Action publish)
    { _input = input; _current = current; _read = read; _accept = accept; _publish = publish; }
    private bool Current(ResultViewportImageLease image) => !_disposed && _current() && ReferenceEquals(_image, image) && image.IsCurrent;
    internal static bool Supported(RoiEditItem row)
    {
        var kind = row.Geometry.Get<string>("kind");
        return kind == "rectangle" || kind == "polygon" && (!row.Geometry.Contains("closed") || row.Geometry.Get<bool>("closed")) || kind == "annulus" &&
            Math.Abs(Math.Abs(row.Geometry.Get<double>("end-radians") - row.Geometry.Get<double>("start-radians")) - 2 * Math.PI) <= 1e-9;
    }
    private bool Attach(out string error)
    {
        error = "";
        ResultViewportImageLease? candidate = null;
        try
        {
            if (_disposed || !_current()) throw new InvalidOperationException("当前区域编辑范围已失效。");
            candidate = _input() ?? throw new InvalidOperationException("当前节点没有已证明的实际输入图像，区域手势不可用。");
            var image = candidate;
            if (!image.IsCurrent) throw new InvalidOperationException("实际输入图像借阅已失效。");
            if (_image is not null && _interaction?.IsCurrent == true && _image.IsCurrent && image.Receipt == _image.Receipt)
            { if (ReferenceEquals(image, _image)) candidate = null; return true; }
            RetireFrame(); _image = image; candidate = null; ++_revision;
            var session = new NativeRoiSession(image.Image, ReadSnapshot, Accept, () => Current(image),
                () => new(false, false), _group, canDraw: group => _groups.Contains(group));
            _session = session;
            session.Changed += OnChanged; session.DrawRequested += OnDraw;
            _interaction = image.Viewport.BeginRoiEditing(image.Receipt, session, () => Current(image));
            StatusChanged?.Invoke("使用原件中心、边缘及旋转手柄；私有区域专属撤销重做尚未接入，接纳后使用方案历史。");
            return IsActive;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
        { error = exception.Message; RetireFrame(); StatusChanged?.Invoke(error); return false; }
        finally { candidate?.Dispose(); }
    }
    private void OnDraw(RoiDrawRequest request)
    {
        if (request.Kind is "rectangle" or "annulus" or "polygon") return;
        _session?.CancelDraw(); StatusChanged?.Invoke("该形状没有可无损回写的正式参数模型；当前不开放绘制。");
    }
    private RoiEditSnapshot ReadSnapshot()
    {
        if (_image is null || !Current(_image)) throw new InvalidOperationException("区域输入帧或编辑草稿已失效。");
        UpdateScene(_read());
        return new(new(_scope, _revision, _image.Image.Frame), Array.AsReadOnly(_scene.Where(Supported).ToArray()));
    }
    private RoiEditAcceptance Accept(RoiEditStamp expected, RoiEditProposal proposal)
    {
        var before = ReadSnapshot();
        if (_accepting || expected != before.Stamp) return RoiEditAcceptance.Reject(before, "区域版本已变化，请重新操作。");
        if (proposal is RoiEditProposal.Undo or RoiEditProposal.Redo)
            return RoiEditAcceptance.Reject(before, "私有区域草稿尚未具备原件专属撤销重做；接纳后的历史由方案统一管理。");
        _accepting = true;
        try
        {
            var accepted = _accept(proposal);
            UpdateScene(accepted.Items); ++_revision;
            var after = new RoiEditSnapshot(new(_scope, _revision, before.Stamp.Frame), Array.AsReadOnly(_scene.Where(Supported).ToArray()));
            IReadOnlyList<RoiEditTarget> targets = proposal switch
            {
                RoiEditProposal.Create when accepted.Selection is RoiEditSelection.Select select => new[] { select.Target },
                RoiEditProposal.Replace replace => new[] { replace.Target },
                RoiEditProposal.Delete delete => delete.Targets,
                _ => Array.Empty<RoiEditTarget>()
            };
            return new(true, after, targets, accepted.Selection, Publish: () => { if (!_disposed && _current()) _publish(); });
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
        { return RoiEditAcceptance.Reject(before, error.Message); }
        finally { _accepting = false; }
    }
    private static bool SameValue(object? left, object? right) => (left, right) switch
    {
        (RecordValue a, RecordValue b) => SameFields(a.CopyFields(), b.CopyFields()),
        (SequenceValue a, SequenceValue b) => a.Count == b.Count && a.Zip(b).All(pair => SameValue(pair.First, pair.Second)),
        _ => Equals(left, right)
    };
    private static bool SameFields(IReadOnlyDictionary<string, object?> a, IReadOnlyDictionary<string, object?> b)
        => a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && SameValue(pair.Value, value));
    private static bool SameRows(IReadOnlyList<RoiEditItem> a, IReadOnlyList<RoiEditItem> b)
        => a.Count == b.Count && a.Zip(b).All(pair => pair.First.Group == pair.Second.Group && pair.First.Id == pair.Second.Id
            && pair.First.Name == pair.Second.Name && pair.First.Enabled == pair.Second.Enabled && pair.First.Visible == pair.Second.Visible
            && SameValue(pair.First.Geometry, pair.Second.Geometry));
    private void UpdateScene(IReadOnlyList<RoiEditItem> rows)
    { if (!SameRows(_scene, rows)) { _scene = Array.AsReadOnly(rows.ToArray()); ++_revision; } }
    public void SetScene(IEnumerable<RoiEditItem> items)
    {
        UpdateScene(items.ToArray());
        if (_image is not null && !Current(_image)) RetireFrame();
        else if (!_accepting) _session?.Refresh();
        var omitted = _scene.Count(row => !Supported(row));
        if (omitted > 0) StatusChanged?.Invoke($"{omitted}项区域保留在完整参数草稿；原件尚无对应编辑入口，未投影到手势层。");
    }
    public void SetEditableGroups(IEnumerable<string> groups)
    { _groups = groups.ToHashSet(StringComparer.Ordinal); if (!_groups.Contains(_group)) CancelGesture(); }
    public bool BeginDraw(string group, string kind, out string error)
    {
        error = "";
        if (!_groups.Contains(group)) { error = "继承来源不能写入本地区域。"; return false; }
        if (kind is not ("rectangle" or "annulus" or "polygon"))
        { error = "原件当前仅接入矩形、完整圆环及多边形的无损参数编辑。"; StatusChanged?.Invoke(error); return false; }
        _group = group;
        if (!Attach(out error)) return false;
        _session!.SetDrawingGroup(group);
        if (_session.BeginDraw(kind) is null) { error = _session.Error; return false; }
        return true;
    }
    public void Select(string group, Guid? id)
    {
        _group = group; if (!Attach(out _)) return;
        _session!.SetDrawingGroup(group);
        if (id.HasValue && !_scene.Any(row => row.Group == group && row.Id == id && Supported(row)))
        { StatusChanged?.Invoke("所选区域保留数值模型，当前原件手势未接入该形状。"); return; }
        _session.Select(group, id);
    }
    public void SetAction(RoiEditAction action)
    {
        if (!Attach(out _)) return;
        CancelGesture();
        StatusChanged?.Invoke(action switch { RoiEditAction.Move => "拖动原件中心手柄移动区域。", RoiEditAction.Resize => "拖动原件边缘手柄调整区域。", RoiEditAction.Rotate => "拖动原件旋转手柄调整角度。", _ => "使用原件区域选择及手柄。" });
    }
    private void OnChanged()
    { if (_session?.CurROIData is { } item) SelectionChanged?.Invoke(item.Group, item.Id); else SelectionChanged?.Invoke(_group, null); }
    internal void Activate() => Attach(out _);
    internal void RefreshCurrentness() { if (_image is not null && !Current(_image)) RetireFrame(); }
    public void CancelGesture() => _interaction?.CancelGesture();
    private void RetireFrame()
    {
        var interaction = _interaction; _interaction = null;
        var session = _session; _session = null;
        var image = _image; _image = null;
        try { interaction?.Dispose(); }
        finally { try { if (session is not null) { session.Changed -= OnChanged; session.DrawRequested -= OnDraw; session.Dispose(); } } finally { image?.Dispose(); } }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; RetireFrame(); }
}
