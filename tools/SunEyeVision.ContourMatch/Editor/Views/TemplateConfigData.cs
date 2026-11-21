using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板配置窗口的数据模型（等价原厂 HPFeaturepatMatchDialogData，但用本方数据体系实现）。
/// 持有原厂 xaml 绑定的全部命令与数据属性；窗口 DataContext 指向本模型（MVVM 与原厂一致）。
/// </summary>
public sealed class TemplateConfigData : INotifyPropertyChanged
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Action _createTemplate;
    private readonly Action _saveProject;
    private readonly Action _loadProject;
    private readonly NativeRoiFrameSource? _frames;
    private readonly ObservableCollection<TemplateRoiItem> _items = new();
    private readonly ObservableCollection<object> _shapeList = new();
    private RecordValue _document;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── 命令（原厂 xaml 绑定，RelativeSource TemplatedParent → 窗口 → DataContext 本模型转发） ──
    public ICommand SelectCurImageCommand { get; }
    public ICommand SelectOtherImageCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand MatchCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BackStepCommand { get; }
    public ICommand ForwardStepCommand { get; }
    public ICommand RadioButtonCommand { get; }
    public ICommand IsUsePenCommand { get; }
    public ICommand RubberCommand { get; }
    public ICommand Rubber2Command { get; }
    public ICommand AddROICommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand GirdMouseDownCommand { get; }
    public ICommand ZoomOrgBtnCommand { get; }
    public ICommand ZoomFullBtnCommand { get; }
    public ICommand ConfirmCommand { get; }

    // ── 数据属性（原厂 xaml 绑定） ──
    public IImageData? ImageDataSource { get; private set; }
    public IList ItemsData => _items;
    public IList SignList { get; } = new ObservableCollection<object>();
    public IList ROILists { get; } = new ObservableCollection<IList>();
    public IList ShapeList => _shapeList;
    public IList SelectedROIList { get; set; }
    public Type DrawROIType { get; set; } = typeof(INormalizedRectROI);
    public bool IsStateForDrawROI { get; set; }
    public bool IsUsePen { get; set; }
    public int PyramidScaleFlag { get; set; }
    public int PyramidScaleLevel { get; set; }
    public int PyramidScaleRLevel { get; set; }
    public int EdgeThresholdFlag { get; set; }
    public int EdgeThreshold { get; set; }
    public int ChainFlag { get; set; }
    public int MinChainLen { get; set; }
    public int MaskModelFlag { get; set; }
    public double PanRectWidth { get; set; } = 10;
    public double PanRectWidth2 { get; set; } = 10;
    public bool BackEnable => _historyIndex > 0;
    public bool ForwardEnable => _historyIndex < _history.Count - 1;
    public double FixPointX { get; set; }
    public double FixPointY { get; set; }

    private readonly List<RecordValue> _history = new();
    private int _historyIndex = -1;
    private bool _confirmed;
    public bool Confirmed => _confirmed;

    public TemplateConfigData(
        IToolSettingsDraft draft,
        Action<FieldSettings>? acceptCandidate,
        Action createTemplate,
        Action saveProject,
        Action loadProject,
        RecordValue document,
        NativeRoiFrameSource? frames)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _createTemplate = createTemplate;
        _saveProject = saveProject;
        _loadProject = loadProject;
        _frames = frames;
        _document = document;
        SelectedROIList = _items;
        LoadFromDraft();

        SelectCurImageCommand = new DelegateCommand(SelectCurrentImage);
        SelectOtherImageCommand = new DelegateCommand(SelectOtherImage);
        SaveCommand = new DelegateCommand(() => { Snapshot(); _saveProject(); });
        MatchCommand = new DelegateCommand(MatchModel);
        ClearCommand = new DelegateCommand(ClearRois);
        BackStepCommand = new DelegateCommand(BackStep);
        ForwardStepCommand = new DelegateCommand(ForwardStep);
        RadioButtonCommand = new DelegateCommand<object>(SelectShape);
        IsUsePenCommand = new DelegateCommand(ResetConfig);
        RubberCommand = new DelegateCommand<object>(RubberOn);
        Rubber2Command = new DelegateCommand<object>(Rubber2On);
        AddROICommand = new DelegateCommand<object>(AddRoi);
        DeleteCommand = new DelegateCommand<object>(DeleteRoi);
        GirdMouseDownCommand = new DelegateCommand(SetCrossFromImage);
        ZoomOrgBtnCommand = new DelegateCommand(ZoomOrigin);
        ZoomFullBtnCommand = new DelegateCommand(ZoomFull);
        ConfirmCommand = new DelegateCommand(Confirm);
    }

    private void LoadFromDraft()
    {
        var fields = _draft.CaptureFields();
        var spec = ContourMatchFields.Schema;
        int Int(string id) => fields.Read(id) is ConstantSetting { Value: long v } ? (int)v : 0;
        PyramidScaleFlag = Int(ContourMatchFields.PyramidMode);
        PyramidScaleLevel = Int(ContourMatchFields.PyramidScaleLevel);
        PyramidScaleRLevel = Int(ContourMatchFields.PyramidScaleRLevel);
        EdgeThresholdFlag = Int(ContourMatchFields.EdgeThresholdMode);
        EdgeThreshold = Int(ContourMatchFields.EdgeThreshold);
        ChainFlag = Int(ContourMatchFields.ChainMode);
        MinChainLen = Int(ContourMatchFields.MinChainLen);
        MaskModelFlag = Int(ContourMatchFields.MaskModel);
        _history.Add(_document);
        _historyIndex = 0;
        RebuildRoiLists();
    }

    /// <summary>本方 RoiDocumentValue → ImageViewBox 期望的 IROI 列表（正/背景样本各一组）。</summary>
    private void RebuildRoiLists()
    {
        _items.Clear();
        ROILists.Clear();
        var positive = new ObservableCollection<object>();
        var background = new ObservableCollection<object>();
        foreach (var region in RoiDocumentValue.Items(_document, RoiDocumentValue.Inspection))
        {
            var item = ToRoiItem(region, true, _items.Count);
            _items.Add(item); positive.Add(item);
        }
        foreach (var region in RoiDocumentValue.Items(_document, RoiDocumentValue.Mask))
        {
            var item = ToRoiItem(region, false, _items.Count);
            _items.Add(item); background.Add(item);
        }
        ROILists.Add(positive);
        ROILists.Add(background);
        Raise(nameof(ItemsData)); Raise(nameof(ROILists));
    }

    private static TemplateRoiItem ToRoiItem(RecordValue geometry, bool positive, int index)
    {
        var item = new TemplateRoiItem { IsPositive = positive, Index = index };
        var kind = geometry.Get<string>("kind");
        var center = RoiGeometryEditing.Center(geometry);
        item.CenterPoint = new Point(center.X, center.Y);
        if (kind == "polygon")
        {
            foreach (var p in RoiGeometryEditing.Vertices(geometry))
                item.PolygonPoints.Add(new Point(p.X, p.Y));
        }
        else
        {
            item.Width = geometry.Get<double>("width");
            item.Height = geometry.Get<double>("height");
            item.Angle = geometry.Get<double>("angle-degrees");
        }
        return item;
    }

    private void SelectCurrentImage()
    {
        if (_frames is null) return;
        using var lease = _frames.Borrow(ContourMatchSchema.ImageInput, interactive: false);
        if (lease is null || !lease.IsCurrent) return;
        ImageDataSource = new FrameImageData(lease.Image);
        Raise(nameof(ImageDataSource));
        _frames.Refresh();
    }

    /// <summary>原厂 AddROICommandExecute（HPFeaturepatMatchDialogData.cs:469-490）语义。</summary>
    private void AddRoi(object? roi)
    {
        IsStateForDrawROI = false;
        if (roi is ICrossROI cross)
        {
            FixPointX = cross.CenterPoint.X;
            FixPointY = cross.CenterPoint.Y;
            return;
        }
        if (roi is TemplateRoiItem item && !_items.Contains(item))
        {
            item.Index = _items.Count;
            _items.Add(item);
            ((IList)ROILists[item.IsPositive ? 0 : 1]).Add(item);
            Raise(nameof(ItemsData));
            Snapshot();
        }
    }

    private void DeleteRoi(object? roi)
    {
        if (roi is not TemplateRoiItem item) return;
        _items.Remove(item);
        ((IList)ROILists[item.IsPositive ? 0 : 1]).Remove(item);
        Raise(nameof(ItemsData));
        Snapshot();
    }

    private void ClearRois()
    {
        _items.Clear();
        foreach (var list in ROILists) ((IList)list).Clear();
        Raise(nameof(ItemsData));
        Snapshot();
    }

    private void Snapshot()
    {
        if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(_document);
        _historyIndex = _history.Count - 1;
        Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable));
    }

    private void BackStep() { if (_historyIndex > 0) { _historyIndex--; _document = _history[_historyIndex]; RebuildRoiLists(); Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable)); } }
    private void ForwardStep() { if (_historyIndex < _history.Count - 1) { _historyIndex++; _document = _history[_historyIndex]; RebuildRoiLists(); Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable)); } }

    // ── 原厂命令实现（HPFeaturepatMatchDialogUI.cs 语义） ──

    /// <summary>选择其他图像：弹文件对话框读图（原厂 :918-944）。本方用 HalImageFactory 从文件读。 </summary>
    private void SelectOtherImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图像文件|*.bmp;*.png;*.jpg;*.tif;*.tiff|所有文件|*.*",
            Title = "选择其他图像"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(dialog.FileName);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            ImageDataSource = new BitmapImageData(bitmap);
            ResetConfigCore();
            Raise(nameof(ImageDataSource));
        }
        catch (Exception ex)
        {
            ImageDataSource = null;
            Raise(nameof(ImageDataSource));
            System.Windows.MessageBox.Show(ex.Message, "选择图像失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>生成模型（原厂 MatchCommandExecute :1499-1538）：先 Arrow 态、生成后退出绘制态。</summary>
    private void MatchModel()
    {
        IsStateForDrawROI = false;
        Raise(nameof(IsStateForDrawROI));
        Snapshot();
        _createTemplate();
    }

    /// <summary>形状切换（原厂 RadioButtonCommandExecute :1549-1580）：按形状名设 DrawROIType。</summary>
    private void SelectShape(object? parameter)
    {
        var name = parameter as string ?? (parameter as System.Windows.Controls.RadioButton)?.Name ?? "";
        DrawROIType = name switch
        {
            "hikFrame" or "Box" => typeof(INormalizedRectROI),
            "hikPolygon" or "Polygon" => typeof(INormalizedPolygonROI),
            "hikCoverCircle" or "Circle" => typeof(INormalizedCircleROI),
            "hikSector" => typeof(INormalizedSectorROI),
            "hikCross" or "Cross" => typeof(INormalizedPointROI),
            _ => DrawROIType
        };
        IsStateForDrawROI = false;
        Raise(nameof(DrawROIType)); Raise(nameof(IsStateForDrawROI));
    }

    /// <summary>全部屏蔽（原厂 IsUsePenCommandExecute :1836-1860）：ResetConfig + 清匹配中心手动标志。</summary>
    private void ResetConfig()
    {
        IsUsePen = !IsUsePen;
        ResetConfigCore();
        Raise(nameof(IsUsePen));
    }

    private void ResetConfigCore()
    {
        IsStateForDrawROI = false;
        _history.Clear();
        _historyIndex = -1;
        Snapshot();
        Raise(nameof(IsStateForDrawROI));
    }

    /// <summary>橡皮擦（原厂 RubberCommandExecute :1362-1395）：进入 ToolROIModel 绘制态并绑宽度。</summary>
    private void RubberOn(object? parameter)
    {
        PanRectWidth2 = PanRectWidth;
        DrawROIType = typeof(INormalizedRectROI);
        IsStateForDrawROI = true;
        Raise(nameof(DrawROIType)); Raise(nameof(IsStateForDrawROI)); Raise(nameof(PanRectWidth2));
    }

    private void Rubber2On(object? parameter)
    {
        DrawROIType = typeof(INormalizedPolygonROI);
        IsStateForDrawROI = true;
        Raise(nameof(DrawROIType)); Raise(nameof(IsStateForDrawROI));
    }

    /// <summary>网格点击设匹配中心（原厂 GirdMouseDownCommandExecute :1864-1870）。</summary>
    private void SetCrossFromImage()
    {
        if (!IsStateForDrawROI) return;
        IsStateForDrawROI = false;
        Raise(nameof(IsStateForDrawROI));
    }

    private void ZoomOrigin() => ViewportAction?.Invoke("origin");
    private void ZoomFull() => ViewportAction?.Invoke("full");

    /// <summary>由窗口注入的视口动作（缩放/全屏需操作 ImageViewBox 实例）。</summary>
    public Action<string>? ViewportAction { get; set; }

    private void Confirm()
    {
        if (_items.Count == 0) { Snapshot(); _confirmed = true; return; }
        Snapshot();
        _confirmed = true;
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>从文件选图（SelectOtherImage）的最小 IImageData 适配，按 Bgra32 拷贝像素。</summary>
    private sealed class BitmapImageData : IImageData
    {
        private readonly System.Windows.Media.Imaging.BitmapSource _source;
        private readonly byte[] _buffer;
        public BitmapImageData(System.Windows.Media.Imaging.BitmapSource source)
        {
            _source = source;
            var stride = source.PixelWidth * 4;
            _buffer = new byte[stride * source.PixelHeight];
            source.CopyPixels(_buffer, stride, 0);
        }
        public int Width => _source.PixelWidth;
        public int Height => _source.PixelHeight;
        public string PixelFormat => "Bgra32";
        public string MemoryAddress => "";
        public byte[] Buffer { get => _buffer; set => throw new InvalidOperationException("显示缓冲只读。"); }
    }

    /// <summary>借当前帧像素的最小 IImageData 适配（工具层，不依赖 UI 项目）。</summary>
    private sealed class FrameImageData : IImageData
    {
        private readonly IReadOnlyImage _image;
        private readonly byte[] _buffer;
        public FrameImageData(IReadOnlyImage image)
        {
            _image = image;
            _buffer = image.CopyPixels();
        }
        public int Width => _image.Layout.Width;
        public int Height => _image.Layout.Height;
        public string PixelFormat => "Bgra32";
        public string MemoryAddress => "";
        public byte[] Buffer { get => _buffer; set => throw new InvalidOperationException("显示缓冲由视口持有，不能替换规范像素。"); }
    }
}
