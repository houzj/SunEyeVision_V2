using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>
/// ROI 组视图：只负责把 <see cref="RoiGroupModel"/> 交给模板渲染。
/// <para>
/// 本类不向任何集合 <c>Add</c> 元素——行的创建与销毁全部由 <see cref="RoiFieldRowPresenter"/>
/// 在模板实例化时完成，视觉父子关系始终是"呈现器 → 行"这一条。这里只登记行的引用用于
/// 状态聚合（对应原 <c>NativeRoiUiDraftControls.ActiveSources()</c> 的职责），不改变父子关系。
/// </para>
/// <para>
/// 这与原厂 <c>ROISelecter</c>（数据节点 + DataTemplate）结构一致：数据进、模板出。
/// </para>
/// </summary>
public partial class RoiGroupView : UserControl, IDisposable
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(RoiGroupModel), typeof(RoiGroupView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty FieldFactoryProperty = DependencyProperty.Register(
        nameof(FieldFactory), typeof(Func<RoiFieldRef, DeclaredFieldRow>), typeof(RoiGroupView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty DetectionEditorFactoryProperty = DependencyProperty.Register(
        nameof(DetectionEditorFactory), typeof(Func<NativeRoiEditor?>), typeof(RoiGroupView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty MaskEditorFactoryProperty = DependencyProperty.Register(
        nameof(MaskEditorFactory), typeof(Func<NativeRoiEditor?>), typeof(RoiGroupView),
        new PropertyMetadata(null));

    private readonly List<(RoiFieldRef Field, DeclaredFieldRow Row)> _rows = [];

    public RoiGroupView()
    {
        InitializeComponent();
    }

    /// <summary>要呈现的 ROI 组数据节点。</summary>
    public RoiGroupModel? Model
    {
        get => (RoiGroupModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>行工厂：由宿主提供，模板内的呈现器调用它创建行。</summary>
    public Func<RoiFieldRef, DeclaredFieldRow>? FieldFactory
    {
        get => (Func<RoiFieldRef, DeclaredFieldRow>?)GetValue(FieldFactoryProperty);
        set => SetValue(FieldFactoryProperty, value);
    }

    /// <summary>检测区绘制控件工厂：由模板内的编辑器呈现器调用。</summary>
    public Func<NativeRoiEditor?>? DetectionEditorFactory
    {
        get => (Func<NativeRoiEditor?>?)GetValue(DetectionEditorFactoryProperty);
        set => SetValue(DetectionEditorFactoryProperty, value);
    }

    /// <summary>屏蔽区绘制控件工厂：由模板内的编辑器呈现器调用。</summary>
    public Func<NativeRoiEditor?>? MaskEditorFactory
    {
        get => (Func<NativeRoiEditor?>?)GetValue(MaskEditorFactoryProperty);
        set => SetValue(MaskEditorFactoryProperty, value);
    }

    /// <summary>字段值变化通知（聚合各行 Changed）。</summary>
    public event Action? Changed;

    /// <summary>由呈现器在行创建后登记；仅保存引用，不改变视觉父子关系。</summary>
    internal void AttachRow(RoiFieldRef field, DeclaredFieldRow row)
    {
        _rows.RemoveAll(item => ReferenceEquals(item.Row, row));
        _rows.Add((field, row));
        row.Changed += OnRowChanged;
    }

    /// <summary>由呈现器在行销毁前注销。</summary>
    internal void DetachRow(RoiFieldRef field, DeclaredFieldRow row)
    {
        row.Changed -= OnRowChanged;
        _rows.RemoveAll(item => ReferenceEquals(item.Row, row));
    }

    private void OnRowChanged() => Changed?.Invoke();

    // ── 区域编辑器（绘制区）：同样由呈现器创建，这里只登记引用 ──

    private readonly Dictionary<string, NativeRoiEditor> _editors = new(StringComparer.Ordinal);

    /// <summary>可见性绑定的来源控件；对应原控件对外暴露的 GraphicVisibilitySource。</summary>
    public FrameworkElement GraphicVisibilitySource => this;

    /// <summary>指定组当前是否本地绘制；转发到数据节点，保持与原控件同名接口。</summary>
    public bool IsLocalDrawing(string group) => Model?.IsLocalDrawing(group) ?? false;

    /// <summary>屏蔽区是否使用继承来源。</summary>
    public bool IsInheritedMask => Model?.IsInheritedMask ?? false;

    /// <summary>是否处于图像区域模式。</summary>
    public bool IsImageArea => Model?.IsImageArea ?? false;

    /// <summary>检测区绘制控件；未创建时为 null。</summary>
    public NativeRoiEditor? DetectionEditor
        => _editors.TryGetValue("inspection", out var editor) ? editor : null;

    /// <summary>屏蔽区绘制控件；未创建时为 null。</summary>
    public NativeRoiEditor? MaskEditor
        => _editors.TryGetValue("mask", out var editor) ? editor : null;

    internal void AttachEditor(string slot, NativeRoiEditor editor)
        => _editors[slot] = editor;

    internal void DetachEditor(string slot, NativeRoiEditor editor)
    {
        if (_editors.TryGetValue(slot, out var existing) && ReferenceEquals(existing, editor))
            _editors.Remove(slot);
    }

    /// <summary>
    /// 释放引用登记。真实的行与编辑器由各呈现器在自己卸载时 Dispose，
    /// 这里只清空聚合用的引用，避免视图被复用时拿到已销毁的元素。
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _rows) item.Row.Changed -= OnRowChanged;
        _rows.Clear();
        _editors.Clear();
        Changed = null;
    }

    /// <summary>当前参与值捕获的行；判定完全由数据节点给出。</summary>
    private IEnumerable<DeclaredFieldRow> ActiveRows()
        => Model is null
            ? Enumerable.Empty<DeclaredFieldRow>()
            : _rows.Where(item => Model.IsFieldActive(item.Field)).Select(item => item.Row);

    /// <summary>激活行中是否存在尚未提交的编辑。</summary>
    public bool HasPendingEdits => ActiveRows().Any(row => row.HasPendingEdits);

    /// <summary>
    /// 提交所有激活行的待定编辑。对应原 <c>NativeRoiUiDraftControls.TryCaptureActiveSources</c>：
    /// 捕获期间若激活集发生变化，视为来源已切换并拒绝。
    /// </summary>
    public bool TryCaptureActiveSources(out string error)
    {
        var sources = ActiveRows().ToArray();
        foreach (var row in sources)
        {
            if (!ActiveRows().Contains(row))
            {
                error = "捕获期间 ROI 来源已切换，请重试。";
                return false;
            }
            if (row.HasPendingEdits && !row.TryCommitPending())
            {
                error = string.IsNullOrEmpty(row.Error) ? "ROI 来源尚未接纳。" : row.Error;
                return false;
            }
        }
        if (!sources.SequenceEqual(ActiveRows()))
        {
            error = "捕获期间 ROI 来源已切换，请重试。";
            return false;
        }
        error = "";
        return true;
    }
}
