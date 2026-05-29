using System;
using System.Runtime.CompilerServices;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Tool.BlobAnalysis.Views
{
    /// <summary>
    /// Blob分析工具调试控件
    /// </summary>
    public partial class BlobAnalysisToolDebugControl
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
                typeof(BlobAnalysisParameters),
                typeof(BlobAnalysisToolDebugControl),
                new PropertyMetadata(null, OnParametersChanged));

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlobAnalysisToolDebugControl control)
            {
                var oldParams = e.OldValue as BlobAnalysisParameters;
                var newParams = e.NewValue as BlobAnalysisParameters;

                PluginLogger.Info($"[OnParametersChanged] Parameters 变化 | " +
                    $"Old={(oldParams != null ? oldParams.GetHashCode().ToString("X8") : "null")} | " +
                    $"New={(newParams != null ? newParams.GetHashCode().ToString("X8") : "null")}",
                    "BlobAnalysisTool");

                // 参数变更时立即更新位置修正矩阵
                control.UpdatePositionCorrectionMatrix();
            }
        }

        /// <summary>
        /// 参数实例
        /// </summary>
        public BlobAnalysisParameters Parameters
        {
            get => (BlobAnalysisParameters)GetValue(ParametersProperty);
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
                PluginLogger.Info("已为区域管理器设置主窗口ImageControl", "BlobAnalysisTool");
            }
        }

        #endregion

        #region 构造函数

        public BlobAnalysisToolDebugControl()
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

            Parameters = new BlobAnalysisParameters();
            PluginLogger.Success("BlobAnalysisToolDebugControl 构造函数完成", "BlobAnalysisTool");
        }

        public BlobAnalysisToolDebugControl(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
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

            if (parameters is BlobAnalysisParameters blobParams)
            {
                Parameters = blobParams;

                var regionsCount = Parameters.InspectionRegions?.Regions?.Count ?? 0;

                if (regionsCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] InspectionRegions.Regions.Count=0", "BlobAnalysisTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | Regions.Count={regionsCount}", "BlobAnalysisTool");
                }

                PluginLogger.Success($"已加载节点参数: Threshold={Parameters.Threshold}", "BlobAnalysisTool");
            }
            else
            {
                PluginLogger.Warning($"参数类型不匹配: 期望 BlobAnalysisParameters，实际 {parameters?.GetType().Name}", "BlobAnalysisTool");
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

                // 从 ImageSourceRef 恢复 ImageSource（加载后重建运行时数据）
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
                throw new InvalidOperationException("图像源节点尚未执行，请先执行前驱节点");
            }

            var image = _dataProvider.GetPropertyValue(
                imageSource.SourceNodeId,
                imageSource.PropertyName
            ) as IVisionImage;

            if (image == null || image.IsEmpty)
            {
                throw new InvalidOperationException("无法获取图像数据或图像为空");
            }

            if (Tool is not BlobAnalysisTool blobTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (BlobAnalysisParameters)Parameters!;
            runParams.ResolvedInputImage = image;
            var result = blobTool.Run(runParams);

            // 执行完成后，设置位置修正矩阵到 ROI 编辑器
            UpdatePositionCorrectionMatrix();

            return result;
        }

        /// <summary>
        /// 从 DataSourceQueryService 实时查询位置修正矩阵并设置到 ROI 编辑器
        /// 与结果层（BlobAnalysisTool.Run）使用同一数据源，保证交互层与结果层一致
        /// </summary>
        /// <remarks>
        /// 使用 _dataProvider.GetPropertyValue() 实时查询而非 source.CurrentValue，
        /// 因为 CurrentValue 来自构造时缓存的 AvailableDataSource 实例，
        /// 工作流重新执行后新结果仅注入 DataSourceQueryService 缓存，旧实例的 CurrentValue 不会自动更新。
        /// </remarks>
        internal void UpdatePositionCorrectionMatrix()
        {
            PluginLogger.Info("[UpdatePositionCorrectionMatrix] 开始查询矩阵", "BlobAnalysisTool");

            if (Parameters?.IsPositionCorrectionEnabled == true
                && _dataProvider != null
                && Parameters.PositionCorrectionBindingSource != null)
            {
                var source = Parameters.PositionCorrectionBindingSource;

                PluginLogger.Info($"[UpdatePositionCorrectionMatrix] 查询参数 | " +
                    $"SourceNodeId={source.SourceNodeId} | PropertyName={source.PropertyName}",
                    "BlobAnalysisTool");

                // 实时查询，保证拿到工作流重新执行后的最新矩阵值
                var matrix = _dataProvider.GetPropertyValue(
                    source.SourceNodeId, source.PropertyName) as AffineMatrix;

                // ★ 诊断：矩阵详细信息
                var matrixValues = matrix != null
                    ? $"[{matrix.M11:F4},{matrix.M12:F4},{matrix.M21:F4},{matrix.M22:F4},{matrix.OffsetX:F2},{matrix.OffsetY:F2}]"
                    : "null";
                PluginLogger.Info($"[UpdatePositionCorrectionMatrix] 查询结果 | Matrix={matrixValues}", "BlobAnalysisTool");

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

            PluginLogger.Warning("[UpdatePositionCorrectionMatrix] 矩阵为null，清空区域变换", "BlobAnalysisTool");
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
            PluginLogger.Info("[OnExternalResultArrived] 外部结果到达，刷新位置修正矩阵", "BlobAnalysisTool");

            PluginLogger.Info($"[OnExternalResultArrived] 前置检查 | " +
                $"IsEnabled={Parameters?.IsPositionCorrectionEnabled} | " +
                $"DataProvider={_dataProvider != null} | " +
                $"BindingSource={Parameters?.PositionCorrectionBindingSource != null}",
                "BlobAnalysisTool");

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
