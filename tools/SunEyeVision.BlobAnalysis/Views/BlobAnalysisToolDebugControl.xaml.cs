using System;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;

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

            if (inspectionRegionEditor != null && imageControl != null)
            {
                inspectionRegionEditor.SetMainImageControl(imageControl);
                PluginLogger.Info("已为检测区域编辑器设置主窗口ImageControl", "BlobAnalysisTool");
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

                var inspectionRegions = Parameters.InspectionRegions;
                var inspectionRegionsCount = inspectionRegions?.Regions?.Count ?? 0;

                if (inspectionRegionsCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] InspectionRegions.Regions.Count=0", "BlobAnalysisTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | Regions.Count={inspectionRegionsCount}", "BlobAnalysisTool");
                }

                PluginLogger.Success($"已加载节点参数: Threshold={Parameters.Threshold}", "BlobAnalysisTool");
            }
            else
            {
                PluginLogger.Warning($"参数类型不匹配: 期望 BlobAnalysisParameters，实际 {parameters?.GetType().Name}", "BlobAnalysisTool");
            }
        }

        #endregion

        #region RegionEditor

        private void InitializeInspectionRegionEditor()
        {
            if (inspectionRegionEditor == null || Parameters == null || Parameters.InspectionRegions == null)
            {
                PluginLogger.Warning("[InitializeInspectionRegionEditor] 跳过初始化", "BlobAnalysisTool");
                return;
            }

            inspectionRegionEditor.SetRegionsSource(Parameters.InspectionRegions.Regions);

            PluginLogger.Success("检测区域编辑器初始化完成", "BlobAnalysisTool");
        }

        #endregion

        #region 资源清理

        public void Cleanup()
        {
            if (inspectionRegionEditor != null)
            {
                inspectionRegionEditor.ClearMainOverlay();
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
                inspectionRegionEditor.AvailableDataSources = AvailableDataSources;
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

            if (Tool is not BlobAnalysisTool blobTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            var runParams = (BlobAnalysisParameters)Parameters!.Clone();
            runParams.ResolvedInputImage = imageMat;
            return blobTool.Run(runParams);
        }

        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        #endregion

        #region 结果判断逻辑

        private bool EvaluateResult(Mat binaryImage)
        {
            if (binaryImage == null || binaryImage.Empty())
            {
                return !Parameters.ResultConfig.IsEmptyCheckEnabled;
            }

            var allChecksPassed = true;

            // 白色像素比例判断
            if (Parameters.ResultConfig.IsWhitePixelRatioCheckEnabled)
            {
                var whitePixelCount = Cv2.CountNonZero(binaryImage);
                var totalPixels = binaryImage.Rows * binaryImage.Cols;
                var ratio = (double)whitePixelCount / totalPixels * 100;

                if (ratio < Parameters.ResultConfig.WhitePixelRatioMin || ratio > Parameters.ResultConfig.WhitePixelRatioMax)
                {
                    allChecksPassed = false;
                    PluginLogger.Warning($"白色像素比例 {ratio:F1}% 不在范围内", "BlobAnalysisTool");
                }
            }

            // Blob数量判断
            if (Parameters.ResultConfig.IsBlobCountCheckEnabled)
            {
                // 使用筛选后的Blob数量 - 需要从结果获取
                // 此处暂时用 AllBlobs 的 Count，后续优化
            }

            return allChecksPassed;
        }

        #endregion

        #region IToolControlInitializer 实现

        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            InitializeInspectionRegionEditor();
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion
    }
}
