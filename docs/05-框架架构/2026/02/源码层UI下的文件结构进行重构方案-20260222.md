src/UI/
├── App.xaml                    # 应用程序入口
├── App.xaml.cs
│
├── Views/                      # 【视图层】XAML 视图文件
│   ├── Windows/                # 窗口
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── AboutWindow.xaml
│   │   ├── DebugWindow.xaml    # 诊断窗口
│   │   └── HelpWindow.xaml
│   │
│   ├── Controls/               # 自定义控件
│   │   ├── Canvas/             # 画布控件
│   │   │   ├── WorkflowCanvasControl.xaml
│   │   │   ├── NativeDiagramControl.xaml
│   │   │   └── VirtualizedCanvas.cs
│   │   ├── Toolbox/            # 工具箱控件
│   │   │   ├── ToolboxControl.xaml
│   │   │   └── ToolboxControl.xaml.cs
│   │   ├── Panels/             # 面板控件
│   │   │   ├── PropertyPanelControl.xaml
│   │   │   └── ImagePreviewControl.xaml
│   │   ├── Common/             # 通用控件
│   │   │   ├── LoadingWindow.xaml
│   │   │   ├── SelectionBox.xaml
│   │   │   └── SplitterWithToggle.cs
│   │   └── Templates/          # 模板和样式
│   │       ├── CanvasTemplateSelector.cs
│   │       └── ToolDebugTemplates.xaml
│   │
│   └── Resources/              # 资源文件
│       ├── Styles/             # 样式
│       ├── Icons/              # 图标
│       └── AppResources.xaml
│
├── ViewModels/                 # 【视图模型层】
│   ├── MainWindowViewModel.cs
│   ├── WorkflowViewModel.cs
│   ├── WorkflowTabViewModel.cs
│   ├── WorkflowTabControlViewModel.cs
│   ├── ToolboxViewModel.cs
│   ├── PropertyPanelViewModel.cs
│   ├── DevicePanelViewModel.cs
│   ├── ViewModelBase.cs
│   └── RelayCommand.cs
│
├── Converters/                 # 【值转换器】
│   ├── Visibility/             # 可见性转换
│   │   ├── BoolToVisibilityConverter.cs
│   │   ├── InverseBoolToVisibilityConverter.cs
│   │   ├── NullToVisibilityConverter.cs
│   │   ├── StringToVisibilityConverter.cs
│   │   └── IntToVisibilityConverter.cs
│   ├── Workflow/               # 工作流相关
│   │   ├── RunModeConverter.cs
│   │   ├── RunModeButtonConverter.cs
│   │   └── WorkflowStateToColorConverter.cs
│   ├── Path/                   # 路径计算相关
│   │   ├── SmartPathConverter.cs
│   │   ├── SmartPathMultiConverter.cs
│   │   └── PointOffsetConverter.cs
│   ├── Node/                   # 节点显示相关
│   │   ├── NodeDisplayConverter.cs
│   │   └── ImageAreaHeightConverter.cs
│   └── UI/                     # UI通用转换
│       ├── BoolToActiveConverter.cs
│       ├── BoolToSelectedConverter.cs
│       ├── ExpandIconConverter.cs
│       └── ContinuousRunIconConverter.cs
│
├── Adapters/                   # 【适配器层】
│   ├── DiagramAdapter.cs
│   ├── IDiagramAdapter.cs
│   ├── NodeDisplay/            # 节点显示适配器
│   │   ├── INodeDisplayAdapter.cs
│   │   ├── NodeDisplayAdapterFactory.cs
│   │   ├── NodeDisplayAdapterConfig.cs
│   │   ├── DefaultNodeDisplayAdapter.cs
│   │   ├── ImageSourceNodeDisplayAdapter.cs
│   │   ├── ProcessingNodeDisplayAdapter.cs
│   │   └── ...
│   └── LibraryValidator.cs
│
├── Services/                   # 【服务层】按职责细分
│   ├── Canvas/                 # 画布服务
│   │   ├── CanvasConfig.cs
│   │   ├── CanvasEngineManager.cs
│   │   ├── CanvasStateManager.cs
│   │   ├── CanvasHelper.cs
│   │   └── ICanvasEngine.cs
│   │
│   ├── Workflow/               # 工作流服务
│   │   ├── WorkflowExecutionManager.cs
│   │   ├── WorkflowNodeFactory.cs
│   │   └── IWorkflowNodeFactory.cs
│   │
│   ├── Interaction/            # 交互服务（原 WorkflowCanvasService）
│   │   ├── NodeDragHandler.cs
│   │   ├── ConnectionDragHandler.cs
│   │   ├── PortInteractionHandler.cs
│   │   ├── BoxSelectionHandler.cs
│   │   └── NodeSequenceManager.cs
│   │
│   ├── Connection/             # 连接服务
│   │   ├── ConnectionService.cs
│   │   ├── ConnectionPathService.cs
│   │   ├── ConnectionPathCache.cs
│   │   └── ConnectionBatchUpdateManager.cs
│   │
│   ├── Path/                   # 路径计算服务
│   │   ├── Calculators/
│   │   │   ├── IPathCalculator.cs
│   │   │   ├── OrthogonalPathCalculator.cs
│   │   │   ├── BezierPathCalculator.cs
│   │   │   ├── AIStudioPathCalculator.cs
│   │   │   └── PathCalculatorFactory.cs
│   │   └── Planning/           # 路径规划
│   │
│   ├── Node/                   # 节点服务
│   │   ├── NodeIndexManager.cs
│   │   ├── NodeSelectionService.cs
│   │   └── PortService.cs
│   │
│   ├── Rendering/              # 渲染服务
│   │   ├── CanvasRenderer.cs
│   │   ├── GeometryOptimizer.cs
│   │   └── SpatialIndex.cs
│   │
│   ├── Thumbnail/              # 缩略图服务（原 Controls/Rendering）
│   │   ├── SmartThumbnailLoader.cs
│   │   ├── ThumbnailCacheManager.cs
│   │   ├── PriorityThumbnailLoader.cs
│   │   ├── IThumbnailDecoder.cs
│   │   ├── Decoders/
│   │   │   ├── WicGpuDecoder.cs
│   │   │   ├── ImageSharpDecoder.cs
│   │   │   └── AdvancedGpuDecoder.cs
│   │   └── Caching/
│   │       ├── GPUCache.cs
│   │       └── WeakReferenceCache.cs
│   │
│   ├── Performance/            # 性能服务
│   │   ├── PerformanceMonitor.cs
│   │   ├── PerformanceBenchmark.cs
│   │   ├── MemoryPressureMonitor.cs
│   │   ├── BatchUpdateManager.cs
│   │   └── EnhancedBatchUpdateManager.cs
│   │
│   ├── Toolbox/                # 工具箱服务
│   │   ├── ToolboxInteractionTimer.cs
│   │   ├── ToolboxPopupStateManager.cs
│   │   └── ToolboxToolCacheManager.cs
│   │
│   └── Plugin/                 # 插件服务
│       └── PluginUIAdapter.cs
│
├── Models/                     # 【UI 数据模型】
│   ├── WorkflowNodeModel.cs    # 重命名，避免与 Workflow.WorkflowNode 冲突
│   ├── NodeStyleConfig.cs
│   ├── LayoutConfig.cs
│   ├── ToolItem.cs
│   ├── PropertyItem.cs
│   ├── DeviceItem.cs
│   ├── NodeImageData.cs
│   ├── WorkflowInfo.cs
│   └── ObservableObject.cs
│
├── Commands/                   # 【命令模式】撤销/重做
│   ├── CommandManager.cs
│   ├── AddNodeCommand.cs
│   ├── DeleteNodeCommand.cs
│   ├── MoveNodeCommand.cs
│   ├── AddConnectionCommand.cs
│   ├── DeleteConnectionCommand.cs
│   ├── BatchMoveNodesCommand.cs
│   └── BatchDeleteNodesCommand.cs
│
├── Diagnostics/                # 【诊断工具】
│   ├── DebugControlManager.cs
│   ├── IDebugControlProvider.cs
│   ├── SharedDebugControl.xaml
│   └── ConsoleLogger.cs
│
├── Events/                     # 【事件定义】
│   └── UIEvents.cs
│
├── Extensions/                 # 【扩展方法】
│   └── WorkflowNodeExtensions.cs
│
└── Infrastructure/             # 【基础设施】
    ├── ServiceLocator.cs
    ├── DefaultInputProvider.cs
    ├── IInputProvider.cs
    └── UIEventPublisher.cs
