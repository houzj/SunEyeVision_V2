using System;
using System.Windows;
using System.Windows.Controls;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Tool.ImageThreshold.Models;

namespace SunEyeVision.Tool.ImageThreshold.Views
{
    /// <summary>
    /// 图像二值化工具调试控件
    /// </summary>
    public partial class ImageThresholdToolDebugControl : ToolDebugControlBase
    {
        #region 字段

        private DataSourceQueryService? _dataProvider;

        #endregion

        #region 属性

        /// <summary>
        /// 参数依赖属性
        /// </summary>
        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(ImageThresholdParameters),
                typeof(ImageThresholdToolDebugControl), new PropertyMetadata(null, OnParametersChanged));

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageThresholdToolDebugControl control)
            {
                var oldParams = e.OldValue as ImageThresholdParameters;
                var newParams = e.NewValue as ImageThresholdParameters;

                PluginLogger.Info($"[OnParametersChanged] Parameters 变化 | " +
                    $"Old={(oldParams != null ? oldParams.GetHashCode().ToString("X8") : "null")} | " +
                    $"New={(newParams != null ? newParams.GetHashCode().ToString("X8") : "null")}",
                    "ImageThresholdTool");
            }
        }

        /// <summary>
        /// 工具参数
        /// </summary>
        public ImageThresholdParameters Parameters
        {
            get => (ImageThresholdParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        public AvailableDataSource? SelectedImageSource => Parameters?.ImageSource;

        #endregion

        #region 主窗口ImageControl绑定

        private ImageControl? _mainImageControl;

        public override void SetMainImageControl(ImageControl? imageControl)
        {
            _mainImageControl = imageControl;

            if (regionManager != null && imageControl != null)
            {
                regionManager.SetMainImageControl(imageControl);
            }
        }

        #endregion

        #region 构造函数

        public ImageThresholdToolDebugControl()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += OnWindowClosing;
                }
            };

            // 方法选择事件
            if (methodComboBox != null)
            {
                methodComboBox.SelectionChanged += OnMethodChanged;
            }

            Parameters = new ImageThresholdParameters();
            PluginLogger.Success("ImageThresholdToolDebugControl 构造函数完成", "ImageThresholdTool");
        }

        #endregion

        #region 资源清理

        private void Cleanup()
        {
            regionManager?.ClearMainOverlay();
            _mainImageControl = null;
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }

        #endregion

        #region 基类重写

        /// <summary>
        /// 设置参数引用
        /// </summary>
        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);

            if (parameters is ImageThresholdParameters thresholdParams)
            {
                Parameters = thresholdParams;

                var regionsCount = Parameters.InspectionRegions?.Regions?.Count ?? 0;

                if (regionsCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] InspectionRegions.Regions.Count=0", "ImageThresholdTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | Regions.Count={regionsCount}", "ImageThresholdTool");
                }

                PluginLogger.Success($"已加载节点参数: Method={Parameters.Method}", "ImageThresholdTool");
            }
            else
            {
                PluginLogger.Warning($"参数类型不匹配: 期望 ImageThresholdParameters，实际 {parameters?.GetType().Name}", "ImageThresholdTool");
            }
        }

        public override void SetCurrentNode(object node)
        {
            if (node == null) return;
            base.SetCurrentNode(node);
        }

        /// <summary>
        /// 执行工具
        /// </summary>
        protected override object ExecuteTool()
        {
            try
            {
                var imageSource = Parameters?.ImageSource;
                if (imageSource == null)
                {
                    throw new InvalidOperationException("请选择输入图像源");
                }

                if (_dataProvider == null)
                {
                    throw new InvalidOperationException("数据提供者未初始化");
                }

                if (!_dataProvider.HasNodeExecuted(imageSource.SourceNodeId))
                {
                    throw new InvalidOperationException("图像源节点尚未执行，请先执行前驱节点");
                }

                var imageMat = _dataProvider.GetPropertyValue(
                    imageSource.SourceNodeId,
                    imageSource.PropertyName
                ) as Mat;

                if (imageMat == null || imageMat.Empty())
                {
                    throw new InvalidOperationException("无法获取图像数据或图像为空");
                }

                var runParams = (ImageThresholdParameters)Parameters!;
                runParams.ResolvedInputImage = imageMat;

                var thresholdTool = new ImageThresholdTool();
                return thresholdTool.Run(runParams);
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"执行工具失败: {ex.Message}", "ImageThresholdTool", ex);

                var errorResult = new ImageThresholdResults
                {
                    ToolId = "image-threshold",
                    ToolName = "图像二值化",
                    Timestamp = DateTime.Now
                };
                errorResult.SetError(ex.Message, ex);
                return errorResult;
            }
        }

        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion

        #region 数据提供者设置

        public override void SetDataProvider(object provider)
        {
            base.SetDataProvider(provider);

            if (provider is DataSourceQueryService queryService)
            {
                _dataProvider = queryService;
                regionManager.AvailableDataSources = AvailableDataSources;
                Parameters?.RestoreImageSource(AvailableDataSources);
            }

            // 注意：不设置 imageSourceSelector.DataContext = this
            // imageSourceSelector 继承根元素的 DataContext (即 Parameters 对象)
            // {Binding ImageSource, Mode=TwoWay} 需要从 Parameters 上解析
            // 如果改成 this，绑定会解析到调试控件自身，导致图像源选择不同步

            // 注意：不设置 regionManager.DataContext = this
            // regionManager 继承根元素的 DataContext (即 Parameters 对象)
            // {Binding InspectionRegions.Regions} 需要从 Parameters 上解析
            // 如果改成 this，绑定会解析到调试控件自身，导致区域数据不同步
        }

        #endregion

        #region 方法切换

        /// <summary>
        /// 当二值化方法改变时，显示/隐藏相关参数
        /// </summary>
        private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
        {
            if (methodComboBox == null) return;

            var selectedIndex = methodComboBox.SelectedIndex;
            var isGlobalOrOtsu = selectedIndex == 0 || selectedIndex == 1;
            var isSmoothHisto = selectedIndex == 2;
            var isDynamic = selectedIndex == 3;
            var isLocalStats = selectedIndex == 4;

            // 阈值滑块（仅 Global 模式显示）
            if (thresholdParam != null)
                thresholdParam.Visibility = isGlobalOrOtsu ? Visibility.Visible : Visibility.Collapsed;

            // 亮暗选择（Dynamic/LocalStats 模式显示）
            if (lightDarkComboBox != null)
                lightDarkComboBox.Visibility = (isDynamic || isLocalStats) ? Visibility.Visible : Visibility.Collapsed;

            // 偏移量（仅 Dynamic 模式显示）
            if (offsetParam != null)
                offsetParam.Visibility = isDynamic ? Visibility.Visible : Visibility.Collapsed;

            // 平滑核大小（SmoothHisto/Dynamic 模式显示）
            if (smoothKernelParam != null)
                smoothKernelParam.Visibility = (isSmoothHisto || isDynamic) ? Visibility.Visible : Visibility.Collapsed;

            // 掩码宽/高、标准差缩放、绝对阈值偏移（仅 LocalStats 模式显示）
            if (maskWidthParam != null)
                maskWidthParam.Visibility = isLocalStats ? Visibility.Visible : Visibility.Collapsed;
            if (maskHeightParam != null)
                maskHeightParam.Visibility = isLocalStats ? Visibility.Visible : Visibility.Collapsed;
            if (stdDevScaleParam != null)
                stdDevScaleParam.Visibility = isLocalStats ? Visibility.Visible : Visibility.Collapsed;
            if (absThresholdParam != null)
                absThresholdParam.Visibility = isLocalStats ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
