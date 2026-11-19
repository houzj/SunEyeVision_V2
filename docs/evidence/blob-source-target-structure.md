# Blob 分析单项目目标结构

本结构以 VisionMaster 4.4.0 Build20250516 的职责和发布边界为基线，结合当前仓库已经落地的单项目迁移。原厂的算法 DLL、Cs 桥接 DLL、AlgorithmTab XML、Display XML 和 ToolItemInfo XML 是发布事实。本方底层算法允许使用 HALCON，工具 UI、参数语义、端口、结果和生命周期仍由本方统一框架按原厂证据承载。

```text
tools/
└─ SunEyeVision.Tool.BlobAnalysis/
   ├─ SunEyeVision.Tool.BlobAnalysis.csproj
   │  # Blob 唯一项目，编译为 SunEyeVision.Tool.BlobAnalysis.Runtime.dll。
   │  # 同一程序集声明 Runtime 与 Editor 两个 ToolComponent 身份。
   ├─ BlobTool.cs
   │  # 工具入口、ToolId、能力、输入输出、参数与结果接缝。
   ├─ BlobPresentation.cs
   │  # Blob 结果呈现声明接缝。
   ├─ Algorithms/
   │  ├─ BlobAlgorithm.cs / BlobAlgorithm.Boundary.cs / BlobAlgorithm.Operations.cs
   │  │  # Blob 算法入口、分割、连通域、特征计算、边界和结果组装。
   │  ├─ BlobThresholdTransform.cs
   │  │  # 阈值模式和值域到算法输入的转换。
   │  ├─ BlobCandidateSelection.cs
   │  │  # 候选筛选、排序和数量限制。
   │  ├─ BlobLocalRoiRasterizer.cs / BlobResultGeometry.cs
   │  │  # ROI 栅格化和结果几何计算。
   │  ├─ BlobNumericSemantics.cs
   │  │  # 数值边界和精度语义。
   │  ├─ HalconBlobAlgorithmContext.cs
   │  │  # 单次算法调用期间的 HALCON 临时上下文和恢复责任。
   │  └─ BlobDiagnostics.cs
   │     # 算法诊断和耗时记录。
   ├─ Parameters/
   │  ├─ BlobSettings.cs
   │  │  # 可持久化设置模型。
   │  ├─ BlobParameterDefinition.cs
   │  │  # 参数 XML、字段类型、默认值、范围、枚举和绑定映射。
   │  ├─ BlobFindParameters.cs
   │  │  # 冻结设置到算法参数的投影。
   │  ├─ BlobImageRoi.cs / BlobLocalRoi.cs
   │  │  # 图像 ROI 输入和本地 ROI 几何。
   │  ├─ BlobRoiInheritance.cs / BlobMaskInheritance.cs
   │  │  # ROI、屏蔽区继承和输入清理。
   │  └─ BlobInput.cs / BlobFixtureInputs.cs / BlobMetadataAttributes.cs / BlobThresholdCapabilities.cs
   │     # 输入组合、测试夹具、元数据和能力门禁。
   ├─ Results/
   │  ├─ BlobOutput.cs
   │  │  # 单次算法结果及输出资源所有权。
   │  ├─ BlobSchema.cs
   │  │  # 输入端口、输出端口和端口类型。
   │  ├─ BlobResultDisplay.cs / BlobDisplayDefinition.cs
   │  │  # 结果显示字段和 BlobDisplay.xml 映射。
   │  ├─ BlobResultProjection.cs / BlobOutputDisplay.cs
   │  │  # 结果树、表格和渲染器数据投影。
   │  └─ BlobRoiDisplayProjection.cs
   │     # ROI 结果显示投影。
   ├─ Editor/
   │  ├─ BlobEditorFactory.cs
   │  │  # 同程序集 Editor 入口。
   │  ├─ Views/BlobParameterEditor.xaml(.cs)
   │  │  # Blob 专属参数页接缝和原厂 ROI/运行页组合。
   │  ├─ Views/BlobUiComposition.cs / BlobRoiUi.cs / BlobRoiFields.cs
   │  │  # Blob 专属组装和 ROI 数据接缝。
   │  ├─ Views/BlobLocalRoiOwner*.cs / BlobRunUi.cs / BlobDisplayUi.cs
   │  │  # ROI、运行参数、结果显示和专属状态投影。
   │  └─ Design/BlobDesignData.cs
   │     # 设计时数据。
   └─ Resources/
      ├─ BlobParameters.xml
      │  # 参数声明映射，页签/分组/字段顺序按原厂 AlgorithmTab 证据。
      ├─ BlobDisplay.xml
      │  # 结果显示声明映射。
      └─ OriginalReleaseUnit/
         # 原厂 Algorithm、AlgorithmTab、Display、Cs、ToolItemInfo 和映射清单。
```

## 公共框架边界

```text
src/Plugin.Wpf/
├─ Presenters/Parameters/  # 字段模板、参数页和引用状态
├─ Presenters/Regions/     # ROI、继承来源和区域编辑器
├─ Presenters/Results/     # 结果树、表格和图像叠加
├─ Presenters/Commands/    # 底栏命令和执行会话
└─ RuntimeToolEditorWindow.cs  # 统一窗口生命周期、关闭排空和资源释放
```

Blob 工具保留专属数据接缝和原厂证据映射。公共控件、页签容器、字段模板、ROI 容器、结果树、底栏命令和窗口生命周期由统一框架提供。

## 发布映射

```text
SunEyeVision.Tool.BlobAnalysis.Runtime.dll
├─ Runtime: SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool
└─ Editor:  SunEyeVision.Tool.BlobAnalysis.Wpf.BlobEditorFactory
```

Editor 类型命名保留现有程序集兼容标识，物理工程已经归并到单项目。发布载荷只包含一个 Blob 程序集，Runtime-only 加载按角色过滤，PE 校验按同一路径的完整 Runtime/Editor 声明核验。
