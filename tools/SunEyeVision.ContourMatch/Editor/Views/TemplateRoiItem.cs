using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板窗口的持久 ROI 数据项（原厂 ItemsData 元素），实现 ImageViewBox 期望的
/// INormalizedRectROI / INormalizedPolygonROI 数据接口。几何为归一化坐标（0~1，与原厂一致）。
/// 绘制控件映射由 ROITempConverter + [ROIDisplayForInterfaceType] 自动完成（RectROI.cs:20 等）。
/// </summary>
public sealed class TemplateRoiItem : INormalizedRectROI, INormalizedPolygonROI
{
    private Point _centerPoint;
    private double _width;
    private double _height;
    private double _angle;
    private ObservableCollection<Point> _polygonPoints = new();

    public ICommand DataChangedCommand { get; set; } = new DelegateCommand(() => { });
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>正样本(true)/背景样本(false)——原厂 Sign 语义。</summary>
    public bool IsPositive { get; set; } = true;
    /// <summary>区域序号（原厂 ItemsData 显示用）。</summary>
    public int Index { get; set; }
    public string Name { get; set; } = "";

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

    public ObservableCollection<Point> PolygonPoints
    {
        get => _polygonPoints;
        set { _polygonPoints = value; Raise(nameof(PolygonPoints)); }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
