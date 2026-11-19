# Blob 分析完整同构差异表

对照基线为 VisionMaster 4.4.0 Build20250516。按《完整同构复核清单与计划-20261119》的十三层复核层级逐层列出原厂证据、本方实现、差异、证据状态和闭合动作。底层算法库差异按既定规则允许。

原厂证据副本位于 `tools/SunEyeVision.Tool.BlobAnalysis/Resources/OriginalReleaseUnit/`，反编译源码位于 `docs/07-工具逆向/decompiled/`。

## 一、发布单元量化对照

| 项 | 原厂 | 本方 | 差异 |
|---|---|---|---|
| 运行单元 | `IMVSBlobFindModu.dll` | `SunEyeVision.Tool.BlobAnalysis.Runtime.dll` | 允许（语言与宿主不同） |
| 桥接单元 | `IMVSBlobFindModuCs.dll` | 同一共享程序集 | 程序集拆分未实施，见职责等价规则 |
| 模块元数据 | `IMVSBlobFindModu.xml` (16 KB) | `BlobReleaseUnitManifest.json` | 格式不同，职责需逐项对应 |
| 参数定义 | `IMVSBlobFindModuAlgorithm.xml` (11 KB) | `BlobParameters.xml` | **与页签文件合并** |
| 页签与控件 | `IMVSBlobFindModuAlgorithmTab.xml` (121 KB) | 合并在 `BlobParameters.xml` | **未分离** |
| 属性映射 | `IMVSBlobFindModuCs.xml` (41 KB) | `ParameterDefinition` 绑定表 | 格式不同 |
| 结果显示 | `IMVSBlobFindModuDisplay.xml` (10 KB) | `BlobDisplay.xml` | 对应 |
| 工具箱元数据 | `ToolItemInfo.xml` | `ToolItemInfo.xml` 未随工具发布 | **未进入发布包** |

原厂 `ToolItemInfo.xml` 只有 `name`、`priority`、`toolTip`、`helpUrl` 四项，没有 `displayType`，因此 Blob 节点使用平台默认形状；这与清单"不能由当前节点视觉反推原厂结构"一致。

## 二、十三层差异

### 1 工具部署

- 原厂：算法 DLL、Cs DLL、六个 XML 同目录发布。
- 本方：单项目编译一个共享程序集，同一程序集声明 Runtime 与 Editor 两个组件身份。
- 差异：程序集拆分未实施；证据缓存 `Resources/OriginalReleaseUnit/` 仍在产品源码树内且被 csproj 链接；旧 `tools/SunEyeVision.BlobAnalysis/` 工程目录仍在仓库。
- 状态：`derived`
- 闭合动作：证据归档到 `docs/evidence/original-release-units/`，门禁改读归档路径，移除 csproj 证据链接，删除旧工程目录。

### 2 节点画布

- 原厂：`ToolItemInfo.xml` 仅四项元数据，无 `displayType`；节点形状由平台按模块目录声明决定。
- 本方：节点通过工具目录注册，未单独声明形状。
- 差异：原厂模块目录层的 `displayType` 未在本方对应位置取证，当前无法确认 Blob 是否与其它工具一致走默认形状。
- 状态：`inferred`
- 闭合动作：补采原厂模块目录 `ToolItemInfo.xml` 完整副本，核对 `displayType` 缺省行为。

### 3 连线

- 原厂：`IMVSBlobFindModuCs.xml` 定义属性映射与端口。
- 本方：`BlobSchema.Outputs` 声明 50 余个输出端口，输入为 `input.image` 与 `input.pose`。
- 差异：端口数量与类型逐项对应，但原厂 Cs.xml 的端口分组语义未逐条比对。
- 状态：`derived`
- 闭合动作：按 Cs.xml 逐端口核对类型与可空性。

### 4 参数存储

- 原厂：`BlobFindParam : CModuleParamBase`（2065 行）以属性直接持有参数值。
- 本方：`BlobParameters.xml` 是默认值、范围、枚举的唯一真源；`BlobSettings` 构造时逐项从 `BlobParameterDefinition.Instance.Default<T>()` 取值，`BlobParameterDefinition.Load()` 从嵌入资源读取同一 XML。
- 差异：无第二份默认值来源，符合"参数只有一个正式来源"；但运行时值住在编辑器草稿，执行时冻结成 `BlobSettings`，该冻结链未与原厂 `CModuleParamBase` 时序对照。
- 状态：`derived`
- 闭合动作：核对冻结快照与执行读取的时序。

### 5 声明加载

- 原厂：参数定义、页签与控件、属性映射、结果显示四个独立文件。
- 本方：参数定义与页签分组**合并在** `BlobParameters.xml`，`Group` 元素同时携带 `page`、`section`、`vmCategory`。
- 差异：清单明确"未经证据不能与参数文件合并"，当前为合并实现。
- 状态：`derived`（偏差已确认）
- 闭合动作：拆出独立页签声明文件，解析器与门禁同步改读。

### 6 页面组装

- 原厂：平台 `ParamsTabControl` + `AlgorithmTabRoot` 统一组装，Blob 无专属 Control 程序集。
- 本方：`BlobUiComposition`（247 行）在公共 `DeclaredSettingsView` 之外承担页面构建钩子、ROI 输入页插入、位置修正辅助控件可见性、输入源支持状态和范围大小校验。
- 差异：页面级编排职责仍在工具层，其中"下限不得大于上限"的范围校验属于原厂序范围控件的固有行为，应由公共控件承担。
- 状态：`derived`（偏差已确认）
- 闭合动作：逐项判定每个职责的原厂归属，公共能力收回框架，工具只保留 ROI 与输入源专属接缝。

### 7 公共控件

- 原厂：数值、枚举、引用、布尔、图像、ROI、范围、颜色控件由平台 DataTemplate 提供。
- 本方：使用公共 Native 控件与 `DeclaredFieldTemplateRegistry`。
- 差异：无。
- 状态：`verified`（基本页静态门禁通过，Agent Desktop 快照不再泄露框架类型名）

### 8 工具专属控件

- 原厂：Blob 无专属 UI Control 程序集。
- 本方：无 Blob 专属 UI 程序集，编辑器并入单项目 Editor 目录。
- 差异：无。
- 状态：`verified`

### 9 ROI 和引用

- 原厂：`BlobFindRoiManager`（90 行）是工具 Cs 层专属类，持有 `RoiRectangle`、`RoiAnnulus`、`BlindPolygonString`，构造接收 `CModuleParamBase` 与 ROI 名称类型；视觉呈现走平台 `ROISelecter`。
- 本方：`BlobRoiUi`、`BlobLocalRoiOwner`、`BlobLocalRoiProjection` 为工具接缝，视觉由公共 `RoiGroupView` 承载。
- 差异：数据与视觉已分离，与原厂一致；原厂 ROI 模板与事件顺序仍无同版本运行证据。
- 状态：`derived`
- 闭合动作：采集原厂 ROI 交互运行证据，核对事件顺序。

### 10 结果显示

- 原厂：`ModuleResultView` 按数据类型组合显示，`BlobFindResult`（219 行）提供结果属性。
- 本方：`BlobDisplayUi` 配置可见性，`BlobSchema.OutputDisplay` 与公共结果呈现器组合。
- 差异：原厂结果树的显示顺序与事件时序未取证。
- 状态：`derived`
- 闭合动作：采集原厂结果页运行证据。

### 11 执行会话

- 原厂：`IMVSBlobFindModuTool : VmModule`（64 行）只有 `Run()`。
- 本方：`BlobTool`（210 行）实现十三个宿主接口，覆盖工具身份、参数声明、结果声明、执行输入、引用来源、已知设置校验、休眠默认值、字段联动、运行时初始化和结果呈现。
- 差异：接口数量多源自本方宿主用接口而非基类，职责上仍对应原厂 VmModule 与 Cs 层；但"算法成功与失败结果回写"没有可判定证据。
- 状态：`derived`
- 闭合动作：执行一次可判定成功的生产链并记录结果文本与状态字段。

### 12 持久化

- 原厂：参数经 `CModuleParamBase` 序列化，方案保存重开由宿主承担。
- 本方：`BlobSettings` 为可持久化记录，字段由 XML 声明驱动。
- 差异：保存重开在 Blob 上仅做过人工清单登记，未形成自动化证据。
- 状态：`inferred`
- 闭合动作：执行保存、关闭、重开、换节点自动链路。

### 13 生命周期

- 原厂：`BaseWindow` 的 `OnClosing`/`OnClosed` 与释放调用顺序。
- 本方：`BlobUiComposition.Dispose` 逐项释放 ROI、辅助控件、输入组合；Agent Desktop 已验证关闭后进程退出。
- 差异：原厂迟到回调拒写顺序未对照；本方关闭超时曾触发强制退出兜底。
- 状态：`derived`
- 闭合动作：补迟到回调与重复打开的自动用例。

## 三、状态汇总

| 状态 | 层数 | 层 |
|---|---|---|
| `verified` | 2 | 公共控件、工具专属控件 |
| `derived` | 9 | 工具部署、连线、参数存储、声明加载、页面组装、ROI、结果显示、执行会话、生命周期 |
| `inferred` | 2 | 节点画布、持久化 |
| `unconfirmed` | 0 | — |

按清单判定规则，Blob 尚不能称为工具同构：存在 2 个 `inferred` 层，且声明加载与页面组装两层为已确认偏差。

## 四、必须先闭合的四项

1. **声明分离**：参数定义与页签分组分离，解析器与门禁改读两个文件。
2. **页面编排回收**：Blob 工具层不再承担公共范围校验与页面级编排。
3. **发布单元收口**：证据缓存归档出产品源码树，旧工程目录删除。
4. **两项 inferred 转 derived 或 verified**：节点画布补原厂模块目录证据，持久化补保存重开自动链路。

前三项不依赖新的原厂运行证据，可立即实施；第四项中节点画布需要补采原厂模块目录文件，持久化需要自动链路用例。
