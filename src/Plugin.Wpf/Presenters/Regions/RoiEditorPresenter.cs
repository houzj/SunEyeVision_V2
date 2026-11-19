using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>
/// 区域编辑器（绘制区）的呈现器，供 <c>DataTemplate</c> 或直接布局使用。
/// <para>
/// 与字段行呈现器同构：<b>自己创建并独占持有</b>一个 <see cref="NativeRoiEditor"/>，
/// 只放进自己的 <see cref="ContentControl.Content"/>。创建出的编辑器以引用方式登记到
/// <see cref="Owner"/>，供 ROI 组聚合使用，不改变视觉父子关系。
/// </para>
/// <para>
/// 这样 ROI 组的"字段行 + 绘制区"全部由模板/呈现器创建，代码里不再出现
/// <c>Children.Add(外部元素)</c>，与原厂 <c>ROISelecter</c> 的所有权模型一致。
/// </para>
/// </summary>
public sealed class RoiEditorPresenter : ContentControl, IDisposable
{
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(
        nameof(Owner), typeof(RoiGroupView), typeof(RoiEditorPresenter),
        new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty EditorFactoryProperty = DependencyProperty.Register(
        nameof(EditorFactory), typeof(Func<NativeRoiEditor?>), typeof(RoiEditorPresenter),
        new PropertyMetadata(null, OnChanged));

    /// <summary>该呈现器承载哪一组：<c>inspection</c> 或 <c>mask</c>。</summary>
    public static readonly DependencyProperty SlotProperty = DependencyProperty.Register(
        nameof(Slot), typeof(string), typeof(RoiEditorPresenter),
        new PropertyMetadata(null, OnChanged));

    private NativeRoiEditor? _editor;
    private bool _disposed;

    public RoiGroupView? Owner
    {
        get => (RoiGroupView?)GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    public Func<NativeRoiEditor?>? EditorFactory
    {
        get => (Func<NativeRoiEditor?>?)GetValue(EditorFactoryProperty);
        set => SetValue(EditorFactoryProperty, value);
    }

    public string? Slot
    {
        get => (string?)GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        => ((RoiEditorPresenter)d).Rebuild();

    private void Rebuild()
    {
        if (_disposed) return;
        DetachEditor();
        var factory = EditorFactory;
        var slot = Slot;
        if (factory is null || string.IsNullOrEmpty(slot)) return;
        var created = factory();
        if (created is null) return;
        _editor = created;
        Content = created;
        Owner?.AttachEditor(slot, created);
    }

    private void DetachEditor()
    {
        var editor = _editor;
        var slot = Slot;
        _editor = null;
        if (editor is null) return;
        if (!string.IsNullOrEmpty(slot)) Owner?.DetachEditor(slot, editor);
        Content = null;
        editor.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachEditor();
    }
}
