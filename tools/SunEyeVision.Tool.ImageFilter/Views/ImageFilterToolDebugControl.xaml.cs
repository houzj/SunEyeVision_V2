using System;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ImageFilter.Views
{
    /// <summary>
    /// 图像滤波工具调试控件
    /// </summary>
    public partial class ImageFilterToolDebugControl
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
                typeof(ImageFilterParameters),
                typeof(ImageFilterToolDebugControl),
                new PropertyMetadata(null, OnParametersChanged));

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageFilterToolDebugControl control)
            {
                var oldParams = e.OldValue as ImageFilterParameters;
                var newParams = e.NewValue as ImageFilterParameters;

                PluginLogger.Info($"[OnParametersChanged] Parameters 变化 | " +
                    $"Old={(oldParams != null ? oldParams.GetHashCode().ToString("X8") : "null")} | " +
                    $"New={(newParams != null ? newParams.GetHashCode().ToString("X8") : "null")}",
                    "ImageFilterTool");
            }
        }

        /// <summary>
        /// 参数实例
        /// </summary>
        public ImageFilterParameters Parameters
        {
            get => (ImageFilterParameters)GetValue(ParametersProperty);
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
                PluginLogger.Info("已为区域管理器设置主窗口ImageControl", "ImageFilterTool");
            }
        }

        #endregion

        #region 构造函数

        public ImageFilterToolDebugControl()
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

            Parameters = new ImageFilterParameters();
            PluginLogger.Success("ImageFilterToolDebugControl 构造函数完成", "ImageFilterTool");
        }

        public ImageFilterToolDebugControl(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
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

            if (parameters is ImageFilterParameters filterParams)
            {
                Parameters = filterParams;

                var regionsCount = Parameters.InspectionRegions?.Regions?.Count ?? 0;

                if (regionsCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] InspectionRegions.Regions.Count=0", "ImageFilterTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | Regions.Count={regionsCount}", "ImageFilterTool");
                }

                PluginLogger.Success($"已加载节点参数: FilterType={Parameters.FilterTypeValue}", "ImageFilterTool");
            }
            else
            {
                PluginLogger.Warning($"参数类型不匹配: 期望 ImageFilterParameters，实际 {parameters?.GetType().Name}", "ImageFilterTool");
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
                Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
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

            if (Tool is not ImageFilterTool filterTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (ImageFilterParameters)Parameters!;
            runParams.ResolvedInputImage = imageMat;
            return filterTool.Run(runParams);
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
