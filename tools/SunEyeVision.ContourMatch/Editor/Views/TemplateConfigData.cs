using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

// TemplateImageSnapshot / TemplateMaskSnapshot 已独立到 TemplateSnapshots.cs：
// 它们是纯数据快照，与窗口模型的生命周期无关，多个文件都要引用。

/// <summary>
/// 模板配置窗口的数据模型（等价原厂 HPFeaturepatMatchDialogData，但用本方数据体系实现）。
/// 持有原厂 xaml 绑定的全部命令与数据属性；窗口 DataContext 指向本模型（MVVM 与原厂一致）。
/// </summary>
public sealed class TemplateConfigData : INotifyPropertyChanged
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Action<RecordValue> _acceptDocument;
    private readonly Action _createTemplate;
    private readonly Func<TemplateImageSnapshot?, RecordValue, TemplateMaskSnapshot, string?>? _createTemplateWithMasks;
    private readonly Action<string, string>? _commitTemplate;
    private readonly Action _saveProject;
    private readonly Action _loadProject;
    private readonly NativeRoiFrameSource? _frames;
    private readonly ObservableCollection<TemplateRoiItem> _items = new();
    private readonly ObservableCollection<object> _shapeList = new();
    /// <summary>模型轮廓点容器；原厂 RefreshPoints 复用同一 IMultiPoint，本方同样不每轮重建。</summary>
    private MultiPoint? _modelOutline;
    private RecordValue _document;
    private string? _pendingTemplatePayload;

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>确认并完成回写后触发；由窗口负责关闭（原厂确定按钮语义）。</summary>
    public event Action? ConfirmCompleted;
    /// <summary>擦除掩膜改变后通知宿主重绘原厂 tempImage 覆盖层。</summary>
    public event Action? EraseMaskChanged;
    /// <summary>训练 ROI 几何、符号或列表改变后通知宿主重绘原厂 DrawMask 覆盖层。</summary>
    public event Action? RoiVisualChanged;

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
    public IList SelectedROIList
    {
        get => _selectedRoiList;
        private set
        {
            if (ReferenceEquals(_selectedRoiList, value)) return;
            _selectedRoiList = value;
            Raise(nameof(SelectedROIList));
        }
    }
    private IList _selectedRoiList = null!;
    /// <summary>绘制数据类型：必须是可实例化的具体类（框架 Draw 内部会 Activator.CreateInstance）。</summary>
    public Type DrawROIType
    {
        get => _drawRoiType;
        private set
        {
            if (_drawRoiType == value) return;
            _drawRoiType = value;
            Raise(nameof(DrawROIType));
        }
    }
    private Type _drawRoiType = typeof(RectRoiItem);
    public bool IsStateForDrawROI
    {
        get => _isStateForDrawRoi;
        private set
        {
            if (_isStateForDrawRoi == value) return;
            _isStateForDrawRoi = value;
            Raise(nameof(IsStateForDrawROI));
        }
    }
    private bool _isStateForDrawRoi;
    /// <summary>底栏匹配中心锁定（原厂 MatchPointLock）。</summary>
    public bool MatchPointLock
    {
        get => _matchPointLock;
        set
        {
            if (_matchPointLock == value) return;
            _matchPointLock = value;
            Raise(nameof(MatchPointLock));
            // 原厂 HikTextBoxStyle 的 DataTrigger：MatchPointLock=True 时底栏 FixX/FixY 禁用。
            Raise(nameof(IsMatchPointEditable));
        }
    }
    private bool _matchPointLock;

    /// <summary>底栏匹配中心输入框是否可编辑（原厂锁定勾选后禁用）。</summary>
    public bool IsMatchPointEditable => !_matchPointLock;
    /// <summary>全部屏蔽（原厂 IsUsePen）：开启后画布进入屏蔽绘制态，xaml 触发器据此切换工具条。</summary>
    public bool IsUsePen
    {
        get => _isUsePen;
        set
        {
            if (_isUsePen == value) return;
            _isUsePen = value;
            Raise(nameof(IsUsePen));
            Raise(nameof(ActiveMask));
            // 切换「全部屏蔽」等于换一层掩膜（原厂 DrawMask 按 IsUsePen 选 otherMask / otherMask2，
            // 两层颜色不同），覆盖层必须立即重绘，否则仍显示上一层的颜色与形状。
            EraseMaskChanged?.Invoke();
        }
    }
    private bool _isUsePen;
    /// <summary>原厂 IsSetCrossByManual：中心点是否由用户手动指定。</summary>
    public bool IsSetCrossByManual { get; private set; }
    public int PyramidScaleFlag { get; set; }
    public int PyramidScaleLevel { get; set; }
    public int PyramidScaleRLevel { get; set; }
    public int EdgeThresholdFlag { get; set; }
    public int EdgeThreshold { get; set; }
    /// <summary>HALCON optimization 索引（0=自动、1=不优化、2/3/4=低/中/高）。</summary>
    public int OptimizationIndex { get; set; }
    /// <summary>HALCON min_contrast（1~255）。</summary>
    public int MinContrast { get; set; } = 5;
    /// <summary>HALCON sub_pixel 索引（0=无、1=插值、2=最小二乘）。</summary>
    public int SubPixelIndex { get; set; } = 2;
    private double _panRectWidth = 10;
    /// <summary>
    /// 橡皮擦笔刷尺寸（原厂 PanRectWidth，fun2）。
    /// 原厂 RubberControl.DWidth/DHeight 在进入橡皮擦模式时 SetBinding("PanRectWidth")
    /// （Rubber2CommandExecute:1340-1341），滑块一动手写进绑定源、笔刷立即变；
    /// 本方同样不做“进入模式时拷贝一次”的中间量，见 <see cref="WipeSize"/>。
    /// </summary>
    public double PanRectWidth
    {
        get => _panRectWidth;
        set
        {
            if (Math.Abs(_panRectWidth - value) < double.Epsilon) return;
            _panRectWidth = value;
            Raise(nameof(PanRectWidth));
            if (!_wipeToPaintMask)
            {
                Raise(nameof(WipeSize));
                Raise(nameof(BrushSize));
            }
        }
    }

    private double _panRectWidth2 = 10;
    /// <summary>画笔笔刷尺寸（原厂 PanRectWidth2，fun，绑定 RubberControl 的 DWidth/DHeight）。</summary>
    public double PanRectWidth2
    {
        get => _panRectWidth2;
        set
        {
            if (Math.Abs(_panRectWidth2 - value) < double.Epsilon) return;
            _panRectWidth2 = value;
            Raise(nameof(PanRectWidth2));
            if (_wipeToPaintMask)
            {
                Raise(nameof(WipeSize));
                Raise(nameof(BrushSize));
            }
        }
    }

    /// <summary>
    /// 当前笔刷像素尺寸。等价于原厂 RubberControl 的 DWidth/DHeight：
    /// 橡皮擦绑 PanRectWidth、画笔绑 PanRectWidth2，随滑块实时变化。
    /// </summary>
    public int WipeSize => Math.Max(1, (int)Math.Round(_wipeToPaintMask ? PanRectWidth2 : PanRectWidth));

    /// <summary>兼容旧调用的别名，等同 <see cref="WipeSize"/>。</summary>
    public int BrushSize => WipeSize;

    /// <summary>
    /// 当前笔刷写入哪一层：原厂 fun(画笔) 的 rubber2_OnWipe 写 otherMask2，
    /// fun2(橡皮擦) 的 rubber_OnWipe 写 otherMask（decompiled:1330-1374、1416-1500），
    /// 与「全部屏蔽」IsUsePen 无关。用 IsUsePen 选层会导致画笔写进橡皮擦层。
    /// </summary>
    private bool _wipeToPaintMask;
    public bool BackEnable => _historyIndex > 0;
    public bool ForwardEnable => _historyIndex < _history.Count - 1;
    public double FixPointX
    {
        get => _fixPointX;
        set
        {
            if (Math.Abs(_fixPointX - value) < double.Epsilon) return;
            _fixPointX = value;
            Raise(nameof(FixPointX));
            // 原厂 FixPointX/Y 与画布十字是同一份数据（CrossROI.CenterPoint 双向绑定），
            // 数值一变标记就要跟到新位置；本方没有那条绑定，在这里显式同步。
            SyncCrossRoi();
        }
    }
    public double FixPointY
    {
        get => _fixPointY;
        set
        {
            if (Math.Abs(_fixPointY - value) < double.Epsilon) return;
            _fixPointY = value;
            Raise(nameof(FixPointY));
            SyncCrossRoi();
        }
    }
    private double _fixPointX;
    private double _fixPointY;
    /// <summary>
    /// 模板存图（原厂 cbIsSaveImage，默认开）：开=把训练原图存进模板，关=确定时丢弃原图。
    /// 证据：decompiled HPFeaturepatMatchDialogUI.cs:1650-1656（关时清空 CurImageDataA/ModelImageData/otherMask）。
    /// </summary>
    public bool IsSaveImage
    {
        get => _isSaveImage;
        set
        {
            if (_isSaveImage == value) return;
            _isSaveImage = value;
            Raise(nameof(IsSaveImage));
        }
    }
    private bool _isSaveImage = true;
    /// <summary>本次编辑的模板序号（多模板列表的选中项）。</summary>
    public int TemplateIndex { get; }
    /// <summary>当前帧快照（Gray8），建模板后作为"模板原图"存进载荷。</summary>
    private FrameImageData? _frameImage;
    /// <summary>原厂 otherMask：擦除轮廓点模式使用的掩膜。</summary>
    public byte[]? EraseMask { get; private set; }
    /// <summary>原厂 otherMask2：全部屏蔽/画笔模式使用的掩膜。</summary>
    public byte[]? PaintMask { get; private set; }
    public int MaskWidth { get; private set; }
    public int MaskHeight { get; private set; }
    // 原厂把掩膜作为当前窗口会话数据处理：载入旧模板时沿用其值，清空或再次绘制后以当前会话为准。
    // 两个标志必须独立，避免清空橡皮擦层时误回退或覆盖屏蔽层。
    private bool _eraseMaskSpecified;
    private bool _paintMaskSpecified;
    /// <summary>橡皮擦/画笔是否处于激活态（窗口据此挂画布鼠标事件做笔刷擦除）。</summary>
    public bool IsEraseMode { get; private set; }
    /// <summary>当前笔刷尺寸（原厂橡皮擦绑 PanRectWidth、画笔绑 PanRectWidth2）。</summary>
    private double _lastWipeX;
    private double _lastWipeY;
    private bool _hasLastWipePoint;

    /// <summary>
    /// 覆盖层当前显示的掩膜：跟随**当前笔刷**而不是「全部屏蔽」开关。
    /// 橡皮擦写 otherMask（擦除层），画笔写 otherMask2（屏蔽层），
    /// 显示层必须与写入层一致，否则擦完看不到红/蓝覆盖。
    /// </summary>
    public byte[]? ActiveMask => _wipeToPaintMask ? PaintMask : EraseMask;

    internal TemplateMaskSnapshot CaptureMaskSnapshot()
        => new(
            EraseMask is null ? null : (byte[])EraseMask.Clone(),
            _eraseMaskSpecified,
            PaintMask is null ? null : (byte[])PaintMask.Clone(),
            _paintMaskSpecified,
            MaskWidth,
            MaskHeight);

    /// <summary>原厂 fun2 仅在已生成且有轮廓点的模型上可用。</summary>
    public bool EditOutlinePointsEnable
    {
        get
        {
            try
            {
                // 必须读“当前有效载荷”（优先 _pendingTemplatePayload），不能直接读草稿：
                // 生成模型后新载荷还没写回草稿，直接读草稿会让「擦除轮廓点」一直禁用。
                var payload = ReadModelPayload();
                if (payload.Length == 0) return false;
                var entries = ContourMatchTemplateSet.DecodeEntries(payload).ToList();
                // 新建模板尚未成条：没有可擦除的轮廓点，按钮保持禁用（原厂 fun2 同语义）。
                if (TemplateIndex >= entries.Count) return false;
                var index = Math.Clamp(TemplateIndex, 0, entries.Count - 1);
                return entries[index].Template.PointCount > 0;
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>
    /// 笔刷擦除：把 (x,y) 起、尺寸 size×size 的像素块写入掩膜。
    /// 语义对齐原厂 RubberControl.wipe（decompiled:301-350：按笔刷宽高覆盖轨迹像素）。
    /// </summary>
    public void WipeAt(double x, double y, int size)
    {
        if (_frameImage is null || size <= 0) return;
        var width = _frameImage.Width;
        var height = _frameImage.Height;
        if (width <= 0 || height <= 0) return;
        var mask = _wipeToPaintMask ? PaintMask : EraseMask;
        if (mask is null || MaskWidth != width || MaskHeight != height)
        {
            mask = new byte[width * height];
            if (_wipeToPaintMask) PaintMask = mask; else EraseMask = mask;
            MaskWidth = width;
            MaskHeight = height;
        }
        if (_wipeToPaintMask) _paintMaskSpecified = true; else _eraseMaskSpecified = true;

        // 原厂 Rublock：RubberControl 的笔刷光标是 size×size 方块（RubberControl.xaml:4 的
        // Border，DWidth/DHeight 同时绑定 PanRectWidth），拖动时按「上一笔 → 当前笔」连续覆盖，
        // 每次 MouseMove 都写一次掩膜（owner_OnDrawRoi_MouseMoveChange:242-295 → wipe）。
        // 这里沿轨迹插值填方块，等价于原厂逐帧覆盖，且不会因水平/垂直拖动退化。
        var fromX = _hasLastWipePoint ? _lastWipeX : x;
        var fromY = _hasLastWipePoint ? _lastWipeY : y;
        var distance = Math.Max(Math.Abs(x - fromX), Math.Abs(y - fromY));
        var steps = Math.Max(1, (int)Math.Ceiling(distance));
        for (var step = 0; step <= steps; step++)
        {
            var t = step / (double)steps;
            FillBrush(mask, width, height, fromX + (x - fromX) * t, fromY + (y - fromY) * t, size);
        }
        _lastWipeX = x;
        _lastWipeY = y;
        _hasLastWipePoint = true;
        EraseMaskChanged?.Invoke();
    }

    private static void FillBrush(byte[] mask, int width, int height, double x, double y, int size)
    {
        var half = Math.Max(0, size / 2);
        var left = Math.Clamp((int)Math.Round(x) - half, 0, width - 1);
        var right = Math.Clamp((int)Math.Round(x) + half, 0, width - 1);
        var top = Math.Clamp((int)Math.Round(y) - half, 0, height - 1);
        var bottom = Math.Clamp((int)Math.Round(y) + half, 0, height - 1);
        for (var row = top; row <= bottom; row++)
            Array.Fill(mask, (byte)1, row * width + left, right - left + 1);
    }

    /// <summary>
    /// 原厂 RubberControl.OnRubberFinish 的轨迹收尾（RubberControl.xaml.cs:227-240）：
    /// 重置 oldEnd 并触发 OnRubberFinish → 编辑器记录一次历史，撤销才能回到擦除前。
    /// 拖动过程中每帧都 Snapshot 会爆历史栈，所以只在抬手时记一次。
    /// </summary>
    public void EndWipe()
    {
        if (!_hasLastWipePoint) return;
        _hasLastWipePoint = false;
        Snapshot();
    }

    /// <summary>清空擦除掩膜（切回箭头/换形状时调用）。</summary>
    public void ClearEraseMask()
    {
        // 对齐原厂 DoClear：当前会话同时清除 otherMask 与 otherMask2；
        // 两层分别标记为已明确清空，后续建模不得从旧模板载荷回退。
        _eraseMaskSpecified = true;
        _paintMaskSpecified = true;
        EraseMask = null;
        PaintMask = null;
        MaskWidth = 0;
        MaskHeight = 0;
        _hasLastWipePoint = false;
        EraseMaskChanged?.Invoke();
    }

    private sealed record HistorySnapshot(
        RecordValue Document,
        byte[]? EraseMask,
        byte[]? PaintMask,
        int Width,
        int Height,
        double FixPointX,
        double FixPointY,
        bool IsSetCrossByManual,
        bool EraseMaskSpecified,
        bool PaintMaskSpecified);
        // 原厂 RecordHistory（UI:779-803）只写 ItemsData / otherMask / otherMask2 / FixPointX/Y，
        // 并在保存前显式把 ModelData 置 null（:789）：历史快照**不包含模型载荷**。
        // 撤销只回退样本、掩膜与匹配中心，模型载荷保持在当前状态（需要时再重新生成）。

    private readonly List<HistorySnapshot> _history = new();
    private int _historyIndex = -1;
    private bool _confirmed;
    private bool _modelDirty;
    public bool Confirmed => _confirmed;

    /// <summary>
    /// 原厂 XAML 的 PxSlider 用 CallMethodAction 在 DataContext 上回调用这两个方法
    /// （原厂 DataContext 是 HPFeaturepatMatchDialogUI）。本方浮层 DataContext 是
    /// TemplateConfigData，缺方法会让滑块 DragCompleted 抛
    /// “Could not find method named 'ThumbMouseLeftButtonUp'” 并中断拖动。
    /// 原厂用途是拖笔刷尺寸期间抑制画布 ROI 绘制；本方浮层与画布分离，无需额外状态。
    /// </summary>
    public void ThumbMouseLeftButtonDown() { }

    /// <summary>见 <see cref="ThumbMouseLeftButtonDown"/>。</summary>
    public void ThumbMouseLeftButtonUp() { }

    /// <summary>
    /// 抑制建模成功/失败的模态提示。自动化测试直接执行 ConfirmCommand/MatchCommand
    /// 时会走到 MatchModel，若不抑制会在测试宿主里弹出阻塞式对话框，弹到用户桌面上
    /// 并表现为“界面一直报模型创建失败”。生产路径保持默认 false。
    /// </summary>
    public bool SuppressDialogs { get; set; }

    public TemplateConfigData(
        IToolSettingsDraft draft,
        Action<FieldSettings>? acceptCandidate,
        Action createTemplate,
        Action saveProject,
        Action loadProject,
        RecordValue document,
        NativeRoiFrameSource? frames,
        Action<RecordValue>? acceptDocument = null,
        int templateIndex = 0,
        Func<TemplateImageSnapshot?, RecordValue, TemplateMaskSnapshot, string?>? createTemplateWithMasks = null,
        Action<string, string>? commitTemplate = null)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _acceptDocument = acceptDocument ?? (_ => { });
        TemplateIndex = templateIndex;
        _createTemplate = createTemplate;
        _createTemplateWithMasks = createTemplateWithMasks;
        _commitTemplate = commitTemplate;
        _saveProject = saveProject;
        _loadProject = loadProject;
        _frames = frames;
        _document = document;
        SelectedROIList = _items;
        // 原厂轮廓匹配的符号列表只有两项：匹配(Lang_Match) + 屏蔽(Lang_Blind)。
        // 证据：decompiled HPFeaturepatMatchDialogUI.InitSign（:515-535）追加三项中的首尾，
        // HPPyramidFeaturepatMatchDialogUI.CustomInitSign 为空实现（不追加"增强匹配"）。
        SignList.Add(SignChoice.Match);
        SignList.Add(SignChoice.Blind);
        LoadFromDraft();

        SelectCurImageCommand = new DelegateCommand(SelectCurrentImage);
        SelectOtherImageCommand = new DelegateCommand(SelectOtherImage);
        SaveCommand = new DelegateCommand(SaveModelProject);
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
        _eraseMaskSpecified = false;
        _paintMaskSpecified = false;
        var fields = _draft.CaptureFields();
        // 枚举字段在草稿里按枚举名（Text）存储，整数按 long 存储，两者都要能读回。
        int Int(string id) => fields.Read(id) switch
        {
            ConstantSetting { Value: long value } => (int)value,
            ConstantSetting { Value: int value } => value,
            ConstantSetting { Value: string name } => EnumOrdinal(id, name),
            _ => 0
        };
        static int EnumOrdinal(string id, string name)
        {
            // id 是"field:GUID"形式的字段标识，必须按 Id 查声明（Spec 只接受属性名）。
            var type = ContourMatchFields.SpecById(id).Type;
            return type.IsEnum && Enum.TryParse(type, name, ignoreCase: false, out var value)
                ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }
        PyramidScaleFlag = Int(ContourMatchFields.PyramidMode);
        PyramidScaleLevel = Int(ContourMatchFields.PyramidScaleLevel);
        PyramidScaleRLevel = Int(ContourMatchFields.PyramidScaleRLevel);
        EdgeThresholdFlag = Int(ContourMatchFields.EdgeThresholdMode);
        EdgeThreshold = Int(ContourMatchFields.EdgeThreshold);
        OptimizationIndex = Int(ContourMatchFields.Optimization);
        MinContrast = Int(ContourMatchFields.MinContrast);
        SubPixelIndex = Int(ContourMatchFields.SubPixel);
        _history.Add(CaptureHistory());
        _historyIndex = 0;
        _modelDirty = false;
        RebuildRoiLists();
        // 原厂语义（decompiled FeaturepatMatchConfigPanalData.cs:279 新建 / :313-333 编辑）：
        // - 新建模板：取模块当前帧（GetCurImage）
        // - 编辑已有模板：必须用模板里存档的原图（CurImageDataA），不会再取当前帧
        var payload = fields.Read(ContourMatchFields.ModelData) is ConstantSetting { Value: string text } ? text : "";
        _pendingTemplatePayload = payload.Length == 0 ? null : payload;
        if (payload.Length == 0)
        {
            SelectCurrentImage();
            Raise(nameof(EditOutlinePointsEnable));
            return;
        }
        try
        {
            var entries = ContourMatchTemplateSet.DecodeEntries(payload);
            if (TemplateIndex >= entries.Count)
            {
                // 新建模板：OpenCreate 把 _editIndex 设成追加位（entries.Count），
                // 不能把它当成“编辑已有模板”，否则画布会加载上一个模板的原图与轮廓点。
                SelectCurrentImage();
                Raise(nameof(EditOutlinePointsEnable));
                return;
            }
            var template = entries[Math.Clamp(TemplateIndex, 0, entries.Count - 1)].Template;
            if (template.Image is not { Length: > 0 } image)
            {
                // 原厂 :313-318：模型不包含原图时不允许编辑。
                EditBlockedReason = "模型不包含原图，不允许编辑。请重新创建模板，或在创建时保持模板存图开启。";
                System.Windows.MessageBox.Show(EditBlockedReason, "模板配置",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _frameImage = new FrameImageData(image, template.ImageWidth, template.ImageHeight);
            ImageDataSource = _frameImage;
            NormalizeLoadedRoiItems(_frameImage.Width, _frameImage.Height);
            // VM 的 FixPointX/Y 在底栏和 PointsData 中都是当前图像像素坐标；
            // 文档里的 ROI 几何仍保持 0~1 归一化。
            // 原厂模板保存了匹配中心，编辑同一模板时优先恢复；旧载荷没有该段才用样本中心重算。
            if (template.FixPointX is { } storedFixX && template.FixPointY is { } storedFixY)
            {
                FixPointX = storedFixX;
                FixPointY = storedFixY;
            }
            else
            {
                AutoSetFixPoint();
            }
            // 原厂 ModelItem 同时保存 otherMask 与 otherMask2；编辑既有模板时必须把两者
            // 恢复到各自的画笔状态，不能把它们合并后再显示，否则切换工具会丢失另一层。
            EraseMask = template.Mask is { Length: > 0 } mask ? (byte[])mask.Clone() : null;
            PaintMask = template.Mask2 is { Length: > 0 } mask2 ? (byte[])mask2.Clone() : null;
            MaskWidth = template.MaskWidth > 0 ? template.MaskWidth : template.Mask2Width;
            MaskHeight = template.MaskHeight > 0 ? template.MaskHeight : template.Mask2Height;
            Raise(nameof(ImageDataSource));
            RefreshModelOutline(template);
            EraseMaskChanged?.Invoke();
            Raise(nameof(EditOutlinePointsEnable));
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            EditBlockedReason = "模板数据无法解析，不能编辑：" + error.Message;
            System.Windows.MessageBox.Show(EditBlockedReason, "模板配置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>编辑被拒绝的原因（非空表示本次窗口不可用于编辑）。</summary>
    public string? EditBlockedReason { get; private set; }

    /// <summary>特殊 ROI（原厂 SpecialRoiList）：匹配中心十字等不参与训练的区域。</summary>
    private readonly ObservableCollection<object> _specialRoiList = new();

    /// <summary>
    /// 画布上的匹配中心图元。FixPointX/Y 只是底栏数值，必须在 SpecialRoiList 里
    /// 有一个对应十字，画布才看得到匹配中心（原厂 SpecialRoiList 里的 CrossROI）。
    /// </summary>
    private CrossRoiItem? _crossRoi;

    /// <summary>
    /// 按当前 FixPointX/Y 同步匹配中心十字；没有就创建。
    /// 现象对齐原厂：画布始终有一个中心标记，位置随三阶段变化——
    /// ① 打开时 InitData 把 FixPoint 清零（HPFeaturepatMatchDialogUI.cs:837-848）→ 左上角；
    /// ② 画完样本后 AutoSetFixPoint 取样本中心（HPFeaturepatMatchDialogData.cs:497-551）→ 区域中心；
    /// ③ 生成模型后 RefreshPoints 用 Find 结果定位（:1746-1771）→ 真实匹配中心。
    /// </summary>
    private void SyncCrossRoi()
    {
        var width = _frameImage?.Width > 0 ? _frameImage.Width : 0;
        var height = _frameImage?.Height > 0 ? _frameImage.Height : 0;
        if (width == 0 || height == 0) return;
        // CrossROI 的 CenterPoint 是图像像素坐标：CrossROI.Draw 只在实现 INormalizedROI 时
        // 才除宽高（CrossROI.cs:41-49），ChangeContainerPosition 直接乘 ControlToImageRadio
        // （:60-63）。CrossRoiItem 不实现 INormalizedROI，因此这里必须传像素值；
        // 传归一化值会让标记永远贴在左上角。
        var center = new Point(FixPointX, FixPointY);
        if (_crossRoi is null)
        {
            _crossRoi = new CrossRoiItem { Visible = true, IsPositive = true, CenterPoint = center };
            _specialRoiList.Add(_crossRoi);
            // 只有新增图元才需要让 ROI 列表重建；频繁 Raise 会连带清掉轮廓点层。
            Raise(nameof(ROILists));
            return;
        }
        // 已有图元改数据即可：CrossROI.CrossCenterPoint 双向绑定 ICrossROI.CenterPoint，
        // 依赖属性随 PropertyChanged 更新位置，不需要重建整个 ROI 层。
        _crossRoi.CenterPoint = center;
    }

    /// <summary>
    /// 本方 RoiDocumentValue → ImageViewBox 期望的 ROI 列表。
    /// 与原厂同构（HPFeaturepatMatchDialogData.RebuildROILists:456-466）：
    /// [0] = 全部样本（原厂 ItemsData，正样本与背景样本都在里面，靠 Sign 列区分）；
    /// [1] = 特殊 ROI（原厂 SpecialRoiList，匹配中心十字）。
    /// 此前误写成「[0]=正样本、[1]=背景样本」，画布显示语义与原厂不一致。
    /// </summary>
    private void RebuildRoiLists()
    {
        _items.Clear();
        // 原厂 DoClear（HPFeaturepatMatchDialogUI.cs:870-889）与 RebuildROILists
        // （HPFeaturepatMatchDialogData.cs:456-461）都不清 SpecialRoiList：
        // 匹配中心十字必须保留，不能随样本列表重建一起丢掉。
        ROILists.Clear();
        foreach (var region in RoiDocumentValue.Items(_document, RoiDocumentValue.Inspection))
        {
            var item = ToRoiItem(region, true, _items.Count + 1);
            item.PropertyChanged += OnRoiPropertyChanged;
            _items.Add(item);
        }
        foreach (var region in RoiDocumentValue.Items(_document, RoiDocumentValue.Mask))
        {
            var item = ToRoiItem(region, false, _items.Count + 1);
            item.PropertyChanged += OnRoiPropertyChanged;
            _items.Add(item);
        }
        ROILists.Add(_items);
        ROILists.Add(_specialRoiList);
        // 原厂 ItemsData_CollectionChanged（HPFeaturepatMatchDialogUI.cs:850-863）先 AutoSetFixPoint；
        // 样本增删后匹配中心要重算，画布标记跟着移动（空列表时回左上角）。
        AutoSetFixPoint();
        SyncCrossRoi();
        Raise(nameof(ItemsData)); Raise(nameof(ROILists));
        RoiVisualChanged?.Invoke();
    }

    private HistorySnapshot CaptureHistory()
        => new(_document,
            EraseMask is null ? null : (byte[])EraseMask.Clone(),
            PaintMask is null ? null : (byte[])PaintMask.Clone(),
            MaskWidth, MaskHeight, FixPointX, FixPointY, IsSetCrossByManual,
            _eraseMaskSpecified, _paintMaskSpecified);

    private void RestoreHistory(HistorySnapshot state)
    {
        _document = state.Document;
        EraseMask = state.EraseMask is null ? null : (byte[])state.EraseMask.Clone();
        PaintMask = state.PaintMask is null ? null : (byte[])state.PaintMask.Clone();
        MaskWidth = state.Width;
        MaskHeight = state.Height;
        FixPointX = state.FixPointX;
        FixPointY = state.FixPointY;
        IsSetCrossByManual = state.IsSetCrossByManual;
        _eraseMaskSpecified = state.EraseMaskSpecified;
        _paintMaskSpecified = state.PaintMaskSpecified;
        // 原厂 ChangeHistory（UI:805-820）只恢复 ItemsData / 两层掩膜 / FixPoint，
        // 不回退 ModelData（RecordHistory:789 保存前已把 ModelData 置 null）。
        // 撤销后样本与模型不再匹配，这里标记为待重建。
        _modelDirty = true;
        RebuildRoiLists();
        Raise(nameof(FixPointX));
        Raise(nameof(FixPointY));
        Raise(nameof(EditOutlinePointsEnable));
        EraseMaskChanged?.Invoke();
    }

    private void OnRoiPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(TemplateRoiItem.SignIndex)
            or nameof(TemplateRoiItem.Visible)
            or nameof(TemplateRoiItem.CenterPoint)
            or nameof(TemplateRoiItem.Width)
            or nameof(TemplateRoiItem.Height)
            or nameof(TemplateRoiItem.Angle)
            or nameof(TemplateRoiItem.Radius)
            or nameof(TemplateRoiItem.MinRadius)
            or nameof(TemplateRoiItem.StartAngle)
            or nameof(TemplateRoiItem.EndAngle)
            or nameof(TemplateRoiItem.PolygonPoints))
        {
            // 原厂 RoiParamChange（HPFeaturepatMatchDialogData.cs:492-495）：
            // 样本几何变化后重算匹配中心，画布标记跟随。
            AutoSetFixPoint();
            // TemplateConfigWindow uses the same ItemsData notification to rebuild
            // the VM DrawMask-compatible ROI overlay when an existing ROI is moved
            // or resized.  CollectionChanged alone does not cover geometry edits.
            Snapshot();
            Raise(nameof(ItemsData));
            RoiVisualChanged?.Invoke();
        }
    }

    /// <summary>
    /// 原厂 HPFeaturepatMatchDialogData.AutoSetFixPoint：未锁定且没有手动十字时，
    /// 用所有训练 ROI 的几何中心平均值作为匹配中心；空列表回到 (0,0)。
    /// </summary>
    private void AutoSetFixPoint()
    {
        if (MatchPointLock) return;
        if (_items.Count == 0)
        {
            // 原厂空样本分支（HPFeaturepatMatchDialogData.cs:545-550）：
            // 清掉手动标记并把中心归零，画布标记回到图像左上角。
            IsSetCrossByManual = false;
            FixPointX = 0;
            FixPointY = 0;
            return;
        }
        // 用户手动指定过中心就不再被自动中心覆盖（原厂 IsSetCrossByManual 语义）。
        if (IsSetCrossByManual) return;
        var centerX = _items.Average(CenterX);
        var centerY = _items.Average(CenterY);
        var width = _frameImage?.Width > 0 ? _frameImage.Width : 1;
        var height = _frameImage?.Height > 0 ? _frameImage.Height : 1;
        FixPointX = centerX * width;
        FixPointY = centerY * height;

        static double CenterX(TemplateRoiItem item)
            => item.Kind == "polygon" && item.PolygonPoints.Count > 0
                ? (item.PolygonPoints.Min(p => p.X) + item.PolygonPoints.Max(p => p.X)) / 2d
                : item.CenterPoint.X;

        static double CenterY(TemplateRoiItem item)
            => item.Kind == "polygon" && item.PolygonPoints.Count > 0
                ? (item.PolygonPoints.Min(p => p.Y) + item.PolygonPoints.Max(p => p.Y)) / 2d
                : item.CenterPoint.Y;
    }

    /// <summary>
    /// 文档 item → 样本对象。<paramref name="entry"/> 是 RoiDocumentValue.Item 的结构
    /// （id/name/enabled/visible/shape），几何在 shape 子记录里，必须按文档声明读取。
    /// </summary>
    private static TemplateRoiItem ToRoiItem(RecordValue entry, bool positive, int index)
    {
        var shape = entry.Get<RecordValue>("shape");
        var kind = shape.Get<string>("kind");
        // 按形状创建具体数据类：列表统一以 TemplateRoiItem 承载，绘制时才需要具体接口类型。
        TemplateRoiItem item = kind switch
        {
            "polygon" => new PolygonRoiItem(),
            "circle" => new CircleRoiItem(),
            "sector" => new SectorRoiItem(),
            "point" => new PointRoiItem(),
            "cross" => new CrossRoiItem(),
            _ => new RectRoiItem()
        };
        item.IsPositive = positive;
        item.Index = index;
        // 保留原身份与名称：往返文档后名称不丢失（原厂 MatchModelName 依赖样本名）。
        item.Name = entry.Get<string?>("name") ?? "";
        item.Id = Guid.TryParse(entry.Get<string?>("id"), out var id) ? id : Guid.NewGuid();
        item.Enabled = entry.Get<bool?>("enabled") ?? true;
        item.Visible = entry.Get<bool?>("visible") ?? true;
        if (kind == "polygon")
        {
            foreach (var point in shape.Get<SequenceValue>("vertices").Cast<RecordValue>())
                item.PolygonPoints.Add(new Point(point.Get<double>("x"), point.Get<double>("y")));
            if (item.PolygonPoints.Count > 0)
                item.CenterPoint = new Point(item.PolygonPoints.Average(p => p.X), item.PolygonPoints.Average(p => p.Y));
        }
        else
        {
            if (shape.Contains("center"))
            {
                var center = shape.Get<RecordValue>("center");
                item.CenterPoint = new Point(center.Get<double>("x"), center.Get<double>("y"));
            }
            if (shape.Contains("width")) item.Width = shape.Get<double>("width");
            if (shape.Contains("height")) item.Height = shape.Get<double>("height");
            if (shape.Contains("angle-degrees")) item.Angle = shape.Get<double>("angle-degrees");
            if (shape.Contains("outer-radius")) item.Radius = shape.Get<double>("outer-radius");
            if (shape.Contains("inner-radius")) item.MinRadius = shape.Get<double>("inner-radius");
            if (shape.Contains("start-radians")) item.StartAngle = shape.Get<double>("start-radians") * 180d / Math.PI;
            if (shape.Contains("end-radians")) item.EndAngle = shape.Get<double>("end-radians") * 180d / Math.PI;
            // 圆/扇形若只有半径没有宽高，用直径补齐（与原厂半径语义对齐）。
            if (item.Width <= 0 && item.Radius > 0) item.Width = item.Radius * 2;
            if (item.Height <= 0 && item.Radius > 0) item.Height = item.Radius * 2;
        }
        return item;
    }

    /// <summary>
    /// TemplateTrainingCodec 持久化的是 HALCON 训练所需的像素坐标，而编辑器 ROI
    /// 使用 0~1 归一化坐标。原厂加载 ModelItem 时完成同一层转换；若漏掉这里，
    /// 重新打开模板会把 1280×960 的像素值再次乘图像尺寸，导致 ROI 与覆盖层完全跑出画面。
    /// </summary>
    private void NormalizeLoadedRoiItems(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return;
        var radial = Math.Min(imageWidth, imageHeight);
        foreach (var item in _items)
        {
            if (Math.Abs(item.CenterPoint.X) > 1.5 || Math.Abs(item.CenterPoint.Y) > 1.5)
                item.CenterPoint = new System.Windows.Point(item.CenterPoint.X / imageWidth, item.CenterPoint.Y / imageHeight);
            if (Math.Abs(item.Width) > 1.5) item.Width /= imageWidth;
            if (Math.Abs(item.Height) > 1.5) item.Height /= imageHeight;
            if (Math.Abs(item.Radius) > 1.5) item.Radius /= radial;
            if (Math.Abs(item.MinRadius) > 1.5) item.MinRadius /= radial;
            for (var index = 0; index < item.PolygonPoints.Count; index++)
            {
                var point = item.PolygonPoints[index];
                if (Math.Abs(point.X) > 1.5 || Math.Abs(point.Y) > 1.5)
                    item.PolygonPoints[index] = new System.Windows.Point(point.X / imageWidth, point.Y / imageHeight);
            }
        }
        Raise(nameof(ItemsData));
        RoiVisualChanged?.Invoke();
    }

    /// <summary>
    /// 取当前图像并显示，等价原厂 HPFeaturepatMatchDialogUI.GetCurImage（decompiled:946-988）：
    /// 打开模板配置即主动取模块首帧并赋给 CurImage → ShowImage → ImageDataSource。
    /// 原厂触发点：decompiled:FeaturepatMatchConfigPanalData.cs:279（新建模板时 GetCurImage()）。
    /// 差异登记：原厂要求 Gray8 否则报 Lang_UnsupportFormatWarning；原厂编辑已有模板用持久化的
    /// CurImageDataA（ModelItem.cs 的 SolModelFile_{Index}_CurImageData 槽），无原图则拒绝编辑——本方尚未持久化模板原图。
    /// </summary>
    private void SelectCurrentImage()
    {
        if (_frames is null) return;
        // 原厂 GetCurImage 从模块输入关系直接取首帧，不要求结果视口当前显示输入图。
        // 先走宿主的正式输入捕获，避免当前结果区显示输出图时 Borrow 被显示选择门控。
        try
        {
        using (var inputs = _frames.CaptureInputs(new[] { ContourMatchSchema.ImageInput }))
        {
            if (inputs is { IsCurrent: true } && inputs.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput) is { } image)
            {
                if (image.Layout.Format != PixelLayout.Gray8)
                {
                    System.Windows.MessageBox.Show(
                        "当前图像不是 8 位灰度图，模板配置仅支持灰度图像。",
                        "不支持格式", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _frameImage = new FrameImageData(image);
                ImageDataSource = _frameImage;
                Raise(nameof(ImageDataSource));
                ResetConfigCore();
                return;
            }
        }
        }
        catch (InvalidOperationException)
        {
            // 输入预览尚未就绪时回退到当前可见帧借用；保持窗口可打开并等待宿主后续刷新。
        }
        // 输入捕获不可用时保留当前帧借用回退；它仍会遵守宿主的当前帧/生命周期证明。
        using var lease = _frames.Borrow(ContourMatchSchema.ImageInput, interactive: false);
        if (lease is null || !lease.IsCurrent) return;
        if (lease.Image.Layout.Format != PixelLayout.Gray8)
        {
            System.Windows.MessageBox.Show(
                "当前图像不是 8 位灰度图，模板配置仅支持灰度图像。",
                "不支持格式", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _frameImage = new FrameImageData(lease.Image);
        ImageDataSource = _frameImage;
        Raise(nameof(ImageDataSource));
        ResetConfigCore();
    }

    /// <summary>原厂 AddROICommandExecute（HPFeaturepatMatchDialogData.cs:469-490）语义。</summary>
    private void AddRoi(object? roi)
    {
        IsStateForDrawROI = false;
        // 原厂 hikCross 只进入 SpecialRoiList，用于匹配中心，不进入训练样本 ItemsData。
        if (roi is CrossRoiItem crossRoi)
        {
            foreach (var existing in _specialRoiList.OfType<CrossRoiItem>().ToArray())
                _specialRoiList.Remove(existing);
            _specialRoiList.Add(crossRoi);
            _crossRoi = crossRoi;
            // CrossROI 的 CenterPoint 是图像像素坐标（非归一化），直接取用。
            FixPointX = crossRoi.CenterPoint.X;
            FixPointY = crossRoi.CenterPoint.Y;
            IsSetCrossByManual = true;
            SelectedROIList = _specialRoiList;
            Raise(nameof(FixPointX));
            Raise(nameof(FixPointY));
            Raise(nameof(SelectedROIList));
            Raise(nameof(ROILists));
            Snapshot();
            return;
        }
        if (roi is ICrossROI cross)
        {
            FixPointX = cross.CenterPoint.X;
            FixPointY = cross.CenterPoint.Y;
            IsSetCrossByManual = true;
            Raise(nameof(FixPointX));
            Raise(nameof(FixPointY));
            Snapshot();
            return;
        }
        if (roi is TemplateRoiItem item)
        {
            // ImageViewBox.AddROI 已先把 ROI 放入 SelectedROIList；原厂 AddRoiDelegate
            // 仍会为新项挂 DataChangedCommand，集合变更再负责自动中心、历史和匹配。
            // 不能因列表中已存在就跳过索引、订阅和快照，否则生成模型/确定读取不到完整文档。
            if (!_items.Contains(item))
                _items.Add(item);
            item.SetIndex(_items.IndexOf(item) + 1);
            // 橡皮擦绘制的区域作为背景样本（从训练域扣除），其余为前景正样本。
            if (_drawAsBackground) item.IsPositive = false;
            _drawAsBackground = false;
            item.Name = string.IsNullOrWhiteSpace(item.Name)
                ? (item.IsPositive ? DefaultRoiName(item) : "擦除")
                : item.Name;
            item.PropertyChanged -= OnRoiPropertyChanged;
            item.PropertyChanged += OnRoiPropertyChanged;
            AutoSetFixPoint();
            Raise(nameof(ItemsData));
            Raise(nameof(ROILists));
            Snapshot();
            RoiVisualChanged?.Invoke();
        }
    }

    private static string DefaultRoiName(TemplateRoiItem item)
        => (item.Kind switch
        {
            "rectangle" => "矩形",
            "polygon" => "多边形",
            "circle" => "圆形",
            "sector" => "扇形",
            "point" => "点",
            // 防御：匹配中心不该进训练样本列表，真出现时也别叫“区域N”。
            "cross" => "匹配中心",
            _ => "区域"
        }) + item.Index;
    private void DeleteRoi(object? roi)
    {
        if (roi is not TemplateRoiItem item) return;
        if (ReferenceEquals(_crossRoi, item)) _crossRoi = null;
        _items.Remove(item);
        _specialRoiList.Remove(item);
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index].SetIndex(index + 1);
        }
        // 原厂 ItemsData_CollectionChanged（UI:850-863）：非空 → AutoSetFixPoint，
        // 空 → DoClear（清两层掩膜、ShapeList、外部点层；不清 FixPoint）。
        if (_items.Count == 0)
        {
            ClearEraseMask();
            _modelOutline = null;
            _shapeList.Clear();
            Raise(nameof(ShapeList));
        }
        else
        {
            AutoSetFixPoint();
        }
        Raise(nameof(ItemsData));
        Raise(nameof(ROILists));
        Raise(nameof(EditOutlinePointsEnable));
        Snapshot();
        RoiVisualChanged?.Invoke();
    }

    private void ClearRois()
    {
        _items.Clear();
        _specialRoiList.Clear();
        _shapeList.Clear();
        ClearEraseMask();
        if (!MatchPointLock)
        {
            IsSetCrossByManual = false;
            FixPointX = 0;
            FixPointY = 0;
            Raise(nameof(FixPointX));
            Raise(nameof(FixPointY));
        }
        IsStateForDrawROI = false;
        Raise(nameof(ItemsData));
        Raise(nameof(ROILists));
        Raise(nameof(ShapeList));
        Snapshot();
        RoiVisualChanged?.Invoke();
    }

    private void Snapshot()
    {
        // 编辑过程中样本列表已变化，_document 必须跟着重建，否则历史栈里压入的都是同一份，撤销/重做无效。
        _document = BuildDocument();
        if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(CaptureHistory());
        _historyIndex = _history.Count - 1;
        _modelDirty = true;
        Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable));
    }

    private void BackStep() { if (_historyIndex > 0) { _historyIndex--; RestoreHistory(_history[_historyIndex]); Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable)); } }
    private void ForwardStep() { if (_historyIndex < _history.Count - 1) { _historyIndex++; RestoreHistory(_history[_historyIndex]); Raise(nameof(BackEnable)); Raise(nameof(ForwardEnable)); } }

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
            var selected = new BitmapImageData(bitmap);
            // 原厂把所选文件写入 CurImage；后续 ShowPoints 必须使用这张图，
            // 不能只替换显示源而让建模入口继续捕获宿主当前帧。
            _frameImage = new FrameImageData(selected.Buffer, selected.Width, selected.Height);
            ImageDataSource = _frameImage;
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

    private TemplateImageSnapshot? CurrentImageSnapshot()
        => _frameImage is { PixelFormat: "Gray8" } frame
            ? new TemplateImageSnapshot((byte[])frame.GrayPixels.Clone(), frame.Width, frame.Height)
            : null;

    /// <summary>
    /// 原厂 SaveCommandExecute：先把当前参数/掩膜状态提交到模型载荷，再进入导出对话框。
    /// 导出文件格式由工具宿主提供，本命令不绕过宿主直接写文件。
    /// </summary>
    private void SaveModelProject()
    {
        // 原厂 SaveCommand 先走 ShowPoints，再打开导出对话框；因此当前窗口内
        // 尚未生成的 ROI、掩膜和模板原图不能被静默丢掉。
        ApplyDocument();
        ApplyTrainingParameters();
        ApplyEraseMask();
        if (_modelDirty || !HasValidModel(ReadModelPayload()))
        {
            MatchModel();
            if (_modelDirty || !HasValidModel(ReadModelPayload())) return;
        }
        ApplyTemplateImage();
        Snapshot();
        _saveProject();
    }

    /// <summary>生成模型（原厂 MatchCommandExecute :1499-1538）：先 Arrow 态、生成后退出绘制态。</summary>
    private void MatchModel()
    {
        IsStateForDrawROI = false;
        IsEraseMode = false;
        Raise(nameof(IsStateForDrawROI)); Raise(nameof(IsEraseMode));
        try
        {
            ApplyDocument();
            ApplyTrainingParameters();
            ApplyEraseMask();
            Snapshot();
            if (!HasUsableTrainingSource())
                throw new InvalidOperationException("请先绘制至少一个匹配样本，或使用画笔屏蔽区域后再生成模型。");
            var captured = CurrentImageSnapshot();
            string? generatedPayload = null;
            if (_createTemplateWithMasks is not null)
                generatedPayload = _createTemplateWithMasks(captured, BuildDocument(), CaptureMaskSnapshot());
            else
                _createTemplate();
            if (!string.IsNullOrWhiteSpace(generatedPayload))
                _pendingTemplatePayload = WithTrainingImage(MergeGeneratedTemplate(generatedPayload), captured);
            var after = ReadModelPayload();
            TemplateConfigDiagnostics.Write("match-model",
                $"delegate={_createTemplateWithMasks is not null};generated={generatedPayload?.Length ?? -1};after={after.Length}");
            if (!HasValidModel(after))
                // 两种失败原因完全不同，不能合并成一句：① 窗口没接建模委托（工具版本问题）；
                // ② 建模返回了载荷但读不回有效轮廓点（数据问题）。
                throw new InvalidOperationException(_createTemplateWithMasks is null
                    ? "模型创建未返回有效模板数据：当前窗口没有接入模型创建委托，请确认运行的是最新版本程序。"
                    : $"模型创建未返回有效模板数据：建模返回 {(generatedPayload?.Length ?? 0)} 字符，读回 {after.Length} 字符。");
            var createdEntries = ContourMatchTemplateSet.DecodeEntries(after).ToList();
            var createdIndex = Math.Clamp(TemplateIndex, 0, createdEntries.Count - 1);
            var created = createdEntries[createdIndex].Template;
            // 现象对齐原厂第三阶段：生成模型后匹配中心落在 Find 到的真实位置。
            // 手动指定过中心或已锁定时不覆盖（原厂 IsSetCrossByManual / MatchPointLock 语义）。
            if (!IsSetCrossByManual && !MatchPointLock && created.PreviewPose is { } pose)
            {
                FixPointX = pose.Column;
                FixPointY = pose.Row;
                Raise(nameof(FixPointX)); Raise(nameof(FixPointY));
            }
            // 原厂模板连匹配中心一起保存（_FixPointX/_FixPointY 随 BaseData），
            // 再次编辑同一模板时恢复，而不是用样本中心重算。
            created = created.WithFixPoint(FixPointX, FixPointY);
            createdEntries[createdIndex] = createdEntries[createdIndex] with { Template = created };
            _pendingTemplatePayload = ContourMatchTemplateSet.Encode(createdEntries);
            RefreshModelOutline(created);
            SyncCrossRoi();
            _modelDirty = false;
            Raise(nameof(EditOutlinePointsEnable));
            if (!SuppressDialogs)
                System.Windows.MessageBox.Show("创建模版成功", "模板配置", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            _modelDirty = true;
            Raise(nameof(EditOutlinePointsEnable));
            if (!SuppressDialogs)
                System.Windows.MessageBox.Show("模型创建失败：" + error.Message, "模板配置", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool HasUsableTrainingSource()
        => _items.Any(item => item.IsPositive && item.Enabled)
           || PaintMask is { Length: > 0 } mask && mask.Any(value => value != 0);

    private string ReadModelPayload()
        => _pendingTemplatePayload
           ?? (_draft.CaptureFields().Read(ContourMatchFields.ModelData) is ConstantSetting { Value: string text }
               ? text : "");

    private string MergeGeneratedTemplate(string singleTemplatePayload)
    {
        var generated = ContourMatchTemplate.Decode(singleTemplatePayload);
        var entries = new List<ContourMatchTemplateSet.Entry>();
        var current = _draft.CaptureFields().Read(ContourMatchFields.ModelData) is ConstantSetting { Value: string text }
            ? text : "";
        if (!string.IsNullOrWhiteSpace(current))
        {
            try { entries = ContourMatchTemplateSet.DecodeEntries(current).ToList(); }
            catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException) { entries.Clear(); }
        }
        var index = Math.Max(0, TemplateIndex);
        var entry = new ContourMatchTemplateSet.Entry(generated,
            index < entries.Count ? entries[index].Name : ContourMatchTemplateSet.NewTemplateName(index),
            index < entries.Count ? entries[index].Selected : true,
            index < entries.Count ? entries[index].Training : "");
        if (index < entries.Count) entries[index] = entry;
        else entries.Add(entry);
        return ContourMatchTemplateSet.Encode(entries);
    }

    /// <summary>
    /// 原厂 ModelItem 保存 CurImageData（decompiled HPFeaturepatMatchDialogUI.cs:1650-1656）：
    /// 「模板存图」开启时，本次建模用的训练原图随模板保存。右侧 98×98 预览的数据源是
    /// ModelImageData（由 CurImageData 裁剪而来），模板没有原图时预览只能回落到占位图，
    /// 之后也不允许再次编辑。生成模型即写入，不依赖「确定」按钮的调用顺序。
    /// </summary>
    private string WithTrainingImage(string payload, TemplateImageSnapshot? captured)
    {
        if (!IsSaveImage || captured is null) return payload;
        if (captured.Width <= 0 || captured.Height <= 0
            || captured.GrayPixels.Length < checked(captured.Width * captured.Height))
            throw new InvalidOperationException("训练原图像素与尺寸不符，无法随模板保存。");
        var entries = ContourMatchTemplateSet.DecodeEntries(payload).ToList();
        var index = Math.Clamp(TemplateIndex, 0, entries.Count - 1);
        entries[index] = entries[index] with
        {
            Template = entries[index].Template.WithImage(captured.GrayPixels, captured.Width, captured.Height)
        };
        return ContourMatchTemplateSet.Encode(entries);
    }

    private static bool HasValidModel(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return false;
        try { return ContourMatchTemplateSet.DecodeEntries(payload).Any(entry => entry.Template.PointCount > 0); }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// 对齐原厂 RefreshPoints：把已创建模型的 HALCON 轮廓投影到 ImageViewBox。
    /// 模型轮廓保持原生模型坐标，按训练匹配中心平移到当前图像像素坐标后，
    /// 交给共享 MultiPointShape 渲染，不能用矩形 ROI 伪造模板轮廓。
    /// </summary>
    private void RefreshModelOutline(ContourMatchTemplate template)
    {
        // 原厂 RefreshPoints（HPFeaturepatMatchDialogUI.cs:1746-1771）复用 ShapeList 里已有的
        // IMultiPoint，只清空点集后重填，不每轮新建容器。
        var batch = _modelOutline ??= new MultiPoint();
        batch.MicroPoints.Clear();
        if (_frameImage is null || template.PointCount <= 0)
        {
            PublishModelOutline(batch);
            return;
        }

        var modelPoints = template.ReadOutlinePoints();
        TemplateConfigDiagnostics.Write("outline-read", $"points={modelPoints.Count};pose={(template.PreviewPose is null ? "fallback" : "find")};fix={FixPointX:0.######},{FixPointY:0.######};image={_frameImage.Width}x{_frameImage.Height}");
        if (modelPoints.Count == 0)
        {
            PublishModelOutline(batch);
            return;
        }

        // FixPointX/Y already follow the VM pixel-coordinate contract.
        var pose = template.PreviewPose;
        var referenceColumn = pose?.Column ?? FixPointX;
        var referenceRow = pose?.Row ?? FixPointY;
        var angle = pose?.Angle ?? 0;
        var scaleX = pose?.ScaleX ?? 1;
        var scaleY = pose?.ScaleY ?? 1;
        if (!double.IsFinite(referenceColumn) || !double.IsFinite(referenceRow)
            || !double.IsFinite(angle) || !double.IsFinite(scaleX) || !double.IsFinite(scaleY)
            || scaleX <= 0 || scaleY <= 0)
            throw new InvalidOperationException("模板 Find 姿态不是有效图像坐标。");
        // 原厂 ShowPoints/RefreshPoints 只刷新 ShapeList，不把 Find 预览姿态写回
        // FixPointX/Y。匹配中心的来源只有 InitData 清零、ROI 自动中心或用户
        // CrossROI 手动设置；否则生成一次模板后底栏会被错误地改成 Find 结果。
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);

        var minX = double.PositiveInfinity; var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity; var maxY = double.NegativeInfinity;
        foreach (var point in modelPoints)
        {
            // GetShapeModelContours 的点以模型参考点 (0,0) 为原点；
            // 用创建后 Find 返回的 row/column/angle/scale 做同一套仿射变换。
            var imagePoint = new Point(
                referenceColumn + cos * scaleX * point.X - sin * scaleY * point.Y,
                referenceRow + sin * scaleX * point.X + cos * scaleY * point.Y);
            minX = Math.Min(minX, imagePoint.X); minY = Math.Min(minY, imagePoint.Y);
            maxX = Math.Max(maxX, imagePoint.X); maxY = Math.Max(maxY, imagePoint.Y);
            // 原厂 RefreshPoints 使用 MultiPoint.MicroPoints，而不是 OutlineData。
            // 原厂 MultiPointShape.CreatPointK 的公式按图像像素坐标计算：
            // (point + viewOffset) * viewScale。Find 仿射后的 imagePoint
            // 已经是当前图像像素坐标，不能再次归一化，否则会全部挤到左上角。
            batch.MicroPoints.Add(new MicroPoint(imagePoint.X, imagePoint.Y, ModelOutlineStroke));
        }
        TemplateConfigDiagnostics.Write("outline-published", $"microPoints={batch.MicroPoints.Count};bounds={minX:0.##},{minY:0.##}-{maxX:0.##},{maxY:0.##}");
        PublishModelOutline(batch);
    }

    /// <summary>原厂 RefreshPoints:1765 给模型轮廓点赋的 MicroPoint.Color。</summary>
    private const string ModelOutlineStroke = "#66FF00";

    /// <summary>
    /// 对齐原厂 UpdateShapeDisplay（HPFeaturepatMatchDialogUI.cs:1773-1782）：
    /// 清空后把同一个 IMultiPoint 挂回；外部点层（pointsImage）的清理与重绘
    /// 由窗口在 ShapeList 变更通知里执行（TemplateConfigWindow.DataPropertyChanged）。
    /// </summary>
    private void PublishModelOutline(MultiPoint batch)
    {
        _shapeList.Clear();
        _shapeList.Add(batch);
        Raise(nameof(ShapeList));
    }

    /// <summary>把当前擦除掩膜写入正在编辑的模板载荷（原厂 otherMask 随模型保存）。</summary>
    private void ApplyEraseMask()
    {
        var payload = ReadModelPayload();
        if (payload.Length == 0) return;
        try
        {
            var entries = ContourMatchTemplateSet.DecodeEntries(payload).ToList();
            // 新建模板（TemplateIndex = 追加位）此时还没有自己的条目：直接返回，
            // 否则 Math.Clamp 会把当前笔刷掩膜写到列表最后一个已有模板上。
            if (TemplateIndex >= entries.Count) return;
            var index = Math.Clamp(TemplateIndex, 0, entries.Count - 1);
            var updated = entries[index].Template.WithMasks(EraseMask, PaintMask, MaskWidth, MaskHeight);
            entries[index] = entries[index] with { Template = updated };
            var encoded = ContourMatchTemplateSet.Encode(entries);
            if (string.Equals(encoded, payload, StringComparison.Ordinal)) return;
            _pendingTemplatePayload = encoded;
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            System.Windows.MessageBox.Show("保存擦除掩膜失败：" + error.Message, "模板配置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static byte[]? MergeMasks(byte[]? first, byte[]? second, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;
        if (first is null && second is null) return null;
        var length = checked(width * height);
        var merged = new byte[length];
        if (first is not null)
            for (var i = 0; i < Math.Min(length, first.Length); i++) merged[i] |= first[i];
        if (second is not null)
            for (var i = 0; i < Math.Min(length, second.Length); i++) merged[i] |= second[i];
        return merged.Any(value => value != 0) ? merged : null;
    }

    /// <summary>形状切换（原厂 RadioButtonCommandExecute :1549-1580）：按形状名设 DrawROIType。</summary>
    /// <summary>
    /// 形状按钮（原厂 RadioButtonCommandExecute，decompiled:1549-1625）：
    /// 选中可绘制形状 → 设 DrawROIType **并置 IsStateForDrawROI=true 进入绘制态**，编辑目标设为本模板样本列表；
    /// 箭头/其它 → 退出绘制态。此前本方对所有分支都置 false，导致选中形状后画布无法绘制。
    /// </summary>
    private void SelectShape(object? parameter)
    {
        var radioButton = parameter as System.Windows.Controls.RadioButton;
        var name = parameter as string ?? radioButton?.Name ?? "";
        // 原厂命令同时绑定 Checked/Unchecked。Unchecked 只负责退出绘制态，
        // 不能再次进入同一形状，否则一次绘制完成后画布会继续接受第二个 ROI。
        if (radioButton is not null && radioButton.IsChecked != true)
        {
            IsStateForDrawROI = false;
            IsEraseMode = false;
            // 退出笔刷时收尾轨迹：原厂 UnRegisterRoi 会清 oldEnd，否则下次进入时
            // 首个点会被当成“上一笔”，画出跨屏长条。
            EndWipe();
            Raise(nameof(IsEraseMode));
            return;
        }
        // 必须传可实例化的具体数据类（框架 Draw 内部 Activator.CreateInstance(DrawROIType)），
        // 传接口会抛异常导致 mouseDown 崩溃。
        Type? draw = name switch
        {
            "hikFrame" or "Box" => typeof(RectRoiItem),
            "hikPolygon" or "Polygon" => typeof(PolygonRoiItem),
            "hikCoverCircle" or "Circle" => typeof(CircleRoiItem),
            "hikSector" => typeof(SectorRoiItem),
            // 原厂 hikCross 对应 CrossROIModel（ICrossROI），不是普通点 ROI。
            "hikCross" or "Cross" => typeof(CrossRoiItem),
            _ => null
        };
        if (draw is null)
        {
            // 箭头：退出绘制态（原厂 default 分支）。
            IsStateForDrawROI = false;
            Raise(nameof(IsStateForDrawROI));
            return;
        }
        DrawROIType = draw;
        // 原厂 RadioButtonCommandExecute:1595-1603：普通形状写 ItemsData，hikCross 写 SpecialRoiList。
        // hikCross 的数据类已从 PointRoiItem 换成 CrossRoiItem，判断必须跟着改，
        // 否则匹配中心会被当成训练样本加进 _items。
        SelectedROIList = draw == typeof(CrossRoiItem) ? _specialRoiList : _items;
        // 切回普通形状时退出笔刷擦除模式并收尾轨迹。
        _drawAsBackground = false;
        IsEraseMode = false;
        EndWipe();
        IsStateForDrawROI = true;
        Raise(nameof(IsEraseMode));
    }

    /// <summary>全部屏蔽（原厂 IsUsePenCommandExecute :1836-1860）：ResetConfig + 清匹配中心手动标志。</summary>
    private void ResetConfig()
    {
        // IsUsePen 已由 ToggleButton 的双向绑定更新；原厂命令只清理当前绘制状态，
        // 不能在这里再次取反，否则 Checked/Unchecked 都会恢复原状态。
        ResetConfigCore();
    }

    private void ResetConfigCore()
    {
        // 原厂换图 DoClear 只保留会话壳，不保留旧 ModelData；确定前必须重新生成。
        _pendingTemplatePayload = null;
        _items.Clear();
        _specialRoiList.Clear();
        _crossRoi = null;
        _shapeList.Clear();
        ClearEraseMask();
        IsSetCrossByManual = false;
        if (!MatchPointLock)
        {
            FixPointX = 0;
            FixPointY = 0;
            Raise(nameof(FixPointX));
            Raise(nameof(FixPointY));
        }
        IsStateForDrawROI = false;
        SelectedROIList = _items;
        _document = RoiDocumentValue.Empty;
        _history.Clear();
        _historyIndex = -1;
        Snapshot();
        Raise(nameof(ItemsData));
        Raise(nameof(ROILists));
        Raise(nameof(ShapeList));
        Raise(nameof(SelectedROIList));
        Raise(nameof(IsStateForDrawROI));
        Raise(nameof(EditOutlinePointsEnable));
        // 换图/重置后匹配中心归零，画布标记必须回到左上角（原厂 InitData:837-848）。
        SyncCrossRoi();
        RoiVisualChanged?.Invoke();
    }

    /// <summary>
    /// 橡皮擦（原厂 RubberCommandExecute，decompiled:1390-1410）：**笔刷擦像素**，
    /// 不是画几何区域——原厂用 RubberControl 在图上拖动，把轨迹像素写入 otherMask，
    /// 建模时从训练域扣除（rubber_OnWipe :1416-1490）。
    /// 本方：进入笔刷模式由窗口挂画布鼠标事件采集像素（TemplateConfigData.WipeAt）。
    /// </summary>
    private void RubberOn(object? parameter)
    {
        if (parameter is System.Windows.Controls.RadioButton radio && radio.IsChecked != true)
        {
            IsEraseMode = false;
            IsStateForDrawROI = false;
            EndWipe();
            Raise(nameof(IsEraseMode));
            return;
        }
        // 原厂 fun（绘制感兴趣区域/画笔）→ rubber2 → rubber2_OnWipe 写 otherMask2（屏蔽层）。
        _wipeToPaintMask = true;
        _drawAsBackground = false;
        // 原厂 RubberCommandExecute:1375-1376：DrawROIType = ToolROIModel + IsStateForDrawROI = true。
        // 框架在 mouseLBDown 里据此直接 Handled（ImageViewBox.xaml.cs:2142-2149），
        // 既不命中拖动已有 ROI，也不进入图像平移。
        DrawROIType = typeof(ToolRoiItem);
        IsStateForDrawROI = true;
        IsEraseMode = true;
        EndWipe();
        Raise(nameof(DrawROIType)); Raise(nameof(IsStateForDrawROI)); Raise(nameof(IsEraseMode));
    }

    /// <summary>画笔（原厂 Rubber2CommandExecute，笔刷尺寸取 PanRectWidth2）。</summary>
    private void Rubber2On(object? parameter)
    {
        if (parameter is System.Windows.Controls.RadioButton radio && radio.IsChecked != true)
        {
            IsEraseMode = false;
            IsStateForDrawROI = false;
            EndWipe();
            Raise(nameof(IsEraseMode));
            return;
        }
        // 原厂 fun2（擦除轮廓点/橡皮擦）→ rubber → rubber_OnWipe 写 otherMask（擦除层）。
        // 两个笔刷写的是不同层，不能由「全部屏蔽」IsUsePen 决定，否则擦除会写成屏蔽。
        _wipeToPaintMask = false;
        _drawAsBackground = false;
        // 原厂 Rubber2CommandExecute:1342-1343：DrawROIType = ToolROIModel + IsStateForDrawROI = true。
        // 框架在 mouseLBDown 里据此直接 Handled（ImageViewBox.xaml.cs:2142-2149），
        // 既不命中拖动已有 ROI，也不进入图像平移。
        DrawROIType = typeof(ToolRoiItem);
        IsStateForDrawROI = true;
        IsEraseMode = true;
        EndWipe();
        Raise(nameof(DrawROIType)); Raise(nameof(IsStateForDrawROI)); Raise(nameof(IsEraseMode));
        // 切工具后覆盖层要换成对应层（擦除=红、屏蔽=蓝）。
        Raise(nameof(ActiveMask));
        EraseMaskChanged?.Invoke();
    }

    /// <summary>本次绘制是否为擦除（背景样本）；画完一个区域后复位。</summary>
    private bool _drawAsBackground;

    /// <summary>
    /// 网格点击设匹配中心（原厂 GirdMouseDownCommandExecute，decompiled:1864-1873）：
    /// 处于选中心态且画布有有效像素坐标时取该点写入匹配中心，然后回到箭头态。
    /// </summary>
    private void SetCrossFromImage()
    {
        // 原厂用 CrossIsReady 门控网格点击；普通矩形/圆/多边形绘制态不能
        // 把首个鼠标点误当成匹配中心。
        if (!IsStateForDrawROI || DrawROIType != typeof(CrossRoiItem)) return;
        // 原厂锁定语义：锁定期间不允许改匹配中心（底栏输入框同样被禁用）。
        if (MatchPointLock) return;
        if (PixelProbe is { } probe)
        {
            var (x, y) = probe();
            if (x > 0 && y > 0)
            {
                FixPointX = x;
                FixPointY = y;
                IsSetCrossByManual = true;
                Raise(nameof(FixPointX));
                Raise(nameof(FixPointY));
                SyncCrossRoi();
            }
        }
        IsStateForDrawROI = false;
        Raise(nameof(IsStateForDrawROI));
    }

    /// <summary>画布当前像素坐标读取器（窗口注入 ImageViewBox.PixelX/PixelY）。</summary>
    public Func<(double X, double Y)>? PixelProbe { get; set; }

    private void ZoomOrigin() => ViewportAction?.Invoke("origin");
    private void ZoomFull() => ViewportAction?.Invoke("full");

    /// <summary>由窗口注入的视口动作（缩放/全屏需操作 ImageViewBox 实例）。</summary>
    public Action<string>? ViewportAction { get; set; }

    /// <summary>
    /// 确定：把当前样本列表重建为 ROI 文档并回写草稿，同时把右侧 8 项训练参数写入草稿，
    /// 最后触发关闭。原厂确定按钮语义（HPFeaturepatMatchDialogUI 底栏 ConfirmCommand）。
    /// </summary>
    private void Confirm()
    {
        if (_modelDirty || !HasValidModel(ReadModelPayload()))
        {
            MatchModel();
            if (_modelDirty || !HasValidModel(ReadModelPayload())) return;
        }
        Snapshot();
        ApplyDocument();
        ApplyTrainingParameters();
        ApplyTemplateImage();
        if (_pendingTemplatePayload is { Length: > 0 } payload)
            _commitTemplate?.Invoke(payload, TemplateTrainingCodec.Encode(_document));
        _confirmed = true;
        ConfirmCompleted?.Invoke();
    }

    /// <summary>
    /// 原厂确定语义（decompiled HPFeaturepatMatchDialogUI.cs:1650-1656）：
    /// 模板存图开 → 训练原图随模板保存；关 → 丢弃原图（之后不允许编辑）。
    /// 本方把原图写入模型载荷的可选 image 段。
    /// </summary>
    private void ApplyTemplateImage()
    {
        var payload = ReadModelPayload();
        if (payload.Length == 0) return;
        try
        {
            var entries = ContourMatchTemplateSet.DecodeEntries(payload).ToList();
            // 同 ApplyEraseMask：新建尚未成条时不能回写已有模板的原图。
            if (TemplateIndex >= entries.Count) return;
            var index = Math.Clamp(TemplateIndex, 0, entries.Count - 1);
            var template = entries[index].Template;
            ContourMatchTemplate updated;
            if (!IsSaveImage)
            {
                // 原厂 ConfirmCommandExecute 在“模板存图”关闭时清掉 CurImageDataA、
                // ModelImageData 与 otherMask；otherMask2 由原厂 SDK 数据单独管理，
                // 因此这里也只移除 Mask（otherMask），不把两层掩膜错误合并。
                updated = template.WithoutImage();
                if (template.Mask is not null)
                {
                    var width = template.MaskWidth > 0 ? template.MaskWidth : template.Mask2Width;
                    var height = template.MaskHeight > 0 ? template.MaskHeight : template.Mask2Height;
                    updated = updated.WithMasks(null, template.Mask2, width, height);
                }
            }
            else
            {
                updated = _frameImage is { PixelFormat: "Gray8" } frame
                    ? template.WithImage(frame.GrayPixels, frame.Width, frame.Height)
                    : template;
            }
            // 确定时一并保存当前匹配中心（原厂 FixPoint 随模板 BaseData 落盘）。
            updated = updated.WithFixPoint(FixPointX, FixPointY);
            entries[index] = entries[index] with { Template = updated };
            var encoded = ContourMatchTemplateSet.Encode(entries);
            if (string.Equals(encoded, payload, StringComparison.Ordinal)) return;
            _pendingTemplatePayload = encoded;
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            System.Windows.MessageBox.Show("保存模板原图失败：" + error.Message, "模板配置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static ContourMatchTemplate WithoutImage(ContourMatchTemplate template)
        => template.Image is null ? template : template.WithoutImage();

    /// <summary>样本列表 → ROI 文档（inspection=正样本、mask=背景），保留身份/名称/启用态。</summary>
    private void ApplyDocument()
    {
        var document = BuildDocument();
        _document = document;
        _acceptDocument(document);
    }

    private RecordValue BuildDocument()
    {
        var positive = new List<RecordValue>();
        var background = new List<RecordValue>();
        foreach (var item in _items)
        {
            var shape = BuildShape(item, _frameImage?.Width ?? 1, _frameImage?.Height ?? 1);
            var name = string.IsNullOrWhiteSpace(item.Name) ? DefaultRoiName(item) : item.Name;
            var entry = RoiDocumentValue.Item(item.Id, name, shape, item.Enabled, item.Visible);
            (item.IsPositive ? positive : background).Add(entry);
        }
        return RoiDocumentValue.Root(
            RoiDocumentValue.Group(positive.Count == 0 ? RoiDocumentValue.FullDomain : RoiDocumentValue.Explicit, positive),
            RoiDocumentValue.Group(background.Count == 0 ? RoiDocumentValue.None : RoiDocumentValue.Explicit, background));
    }

    /// <summary>
    /// 生成文档要求的 shape 记录。必须使用 <see cref="RoiDocumentValue.ShapeType"/> 构造：
    /// 编辑器侧的 RoiGeometryEditing 用的是另一套几何声明（center 为 PointGeometry），
    /// 直接塞给 RoiDocumentValue.Item 会因 TypeRef 不一致抛 "Invalid record for shape"。
    /// </summary>
    private static RecordValue BuildShape(TemplateRoiItem item, int imageWidth, int imageHeight)
    {
        var scaleX = Math.Max(1, imageWidth);
        var scaleY = Math.Max(1, imageHeight);
        var radialScale = Math.Min(scaleX, scaleY);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = item.Kind,
            ["center"] = RoiDocumentValue.Point(item.CenterPoint.X * scaleX, item.CenterPoint.Y * scaleY)
        };
        if (item.Kind == "polygon" && item.PolygonPoints.Count >= 3)
        {
            fields["vertices"] = new SequenceValue(TypeRef.SequenceOf(RoiDocumentValue.PointType),
                item.PolygonPoints.Select(point => (object?)RoiDocumentValue.Point(point.X * scaleX, point.Y * scaleY)));
        }
        else
        {
            var width = Math.Abs(item.Width) > 0 ? Math.Abs(item.Width) : item.Radius * 2;
            var height = Math.Abs(item.Height) > 0 ? Math.Abs(item.Height) : item.Radius * 2;
            fields["width"] = width * scaleX;
            fields["height"] = height * scaleY;
            fields["angle-degrees"] = item.Angle;
            if (item.Kind is "circle" or "sector")
            {
                fields["inner-radius"] = Math.Abs(item.MinRadius) * radialScale;
                fields["outer-radius"] = (item.Radius > 0 ? item.Radius : width / 2) * radialScale;
            }
            if (item.Kind == "sector")
            {
                fields["start-radians"] = item.StartAngle * Math.PI / 180d;
                fields["end-radians"] = item.EndAngle * Math.PI / 180d;
            }
        }
        return new RecordValue(RoiDocumentValue.ShapeType, fields);
    }
    /// <summary>右侧训练参数写入草稿；枚举字段按枚举名存储（Port 类型为 Text）。</summary>
    private void ApplyTrainingParameters()
    {
        var fields = _draft.CaptureFields();
        var proposals = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
        {
            [ContourMatchFields.PyramidMode] = new ConstantSetting(Enum.GetName(typeof(ContourMatchPyramidMode), (ContourMatchPyramidMode)PyramidScaleFlag) ?? "Auto"),
            [ContourMatchFields.PyramidScaleLevel] = new ConstantSetting((long)PyramidScaleLevel),
            [ContourMatchFields.PyramidScaleRLevel] = new ConstantSetting((long)PyramidScaleRLevel),
            [ContourMatchFields.EdgeThresholdMode] = new ConstantSetting(Enum.GetName(typeof(ContourMatchThresholdMode), (ContourMatchThresholdMode)EdgeThresholdFlag) ?? "Auto"),
            [ContourMatchFields.EdgeThreshold] = new ConstantSetting((long)EdgeThreshold),
            [ContourMatchFields.Optimization] = new ConstantSetting(Enum.GetName(typeof(ContourMatchOptimization), (ContourMatchOptimization)OptimizationIndex) ?? "Auto"),
            [ContourMatchFields.MinContrast] = new ConstantSetting((long)MinContrast),
            [ContourMatchFields.SubPixel] = new ConstantSetting(Enum.GetName(typeof(ContourMatchSubPixel), (ContourMatchSubPixel)SubPixelIndex) ?? "LeastSquares")
        };
        var only = proposals.Where(pair => !Equals(fields.Read(pair.Key), pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (only.Count == 0) return;
        try
        {
            _draft.ApplyFields(only, candidate => _acceptCandidate?.Invoke(candidate));
        }
        catch (ArgumentException error)
        {
            System.Windows.MessageBox.Show(error.Message, "训练参数未被接受", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>从文件选图（SelectOtherImage）的最小 IImageData 适配，按 Bgra32 拷贝像素。</summary>
    private sealed class BitmapImageData : IImageData
    {
        private readonly System.Windows.Media.Imaging.BitmapSource _source;
        private readonly byte[] _buffer;
        public BitmapImageData(System.Windows.Media.Imaging.BitmapSource source)
        {
            // 原厂 SelectOtherImage 通过 GetMono8ImageDataByFileDialog 强制转换 Gray8，
            // 不能把彩色 Bgra32 缓冲直接送入模板建模。
            var gray = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                source, System.Windows.Media.PixelFormats.Gray8, null, 0);
            _source = gray;
            var stride = gray.PixelWidth;
            _buffer = new byte[stride * gray.PixelHeight];
            gray.CopyPixels(_buffer, stride, 0);
        }
        public int Width => _source.PixelWidth;
        public int Height => _source.PixelHeight;
        public string PixelFormat => "Gray8";
        public string MemoryAddress => "";
        public byte[] Buffer { get => _buffer; set => throw new InvalidOperationException("显示缓冲只读。"); }
    }

    /// <summary>借当前帧像素的最小 IImageData 适配（工具层，不依赖 UI 项目）。</summary>
    private sealed class FrameImageData : IImageData
    {
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _buffer;
        private readonly string _format;
        public FrameImageData(IReadOnlyImage image)
        {
            // 借帧租约在方法返回后即释放：像素、尺寸与格式必须在构造时快照，不能持有 image 引用。
            _width = image.Layout.Width;
            _height = image.Layout.Height;
            _format = image.Layout.Format switch
            {
                PixelLayout.Gray8 => "Gray8",
                PixelLayout.Bgr24 => "Bgr24",
                PixelLayout.Rgb24 => "Rgb24",
                _ => "Bgra32"
            };
            _buffer = image.CopyPixels();
        }
        /// <summary>从存档的 Gray8 像素构造（模板原图回显）。</summary>
        public FrameImageData(byte[] grayPixels, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(grayPixels);
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "存档原图尺寸无效。");
            _width = width;
            _height = height;
            _format = "Gray8";
            _buffer = grayPixels;
        }

        public int Width => _width;
        public int Height => _height;
        public string PixelFormat => _format;
        /// <summary>Gray8 存档像素（仅灰度源有效）。</summary>
        public byte[] GrayPixels => _buffer;
        public string MemoryAddress => "";
        public byte[] Buffer { get => _buffer; set => throw new InvalidOperationException("显示缓冲由视口持有，不能替换规范像素。"); }
    }
}









