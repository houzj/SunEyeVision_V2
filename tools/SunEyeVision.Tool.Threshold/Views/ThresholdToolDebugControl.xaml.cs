using System;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.Threshold.Views
{
    /// <summary>
    /// 阈值化工具调试控件 - 纯声明式绑定架构
    /// </summary>
    /// <remarks>
    /// 架构优化：
    /// - 继承 ToolDebugControlBase 获得标准实现
    /// - 支持路由命令（ToolCommands.Execute, ToolCommands.Confirm）
    /// - 支持路由事件（ToolExecutionCompletedEvent）
    /// - 直接持有参数引用，零拷贝实时同步
    /// - 纯声明式XAML绑定，无手动绑定代码
    /// - 使用项目样式系统统一外观
    /// </remarks>
    public partial class ThresholdToolDebugControl
    {
        #region 字段

        // 数据提供者（直接使用 DataSourceQueryService）
        private DataSourceQueryService? _dataProvider;

        #endregion

        #region 属性

        /// <summary>
        /// 关联的工具实例
        /// </summary>
        public IToolPlugin? Tool { get; set; }

        /// <summary>
        /// 参数实例（依赖属性，支持属性变更通知）
        /// </summary>
        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(ThresholdParameters),
                typeof(ThresholdToolDebugControl),
                new PropertyMetadata(null, OnParametersChanged));

        private static void OnParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThresholdToolDebugControl control)
            {
                var oldParams = e.OldValue as ThresholdParameters;
                var newParams = e.NewValue as ThresholdParameters;

                PluginLogger.Info($"[OnParametersChanged] Parameters 依赖属性变化 | " +
                    $"Old={(oldParams != null ? $"HashCode={oldParams.GetHashCode():X8}, InspectionRegions.HashCode={oldParams.InspectionRegions?.GetHashCode():X8}" : "null")} | " +
                    $"New={(newParams != null ? $"HashCode={newParams.GetHashCode():X8}, InspectionRegions.HashCode={newParams.InspectionRegions?.GetHashCode():X8}, Regions.Count={newParams.InspectionRegions?.Regions?.Count ?? 0}" : "null")}",
                    "ThresholdTool");
            }
        }

        /// <summary>
        /// 参数实例（公共属性，用于XAML绑定）
        /// </summary>
        public ThresholdParameters Parameters
        {
            get
            {
                var value = (ThresholdParameters)GetValue(ParametersProperty);
                PluginLogger.Info($"[Parameters.Getter] 被调用 | 返回值={value?.GetType().Name ?? "null"} | InspectionRegions={value?.InspectionRegions?.Regions?.Count ?? 0}", "ThresholdTool");
                return value;
            }
            set
            {
                PluginLogger.Info($"[Parameters.Setter] 被调用 | 新值={value?.GetType().Name ?? "null"} | InspectionRegions={value?.InspectionRegions?.Regions?.Count ?? 0}", "ThresholdTool");
                SetValue(ParametersProperty, value);
            }
        }

        #endregion

        #region 数据源管理

        /// <summary>
        /// 当前选中的图像源（用于图像显示）
        /// </summary>
        public AvailableDataSource? SelectedImageSource => Parameters?.ImageSource;

        #endregion

        #region 主窗口ImageControl绑定

        /// <summary>
        /// 主窗口的ImageControl引用 - 用于区域编辑器绑定
        /// </summary>
        protected ImageControl? _mainImageControl;

        /// <summary>
        /// 设置主窗口的ImageControl - 用于区域编辑器绑定
        /// </summary>
        /// <param name="imageControl">主窗口的ImageControl</param>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            PluginLogger.Info($"[SetMainImageControl] 方法被调用 | ImageControl={imageControl?.GetType().Name ?? "null"}", "ThresholdTool");

            _mainImageControl = imageControl;

            // 设置检测区域编辑器
            if (inspectionRegionEditor != null && imageControl != null)
            {
                PluginLogger.Info($"[SetMainImageControl] 准备设置 inspectionRegionEditor | inspectionRegionEditor={inspectionRegionEditor.GetType().Name}", "ThresholdTool");
                inspectionRegionEditor.SetMainImageControl(imageControl);
                PluginLogger.Info("已为检测区域编辑器设置主窗口ImageControl", "ThresholdTool");
            }
            else
            {
                PluginLogger.Warning($"[SetMainImageControl] 跳过设置 | inspectionRegionEditor={inspectionRegionEditor?.GetType().Name ?? "null"} | imageControl={imageControl?.GetType().Name ?? "null"}", "ThresholdTool");
            }
        }

        #endregion

        #region 构造函数

        public ThresholdToolDebugControl()
        {
            PluginLogger.Info("ThresholdToolDebugControl 构造函数开始", "ThresholdTool");

            InitializeComponent();

            // 基类已自动调用 SetupCommandBindings()

            PluginLogger.Info("InitializeComponent 调用成功", "ThresholdTool");

            // 订阅窗口关闭事件（用于清理资源）
            this.Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += OnWindowClosing;
                    PluginLogger.Info("已订阅窗口关闭事件", "ThresholdTool");
                }
            };

            // 初始化默认参数（设计时使用）
            Parameters = new ThresholdParameters();
            PluginLogger.Info($"默认参数已初始化: TextConfig.OkColor={Parameters.TextConfig.OkColor}, TextConfig.NgColor={Parameters.TextConfig.NgColor}", "ThresholdTool");

            PluginLogger.Success("ThresholdToolDebugControl 构造函数完成", "ThresholdTool");
        }

        public ThresholdToolDebugControl(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
            : this()
        {
            Tool = toolPlugin;
        }

        /// <summary>
        /// 设置节点参数 - 重写基类虚方法
        /// </summary>
        /// <param name="parameters">节点参数实例（与WorkflowNode.Parameters同一实例）</param>
        public override void SetParameters(ToolParameters parameters)
        {
            PluginLogger.Info($"[SetParameters] 开始 | ParametersType={parameters?.GetType().Name ?? "null"} | ParametersHashCode={(parameters?.GetHashCode().ToString("X8") ?? "null")}", "ThresholdTool");

            // 调用基类方法
            base.SetParameters(parameters);

            if (parameters is ThresholdParameters thresholdParams)
            {
                // ★ 通过依赖属性设置参数（自动触发属性变更通知）
                Parameters = thresholdParams;

                // ★ 关键日志：记录 InspectionRegions 状态用于追踪区域序列化问题
                var inspectionRegions = Parameters.InspectionRegions;
                var inspectionRegionsHashCode = inspectionRegions?.GetHashCode() ?? 0;
                var inspectionRegionsCount = inspectionRegions?.Regions?.Count ?? 0;
                var inspectionRegionsCollectionHashCode = inspectionRegions?.Regions?.GetHashCode() ?? 0;

                // ★ 增强日志：检测区域数量是否为0（可能是反序列化问题）
                if (inspectionRegionsCount == 0)
                {
                    PluginLogger.Warning($"[SetParameters] ⚠️ InspectionRegions.Regions.Count=0！可能是反序列化问题 | " +
                        $"Parameters.HashCode={Parameters.GetHashCode():X8} | " +
                        $"InspectionRegions.HashCode={inspectionRegionsHashCode:X8}",
                        "ThresholdTool");
                }
                else
                {
                    PluginLogger.Info($"[SetParameters] 参数注入完成 | " +
                        $"Parameters.HashCode={Parameters.GetHashCode():X8} | " +
                        $"InspectionRegions={inspectionRegions?.GetType().Name ?? "null"} | " +
                        $"InspectionRegions.HashCode={inspectionRegionsHashCode:X8} | " +
                        $"InspectionRegions.Regions.Count={inspectionRegionsCount} | " +
                        $"InspectionRegions.Regions.HashCode={inspectionRegionsCollectionHashCode:X8}",
                        "ThresholdTool");
                }

                // 如果有区域，记录详细信息
                if (inspectionRegions?.Regions != null && inspectionRegions.Regions.Count > 0)
                {
                    foreach (var region in inspectionRegions.Regions)
                    {
                        PluginLogger.Info($"[SetParameters] 初始区域 | Name={region.Name} | Type={region.Parameters?.GetType().Name ?? "null"}",
                            "ThresholdTool");
                    }
                }

                PluginLogger.Success($"已加载节点参数: Threshold={Parameters.Threshold}", "ThresholdTool");
                PluginLogger.Info($"TextConfig.OkColor={Parameters.TextConfig.OkColor}, TextConfig.NgColor={Parameters.TextConfig.NgColor}", "ThresholdTool");

                // ✅ 不再在此调用 InitializeInspectionRegionEditor
                // 初始化移到 InitializeAsync 统一执行
            }
            else
            {
                PluginLogger.Warning($"参数类型不匹配: 期望 ThresholdParameters，实际 {parameters?.GetType().Name}", "ThresholdTool");
            }
        }

        #endregion

        #region RegionEditor

        /// <summary>
        /// 初始化检测区域编辑器
        /// </summary>
        /// <remarks>
        /// 共享引用建立策略：
        /// - XAML 绑定 Regions="{Binding InspectionRegions.Regions}" 仅用于初始化时读取
        /// - 真正的共享引用由本方法命令式建立
        /// - 调用 SetRegionsSource 确保 ViewModel 和 Parameters 指向同一个集合
        /// </remarks>
        private void InitializeInspectionRegionEditor()
        {
            PluginLogger.Info("[InitializeInspectionRegionEditor] 开始初始化", "ThresholdTool");

            if (inspectionRegionEditor == null)
            {
                PluginLogger.Warning("[InitializeInspectionRegionEditor] inspectionRegionEditor 为空，跳过初始化", "ThresholdTool");
                return;
            }

            // ★ 关键检查：Parameters 是否为 null
            if (Parameters == null)
            {
                PluginLogger.Error("[InitializeInspectionRegionEditor] ⚠️ Parameters 为 null，无法初始化区域编辑器！", "ThresholdTool");
                return;
            }

            // ★ 关键检查：InspectionRegions 是否为 null
            if (Parameters.InspectionRegions == null)
            {
                PluginLogger.Error("[InitializeInspectionRegionEditor] ⚠️ Parameters.InspectionRegions 为 null，无法初始化区域编辑器！", "ThresholdTool");
                return;
            }

            // ★ 关键检查：Regions 集合数量
            var regionsCount = Parameters.InspectionRegions.Regions?.Count ?? 0;
            PluginLogger.Info($"[InitializeInspectionRegionEditor] Regions 集合状态 | Count={regionsCount} | RegionsHashCode={(Parameters.InspectionRegions.Regions?.GetHashCode().ToString("X8") ?? "null")}", "ThresholdTool");

            if (regionsCount == 0)
            {
                PluginLogger.Warning("[InitializeInspectionRegionEditor] ⚠️ Regions.Count=0！区域数据可能丢失或反序列化失败", "ThresholdTool");
                // 注意：仍然继续执行，以便用户可以在调试窗口中查看/绘制新区域
            }

            // ★ 核心操作：命令式建立共享引用
            // ViewModel.Regions 和 Parameters.InspectionRegions.Regions 将指向同一个 ObservableCollection
            inspectionRegionEditor.SetRegionsSource(Parameters.InspectionRegions.Regions);

            PluginLogger.Success(
                $"检测区域编辑器初始化完成 | " +
                $"ViewModel.Regions.HashCode={inspectionRegionEditor.ViewModel?.Regions?.GetHashCode():X8} | " +
                $"Parameters.Regions.HashCode={Parameters.InspectionRegions.Regions.GetHashCode():X8} | " +
                $"引用相同={inspectionRegionEditor.ViewModel?.Regions == Parameters.InspectionRegions.Regions}",
                "ThresholdTool");
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 清理资源（窗口关闭时调用）
        /// </summary>
        public void Cleanup()
        {
            PluginLogger.Info("ThresholdToolDebugControl 清理资源", "ThresholdTool");

            // 清理区域编辑器
            if (inspectionRegionEditor != null)
            {
                inspectionRegionEditor.ClearMainOverlay();
                PluginLogger.Success("已清理检测区域编辑器", "ThresholdTool");
            }
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            PluginLogger.Info("窗口关闭，开始清理资源", "ThresholdTool");
            Cleanup();
        }

        #endregion

        #region 数据提供者设置

        public override void SetDataProvider(object dataProvider)
        {
            PluginLogger.Info($"SetDataProvider 被调用，dataProvider = {(dataProvider != null ? "非空" : "null")}", "ThresholdTool");

            // 调用基类方法以填充参数数据源和图像源
            base.SetDataProvider(dataProvider);

            // 保存引用用于执行逻辑
            if (dataProvider is DataSourceQueryService queryService)
            {
                _dataProvider = queryService;
                PluginLogger.Info("使用 DataSourceQueryService", "ThresholdTool");

                // TODO: 为 RegionEditorControl 设置 AvailableDataSources
                // 需要为 inspectionRegionEditor 和 maskRegionEditor 分别设置
                inspectionRegionEditor.AvailableDataSources = AvailableDataSources;
                PluginLogger.Info("已为检测区域编辑器设置参数数据源，总数: {AvailableDataSources.Count}", "ThresholdTool");
            }
            else
            {
                PluginLogger.Warning($"未知的 dataProvider 类型：{dataProvider?.GetType().Name}", "ThresholdTool");
            }
        }

        public override void SetCurrentNode(object node)
        {
            if (node == null)
                return;

            // 调用基类方法（提取 _currentNodeId、填充数据源）
            base.SetCurrentNode(node);

            // ✅ 不再在此提取 Parameters
            // 参数由 SetParameters 方法单独设置
            // InitializeInspectionRegionEditor 由 InitializeAsync 调用
        }




        #endregion

        #region 基类重写

        /// <summary>
        /// 执行工具逻辑 - 基类调用
        /// </summary>
        protected override object ExecuteTool()
        {
            // 检查图像源
            var imageSource = Parameters?.ImageSource;
            if (imageSource == null)
            {
                throw new InvalidOperationException("请选择输入图像源");
            }

            // 检查数据提供者
            if (_dataProvider == null)
            {
                throw new InvalidOperationException("数据提供者未初始化");
            }

            // 获取输入图像
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

            // 检查工具实例
            if (Tool is not ThresholdTool thresholdTool)
            {
                throw new InvalidOperationException("工具实例无效");
            }

            // 克隆参数用于执行（线程安全）
            var runParams = (ThresholdParameters)Parameters.Clone();

            // 执行工具
            return thresholdTool.Run(imageMat, runParams);
        }

        /// <summary>
        /// 判断是否可执行 - 基类调用
        /// </summary>
        protected override bool CanExecuteTool()
        {
            return Parameters?.ImageSource != null && _dataProvider != null;
        }

        #endregion

        #region 执行控制

        protected virtual void OnExecuteRequested()
        {
            // 基类已处理执行逻辑，这里保留是为了向后兼容（如果有其他地方调用）
            // 实际执行由基类的 OnExecuteCommand 调用 ExecuteTool() 完成
        }

        #endregion

        #region 结果判断逻辑

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private bool EvaluateResult(Mat outputImage)
        {
            if (outputImage == null || outputImage.Empty())
            {
                return !Parameters.ResultConfig.IsEmptyCheckEnabled;
            }

            var allChecksPassed = true;

            // 白色像素比例判断
            if (Parameters.ResultConfig.IsWhitePixelRatioCheckEnabled)
            {
                var whitePixelCount = Cv2.CountNonZero(outputImage);
                var totalPixels = outputImage.Rows * outputImage.Cols;
                var ratio = (double)whitePixelCount / totalPixels * 100;

                if (ratio < Parameters.ResultConfig.WhitePixelRatioMin || ratio > Parameters.ResultConfig.WhitePixelRatioMax)
                {
                    allChecksPassed = false;
                    PluginLogger.Warning($"白色像素比例 {ratio:F1}% 不在范围内 [{Parameters.ResultConfig.WhitePixelRatioMin}-{Parameters.ResultConfig.WhitePixelRatioMax}]", "ThresholdTool");
                }
            }

            // 输出均值判断
            if (Parameters.ResultConfig.IsMeanCheckEnabled)
            {
                var mean = Cv2.Mean(outputImage);
                var meanValue = mean.Val0;

                if (meanValue < Parameters.ResultConfig.MeanMin || meanValue > Parameters.ResultConfig.MeanMax)
                {
                    allChecksPassed = false;
                    PluginLogger.Warning($"输出均值 {meanValue:F1} 不在范围内 [{Parameters.ResultConfig.MeanMin}-{Parameters.ResultConfig.MeanMax}]", "ThresholdTool");
                }
            }

            // 输出面积判断
            if (Parameters.ResultConfig.IsAreaCheckEnabled)
            {
                var area = Cv2.CountNonZero(outputImage);

                if (area < Parameters.ResultConfig.AreaMin || area > Parameters.ResultConfig.AreaMax)
                {
                    allChecksPassed = false;
                    PluginLogger.Warning($"输出面积 {area} 不在范围内 [{Parameters.ResultConfig.AreaMin}-{Parameters.ResultConfig.AreaMax}]", "ThresholdTool");
                }
            }

            // 质心判断
            if (Parameters.ResultConfig.IsCentroidXCheckEnabled || Parameters.ResultConfig.IsCentroidYCheckEnabled)
            {
                var moments = Cv2.Moments(outputImage);
                var centroidX = moments.M10 / moments.M00;
                var centroidY = moments.M01 / moments.M00;

                if (Parameters.ResultConfig.IsCentroidXCheckEnabled)
                {
                    if (centroidX < Parameters.ResultConfig.CentroidXMin || centroidX > Parameters.ResultConfig.CentroidXMax)
                    {
                        allChecksPassed = false;
                        PluginLogger.Warning($"质心X {centroidX:F1} 不在范围内 [{Parameters.ResultConfig.CentroidXMin}-{Parameters.ResultConfig.CentroidXMax}]", "ThresholdTool");
                    }
                }

                if (Parameters.ResultConfig.IsCentroidYCheckEnabled)
                {
                    if (centroidY < Parameters.ResultConfig.CentroidYMin || centroidY > Parameters.ResultConfig.CentroidYMax)
                    {
                        allChecksPassed = false;
                        PluginLogger.Warning($"质心Y {centroidY:F1} 不在范围内 [{Parameters.ResultConfig.CentroidYMin}-{Parameters.ResultConfig.CentroidYMax}]", "ThresholdTool");
                    }
                }
            }

            return allChecksPassed;
        }

        #endregion

        #region 按钮事件处理

        private void OnRunButtonClick(object sender, RoutedEventArgs e)
        {
            OnExecuteRequested();
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            PluginLogger.Success("配置已确认", "ThresholdTool");
        }

        #endregion

        #region 图像显示事件

        private void OnToggleOutputImageVisibility(object sender, RoutedEventArgs e)
        {
            Parameters.DisplayConfig.OutputImage.IsVisible = !Parameters.DisplayConfig.OutputImage.IsVisible;
            PluginLogger.Info($"输出图像显示: {Parameters.DisplayConfig.OutputImage.IsVisible}", "ThresholdTool");
        }

        private void OnOpenOutputImageStyle(object sender, RoutedEventArgs e)
        {
            PluginLogger.Info("打开输出图像样式设置", "ThresholdTool");
            // TODO: 打开样式设置弹窗
        }

        private void OnToggleThresholdLineVisibility(object sender, RoutedEventArgs e)
        {
            Parameters.DisplayConfig.ThresholdLine.IsVisible = !Parameters.DisplayConfig.ThresholdLine.IsVisible;
            PluginLogger.Info($"阈值分界线显示: {Parameters.DisplayConfig.ThresholdLine.IsVisible}", "ThresholdTool");
        }

        private void OnOpenThresholdLineStyle(object sender, RoutedEventArgs e)
        {
            PluginLogger.Info("打开阈值分界线样式设置", "ThresholdTool");
            // TODO: 打开样式设置弹窗
        }

        private void OnToggleRegionVisibility(object sender, RoutedEventArgs e)
        {
            Parameters.DisplayConfig.Region.IsVisible = !Parameters.DisplayConfig.Region.IsVisible;
            PluginLogger.Info($"ROI区域显示: {Parameters.DisplayConfig.Region.IsVisible}", "ThresholdTool");
        }

        private void OnOpenRegionStyle(object sender, RoutedEventArgs e)
        {
            PluginLogger.Info("打开ROI区域样式设置", "ThresholdTool");
            // TODO: 打开样式设置弹窗
        }

        private void OnToggleHistogramVisibility(object sender, RoutedEventArgs e)
        {
            Parameters.DisplayConfig.Histogram.IsVisible = !Parameters.DisplayConfig.Histogram.IsVisible;
            PluginLogger.Info($"直方图显示: {Parameters.DisplayConfig.Histogram.IsVisible}", "ThresholdTool");
        }

        private void OnOpenHistogramStyle(object sender, RoutedEventArgs e)
        {
            PluginLogger.Info("打开直方图样式设置", "ThresholdTool");
            // TODO: 打开样式设置弹窗
        }

        #endregion

        #region IToolControlInitializer 实现

        /// <summary>
        /// 异步初始化 - 在所有 Set 方法完成后调用
        /// </summary>
        /// <remarks>
        /// 此时 Parameters、MainImageControl、DataProvider 全部就绪，
        /// 可以安全地初始化区域编辑器等子控件。
        /// </remarks>
        public override async System.Threading.Tasks.Task InitializeAsync()
        {
            // 初始化检测区域编辑器
            InitializeInspectionRegionEditor();

            await System.Threading.Tasks.Task.CompletedTask;
        }

        #endregion

        #region 底部按钮事件

        private void OnContinuousExecute(object sender, RoutedEventArgs e)
        {
            PluginLogger.Info("开始连续执行", "ThresholdTool");
            // TODO: 实现连续执行逻辑
        }

        private void OnExecuteClick(object sender, RoutedEventArgs e)
        {
            OnExecuteRequested();
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            PluginLogger.Success("配置已确认", "ThresholdTool");
        }

        #endregion
    }
}
