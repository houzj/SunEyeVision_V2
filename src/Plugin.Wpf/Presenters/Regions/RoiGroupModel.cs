using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>
/// ROI 组中一个字段扮演的角色。角色决定该字段在何种模式下参与呈现与捕获，
/// 与具体字段 id 无关，因此可被所有 ROI 类工具复用。
/// </summary>
public enum RoiFieldRole
{
    /// <summary>区域类型选择（图像区域 / 几何区域）。不参与值捕获。</summary>
    AreaType,
    /// <summary>检测区绘制方式（本地绘制 / 继承）。不参与值捕获。</summary>
    DetectionMode,
    /// <summary>继承来源：矩形区域。</summary>
    RectangleSource,
    /// <summary>继承来源：扇环区域。</summary>
    AnnulusSource,
    /// <summary>矩形几何参数（中心/宽/高/角度）。</summary>
    RectangleParameter,
    /// <summary>扇环几何参数（中心/内外半径/角度）。</summary>
    AnnulusParameter,
    /// <summary>屏蔽区绘制方式。不参与值捕获。</summary>
    MaskMode,
    /// <summary>屏蔽区继承来源。</summary>
    MaskSource,
    /// <summary>输入掩膜图像。</summary>
    MaskImage,
    /// <summary>输出掩膜开关。</summary>
    OutputMask
}

/// <summary>
/// ROI 组中一个字段的标识：字段 id、显示标签、扮演的角色。
/// 只描述"哪个字段、什么标签、什么角色"，不持有任何控件。
/// </summary>
public sealed record RoiFieldRef(string FieldId, string Label, RoiFieldRole Role);

/// <summary>
/// ROI 组的数据节点，对齐原厂 <c>ROISelecter : NameValueItem</c>。
/// <para>
/// 原厂 <c>ROISelecter</c> 继承 <c>NameValueItem</c>（纯数据节点，无 UIElement 基类），
/// 界面由 <c>DataTemplate</c> 按类型渲染；模板每次应用都会新建元素，因此结构上不存在
/// "同一元素被两处容器引用"的可能。
/// </para>
/// <para>
/// 本类型严格遵循同一模型：<b>只承载字段标识与模式状态，不继承任何 UIElement，不持有控件</b>。
/// 由此替代原 <c>NativeRoiUiDraftControls : StackPanel</c>（后者用 <c>Children.Add</c> 装外部
/// 传入的 <c>DeclaredFieldRow</c>，元素所有权靠代码正确性维护，是双父级的结构性来源）。
/// </para>
/// </summary>
public sealed class RoiGroupModel : INotifyPropertyChanged
{
    private readonly List<RoiFieldRef> _fields = [];
    private int _detectionMode;
    private int _maskMode;
    private int _inheritanceMode;
    private bool _isImageArea;

    public RoiGroupModel(IEnumerable<RoiFieldRef> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields.AddRange(fields);
    }

    /// <summary>本组包含的字段；顺序即呈现顺序。</summary>
    public IReadOnlyList<RoiFieldRef> Fields => _fields;

    /// <summary>检测区模式：0=本地绘制，1=继承。</summary>
    public int DetectionMode
    {
        get => _detectionMode;
        set => Set(ref _detectionMode, value);
    }

    /// <summary>屏蔽区模式：0=本地绘制，1=继承。</summary>
    public int MaskMode
    {
        get => _maskMode;
        set => Set(ref _maskMode, value);
    }

    /// <summary>继承方式：0=矩形区域 1=矩形参数 2=扇环区域 3=扇环参数。</summary>
    public int InheritanceMode
    {
        get => _inheritanceMode;
        set => Set(ref _inheritanceMode, value);
    }

    /// <summary>是否处于"图像区域"模式（不再绘制几何）。</summary>
    public bool IsImageArea
    {
        get => _isImageArea;
        set => Set(ref _isImageArea, value);
    }

    /// <summary>屏蔽区是否使用继承来源。</summary>
    public bool IsInheritedMask => !IsImageArea && MaskMode == 1;

    /// <summary>指定组当前是否本地绘制（= 非图像区域 且 该组模式为绘制）。</summary>
    public bool IsLocalDrawing(string group)
        => !IsImageArea && (string.Equals(group, "inspection", StringComparison.Ordinal)
            ? DetectionMode == 0
            : MaskMode == 0);

    /// <summary>
    /// 指定字段当前是否参与值捕获。对应原 <c>NativeRoiUiDraftControls.ActiveSources()</c> 的判定链：
    /// 输出掩膜始终参与；图像区域下只加掩膜图像；继承时按继承方式取来源或几何参数组；
    /// 屏蔽区继承时加屏蔽来源。
    /// </summary>
    public bool IsFieldActive(RoiFieldRef field) => field.Role switch
    {
        RoiFieldRole.OutputMask => true,
        RoiFieldRole.MaskImage => IsImageArea,
        RoiFieldRole.RectangleSource => !IsImageArea && DetectionMode == 1 && InheritanceMode == 0,
        RoiFieldRole.AnnulusSource => !IsImageArea && DetectionMode == 1 && InheritanceMode == 2,
        RoiFieldRole.RectangleParameter => !IsImageArea && DetectionMode == 1 && InheritanceMode == 1,
        RoiFieldRole.AnnulusParameter => !IsImageArea && DetectionMode == 1 && InheritanceMode == 3,
        RoiFieldRole.MaskSource => IsInheritedMask,
        _ => false
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
