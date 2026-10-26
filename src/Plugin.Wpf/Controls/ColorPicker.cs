using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Themes;

namespace SunEyeVision.Plugin.Wpf.Controls
{
    /// <summary>
    /// 取色器控件
    /// </summary>
    /// <remarks>
    /// 提供颜色选择功能，包含预设颜色和 HSV 颜色选择器。
    /// 
    /// 功能特性：
    /// - 预设颜色板（主题颜色 + 标准颜色）
    /// - HSV 颜色选择器（色相、饱和度、明度）
    /// - RGB 值显示
    /// - 双向数据绑定
    /// 
    /// 使用示例：
    /// <code>
    /// &lt;controls:ColorPicker SelectedColor="{Binding DisplayColor, Mode=TwoWay}" /&gt;
    /// </code>
    /// </remarks>
    [TemplatePart(Name = PART_ColorPreview, Type = typeof(Border))]
    [TemplatePart(Name = PART_RgbText, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_TabControl, Type = typeof(TabControl))]
    [TemplatePart(Name = PART_ThemeColorsPanel, Type = typeof(UniformGrid))]
    [TemplatePart(Name = PART_StandardColorsPanel, Type = typeof(UniformGrid))]
    [TemplatePart(Name = PART_HsvCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_HsvSelector, Type = typeof(Ellipse))]
    [TemplatePart(Name = PART_ValueSlider, Type = typeof(Slider))]
    [TemplatePart(Name = PART_ValueText, Type = typeof(TextBlock))]
    public class ColorPicker : Control
    {
        private const string PART_ColorPreview = "PART_ColorPreview";
        private const string PART_RgbText = "PART_RgbText";
        private const string PART_TabControl = "PART_TabControl";
        private const string PART_ThemeColorsPanel = "PART_ThemeColorsPanel";
        private const string PART_StandardColorsPanel = "PART_StandardColorsPanel";
        private const string PART_HsvCanvas = "PART_HsvCanvas";
        private const string PART_HsvSelector = "PART_HsvSelector";
        private const string PART_ValueSlider = "PART_ValueSlider";
        private const string PART_ValueText = "PART_ValueText";

        private Border _colorPreview;
        private TextBlock _rgbText;
        private TabControl _tabControl;
        private UniformGrid _themeColorsPanel;
        private UniformGrid _standardColorsPanel;
        private Canvas _hsvCanvas;
        private Ellipse _hsvSelector;
        private Slider _valueSlider;
        private TextBlock _valueText;
        private bool _isDragging;

        static ColorPicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker),
                new FrameworkPropertyMetadata(typeof(ColorPicker)));
        }

        #region 依赖属性

        /// <summary>
        /// 选中的颜色
        /// </summary>
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPicker),
                new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        /// <summary>
        /// 选中的颜色
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        #endregion

        #region 路由事件

        /// <summary>
        /// 颜色变更事件
        /// </summary>
        public static readonly RoutedEvent SelectedColorChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectedColorChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<Color>), typeof(ColorPicker));

        /// <summary>
        /// 颜色变更事件
        /// </summary>
        public event RoutedPropertyChangedEventHandler<Color> SelectedColorChanged
        {
            add => AddHandler(SelectedColorChangedEvent, value);
            remove => RemoveHandler(SelectedColorChangedEvent, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 选择主题颜色命令
        /// </summary>
        public static readonly RoutedCommand SelectThemeColorCommand = new RoutedCommand(nameof(SelectThemeColorCommand), typeof(ColorPicker));

        /// <summary>
        /// 选择标准颜色命令
        /// </summary>
        public static readonly RoutedCommand SelectStandardColorCommand = new RoutedCommand(nameof(SelectStandardColorCommand), typeof(ColorPicker));

        #endregion

        #region 预设颜色

        /// <summary>
        /// 主题颜色系列列表（用于 XAML 绑定）
        /// </summary>
        public static List<ThemeColorSeries> ThemeColorSeriesList
        {
            get { return ThemeColorPalette.ThemeColorSeriesList; }
        }

        /// <summary>
        /// 标准颜色列表（用于 XAML 绑定）
        /// </summary>
        public static List<Color> StandardColorList
        {
            get { return ThemeColorPalette.StandardColors; }
        }

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _colorPreview = GetTemplateChild(PART_ColorPreview) as Border;
            _rgbText = GetTemplateChild(PART_RgbText) as TextBlock;
            _tabControl = GetTemplateChild(PART_TabControl) as TabControl;
            _themeColorsPanel = GetTemplateChild(PART_ThemeColorsPanel) as UniformGrid;
            _standardColorsPanel = GetTemplateChild(PART_StandardColorsPanel) as UniformGrid;
            _hsvCanvas = GetTemplateChild(PART_HsvCanvas) as Canvas;
            _hsvSelector = GetTemplateChild(PART_HsvSelector) as Ellipse;
            _valueSlider = GetTemplateChild(PART_ValueSlider) as Slider;
            _valueText = GetTemplateChild(PART_ValueText) as TextBlock;

            if (_hsvCanvas != null)
            {
                _hsvCanvas.MouseLeftButtonDown += OnHsvCanvasMouseDown;
                _hsvCanvas.MouseMove += OnHsvCanvasMouseMove;
                _hsvCanvas.MouseLeftButtonUp += OnHsvCanvasMouseUp;
            }

            if (_valueSlider != null)
            {
                _valueSlider.ValueChanged += OnValueSliderChanged;
            }

            // 动态创建主题颜色和标准颜色按钮
            CreateThemeColorMatrix();
            CreateStandardColorButtons();

            // 绑定命令
            CommandBindings.Add(new CommandBinding(SelectThemeColorCommand, OnSelectThemeColorExecuted));
            CommandBindings.Add(new CommandBinding(SelectStandardColorCommand, OnSelectStandardColorExecuted));

            UpdateVisualState();
        }

        private void OnSelectThemeColorExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is Color color)
            {
                SelectedColor = color;
            }
        }

        private void OnSelectStandardColorExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is Color color)
            {
                SelectedColor = color;
            }
        }

        private void OnSelectedColorChanged(Color oldValue, Color newValue)
        {
            UpdateVisualState();

            var args = new RoutedPropertyChangedEventArgs<Color>(oldValue, newValue, SelectedColorChangedEvent);
            RaiseEvent(args);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ColorPicker)d;
            control.OnSelectedColorChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private void UpdateVisualState()
        {
            var color = SelectedColor;

            if (_colorPreview != null)
            {
                _colorPreview.Background = new SolidColorBrush(color);
            }

            if (_rgbText != null)
            {
                _rgbText.Text = $"R={color.R}, G={color.G}, B={color.B}";
            }
        }

        #region HSV 选择器

        private void OnHsvCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _hsvCanvas?.CaptureMouse();
            UpdateHsvFromCanvasPosition(e.GetPosition(_hsvCanvas));
        }

        private void OnHsvCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateHsvFromCanvasPosition(e.GetPosition(_hsvCanvas));
            }
        }

        private void OnHsvCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _hsvCanvas?.ReleaseMouseCapture();
        }

        private void OnValueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateColorFromHsv();
            
            // 更新亮度值显示
            if (_valueText != null && _valueSlider != null)
            {
                _valueText.Text = _valueSlider.Value.ToString("F2");
            }
        }

        private void UpdateHsvFromCanvasPosition(Point position)
        {
            if (_hsvCanvas == null)
                return;

            double width = _hsvCanvas.ActualWidth;
            double height = _hsvCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            double x = Math.Max(0, Math.Min(width, position.X));
            double y = Math.Max(0, Math.Min(height, position.Y));

            double h = (x / width) * 360;
            double s = y / height;
            double v = _valueSlider?.Value ?? 1.0;

            var hsv = new HsvColor(h, s, v);
            SelectedColor = hsv.ToColor();

            UpdateSelectorPosition(h, s);
        }

        private void UpdateSelectorPosition(double h, double s)
        {
            if (_hsvCanvas == null || _hsvSelector == null)
                return;

            double width = _hsvCanvas.ActualWidth;
            double height = _hsvCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            double x = (h / 360) * width;
            double y = s * height;

            Canvas.SetLeft(_hsvSelector, x - _hsvSelector.Width / 2);
            Canvas.SetTop(_hsvSelector, y - _hsvSelector.Height / 2);
        }

        private void UpdateColorFromHsv()
        {
            if (_hsvCanvas == null || _valueSlider == null)
                return;

            var currentColor = SelectedColor;
            var hsv = HsvColor.FromColor(currentColor);
            hsv.V = _valueSlider.Value;

            SelectedColor = hsv.ToColor();
        }

        #endregion

        #region 动态创建颜色按钮

        /// <summary>
        /// 创建主题颜色矩阵（10列×6行）
        /// </summary>
        private void CreateThemeColorMatrix()
        {
            if (_themeColorsPanel == null)
                return;

            _themeColorsPanel.Children.Clear();
            var seriesList = ThemeColorPalette.ThemeColorSeriesList;

            // 第1-3行：浅色变体（从浅到深）
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < seriesList.Count; col++)
                {
                    var color = seriesList[col].LighterVariants[row];
                    _themeColorsPanel.Children.Add(CreateColorButton(color));
                }
            }

            // 第4行：基础色
            for (int col = 0; col < seriesList.Count; col++)
            {
                var color = seriesList[col].BaseColor;
                _themeColorsPanel.Children.Add(CreateColorButton(color));
            }

            // 第5-6行：深色变体（从深到浅）
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < seriesList.Count; col++)
                {
                    var color = seriesList[col].DarkerVariants[row];
                    _themeColorsPanel.Children.Add(CreateColorButton(color));
                }
            }
        }

        /// <summary>
        /// 创建标准颜色按钮（1行10个）
        /// </summary>
        private void CreateStandardColorButtons()
        {
            if (_standardColorsPanel == null)
                return;

            _standardColorsPanel.Children.Clear();
            var standardColors = ThemeColorPalette.StandardColors;

            foreach (var color in standardColors)
            {
                _standardColorsPanel.Children.Add(CreateColorButton(color));
            }
        }

        /// <summary>
        /// 创建单个颜色按钮
        /// </summary>
        private Button CreateColorButton(Color color)
        {
            var button = new Button
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(1),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(color),
                Tag = color,
                Focusable = false,
                Style = FindResource("ColorPickerButtonStyle") as Style
            };
            button.Click += OnColorButtonClick;
            return button;
        }

        /// <summary>
        /// 处理颜色按钮点击事件
        /// </summary>
        private void OnColorButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                SelectedColor = color;
            }
        }

        #endregion
    }
}
