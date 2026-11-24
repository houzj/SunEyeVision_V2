using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板窗口的 ROI 数据项基类（原厂 ItemsData 元素）。几何为归一化坐标（0~1，与原厂一致）。
/// <para>
/// 注意：<b>不能在这里实现 ROI 接口</b>。框架 <c>ImageViewBox</c> 绘制时用
/// <c>Activator.CreateInstance(DrawROIType)</c> 造数据对象（见 CircleCaliperROI.cs:254 等），
/// 因此 <c>DrawROIType</c> 必须是可实例化的具体类；且 <c>ROITempConverter.GetControlTypeByInterfaceType</c>
/// 对实现多个 ROI 接口的类型无法唯一定位绘制控件。故每种形状各有一个只实现单一接口的具体类，
/// 与原厂一致（HikCoverRoiParameter / HikPolygonRoiParameter / HikCoverCircleRoiParameter /
/// FeatureMathSectorROIModel / CrossROIModel，decompiled:1549-1590）。
/// </para>
/// </summary>
public abstract class TemplateRoiItem : INotifyPropertyChanged, IROIVisible, IROISign, IROIUIType
{
    private Point _centerPoint;
    private double _width;
    private double _height;
    private double _angle;
    private double _radius;
    private double _minRadius;
    private double _startAngle;
    private double _endAngle;
    private double _angleRange = 360;
    private ObservableCollection<Point> _polygonPoints = new();

    protected TemplateRoiItem()
    {
        _polygonPoints.CollectionChanged += PolygonPointsChanged;
    }

    public ICommand DataChangedCommand { get; set; } = new DelegateCommand(() => { });
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>形状类别：rectangle / polygon / circle / sector / point / cross。</summary>
    public abstract string Kind { get; }

    /// <summary>正样本(true)/背景样本(false)——原厂 Sign 语义。</summary>
    private bool _isPositive = true;
    public bool IsPositive
    {
        get => _isPositive;
        set
        {
            if (_isPositive == value) return;
            _isPositive = value;
            Raise(nameof(IsPositive));
            Raise(nameof(Sign));
            Raise(nameof(SignIndex));
            Raise(nameof(RoiFill));
        }
    }

    /// <summary>
    /// 原厂 ROI 控件的填充不是边框颜色：匹配区域用蓝色半透明覆盖，屏蔽区域用红色半透明覆盖。
    /// 共享 ROI 模板只绑定这个可选属性；没有该属性的其它工具仍保持透明填充。
    /// </summary>
    public Brush RoiFill => IsPositive ? MatchFill : BlindFill;
    private static readonly Brush MatchFill = CreateFill(Color.FromArgb(0x80, 0x0F, 0x6A, 0xF3));
    private static readonly Brush BlindFill = CreateFill(Color.FromArgb(0x4C, 0xFF, 0x1A, 0x1A));
    private static Brush CreateFill(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
    /// <summary>区域序号（原厂 ItemsData 显示用）。</summary>
    public int Index { get; set; }
    public string Name { get; set; } = "";
    /// <summary>区域身份：往返 ROI 文档时保持稳定，新增时生成。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>区域覆盖率（原厂圆环/扇形等整域形状语义）。</summary>
    public string Coverage { get; set; } = "Explicit";
    /// <summary>该样本是否参与训练（原厂列表可见性/勾选语义）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>列表选中状态（原厂 ListViewItem.IsSelected 双向绑定）。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Raise(nameof(IsSelected));
        }
    }
    private bool _isSelected;

    /// <summary>
    /// 原厂 IROISign.Sign（bool?）：true=正样本、false=背景样本。
    /// 共享 ROI 模板的 FeatureRectStyle 用 <c>{Binding Sign}</c> 决定填充色
    /// （rectroistyle.xaml:33-59），所以这里必须是 bool?，不能用列表下拉的 int 下标。
    /// </summary>
    public bool? Sign
    {
        get => IsPositive;
        set
        {
            var positive = value != false;
            if (IsPositive == positive) return;
            _isPositive = positive;
            Raise(nameof(IsPositive));
            Raise(nameof(Sign));
            Raise(nameof(SignIndex));
            Raise(nameof(RoiFill));
        }
    }

    /// <summary>
    /// 列表符号下拉下标（原厂 Sign 列，cvtBoolToInt：0=匹配、1=屏蔽）。
    /// 原厂轮廓匹配 SignList 只有两项——Lang_Match 与 Lang_Blind
    /// （HPPyramidFeaturepatMatchDialogUI.CustomInitSign 为空实现，未追加"增强匹配"）。
    /// </summary>
    public int SignIndex
    {
        get => IsPositive ? 0 : 1;
        set
        {
            if (value is not (0 or 1) || IsPositive == (value == 0)) return;
            IsPositive = value == 0;
            Raise(nameof(SignIndex));
        }
    }

    /// <summary>
    /// 原厂 IROIUIType.UIType：标记这是模板样本区域，渲染模板才会套用样本填充色
    /// （rectroistyle.xaml:214-220 的多重触发器同时要求 UIType=true 与 Sign 非空）。
    /// </summary>
    public bool UIType { get; set; } = true;

    /// <summary>列表可见性（原厂 Eye 列，控制该样本区域在图上显示）。</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            Raise(nameof(Visible));
            Raise(nameof(IROIVisible.IsVisible));
        }
    }
    private bool _visible = true;

    /// <summary>原厂 Eye 绑定的是 IROIVisible.IsVisible，而不是普通 Visible 属性。</summary>
    bool IROIVisible.IsVisible
    {
        get => Visible;
        set => Visible = value;
    }

    public Point CenterPoint
    {
        get => _centerPoint;
        set { _centerPoint = value; Raise(nameof(CenterPoint)); }
    }

    public double Width
    {
        get => _width;
        set { _width = value; Raise(nameof(Width)); }
    }

    public double Height
    {
        get => _height;
        set { _height = value; Raise(nameof(Height)); }
    }

    public double Angle
    {
        get => _angle;
        set { _angle = value; Raise(nameof(Angle)); }
    }

    public double Radius
    {
        get => _radius;
        set { _radius = value; Raise(nameof(Radius)); }
    }

    public double MinRadius
    {
        get => _minRadius;
        set { _minRadius = value; Raise(nameof(MinRadius)); }
    }

    public double StartAngle
    {
        get => _startAngle;
        set { _startAngle = value; Raise(nameof(StartAngle)); }
    }

    public double EndAngle
    {
        get => _endAngle;
        set { _endAngle = value; Raise(nameof(EndAngle)); }
    }

    public double AngleRange
    {
        get => _angleRange;
        set { _angleRange = value; Raise(nameof(AngleRange)); }
    }

    public ObservableCollection<Point> PolygonPoints
    {
        get => _polygonPoints;
        set
        {
            if (ReferenceEquals(_polygonPoints, value)) return;
            _polygonPoints.CollectionChanged -= PolygonPointsChanged;
            _polygonPoints = value ?? new ObservableCollection<Point>();
            _polygonPoints.CollectionChanged += PolygonPointsChanged;
            Raise(nameof(PolygonPoints));
        }
    }

    private void PolygonPointsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Raise(nameof(PolygonPoints));

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    internal void SetIndex(int index)
    {
        if (Index == index) return;
        Index = index;
        Raise(nameof(Index));
    }
}

/// <summary>矩形样本（原厂 hikFrame → HikCoverRoiParameter）。</summary>
public sealed class RectRoiItem : TemplateRoiItem, INormalizedRectROI
{
    public override string Kind => "rectangle";
}

/// <summary>多边形样本（原厂 hikPolygon → HikPolygonRoiParameter）。</summary>
public sealed class PolygonRoiItem : TemplateRoiItem, INormalizedPolygonROI
{
    public override string Kind => "polygon";
}

/// <summary>圆形样本（原厂 hikCoverCircle → HikCoverCircleRoiParameter）。</summary>
public sealed class CircleRoiItem : TemplateRoiItem, INormalizedCircleROI
{
    public override string Kind => "circle";
}

/// <summary>扇形样本（原厂 hikSector → FeatureMathSectorROIModel）。</summary>
public sealed class SectorRoiItem : TemplateRoiItem, INormalizedSectorROI
{
    public override string Kind => "sector";
}

/// <summary>
/// 普通点 ROI（INormalizedPointROI → NormalizedPointROI，蓝色斜十字）。
/// 匹配中心不要用它，见 <see cref="CrossRoiItem"/>。
/// </summary>
public sealed class PointRoiItem : TemplateRoiItem, INormalizedPointROI
{
    public override string Kind => "point";
}

/// <summary>
/// 笔刷模式的占位图元（原厂 ToolROIModel，实现 IToolROI）。
/// ROITempConverter.GetControlTypeByInterfaceType 对实现 IToolROI 的类型返回 null
/// （ROITempConverter.cs:77-83），所以画布不会创建任何图元，也不会抛
/// “Can't Find Display ROI Control Type”；
/// 同时 IsStateForDrawROI=true 会让 ImageViewBox.mouseLBDown 走
/// NativeDrawStart/Handled 分支（ImageViewBox.xaml.cs:2142-2149），
/// 从而既不命中拖动已有 ROI，也不平移图像。
/// </summary>
public sealed class ToolRoiItem : TemplateRoiItem, IToolROI
{
    public override string Kind => "tool";
}

/// <summary>
/// 匹配中心十字（原厂 hikCross → CrossROIModel，实现 ICrossROI）。
/// 框架按接口映射到 CrossROI 控件，渲染为草绿色四向短线 + 圆环
/// （crossroistyle.xaml:19-38，ROIBaseColor=#7CFC00），
/// 与 NormalizedPointROI 的蓝色斜十字是两种图元，不能混用。
/// </summary>
public sealed class CrossRoiItem : TemplateRoiItem, ICrossROI
{
    public override string Kind => "cross";
}
