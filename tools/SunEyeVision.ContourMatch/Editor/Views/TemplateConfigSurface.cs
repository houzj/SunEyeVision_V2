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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (Template.FindName("Match", this) is Button match)
        {
            match.Command = _data.MatchCommand;
            match.CommandParameter = true;
        }
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
    public IList? SelectedROIList => _data.SelectedROIList;
    public Type DrawROIType => _data.DrawROIType;
    public bool IsStateForDrawROI => _data.IsStateForDrawROI;
    public bool IsUsePen { get => _data.IsUsePen; set => _data.IsUsePen = value; }
    public bool MatchPointLock { get => _data.MatchPointLock; set => _data.MatchPointLock = value; }
    public int PyramidScaleFlag { get => _data.PyramidScaleFlag; set => _data.PyramidScaleFlag = value; }
    public int PyramidScaleLevel { get => _data.PyramidScaleLevel; set => _data.PyramidScaleLevel = value; }
    public int PyramidScaleRLevel { get => _data.PyramidScaleRLevel; set => _data.PyramidScaleRLevel = value; }
    public int EdgeThresholdFlag { get => _data.EdgeThresholdFlag; set => _data.EdgeThresholdFlag = value; }
    public int EdgeThreshold { get => _data.EdgeThreshold; set => _data.EdgeThreshold = value; }
    public int OptimizationIndex { get => _data.OptimizationIndex; set => _data.OptimizationIndex = value; }
    public int MinContrast { get => _data.MinContrast; set => _data.MinContrast = value; }
    public int SubPixelIndex { get => _data.SubPixelIndex; set => _data.SubPixelIndex = value; }
    public double PanRectWidth { get => _data.PanRectWidth; set => _data.PanRectWidth = value; }
    public double PanRectWidth2 { get => _data.PanRectWidth2; set => _data.PanRectWidth2 = value; }
    public bool BackEnable => _data.BackEnable;
    public bool ForwardEnable => _data.ForwardEnable;
    public double FixPointX { get => _data.FixPointX; set => _data.FixPointX = value; }
    public double FixPointY { get => _data.FixPointY; set => _data.FixPointY = value; }

    internal TemplateConfigData Model => _data;

    /// <summary>
    /// 原厂 XAML 的 PxSlider 用 CallMethodAction 在 DataContext 上回调这两个方法
    /// （TargetObject="{Binding}" 解析到本控件，而不是 PxSlider 自己）：
    /// 原厂 HPFeaturepatMatchDialogUI 用它们在拖动笔刷尺寸滑块期间抑制画布 ROI 绘制。
    /// 本方浮层与画布分离，拖动时鼠标不在画布上，无额外状态需要抑制。
    /// 缺这两个方法会让 CallMethodAction 抛 “Could not find method named …”，中断滑块拖动。
    /// </summary>
    public void ThumbMouseLeftButtonDown() { }

    /// <summary>见 <see cref="ThumbMouseLeftButtonDown"/>。</summary>
    public void ThumbMouseLeftButtonUp() { }
}

