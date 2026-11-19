using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>
/// 单个 ROI 字段行的呈现器，供 <c>DataTemplate</c> 使用。
/// <para>
/// 与旧 <c>NativeRoiUiDraftControls</c> 的根本区别：本控件<b>自己创建并独占持有</b>那一行，
/// 从不接收外部传入的 <see cref="DeclaredFieldRow"/>。模板每次应用生成新的呈现器实例，
/// 行也随之为新实例，因此不存在"同一元素被两处容器引用"。
/// </para>
/// <para>
/// 它只把行放进自己的 <see cref="ContentControl.Content"/>（单一父子关系），不做 <c>Children.Add</c>
/// 式的多元素拼装；创建出的行以"引用登记"方式告知 <see cref="Owner"/>，仅用于状态聚合，
/// 不改变视觉父子关系。
/// </para>
/// </summary>
public sealed class RoiFieldRowPresenter : ContentControl, IDisposable
{
    public static readonly DependencyProperty FieldProperty = DependencyProperty.Register(
        nameof(Field), typeof(RoiFieldRef), typeof(RoiFieldRowPresenter),
        new PropertyMetadata(null, OnFieldChanged));

    public static readonly DependencyProperty FieldFactoryProperty = DependencyProperty.Register(
        nameof(FieldFactory), typeof(Func<RoiFieldRef, DeclaredFieldRow>), typeof(RoiFieldRowPresenter),
        new PropertyMetadata(null, OnFieldChanged));

    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(
        nameof(Owner), typeof(RoiGroupView), typeof(RoiFieldRowPresenter),
        new PropertyMetadata(null, OnFieldChanged));

    private DeclaredFieldRow? _row;
    private bool _disposed;
    // 幂等记录：Field / FieldFactory / Owner 是三个独立依赖属性，模板依次赋值会多次触发
    // Rebuild；若每次都重建就会重复调用行工厂（ComposeInputEditor 第二次必然抛"输入已有编辑器"）。
    // 只有"字段或工厂真正变化"时才重建。
    private RoiFieldRef? _builtField;
    private Func<RoiFieldRef, DeclaredFieldRow>? _builtFactory;

    public RoiFieldRef? Field
    {
        get => (RoiFieldRef?)GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    /// <summary>行工厂：由宿主提供，本控件调用它创建行并独占持有。</summary>
    public Func<RoiFieldRef, DeclaredFieldRow>? FieldFactory
    {
        get => (Func<RoiFieldRef, DeclaredFieldRow>?)GetValue(FieldFactoryProperty);
        set => SetValue(FieldFactoryProperty, value);
    }

    /// <summary>所属的 ROI 组视图，用于登记已创建行以聚合状态。</summary>
    public RoiGroupView? Owner
    {
        get => (RoiGroupView?)GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    private static void OnFieldChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        => ((RoiFieldRowPresenter)d).Rebuild();

    private void Rebuild()
    {
        if (_disposed) return;
        var field = Field;
        var factory = FieldFactory;
        if (field is null || factory is null) return;
        // 同一字段 + 同一工厂已创建过则视为已就绪，直接返回（Owner 变化不影响行的归属）。
        if (_row is not null
            && ReferenceEquals(_builtField, field)
            && ReferenceEquals(_builtFactory, factory))
            return;
        DetachRow();
        var created = factory(field);
        if (created is null) return;
        _row = created;
        _builtField = field;
        _builtFactory = factory;
        Content = created;
        Owner?.AttachRow(field, created);
    }

    private void DetachRow()
    {
        var row = _row;
        var field = Field;
        _row = null;
        _builtField = null;
        _builtFactory = null;
        if (row is null) return;
        if (field is not null) Owner?.DetachRow(field, row);
        Content = null;
        row.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachRow();
    }
}
