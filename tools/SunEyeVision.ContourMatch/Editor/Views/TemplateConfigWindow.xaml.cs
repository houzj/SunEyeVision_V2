using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Data;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板配置窗口：外壳由框架层 BaseWindow 统一提供（深色标题栏/边框/最小化最大化关闭），
/// 内容由 TemplateConfigSurface 承载原厂 hppyramidfeaturepatmatchdialoguistyle.xaml 的 ControlTemplate。
/// 分层：框架壳(BaseWindow) → 内容控件(Surface) → 数据模型(TemplateConfigData)。
/// </summary>
public sealed partial class TemplateConfigWindow : BaseWindow
{
    private readonly TemplateConfigData _data;
    private readonly TemplateConfigSurface _surface;
    private ImageViewBox? _imageViewBox;
    private EventHandler<RoiEventArgs>? _roiAddHandler;
    private Canvas? _extensionCanvas;
    private Image? _roiOverlay;
    private Image? _eraseOverlay;

    /// <summary>笔刷光标方块（原厂 RubberControl），橡皮擦/画笔模式下跟随鼠标。</summary>
    private Border? _brushCursor;
    private ImageViewBox.FreshExtendContent? _previousRefresh;

    public TemplateConfigWindow(
        IToolSettingsDraft draft,
        Action<FieldSettings>? acceptCandidate,
        Action createTemplate,
        Action saveProject,
        Action loadProject,
        Action<RecordValue> acceptDocument,
        RecordValue initialDocument,
        NativeRoiFrameSource? frames,
        Func<string?>? drawBlockReason = null,
        int templateIndex = 0,
        bool matchPointLock = false,
        Func<TemplateImageSnapshot?, RecordValue, TemplateMaskSnapshot, string?>? createTemplateWithMasks = null,
        Action<string, string>? commitTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(acceptDocument);
        TemplateConfigDiagnostics.Write("window-create",
            $"delegate={createTemplateWithMasks is not null};commit={commitTemplate is not null};index={templateIndex};frames={frames is not null}");
        // 没有建模委托就无法生成模型；在开窗时暴露，不要等到点「生成模型」才报“未返回有效数据”。
        if (createTemplateWithMasks is null)
            throw new InvalidOperationException("模板窗口创建时缺少建模委托，无法生成模型。");
        _data = new TemplateConfigData(draft, acceptCandidate, createTemplate, saveProject,
            loadProject, initialDocument, frames, acceptDocument, templateIndex, createTemplateWithMasks, commitTemplate);
        _data.PropertyChanged += DataPropertyChanged;
        _data.MatchPointLock = matchPointLock;
        // 确定按钮回写完成后由窗口关闭（原厂确定语义）；Owned 模态窗口用 DialogResult 收起。
        _data.ConfirmCompleted += () =>
        {
            if (IsLoaded) DialogResult = true; else Close();
        };

        InitializeComponent();
        // 严格照原厂 FeaturepatMatchConfigPanalData.cs:244-265 的宿主结构：
        // BaseWindow(框架壳) + 内容控件(默认样式自动套用原厂 ControlTemplate) + 主题色字典。
        ShowInTaskbar = false;
        Height = 534; Width = 900;
        MinButtonVisibility = Visibility.Collapsed;
        MaxButtonVisibility = Visibility.Visible;

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Foundation/BaseWindow.xaml", UriKind.Relative)
        });
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        // 原厂 :245-246 本地化 + :259 主题色。
        Plugin.Wpf.Themes.Palettes.ColorThemesResourceDictionary.GetInstance(this);
        // 原厂内容控件的 ControlTemplate（品牌中性化迁移）。本方工具 DLL 无 generic.xaml 主题链，
        // DefaultStyleKey 不会向父级资源树查找，故显式把样式赋给 Surface（等价原厂默认样式效果）。
        var originalStyleResources = new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Tool.ContourMatch.Runtime;component/Editor/Views/TemplateConfigWindow.Resources.xaml", UriKind.Relative)
        };
        Resources.MergedDictionaries.Add(originalStyleResources);

        _surface = new TemplateConfigSurface(_data)
        {
            Style = (Style)originalStyleResources[typeof(TemplateConfigSurface)]
        };
        Content = _surface;   // 原厂 :265 baseWindow.Content = featurepatMatchDialogUI
        DataContext = _data;

        _surface.ApplyTemplate();
        var imageViewBox = _surface.Template?.FindName("imageViewBox", _surface) as ImageViewBox;
        if (imageViewBox is not null)
        {
            _imageViewBox = imageViewBox;
            imageViewBox.DataContext = _data;
            // 原厂顺序：ImageViewBox 先触发 RoiAddEvent 取消当前 RadioButton，
            // 再执行 RoiAddCommand。这样 AddRoiCommand 看到的就是一次性绘制状态。
            _roiAddHandler = (_, _) => UncheckDrawingButtons();
            imageViewBox.RoiAddEvent += _roiAddHandler;
            _data.ViewportAction = action =>
            {
                if (action == "origin") imageViewBox.Init();
                else if (action == "full") ShowImageViewFullScreen(imageViewBox);
            };
            // 原厂 GirdMouseDownCommandExecute 直接读 imageViewBox.PixelX/PixelY 作为匹配中心。
            _data.PixelProbe = () => (imageViewBox.PixelX, imageViewBox.PixelY);
            // 原厂 HPFeaturepatMatchDialogUI.OnApplyTemplate/Loaded 将 tempImage
            // 挂到 imageViewBox.ExtenAbove，并在缩放/平移时重新绘制覆盖层。
            // ImageViewBox 的 ExtenAbove 只在它自己的 OnApplyTemplate 之后才存在，
            // 窗口构造阶段还是 null，必须延迟到 Loaded 再幂等创建。
            EnsureOverlays();
            _data.RoiVisualChanged += RefreshRoiOverlay;
            _data.PropertyChanged += TemplateDataPropertyChanged;
            _data.EraseMaskChanged += RefreshEraseOverlay;
            _previousRefresh = imageViewBox.OnFreshExtendContent;
            imageViewBox.OnFreshExtendContent = (sender, tag) =>
            {
                _previousRefresh?.Invoke(sender, tag);
                EnsureOverlays();
                RefreshRoiOverlay();
                RefreshEraseOverlay();
            };
            // 原厂在 Loaded 阶段把 tempImage 加入 ExtenAbove；ApplyTemplate 后直接加入
            // 会被 ImageViewBox 的模板加载流程清掉，因此这里必须重复挂载并刷新。
            Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                EnsureOverlays();
                RefreshRoiOverlay();
                RefreshEraseOverlay();
            });
            // 橡皮擦/画笔：原厂 RubberControl 注册到画布后按笔刷尺寸擦像素（otherMask），
            // 本方在画布上挂鼠标事件，用 PixelX/PixelY 把笔刷覆盖的像素写入擦除掩膜。
            var wiping = false;
            // 原厂 RubberControl 在 Viewbox 的冒泡 MouseMove 里读 PixelX/PixelY
            // （RubberControl.xaml.cs:184/242-249），像素坐标由 ImageViewBox 内部维护。
            // 两个必须点：
            //  ① 用冒泡阶段（Mouse.*Event），Preview 隧道阶段读到的是上一帧 PixelX/PixelY；
            //  ② 不能 CaptureMouse 到 UserControl，否则内部 SourceImage 收不到移动，
            //     坐标会冻结在按下点（表现为“只擦第一下”）。
            // handledEventsToo=true：内部 ROI 控件可能把事件标记为已处理。
            // 这里不能抢先 e.Handled：原位对齐原厂靠 IsStateForDrawROI=true，
            // 由 ImageViewBox.mouseLBDown 自己 Handled（xaml.cs:2142-2149），
            // 从而既不命中拖动已有 ROI、也不进入图像平移。
            // 我们在隧道阶段抢先 Handled 会连带挡掉那段逻辑（表现为 ROI 被拖动）。
            // 用冒泡 + handledEventsToo：内部 Handled 之后仍能收到，且 PixelX/PixelY 已更新。
            imageViewBox.AddHandler(Mouse.MouseDownEvent, new MouseButtonEventHandler((_, e) =>
            {
                if (e.ChangedButton != MouseButton.Left) return;
                if (!_data.IsEraseMode) return;
                wiping = true;
                _data.WipeAt(imageViewBox.PixelX, imageViewBox.PixelY, _data.WipeSize);
            }), true);
            imageViewBox.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, e) =>
            {
                if (_extensionCanvas is not null) UpdateBrushCursor(e.GetPosition(_extensionCanvas));
                if (!wiping || !_data.IsEraseMode) return;
                _data.WipeAt(imageViewBox.PixelX, imageViewBox.PixelY, _data.WipeSize);
            }), true);
            imageViewBox.AddHandler(Mouse.MouseLeaveEvent, new MouseEventHandler((_, _) =>
            {
                if (_brushCursor is not null) _brushCursor.Visibility = Visibility.Collapsed;
                // 鼠标移出画布时收尾，避免 MouseUp 落在画布外导致 wiping 一直为 true。
                if (wiping) { wiping = false; _data.EndWipe(); }
            }), true);
            imageViewBox.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((_, e) =>
            {
                if (e.ChangedButton != MouseButton.Left) return;
                if (!wiping) return;
                wiping = false;
                _data.EndWipe();
            }), true);
        }
    }

    /// <summary>
    /// 原厂 ImageViewBox.RoiAddEvent 会先把 curSelectedRadioButton.IsChecked 置为 false，
    /// 然后才完成 AddROICommand。这样图形工具是一发一收，下一次绘制必须重新选择图形。
    /// </summary>
    private void UncheckDrawingButtons()
    {
        if (_surface.Template is null) return;
        // 原厂 ImageViewBox_RoiAddEvent（UI:490-497）只取消“当前工具”；
        // 这里覆盖所有绘制/笔刷工具，效果一致（每次只能选中一个）。
        foreach (var name in new[] { "hikSector", "hikCoverCircle", "hikFrame", "hikPolygon", "hikCross", "fun", "fun2" })
        {
            if (_surface.Template.FindName(name, _surface) is RadioButton button && button.IsChecked == true)
                button.IsChecked = false;
        }
        // 原厂 RadioButtonCommandExecute Unchecked 分支（UI:1614-1617）：
        // 取消非箭头工具后自动回到「移动图像」箭头，工具栏不会停在没有选中的状态。
        if (_surface.Template.FindName("Arrow", _surface) is RadioButton arrow && arrow.IsChecked != true)
            arrow.IsChecked = true;
    }

    /// <summary>
    /// 原厂 FullScreen 命令不是一次 Enlarge，而是把同一个 ImageViewBox 移入
    /// 独立模态窗口，关闭后再放回原来的 viewGrid。这样全屏按钮不会把“放大”
    /// 和“全屏”混成一个缩放动作，且保留当前 ROI/掩膜/选择状态。
    /// </summary>
    private void ShowImageViewFullScreen(ImageViewBox imageViewBox)
    {
        if (imageViewBox.Parent is not Panel parent) return;
        var index = parent.Children.IndexOf(imageViewBox);
        if (index < 0) return;
        var row = Grid.GetRow(imageViewBox);
        var column = Grid.GetColumn(imageViewBox);
        var rowSpan = Grid.GetRowSpan(imageViewBox);
        var columnSpan = Grid.GetColumnSpan(imageViewBox);
        var horizontal = imageViewBox.HorizontalAlignment;
        var vertical = imageViewBox.VerticalAlignment;
        var width = imageViewBox.Width;
        var height = imageViewBox.Height;
        parent.Children.RemoveAt(index);
        var full = new Window
        {
            Owner = this,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.SingleBorderWindow,
            WindowState = WindowState.Maximized,
            Background = Brushes.Black,
            Content = imageViewBox
        };
        full.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) full.Close();
        };
        try
        {
            full.ShowDialog();
        }
        finally
        {
            full.Content = null;
            parent.Children.Insert(Math.Min(index, parent.Children.Count), imageViewBox);
            Grid.SetRow(imageViewBox, row);
            Grid.SetColumn(imageViewBox, column);
            Grid.SetRowSpan(imageViewBox, rowSpan);
            Grid.SetColumnSpan(imageViewBox, columnSpan);
            imageViewBox.HorizontalAlignment = horizontal;
            imageViewBox.VerticalAlignment = vertical;
            imageViewBox.Width = width;
            imageViewBox.Height = height;
        }
    }

    private void TemplateDataPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 退出笔刷模式要立即收起笔刷光标（原厂 UnRegisterRoi 移除 RubberControl）。
        if (e.PropertyName == nameof(TemplateConfigData.IsEraseMode))
        {
            EnsureOverlays();
            // 原厂 RegisterRoi 把 SourceImage.Cursor 设为 None（RubberControl.xaml.cs:186），
            // UnRegisterRoi 恢复 Arrow（:211）：笔刷模式下系统箭头必须消失，只留笔刷方块。
            if (_imageViewBox is not null)
                _imageViewBox.Cursor = _data.IsEraseMode ? Cursors.None : Cursors.Arrow;
            // 进入笔刷模式时若鼠标已在画布上，立即按当前位置显示光标，不必等移动。
            if (_data.IsEraseMode && _extensionCanvas is not null)
                UpdateBrushCursor(Mouse.GetPosition(_extensionCanvas));
            else if (_brushCursor is not null)
                _brushCursor.Visibility = Visibility.Collapsed;
            return;
        }
        // 拖动尺寸滑块时鼠标在浮层上，不会触发画布 MouseMove；笔刷方块必须自己跟着变。
        if (e.PropertyName == nameof(TemplateConfigData.BrushSize))
        {
            ResizeBrushCursor();
            return;
        }
        // 绘制态结束（画完 ROI、点选匹配中心）后自动回箭头：
        // 原厂 RoiAddEvent:490-497 与 GirdMouseDownCommandExecute:1871 都做 Arrow.IsChecked = true。
        if (e.PropertyName == nameof(TemplateConfigData.IsStateForDrawROI))
        {
            if (!_data.IsStateForDrawROI) UncheckDrawingButtons();
            return;
        }
        if (e.PropertyName is not (nameof(TemplateConfigData.ItemsData) or nameof(TemplateConfigData.ImageDataSource)))
            return;
        Dispatcher.BeginInvoke(new Action(RefreshRoiOverlay));
    }

    /// <summary>
    /// 幂等创建/挂载三层覆盖（训练区域、擦除掩膜、笔刷光标）。
    /// ImageViewBox.ExtenAbove 只在它自己的 OnApplyTemplate 后才有值
    /// （ImageViewBox.xaml.cs:1528 FindName），窗口构造阶段为 null；
    /// 早期在构造里一次性创建会导致三层全部不生效（擦除层与橡皮擦都看不到）。
    /// </summary>
    private void EnsureOverlays()
    {
        if (_imageViewBox is null) return;
        _extensionCanvas ??= _imageViewBox.ExtenAbove as Canvas;
        if (_extensionCanvas is null) return;

        _roiOverlay ??= new Image
        {
            IsHitTestVisible = false,
            Stretch = Stretch.Fill,
            Focusable = false,
            SnapsToDevicePixels = true
        };
        _eraseOverlay ??= new Image
        {
            IsHitTestVisible = false,
            Stretch = Stretch.Fill,
            Focusable = true,
            SnapsToDevicePixels = true
        };
        // 原厂 RubberControl.xaml:3-9：2px 黑边 + 白色 0.5 半透明方块光标。
        _brushCursor ??= new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            SnapsToDevicePixels = true
        };

        Attach(_roiOverlay, 90);
        Attach(_eraseOverlay, 100);
        Attach(_brushCursor, 110);

        void Attach(UIElement element, int zIndex)
        {
            if (!_extensionCanvas.Children.Contains(element)) _extensionCanvas.Children.Add(element);
            Panel.SetZIndex(element, zIndex);
        }
    }

    /// <summary>
    /// 笔刷光标：原厂 RubberControl（2px 黑边 + 白色 0.5 半透明方块），
    /// 尺寸 = BrushSize × OnePixelWidth，鼠标位置居中。
    /// </summary>
    /// <summary>笔刷尺寸变化时同步方块大小（原厂 RubberControl.DWidth/DHeight 绑定 PanRectWidth）。</summary>
    private void ResizeBrushCursor()
    {
        if (_brushCursor is null || _imageViewBox is null) return;
        var scale = _imageViewBox.OnePixelWidth;
        if (scale <= 0) return;
        var size = Math.Max(3d, _data.BrushSize * scale);
        _brushCursor.Width = size;
        _brushCursor.Height = size;
    }

    private void UpdateBrushCursor(System.Windows.Point position)
    {
        if (_brushCursor is null || _imageViewBox is null) return;
        if (!_data.IsEraseMode)
        {
            _brushCursor.Visibility = Visibility.Collapsed;
            return;
        }
        // 原厂 owner_OnDrawRoi_MouseMoveChange（RubberControl.xaml.cs:249-256）：
        // 指针移出图像范围时收起方块，避免在画布空白处显示笔刷。
        if (_imageViewBox.PixelX < 0 || _imageViewBox.PixelY < 0 ||
            _imageViewBox.PixelX >= _imageViewBox.ImageSourceWidth ||
            _imageViewBox.PixelY >= _imageViewBox.ImageSourceHeight)
        {
            _brushCursor.Visibility = Visibility.Collapsed;
            return;
        }
        var scale = _imageViewBox.OnePixelWidth;
        if (scale <= 0) return;
        // 原厂 bd.BorderThickness = 2 / ImageScale（:187）：边框不随缩放变粗。
        if (_imageViewBox.ImageScale > 0)
            _brushCursor.BorderThickness = new Thickness(2 / _imageViewBox.ImageScale);
        var size = Math.Max(3d, _data.BrushSize * scale);
        _brushCursor.Width = size;
        _brushCursor.Height = size;
        Canvas.SetLeft(_brushCursor, position.X - size / 2);
        Canvas.SetTop(_brushCursor, position.Y - size / 2);
        _brushCursor.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 原厂 ROI 列表由 ImageViewBox 绘制；此覆盖层是独立的半透明位图，
    /// 用于复现模板窗口中“匹配区域蓝色、屏蔽区域红色”的 DrawMask 视觉，
    /// 并与 ExtenAbove 一样使用当前 ViewBox 的缩放和平移。
    /// </summary>
    private void RefreshRoiOverlay()
    {
        if (_roiOverlay is null || _extensionCanvas is null || _imageViewBox is null)
            return;
        var image = _data.ImageDataSource;
        var mapCanvas = _imageViewBox.MapCanvas as Canvas;
        var viewbox = _imageViewBox.Viewbox as Viewbox;
        var width = (int)Math.Max(0, Math.Round(mapCanvas?.ActualWidth ?? _extensionCanvas.ActualWidth));
        var height = (int)Math.Max(0, Math.Round(mapCanvas?.ActualHeight ?? _extensionCanvas.ActualHeight));
        var imageWidth = image?.Width ?? 0;
        var imageHeight = image?.Height ?? 0;
        var pixelScale = _imageViewBox.OnePixelWidth;
        if (width <= 0 || height <= 0 || imageWidth <= 0 || imageHeight <= 0 || viewbox is null ||
            viewbox.ActualWidth <= 0 || viewbox.ActualHeight <= 0 || pixelScale <= 0)
        {
            _roiOverlay.Source = null;
            return;
        }

        // ImageViewBox 模板没有给 Viewbox 设置 Canvas.Left/Top；WPF 对未设置值返回 NaN。
        // NaN 进入 MatrixTransform 后整个 RenderTargetBitmap 都不可见。
        var viewboxLeft = FiniteCanvasOffset(Canvas.GetLeft(viewbox));
        var viewboxTop = FiniteCanvasOffset(Canvas.GetTop(viewbox));
        var originX = ((width - viewbox.ActualWidth) / 2.0) + viewboxLeft + _imageViewBox.ImageOffSetX;
        var originY = ((height - viewbox.ActualHeight) / 2.0) + viewboxTop + _imageViewBox.ImageOffSetY;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.PushTransform(new MatrixTransform(new Matrix(pixelScale, 0, 0, pixelScale, originX, originY)));
            // 样本区域填充取自原厂 FeatureRectStyle 的触发器，而不是 DrawMask 的笔刷位图色：
            //   roicontrolbasestyle.xaml:62-64
            //     ROIBaseFeaSignColor        #EC0000 Opacity 0.45  → 背景样本(Sign=false)
            //     ROIBaseFeaSignSpecialColor #60FF00 Opacity 0.30  → 特殊样本(Sign=null)
            //     ROIBaseFeaBackgroundColor  #3884FF Opacity 0.25  → 正样本(Sign=true)
            //   rectroistyle.xaml:214-220 按 Sign 选择上述 Brush。
            // 描边统一 ROIBaseColor #0F6AF3（FeatureRectBaseStyle:47）。
            // 之前误用 DrawMask 的 0x800F6AF3 / 0x4CFF1A1A（那是擦除/屏蔽覆盖层的颜色），
            // 导致样本填充透明度与原厂不一致。
            var matchFill = new SolidColorBrush(Color.FromArgb(0x40, 0x38, 0x84, 0xFF));
            var matchStroke = new SolidColorBrush(Color.FromRgb(0x0F, 0x6A, 0xF3));
            var maskFill = new SolidColorBrush(Color.FromArgb(0x73, 0xEC, 0x00, 0x00));
            var maskStroke = new SolidColorBrush(Color.FromRgb(0x0F, 0x6A, 0xF3));
            matchFill.Freeze(); matchStroke.Freeze(); maskFill.Freeze(); maskStroke.Freeze();
            var matchPen = new Pen(matchStroke, Math.Max(1.0 / pixelScale, 1.0));
            var maskPen = new Pen(maskStroke, Math.Max(1.0 / pixelScale, 1.0));
            matchPen.Freeze(); maskPen.Freeze();

            foreach (var item in _data.ItemsData.OfType<TemplateRoiItem>())
            {
                if (!item.Visible) continue;
                var fill = item.IsPositive ? matchFill : maskFill;
                var pen = item.IsPositive ? matchPen : maskPen;
                DrawRoi(drawing, item, imageWidth, imageHeight, fill, pen);
            }
            drawing.Pop();
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        _roiOverlay.Width = width;
        _roiOverlay.Height = height;
        _roiOverlay.Source = bitmap;
    }

    private static void DrawRoi(DrawingContext drawing, TemplateRoiItem item, int imageWidth, int imageHeight,
        Brush fill, Pen pen)
    {
        // 新绘制项使用归一化坐标；已保存模板恢复项可能是像素坐标。
        var centerX = ToPixel(item.CenterPoint.X, imageWidth);
        var centerY = ToPixel(item.CenterPoint.Y, imageHeight);
        switch (item.Kind)
        {
            case "rectangle":
            {
                var width = ToSpan(item.Width, imageWidth);
                var height = ToSpan(item.Height, imageHeight);
                if (width <= 0 || height <= 0) return;
                drawing.PushTransform(new RotateTransform(item.Angle, centerX, centerY));
                drawing.DrawRectangle(fill, pen, new System.Windows.Rect(centerX - width / 2, centerY - height / 2, width, height));
                drawing.Pop();
                return;
            }
            case "circle":
            {
                var radius = item.Radius > 0 ? ToSpan(item.Radius, Math.Min(imageWidth, imageHeight)) :
                    Math.Min(ToSpan(item.Width, imageWidth), ToSpan(item.Height, imageHeight)) / 2;
                if (radius <= 0) return;
                drawing.DrawEllipse(fill, pen, new System.Windows.Point(centerX, centerY), radius, radius);
                return;
            }
            case "sector":
            {
                var radius = item.Radius > 0 ? ToSpan(item.Radius, Math.Min(imageWidth, imageHeight)) :
                    Math.Min(ToSpan(item.Width, imageWidth), ToSpan(item.Height, imageHeight)) / 2;
                if (radius <= 0) return;
                var start = item.StartAngle * Math.PI / 180.0;
                var end = item.EndAngle * Math.PI / 180.0;
                var startPoint = new System.Windows.Point(centerX + Math.Cos(start) * radius, centerY + Math.Sin(start) * radius);
                var endPoint = new System.Windows.Point(centerX + Math.Cos(end) * radius, centerY + Math.Sin(end) * radius);
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(new System.Windows.Point(centerX, centerY), true, true);
                    context.LineTo(startPoint, true, false);
                    context.ArcTo(endPoint, new System.Windows.Size(radius, radius), 0,
                        Math.Abs(item.EndAngle - item.StartAngle) > 180,
                        SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze();
                drawing.DrawGeometry(fill, pen, geometry);
                return;
            }
            case "polygon" when item.PolygonPoints.Count >= 3:
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    var points = item.PolygonPoints
                        .Select(point => new System.Windows.Point(ToPixel(point.X, imageWidth), ToPixel(point.Y, imageHeight))).ToArray();
                    context.BeginFigure(points[0], true, true);
                    context.PolyLineTo(points.Skip(1).ToArray(), true, true);
                }
                geometry.Freeze();
                drawing.DrawGeometry(fill, pen, geometry);
                return;
            }
            case "point":
            {
                var size = Math.Max(4, Math.Min(imageWidth, imageHeight) * 0.012);
                drawing.DrawLine(pen, new System.Windows.Point(centerX - size, centerY), new System.Windows.Point(centerX + size, centerY));
                drawing.DrawLine(pen, new System.Windows.Point(centerX, centerY - size), new System.Windows.Point(centerX, centerY + size));
                return;
            }
        }

        static double ToPixel(double value, int size)
            => Math.Abs(value) <= 1.5 ? value * size : value;
        static double ToSpan(double value, int size)
            => Math.Abs(value) <= 1.5 ? Math.Abs(value) * size : Math.Abs(value);
    }

    private static double FiniteCanvasOffset(double value)
        => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

    /// <summary>
    /// 对齐原厂 DrawMask：以 ImageViewBox 的显示坐标把像素掩膜投影到
    /// ExtenAbove，使用原厂擦除颜色 0x4CFF1A1A（增量更新时为 0x99FF1A1A）。
    /// </summary>
    private void RefreshEraseOverlay()
    {
        if (_eraseOverlay is null || _extensionCanvas is null || _imageViewBox is null)
            return;
        var mask = _data.ActiveMask;
        var mapCanvas = _imageViewBox.MapCanvas as Canvas;
        var viewbox = _imageViewBox.Viewbox as Viewbox;
        var width = (int)Math.Max(0, Math.Round(mapCanvas?.ActualWidth ?? _extensionCanvas.ActualWidth));
        var height = (int)Math.Max(0, Math.Round(mapCanvas?.ActualHeight ?? _extensionCanvas.ActualHeight));
        if (mask is null || _data.MaskWidth <= 0 || _data.MaskHeight <= 0 || width <= 0 || height <= 0 || viewbox is null)
        {
            _eraseOverlay.Source = null;
            return;
        }

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        var viewWidth = viewbox.ActualWidth;
        var viewHeight = viewbox.ActualHeight;
        var pixelScale = _imageViewBox.OnePixelWidth;
        if (pixelScale <= 0 || viewWidth <= 0 || viewHeight <= 0)
            return;
        var viewboxLeft = FiniteCanvasOffset(Canvas.GetLeft(viewbox));
        var viewboxTop = FiniteCanvasOffset(Canvas.GetTop(viewbox));
        var originX = ((width - viewWidth) / 2.0) + viewboxLeft + _imageViewBox.ImageOffSetX;
        var originY = ((height - viewHeight) / 2.0) + viewboxTop + _imageViewBox.ImageOffSetY;
        // 原厂 DrawMask 按层取色（decompiled HPFeaturepatMatchDialogUI.cs:1222、1275）：
        // otherMask（橡皮擦/擦除轮廓点）= 0x4CFF1A1A 红；
        // otherMask2（画笔/全部屏蔽）= 0x800F6AF3 蓝。写死红色会让“全部屏蔽”层看起来像擦除层。
        var paint = _data.IsUsePen;
        var b = paint ? (byte)0xF3 : (byte)0x1A;
        var g = paint ? (byte)0x6A : (byte)0x1A;
        var r = paint ? (byte)0x0F : (byte)0xFF;
        var a = paint ? (byte)0x80 : (byte)0x4C;
        for (var y = 0; y < _data.MaskHeight; y++)
        {
            for (var x = 0; x < _data.MaskWidth; x++)
            {
                if (mask[y * _data.MaskWidth + x] == 0) continue;
                var left = Math.Max(0, (int)Math.Floor(originX + x * pixelScale));
                var top = Math.Max(0, (int)Math.Floor(originY + y * pixelScale));
                var right = Math.Min(width - 1, (int)Math.Ceiling(originX + (x + 1) * pixelScale));
                var bottom = Math.Min(height - 1, (int)Math.Ceiling(originY + (y + 1) * pixelScale));
                for (var py = top; py <= bottom; py++)
                for (var px = left; px <= right; px++)
                {
                    var i = (py * width + px) * 4;
                    pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = a;
                }
            }
        }
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        _eraseOverlay.Width = width;
        _eraseOverlay.Height = height;
        _eraseOverlay.Source = bitmap;
    }

    protected override void OnClosed(EventArgs e)
    {
        _data.PropertyChanged -= DataPropertyChanged;
        _data.PropertyChanged -= TemplateDataPropertyChanged;
        if (_imageViewBox is not null && _roiAddHandler is not null)
            _imageViewBox.RoiAddEvent -= _roiAddHandler;
        _data.EraseMaskChanged -= RefreshEraseOverlay;
        _data.RoiVisualChanged -= RefreshRoiOverlay;
        if (_imageViewBox is not null && _previousRefresh is not null)
            _imageViewBox.OnFreshExtendContent = _previousRefresh;
        if (_extensionCanvas is not null && _eraseOverlay is not null)
            _extensionCanvas.Children.Remove(_eraseOverlay);
        if (_extensionCanvas is not null && _roiOverlay is not null)
            _extensionCanvas.Children.Remove(_roiOverlay);
        base.OnClosed(e);
    }

    private void DataPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_imageViewBox is null) return;
        if (e.PropertyName == nameof(TemplateConfigData.ImageDataSource))
        {
            // 原厂 DoClear 清掉 ExtenAbove/tempImage；pointsImage 是共享
            // ImageViewBox 的外置覆盖层，必须显式清空，不能只清 ShapeList。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _imageViewBox?.ClearExternShapes();
                if (_imageViewBox is not null) _imageViewBox.NeedRefreshShapes = true;
            }));
            return;
        }
        if (e.PropertyName != nameof(TemplateConfigData.ShapeList)) return;
        // ImageViewBox 的原厂 ShapeItemsPage 通过 NeedRefreshShapes 让
        // MultiPointShape 重新执行 RefreshPoints；仅变更集合不足以重算已存在的画布层。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_imageViewBox is null) return;
            BindingOperations.GetBindingExpression(_imageViewBox, ImageViewBox.ShapeListProperty)?.UpdateTarget();
            // 原厂 UpdateShapeDisplay 无条件 ClearExternShapes，再重新挂入当前 ShapeList；
            // 只在空列表时清理会留下旧轮廓，导致生成/重建后画面不变。
            _imageViewBox.ClearExternShapes();
            _imageViewBox.NeedRefreshShapes = true;
        }));
    }
}
