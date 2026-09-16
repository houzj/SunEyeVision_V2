using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

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

                // 参数变更时立即更新位置修正矩阵
                control.UpdatePositionCorrectionMatrix();
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
                UpdatePositionCorrectionMatrix();
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

                var roisCount = Parameters.InspectionRois?.Rois?.Count ?? 0;

                if (roisCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] InspectionRois.Rois.Count=0", "ImageThresholdTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | Rois.Count={roisCount}", "ImageThresholdTool");
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
                Parameters?.RestorePositionCorrectionSource(AvailableDataSources);

                // ★ 数据提供者就绪后立即查询矩阵（避免 OnParametersChanged 中 _dataProvider==null 导致矩阵丢失）
                UpdatePositionCorrectionMatrix();
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
                throw new InvalidOperationException(
                    $"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");
            }

            var imageVision = _dataProvider.GetPropertyValue(
                imageSource.SourceNodeId,
                imageSource.PropertyName
            ) as IVisionImage;

            if (imageVision == null || imageVision.IsEmpty)
            {
                throw new InvalidOperationException("无法获取图像数据或图像为空");
            }

            if (Tool is not ImageThresholdTool thresholdTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (ImageThresholdParameters)Parameters!;
            runParams.ResolvedInputImage = imageVision;
            var result = ExecuteWithLiveContext(thresholdTool, runParams);

            // 执行完成后，设置位置修正矩阵到 ROI 编辑器
            UpdatePositionCorrectionMatrix();

            return result;
        }

        /// <summary>
        /// 从 DataSourceQueryService 实时查询位置修正矩阵并设置到 ROI 编辑器
        /// 与结果层（Run）使用同一数据源，保证交互层与结果层一致
        /// </summary>
        /// <remarks>
        /// 使用 _dataProvider.GetPropertyValue() 实时查询而非 source.CurrentValue，
        /// 因为 CurrentValue 来自构造时缓存的 AvailableDataSource 实例，
        /// 工作流重新执行后新结果仅注入 DataSourceQueryService 缓存，旧实例的 CurrentValue 不会自动更新。
        /// </remarks>
        internal void UpdatePositionCorrectionMatrix()
        {
            PluginLogger.Info("[UpdatePositionCorrectionMatrix] 开始查询矩阵", "ImageThresholdTool");

            if (Parameters?.IsPositionCorrectionEnabled == true
                && _dataProvider != null
                && Parameters.PositionCorrectionBindingSource != null)
            {
                var source = Parameters.PositionCorrectionBindingSource;

                PluginLogger.Info($"[UpdatePositionCorrectionMatrix] 查询参数 | " +
                    $"SourceNodeId={source.SourceNodeId} | PropertyName={source.PropertyName}",
                    "ImageThresholdTool");

                // 实时查询，保证拿到工作流重新执行后的最新矩阵值
                var matrix = _dataProvider.GetPropertyValue(
                    source.SourceNodeId, source.PropertyName) as AffineMatrix;

                if (matrix != null)
                {
                    Parameters.LogInfo($"设置位置修正矩阵到 ROI 编辑器 (实时查询): {matrix}");
                    regionManager?.SetPositionCorrectionMatrix(matrix);
                    return;
                }
            }
            // ★ _dataProvider 未就绪时不设 null（保留之前的值，如 InitializeAsync 或 SetDataProvider 设置的）
            if (_dataProvider == null)
                return;

            PluginLogger.Warning("[UpdatePositionCorrectionMatrix] 矩阵为null，清空区域变换", "ImageThresholdTool");
            regionManager?.SetPositionCorrectionMatrix(null);
        }

        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        /// <summary>
        /// 外部结果到达通知 - 工作流引擎执行完成后调用，刷新位置修正矩阵
        /// </summary>
        public override void OnExternalResultArrived()
        {
            PluginLogger.Info("[OnExternalResultArrived] 外部结果到达，刷新位置修正矩阵", "ImageThresholdTool");
            UpdatePositionCorrectionMatrix();
        }

        #endregion

        #region IToolControlInitializer 实现

        private bool _isInitialized = false;

        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            // ★ 延迟到所有 Set 方法（SetCurrentNode, SetParameters, SetDataProvider）完成后才更新位置修正矩阵
            if (!_isInitialized)
            {
                _isInitialized = true;
                UpdatePositionCorrectionMatrix();
            }
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion
    }
}
