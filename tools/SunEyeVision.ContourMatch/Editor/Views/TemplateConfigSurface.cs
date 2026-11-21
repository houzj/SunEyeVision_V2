using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂模板窗口的内容承载控件（等价 HPPyramidFeaturepatMatchDialogUI Control）。
/// 外壳（标题栏/边框/三按钮）由框架层 BaseWindow 统一提供；本控件只承载原厂 ControlTemplate 内容，
/// 暴露原厂 xaml 以 TemplatedParent 绑定的全部命令与数据属性（转发到 TemplateConfigData）。
/// </summary>
public sealed class TemplateConfigSurface : ContentControl, INotifyPropertyChanged
{
    private readonly TemplateConfigData _data;
    public new event PropertyChangedEventHandler? PropertyChanged;

    static TemplateConfigSurface()
        => DefaultStyleKeyProperty.OverrideMetadata(typeof(TemplateConfigSurface),
            new FrameworkPropertyMetadata(typeof(TemplateConfigSurface)));

    public TemplateConfigSurface(TemplateConfigData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _data.PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
    }

    // ── 命令 ──
    public ICommand SelectCurImageCommand => _data.SelectCurImageCommand;
    public ICommand SelectOtherImageCommand => _data.SelectOtherImageCommand;
    public ICommand SaveCommand => _data.SaveCommand;
    public ICommand MatchCommand => _data.MatchCommand;
    public ICommand ClearCommand => _data.ClearCommand;
    public ICommand BackStepCommand => _data.BackStepCommand;
    public ICommand ForwardStepCommand => _data.ForwardStepCommand;
    public ICommand RadioButtonCommand => _data.RadioButtonCommand;
    public ICommand IsUsePenCommand => _data.IsUsePenCommand;
    public ICommand RubberCommand => _data.RubberCommand;
    public ICommand Rubber2Command => _data.Rubber2Command;
    public ICommand AddROICommand => _data.AddROICommand;
    public ICommand DeleteCommand => _data.DeleteCommand;
    public ICommand GirdMouseDownCommand => _data.GirdMouseDownCommand;
    public ICommand ZoomOrgBtnCommand => _data.ZoomOrgBtnCommand;
    public ICommand ZoomFullBtnCommand => _data.ZoomFullBtnCommand;
    public ICommand ConfirmCommand => _data.ConfirmCommand;

    // ── 数据 ──
    public object? ImageDataSource => _data.ImageDataSource;
    public IList? ItemsData => _data.ItemsData;
    public IList? SignList => _data.SignList;
    public IList? ROILists => _data.ROILists;
    public IList? ShapeList => _data.ShapeList;
    public IList? SelectedROIList { get => _data.SelectedROIList; set => _data.SelectedROIList = value; }
    public Type DrawROIType { get => _data.DrawROIType; set => _data.DrawROIType = value; }
    public bool IsStateForDrawROI { get => _data.IsStateForDrawROI; set => _data.IsStateForDrawROI = value; }
    public bool IsUsePen { get => _data.IsUsePen; set => _data.IsUsePen = value; }
    public bool MatchPointLock { get; set; }
    public int PyramidScaleFlag { get => _data.PyramidScaleFlag; set => _data.PyramidScaleFlag = value; }
    public int PyramidScaleLevel { get => _data.PyramidScaleLevel; set => _data.PyramidScaleLevel = value; }
    public int PyramidScaleRLevel { get => _data.PyramidScaleRLevel; set => _data.PyramidScaleRLevel = value; }
    public int EdgeThresholdFlag { get => _data.EdgeThresholdFlag; set => _data.EdgeThresholdFlag = value; }
    public int EdgeThreshold { get => _data.EdgeThreshold; set => _data.EdgeThreshold = value; }
    public int ChainFlag { get => _data.ChainFlag; set => _data.ChainFlag = value; }
    public int MinChainLen { get => _data.MinChainLen; set => _data.MinChainLen = value; }
    public int MaskModelFlag { get => _data.MaskModelFlag; set => _data.MaskModelFlag = value; }
    public double PanRectWidth { get => _data.PanRectWidth; set => _data.PanRectWidth = value; }
    public double PanRectWidth2 { get => _data.PanRectWidth2; set => _data.PanRectWidth2 = value; }
    public bool BackEnable => _data.BackEnable;
    public bool ForwardEnable => _data.ForwardEnable;
    public double FixPointX { get => _data.FixPointX; set => _data.FixPointX = value; }
    public double FixPointY { get => _data.FixPointY; set => _data.FixPointY = value; }

    internal TemplateConfigData Model => _data;
}
