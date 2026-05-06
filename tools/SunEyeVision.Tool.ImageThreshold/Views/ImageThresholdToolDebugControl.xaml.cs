using System;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ImageThreshold.Views
{
    /// <summary>
    /// 图像二值化工具调试控件
    /// </summary>
    public partial class ImageThresholdToolDebugControl
    {
        #region 字段

        private DataSourceQueryService? _dataProvider;

        #endregion

        #region 属性

        /// <summary>
        /// 关联的工具实例
        /// </summary>
        public IToolPlugin? Tool { get; set; }

        /// <summary>
        /// 参数实例（依赖属性）
        /// </summary>
        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(ImageThresholdParameters),
                typeof(ImageThresholdToolDebugControl),
                new PropertyMetadata(null, OnParametersChanged));

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
        /// 参数实例
        /// </summary>
        public ImageThresholdParameters Parameters
        {
            get => (ImageThresholdParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        #endregion

        #region 数据源管理

        public AvailableDataSource? SelectedImageSource => Parameters?.ImageSource;

        #endregion

        #region 主窗口ImageControl绑定

        protected ImageControl? _mainImageControl;

        public override void SetMainImageControl(ImageControl? imageControl)
        {
            _mainImageControl = imageControl;

            if (regionManager != null && imageControl != null)
            {
                regionManager.SetMainImageControl(imageControl);
                PluginLogger.Info("已为区域管理器设置主窗口ImageControl", "ImageThresholdTool");
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

            Parameters = new ImageThresholdParameters();
            PluginLogger.Success("ImageThresholdToolDebugControl 构造函数完成", "ImageThresholdTool");
        }

        public ImageThresholdToolDebugControl(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
            : this()
        {
            Tool = toolPlugin;
        }

        /// <summary>
        /// 设置节点参数
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

        #endregion

        #region 资源清理

        public void Cleanup()
        {
            if (regionManager != null)
            {
                regionManager.ClearMainOverlay();
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }

        #endregion

        #region 数据提供者设置

        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);

            if (dataProvider is DataSourceQueryService queryService)
            {
                _dataProvider = queryService;
                regionManager.AvailableDataSources = AvailableDataSources;

                Parameters?.RestoreImageSource(AvailableDataSources);
            }
        }

        public override void SetCurrentNode(object node)
        {
            if (node == null) return;
            base.SetCurrentNode(node);
        }

        #endregion

        #region 基类重写

        protected override object ExecuteTool()
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

            if (Tool is not ImageThresholdTool thresholdTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (ImageThresholdParameters)Parameters!;
            runParams.ResolvedInputImage = imageMat;
            return thresholdTool.Run(runParams);
        }

        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        #endregion

        #region IToolControlInitializer 实现

        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion
    }
}
