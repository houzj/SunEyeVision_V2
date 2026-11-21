**2026-11-20 Blob 标签分析算法与单项目复核**：BlobLabels 已从 Runtime+Wpf 双项目迁移为 `tools/SunEyeVision.BlobLabels/SunEyeVision.Tool.BlobLabels.csproj`，Runtime/Editor 组件注册、解决方案、产品清单和生产探针已统一到单项目。HALCON 算法链执行静态复核，现有 30 项 BlobLabels 测试通过，覆盖阈值区域、连通域、筛选排序、FindCount、判定、结果几何、参数往返和资源释放。原厂 `IMVSBlobFindLabelsModu` 的 Process/ProcessAlg/LimitJudge/ProcessOutput 宿主边界已由 REA 静态确认，外部算法 DLL 已定位并取证，但没有动态黄金样例，因此算法数值等价、阈值合并/反选顺序、连通性、特征定义、排序稳定性、ClassInfo/OverlapRatio/MaxOutPixelNum 和结果资源释放时序继续标记为未确认。完整 Release 构建封存通过，载荷哈希 `423C0BACCBEE835254D9A37AFE1A41D2F990EB9FD2267A66FBD4A5576958D535`；Agent Desktop 启动 Release 进入“太阳眼视觉 - 机器视觉平台”并强制关闭确认进程退出。证据：`docs/evidence/bloblabels-single-project-review-20261120.md`、`docs/evidence/tool-batch-manifest-20261120.json`。**2026-11-20 启动失败与启动卡死修复**：两个独立问题。① 构建后启动失败：`release-payload.json` 只在 UI 工程构建时封存，单独构建工具/插件或探针重建依赖后载荷哈希与磁盘程序不一致，启动被载荷校验拒绝。现在 `installer/Release.Payload.targets` 在任一产品工程（写 Release 根且本次确实编译过）构建后自动封存；解决方案构建仍只由 UI 工程封存一次；并发构建用 `Global\SunEyeVision.ReleaseSeal` 互斥锁串行，并在 `installer/Seal-ReleasePayloadDebounced.ps1` 内按清单新鲜度合并重复请求，UI 最终强制封存。验证：单工具构建封存 1 次、UI 工程构建 5 次+27 次跳过、解决方案构建 1 次，均 0 错误；构建后直接启动 Release 正常。② 启动窗口卡死：UI 线程被同步阶段堵住时，原看门狗只 `Dispatcher.InvokeAsync`，同样排队无法执行，表现为无提示、不超时、关闭按钮无效。现在看门狗在 30 秒后用 `DispatcherPriority.Send` 探测 UI 线程，3 秒无响应即由独立 STA 线程显示最后阶段并强制退出；启动轨迹无条件写入 `%LOCALAPPDATA%\SunEyeVision\Logs\startup-trace.txt`。插件加载异常改为明确提示重新封存/使用启动器。验证：Release 启动 1.3s 进入起始页，轨迹文件正常；FrameMean+ColorExtract 测试 36/36；ControlNamingProbe 退出码 0。

**2026-11-20 FrameMean/ColorExtract 专属控件上移共享层**：原厂 `CommandSelecter_Frame` 与 `ColorExtractList`+`UserScrollView` 不再由工具手写。新增共享 `NativeCommandSelector`（两命令 + 已统计数量 + 清空，使用 CmdTypeRadioButtonStyle/CmdTypeRadbtnDeleteStyle 与 73/16/32 网格）和 `NativeColorRangeList`（卡片列表、颜色空间、三通道直方图/滑条、颜色反转，使用 FourColorBorder/SliderArrange/BooleanControl/LeftRightControl），工具只保留命令值、九字段区间序列化和提交接缝。共享直方图 `HikHistogramSingle` 更名 `HistogramSingle`，新控件不含原厂产品名或品牌名。Agent Desktop 复核：`framemean/run-shared-final.png`、`framemean/basic-shared-final.png`、`framemean/result-shared-final.png`、`colorextract/run-shared-final.png`、`colorextract/basic-shared-final.png`、`colorextract/result-shared-final2.png` 与 VM 原厂运行页对照；ColorExtract 结果页标题与「颜色面积判断」已按原厂修正。ColorExtract 的「颜色区域选择」（RoiSelectControl）仍缺，已登记。验证：全量 Release 构建 0 错误；FrameMean+ColorExtract 测试 36/36；ControlNamingProbe 退出码 0；封存 `F705EAAF52671A3AA4BB8567FB69B9C82CB35F810ADC945785CD7FDFACC37F61`。未做：ColorExtract RoiSelectControl、基本页 ROI 组、TextDisplayGroup、滑条保存重开人工复核。

**2026-11-20 标准基本参数页与亮度测量收口**：新增 `NativeStandardBasicPage`（`src/Plugin.Wpf/Presenters/Parameters/`），把「输入行 → ROI 区域 → 位置修正开关 → Assist 组」的抑制行、ROI 编辑宿主、位置修正联动和释放固化为一个共享入口；工具只提供 `NativeStandardBasicPageSpec`。亮度测量已接入：输入行标签改为「输入源」，ROI 区域显示输出掩膜/ROI区域类型/ROI创建/形状/位置修正，屏蔽区按原厂基本页隐藏，位置修正恢复为开关且不再出现清除 ×；结果显示页顺序改为「结果判断 → 图像显示 → 文本显示」，图像显示为「检测区域、文本」，文本默认内容改为原厂模板。测试同步更新：`IntensityMeasure` 27/27 通过；全量构建 0 错误；封存 `CCDB5878EE08CFF16C493EDFB9F733A6DB566D6D64EBBAB62093732E65BBC24B`。标准页说明见 `docs/evidence/tool-standard-basic-page-20261120.html`，技能新增 `references/standard-basic-page.md`。PixelCount 已改用同一入口，36/36 测试通过。亮度测量结果显示已对齐原厂五行图像显示（检测区域、基准点、运行点、文本、扇环检测区域）。未确认：基准点/运行点/扇环的运行时叠加几何尚未由会话输出、文本占位符运行期解析、算法等价。

**2026-11-20 代码层算法逻辑对齐**：新增 `docs/evidence/algorithm-logic-alignment-20261120.html`，逐工具记录算法链与原厂参数/结果语义的对应。IntensityMeasure、PixelCount、FrameMean、ColorExtract 的输入、区域/掩膜、阈值/统计、颜色空间、面积筛选和输出顺序按声明实现；代码层不判数值精度。仍未闭合：IntensityMeasure 的位置修正变换未消费、直方图对比度公式未取到原厂源码、扇环/基准点/运行点叠加几何未输出。构建 0 错误；PixelCount+IntensityMeasure 63/63，FrameMean+ColorExtract 36/36，探针退出码 0。工程验证阶段再比对结果与精度。

## 工具同构复核规则（2026-11-19）

1. 原厂证据只确认工具职责、参数、页签、分组、端口、结果、资源和生命周期。
2. 原厂与本方唯一允许的实现差异是底层算法库。原厂使用海康自研算法，本方可使用 HALCON 或 OpenCvSharp 完成同等算法应用。
3. HALCON 和 OpenCvSharp 的类型转换、所有权、初始化和资源释放由独立适配层负责；工具的 `Algorithms/` 实现可以在适配层交付的对象上直接调用已选算法库的其他算子，不把第三方生命周期管理塞进工具。
4. 工具层只负责工具入口、参数、输入输出、算法调用接缝、结果声明和工具资源。
5. 公共参数页、页签、字段控件、ROI、结果树、底栏命令和窗口生命周期由统一框架负责。
6. 底层算法库可以替换，工具 UI、参数语义、端口、结果结构和生命周期必须保持原厂对应。
7. 先完成 Blob 分析的工具结构、UI 组装和功能实现复核，再处理其他工具。
8. 没有原厂证据的行为标记为未确认，不自行设计替代。

当前命名口径：`Algorithms/` 是工具算法实现层，`BlobAlgorithm` 是 Blob 算法入口。宿主接口保留 `ExecuteAsync` 作为统一调用合同，不能据此把算法目录描述成独立的“执行层”。`HalconBlobAlgorithmContext` 仅保存一次算法调用期间的 HALCON 临时上下文和恢复责任。

**2026-11-19 Blob 工具瘦身：平台接管 ROI 宿主与显示装配**：按「工具只留算法和结果」的目标推进四批。① 参数解析切到 `DeclaredParameterSchema`，工具只保留三处专有校验（工作流图像输入、输入编辑器集合、设置字段全覆盖），净减约 110 行；② 结果页显示装配上移，新增 `IDisplayDeclaration` 与 `DisplayOptionsFactory`，工具只留文本表达式解析；③ ROI 文档四层结构迁到 `Plugin.Contracts.RoiDocumentValue` 并中性命名 `roi.point/shape/item/group/document`，`BlobLocalRoi` 退化为 48 行薄壳，新增 8 条契约测试；④ **ROI 编辑宿主整体上移**：`BlobLocalRoiOwner` 519 行（编辑器、会话、帧租约、选中同步、待提交原文、延迟激活、预览层）迁到 `src/Plugin.Wpf/Presenters/Regions/NativeRoiEditHost.cs`，工具侧只实现 5 个接口成员（`ModeFieldId`、`LocalModeValue`、`IsPreviewSource`、`PreviewInputIds`、`MaterializePreview`）。工具侧从 4923 行降到 4423 行，`BlobRoi.cs` 从 1084 降到 567。验证：Blob 测试 152 项全通过；全量 Release 构建 0 错误；Release 程序启动正常（标题「SunEyeVision · 起始页」）。未做：`BlobRunUi`（30 行，阈值活性规则→平台条件的薄适配）、`BlobSupportedWindowState`（143 行，图像模式约束）、`BlobUiComposition`（247 行，含总装骨架与 Blob 业务）经评估属工具业务规则，不上移。

**2026-11-19 Blob 十三层差异表闭合**：Blob 同构按十三层逐项复核，`inferred` 与 `unconfirmed` 清零。本轮完成三批：① 证据缓存归档到 `docs/evidence/original-release-units/BlobAnalysis/`（含原厂图标三件套），发布单元、基本页、组装映射三道门禁改读归档路径，旧 `tools/SunEyeVision.BlobAnalysis` 工程删除；② 参数声明按原厂文件边界拆为 `BlobAlgorithm.xml`（104 项 Name+DefaultValue）与 `BlobAlgorithmTab.xml`（3 页签/5 分组/104 参数），解析器加载时按参数名合并回原结构，104 项属性逐项等价；③ 节点画布与持久化由 `inferred` 升 `derived`，节点形状经原厂模块目录实采取证确认两侧均无工具级形状声明。取证纠正：《完整同构复核清单与计划》所述 `IMVSBlobFindModuCs.xml` 实为 C# XML 文档注释，不是参数映射配置，本方无需新增 Cs 映射文件。验证：Blob 测试 131 项全通过；四道静态门禁全通过；全量 Release 构建 0 错误；Release 程序启动正常。未做：人工视觉验收 B1-B10。

**2026-11-19 P0 基线冻结与 P1 差异矩阵补齐**：`main` 上 368 项在途改动按用途归并为 8 条中文提交（锁文件、框架合同、工具迁移、工程清单、测试、验证脚本、忽略规则、文档证据），工作区已干净。`docs/collaboration/acceptance/baseline.json` 记录基线 `da7ac8b78`、版本 2.0.0 alpha、Release 路径，全量 Release 构建 `dotnet build SunEyeVision.slnx -c Release --no-restore` 退出码 0。`docs/evidence/architecture-conformance-matrix-20261119.json` 升级到 revision 2：修正 Blob 发布单元指向已删除 Wpf 工程的过期路径，补充十个工具行（模块名、原厂参数数、我方字段数、自研入口、覆盖项、证据状态与缺口）。关键结论：十个工具**全部仍是 Runtime+Wpf 双工程**，且**全部没有原厂式独立参数/页签/显示声明文件**；If 因原厂模块名仅来自生成脚本、原厂 0 项参数与我方 16 项字段差异无证据解释，标为 `unconfirmed`；ColorExtract 因直方图依赖链未闭合标为 `inferred`。未做：Blob 人工验收 B1-B10 仍全部待确认，ImageSource 单项目迁移未完成。

**2026-11-19 算法命名修正后的复核**：`HalconBlobExecutionScope` 已更名为 `HalconBlobAlgorithmContext`，只表示一次 Blob 算法调用期间的 HALCON 临时上下文。Blob Release 全量构建通过，0 错误、3328 个既有警告。Agent Desktop 重新采集有效连线方案后，Blob 调试窗业务名称闭合，证据见 `docs/evidence/blob-structure-ui-function-review-20261119.md`。算法成功/失败结果和原厂动态回写仍未确认。

**2026-11-19 Blob/ImageSource 静态门禁重跑**：发布单元、Blob 基本页、Blob 组装映射和 ImageSource 字段映射四道门均通过。Blob 映射 `unmappedVmGroupLinks=[]`；ImageSource 25 项页签/字段顺序完全匹配。ImageSource 原厂 XML 保留件存在 `SelecetedTips` 拼写错误，门禁只在内存解析视图修复并记录，不改原始证据。

**2026-11-19 双项目改单项目启动**：盘点确认当前发布合同按“一程序集一个 Runtime/Editor 角色”校验，直接删除 Wpf 项目会使组件封存、载荷清单和编辑器注册失败。已建立 `docs/evidence/single-project-tool-migration-20261119.md`，迁移顺序固定为先改发布合同、再合并 Blob Wpf 源码、再验证单程序集双角色加载，最后删除 Wpf 项目文件。当前尚未删除项目，避免制造无证据的兼容回退。

**2026-11-19 Blob 单项目迁移完成首个门**：发布合同已允许共享程序集的 Runtime/Editor 双声明。Blob WPF 源码已复制到 `tools/SunEyeVision.Tool.BlobAnalysis/Editor/`，Blob 单项目 Release 编译通过。正式载荷显示 `SunEyeVision.Tool.BlobAnalysis.Runtime.dll` 同时包含 Blob Runtime 与 Blob Editor 声明，旧 Blob WPF DLL 未进入载荷，旧 `tools/SunEyeVision.Tool.BlobAnalysis.Wpf/` 项目目录已删除。ImageSource WPF 项目仍待迁移，当前不能宣称全部工具改单项目完成。

**2026-11-19 Blob 单项目启动验证**：共享程序集加载器改为先校验同路径的完整 Runtime/Editor PE 声明，再按角色过滤返回；Runtime 准入允许已声明 Editor 的桌面共享组件，保持纯 Runtime 依赖检查。修复后 Release 经 Agent Desktop 进入 `SunEyeVision · 起始页`。一次优雅关闭超过工具截止时间，强制关闭确认进程退出；`Invoke-SingleToolAudit.ps1` 已加入超时强制退出兜底，避免自动审核卡死。

**2026-11-19 启动卡页根因闭合**：首个启动失败为 Runtime-only 过滤仍按单角色计数，已修复。第二个启动失败为共享程序集被旧“中性 Runtime 不得依赖 WPF”门禁拒绝，已按程序集 Editor 声明放行桌面共享组件。当前 Release Agent Desktop 快照已确认 `SunEyeVision · 起始页` 正常显示，进程可响应；关闭超时由脚本强制退出兜底。最终 Blob 调试窗本轮指针双击回执为 `delivered_unverified`，因此仍沿用此前有效连线方案的双侧 UI 快照作为窗口证据，不把本轮未确认点击冒称新窗口通过。

**2026-11-19 单工具闭口门禁确认**：用户明确要求每个工具必须完成原厂/自研双侧静态对照、方案目标结构核对、Release 构建、Agent Desktop 运行验证和人工确认后，才能进入下一个工具。当前只允许继续收口 Blob，ImageSource 单项目迁移已撤回并暂停。Blob 现处于人工确认门，未获得人工确认前不得宣称 Blob 闭口或开始 ImageSource。

**2026-11-19 Blob 批次一清理**：现行工程和验证项目中的旧 Blob WPF 工程路径已清理，Blob 单项目 Release 编译 0 错误。Blob 发布单元、第三方边界、基本页和组装映射静态门禁重新通过。基本页门禁曾因 Windows PowerShell UTF-8 中文正则误报，已改为结构属性匹配并恢复通过。ImageSource 未改动，仍处于暂停状态。

**2026-11-19 启动页卡住复核**：最后一次 Blob 定向编译改写了 Release 程序集字节，未重新封存时会触发载荷校验错误。随后发现 ImageSource 发布清单的 Runtime/Editor 两行在撤回试探改动时被对调，已恢复原始配对。当前载荷重新封存成功，Agent Desktop 已确认 Release 进入 `SunEyeVision · 起始页`，无启动失败窗口，进程已强制退出并确认结束。该问题已记录为发布封存流程问题，不计入 Blob 算法差异。

**2026-11-19 Blob 人工确认清单**：新增 `docs/evidence/blob-manual-acceptance-20261119.md`，固定 B1-B10 的窗口、页签、输入来源、ROI、运行参数、结果显示、底栏、执行、关闭和外观确认项。自动证据已填入，人工状态全部保持“待确认”。确认前不进入 ImageSource。
**2026-11-18 框架先行暂停轮廓匹配局部优化**：复核原厂 `IMVSContourMatchModuControl` 与当前自研工具链后确认，当前差异不只在字段和样式，而在窗口根、输入行装配、参数页层级、FeatureMatch 公共面板和模板配置窗口职责边界。轮廓匹配局部 UI 优化暂停，现有工作区改动保留但不计作完成。先完成项目结构、公共窗口根、平面参数页、输入/ROI 组装、FeatureMatch 模板面板和生命周期合同的框架审计与重构设计。详细审计见 `docs/collaboration/acceptance/轮廓匹配前置-项目结构与UI装配架构审计-20261118.md`。

**2026-11-18 ImageLoad + BlobAnalysis 试点执行**：已完成 VisionMaster 4.4.0 Build20250516 原厂窗口取证和 ImageLoad/BlobAnalysis 自研入口差异清单，见 `docs/evidence/image-source-blob-complete-ui-difference-20261118.md`。原厂 Agent Desktop 快照确认 ImageLoad 为 420×500 单页并含基本/触发/订阅图片文件夹三组，Blob 为 420×500 三页并含基本参数、运行参数、结果显示页，Blob 基本页含图像输入与 ROI 区域组。当前自研保持声明驱动页根、共享模板和统一生命周期接缝，未新增无证据的自研占位控件。

- Release 单进程顺序构建已重新部署 ImageSource 与 Blob 程序集。构建期间保留既有 `NU1902 log4net 2.0.17`、nullable、XML 文档警告。此前并行构建的资产缓存 I/O 冲突已通过停止残留 MSBuild 进程规避。
- ImageLoad/Blob 专项 UI 会话筛选仍暴露 ROI 页面字段读取为空、ROI 组装失败后的 Ready/输入页状态不稳定、引用行和结果树生产链缺口。宽会话筛选同时包含结果工作区、持久化和运行会话既有回归，均未记作本轮 UI 通过。
- Agent Desktop 已启动 Release `blob-min` 并进入主界面，画布可见 Blob 节点。当前两个节点均暴露为无名重复 UIA row，无法安全命中指定节点，ImageLoad/Blob 自研窗口快照尚未完成。该问题记录为自动化定位缺口，后续使用稳定节点标识或固定脚本复测。
- 本轮状态为“差异清单完成，代码已按现有声明链构建，专项测试未通过，Agent Desktop 窗口级对照待补”。不得标记为 ImageLoad + Blob 完成。
- 已修复 `BlobUiComposition.BuildPage` 首次 `Bind` 阶段异常未隔离的问题。专项回归由此前 2/5 通过提升为 2/5 通过且异常页隔离用例恢复，仍有 ROI 字段读取为空和 Ready 状态超时等 3 项失败。Release 程序已重新启动到 `blob-min` 主界面，已保存 `docs/evidence/self-blob-min-main-20261118.png`。构建/测试再次运行时因程序保持打开锁定 Release 程序集，需关闭程序后再运行。
- 最新 Agent Desktop 已重新采集原厂 ImageLoad 与 Blob。原厂 ImageLoad 当前快照仍为 `0 图像源` 三组结构，原厂 Blob 当前快照为 `2 Blob分析` 三页结构和运行页四个底栏命令，截图见 `docs/evidence/vm-blob-dialog-current-20261118.png`。自研侧当前只保存了 `blob-min` 主界面截图，节点重复 UIA row 阻塞调试窗口自动定位。

**2026-11-18 全量工具迁移矩阵与框架审计**：从 `product/ToolComponents.props` 核对出当前产品登记 **18 个工具**，其中 10 个已有真实 UI 对照证据，8 个仍为 `EvidenceBlocked`。矩阵见 `docs/evidence/tool-migration-matrix-20261118.json`。公共 UI 装配差异已结构化记录，下一闸门为平面输入组装、公共 FeatureMatch 面板和生命周期适配器设计，见 `docs/evidence/framework-ui-assembly-audit-20261118.json`。

**2026-11-18 原厂公共边界与平面输入设计审计完成**：原厂 `FeatureMatchCommonControl` 负责共享模型列表和模型操作，`IMVSHPFeatureMatchModuControl` 负责特征匹配参数根/对话框壳，ContourMatch 与 GrayMatch 各自专门化，MarkFind 保持独立面板，不强行泛化。当前自研输入链为 `ParamListView → NativeParameterRow → DeclaredInputFieldsView → ParamListView`，平面输入设计建议以 `NativeInputComposition` 接口输出正式输入行，统一页拓扑、状态、捕获、代次和释放。证据与设计见 `docs/evidence/original-ui-architecture-audit-20261118.json`、`docs/evidence/framework-flat-input-design-20261118.md`。

**2026-11-18 ColorExtract 原件依赖闭包**：根据 VM 4.4.0 反编译证据启用 HikHistogramSingle 与 SquareArea 的 ColorExtract 最小消费者链。三通道参数行现在按原厂 108 宽直方图 + 两条 SliderArrange 组装，无图像引用时保持原厂空 PointCollection，不生成假数据；SquareArea 使用原厂资源键和网格资源，HikPolygon/HikPath 点同步分支继续标记未确认。定向构建、完整 Release 构建均 0 错误，ColorExtract 测试 20/20 通过。证据见 docs/evidence/color-extract-histogram-dependency-review-20261118.json。已用 Agent Desktop 对改动后的 Release ColorExtract 参数窗口复核：三通道 HikHistogramSingle 框均渲染，关闭窗口后主窗口仍响应并完成释放记录；截图与快照见 Release/_validation/AgentDesktopToolReview/post-color-histogram/。待补运行参数/结果显示页和原厂 GetHistogram1/2/3 动态图像回调。

**2026-11-18 十工具真实 UI 对照批次**：已按 Agent Desktop 采集原厂 VisionMaster test 与当前 Release 两侧窗口证据，覆盖 BlobLabels、ContourMatch、GrayMatch、MarkFind、ColorExtract、DivideImage、FrameMean、If、IntensityMeasure、PixelCount。每项记录窗口尺寸、Tab 顺序、分组、底栏命令和入口阻塞，汇总见 `docs/evidence/ten-tools-real-ui-comparison-20261118.json`。ContourMatch 四个 Tab 已在最新 Release 快照中按原厂顺序呈现，特征模板创建窗口仍待接入；MarkFind 资源改动专用构建通过并已补采 post-release 窗口快照。BlobLabels 的同批次复采被保存方案对话框造成的 AMBIGUOUS_TARGET 阻塞，历史双侧窗口证据仍保留。

**2026-11-18 十工具单工具功能审核补充（多节点联调延期）**：按用户最新边界，本阶段只验单工具功能链；输出树、图像叠加、运行界面和帮助内容涉及多节点协作的部分暂不作为本阶段收口条件。十个工具独立过滤测试全部退出码 0，累计 256 项通过；最终 Release 全量构建 0 错误、3269 个既有警告，封存哈希 `B6411C46AF402665D9C4ABAE0395F077177683D302C0CE5ECEEDFD49270096FB`。固定 Agent Desktop 脚本已重新打开最终 Release 的 `11111 / 颜色抽取1` 并验证关闭释放。 `Invoke-SingleToolAudit.ps1` 新增可选 `-ProbeExecute`，已对 FrameMean、DivideImage、IntensityMeasure、BlobLabels、PixelCount、ColorExtract、ContourMatch、GrayMatch、MarkFind 和 If 验证，自动保存执行结果树并关闭释放。 十工具覆盖矩阵已生成：`docs/evidence/ten-tools-functional-coverage-20261118.json`。 `HikHistogramSingle` 依赖闭包清单：`docs/evidence/color-extract-histogram-dependency-review-20261118.json`。 要求级审计报告：`docs/evidence/ten-tools-requirement-audit-20261118.json`。ColorExtract 已按原厂 RangeInfo 修正 HSV/HSI 色相环回预览；新增颜色区间、启用复选框状态切换和悬停删除已观察；删除第二项后首项保留，点击确定并正常退出。待补：ColorExtract HikHistogramSingle（含 SquareArea、HikPolygon/HikPath 和 ROI 资源）依赖闭包与滑条、FrameMean 原厂相机回调、If 动态分支、DivideImage 内部输入保存重开、IntensityMeasure 单工具结果入口；多节点输出消费、图像传递、ROI/模型继承、分支联调和完整工作流回归统一后置。
**2026-11-17 三线第二批集成（12 工具共存，未人工验收）**：A3 划片拆分（`DivideImage`）、B3 亮度测量（`IntensityMeasure`）、C3 轮廓匹配（`ContourMatch`）与灰度匹配（`GrayMatch`）四条分支全部并入 main，合并冲突仅 `SunEyeVision.slnx` 与 `product/ToolComponents.props`，按并集解决（组件登记 24 条 = 12 工具 × Runtime/Editor）。

- **参数页契约缺陷（本轮教训）**：依次修掉四处——Blob 标签分析（图形分组缺 `DialogGroup`/`DialogTitle`、x/y 未注册文本坐标）、图形定位（`InputSection` 回落到不存在的「基本参数」组）、像素统计（无参数页、输入区未落位、两处 `DecimalPlaces: null`）。根因统一为**呈现层五项隐性契约未对齐**（ReadyTask / InputSection / DialogGroup+Title+Primary / GraphicDisplays 完整消费 / TextDisplays 注册坐标）。
- **欢迎页**：logo 由占位方框改为复用正式品牌图形 `SunEyeVision.BrandDrawing`；分类卡片图标改动已回退；`MinWidth/MinHeight` 提到 1000×616，避免边缘拖动缩放裁掉内容。
- **工具箱映射**：`IMVSContourMatchModu→ContourMatch`、`IMVSGrayMatchModuVA→GrayMatch`、`IMVSDivideImageModu→DivideImage`、`IMVSIntensityMeasureModu→IntensityMeasure`、`IMVSPixelCountModu→PixelCount`、`IMVSMarkFindModu→MarkFind` 等全部 `MappingVerified: true`。
- **已验**：全量 Release 构建 0 错误；测试 60（图像处理）+86（逻辑数据）+109（查找检测）=**255 项全通过**；启动后工具箱七类正常展开；10:42 之后的启动日志中**无**「参数页组装失败 / 不限固定小数位 / input content section」记录（三类异常最后出现时间为修复前的 10:20–10:24）。
- **未验（不得记作通过）**：10 项验收检查表逐项人工执行（工具拖入、参数页分组联动、结果页叠加、连续执行、保存重开、异常路径、生命周期计数、分支 Skipped）、UI 与 VM 原件外观对照。
- **遗留**：引擎未校验「分支源出边数 > 声明控制输出数」；画布不为分支源渲染多出口；二值化内核 602 原生算法取证已产出（`证据补充-核心内核602-20261117.md`）但尚未据此开工工具。
**2026-11-17 三线并行首轮集成（帧均值 / 条件分支If / Blob标签分析，未人工验收）**：三条开发线按"开发一致性分类"并行，各自独立 worktree（`D:\MyWork\line-tools-image|logic|inspect`）与分支（`feature/tools-image-processing|logic-data|inspection`），基线 `945de540f`→`4dcb3cd72`。

- **产出**：A 线帧均值工具 `2119916d7`（+合并 `b5cbe7720`）、B 线条件分支 If `a8c23fb50`（合并 `64e858bc7`）、C 线 Blob 标签分析 `a9f189720`（合并 `289620bed`）；合并到 main 时仅 `SunEyeVision.slnx` 与 `product/ToolComponents.props` 冲突，按并集解决。
- **gitignore 根因二次修复 `4dcb3cd72`**：`runtime/` 规则再次吞掉新工具 `Runtime/` 源码（A 线首次提交就丢了 8 个文件）。已改为通配白名单 `!/tools/*/Runtime/`、`!/tools/*/Runtime/*.cs|*.csproj|packages.lock.json`，覆盖后续所有新工具，不再逐个工具手加。
- **工具箱映射补正**：`IMVSFrameMeanModu`→`FrameMean`、`IMVSBlobFindLabelsModu`→`BlobLabels`（原 `ToolId: null`）、`IfBranchModule`→`If`（原误映射 `Condition`），三处均置 `MappingVerified: true`。
- **已验**：三条线各自与 main 全量 Release 构建 0 错误；新测试项目 16+23+30=69 项全通过；六个工具体 DLL 已部署到 `Release\SunEyeVision_v1.0.0\SunEyeVision\`；`release-payload.json` 含三工具条目；程序启动正常、窗口响应。
- **未验（不得记作通过）**：10 项验收检查表逐项（工具箱可见性与名称图标、拖入画布、参数页分组显隐联动、结果页表格与叠加、单次执行、连续执行、保存重开、异常路径、生命周期计数、分支 Skipped 行为）、UI 与 VM 原件外观对照、人工验收。
- **上报未处理**：①执行引擎不校验"分支源出边数 > 已声明控制输出数"，多出的出边永不选中（应补明确诊断）；②画布不为分支源渲染多个出口，用户只能靠连线顺序区分第 0/1 支；③`IMVSFrameMeanModu` 原件名"帧平均"与工具自查显示名"帧均值"不一致，需定口径。
- **队伍管理教训**：首轮队员结束前未提交、未汇报，代码停在未提交状态；本轮由主线代提交。后续任务书必须写死"未提交即未完成"，主线按工作树 `git log/status` 验收而不是看会话标记。

**2026-11-17 用户强制门槛：工具开发必须先取证再对照开发**：所有工具（含批量并行各线）开发前必须先完成原厂逆向取证，再对照开发，顺序固定为"原厂取证 → 完整链路分析 → 差异确认 → 实施设计 → 改代码 → 按原厂链路验收"；取证未完成不得编码，证据不足时停在设计阶段等补证，禁止先实现后对照。已写入 A 线任务卡并下发 A 线队员（`line-a-image`）；B/C 线任务卡起草时同规则。

**2026-11-17 codebuddy 分支合并主线（集成批完成，未推送）**：`feature/param-window-datatree-codebuddy`（`cd54056e0`，相对 main 领先 169 提交，1168 文件）以 `--no-ff` 合入 main，合并提交 `5fd78bfc2`（双父 `33bae25a5` + `cd54056e0`）；合并前先把本工作区未入库的 11-09 基线同步记录提交为 `33bae25a5`。唯一冲突文件为任务账本，按分支版为主体、回插 11-09 记录解决。

- **合并暴露的漏提交文件**：分支尖端全量构建报 `ImageSourceRunState` 未定义——该类型在仓库全历史中从未存在，根因是保存点提交 `a785163d9`（固定交接单未提交工作）漏加了定义文件。已从分支工作现场 `E:\SunEyeVision_V2`（检出 `cd54056e0` 的 worktree）的未跟踪文件中找回三个文件：`ImageSourceRunState.cs`、`ImageSourceProcessingException.cs`、`ImageSourceStitchState.cs`，提交为 `16faadfee`。
- **gitignore 根因修复**：`.gitignore` 的 `runtime/` 规则（第 189 行）过宽，任何 `Runtime/` 目录新文件被静默忽略；DeviceDriver/HalAdapter/BlobAnalysis 已有白名单，唯 `tools/SunEyeVision.ImageSource/Runtime/` 缺失（保存点漏加的直接原因）。已按既有格式补白名单，`git check-ignore` 验证三文件不再被忽略。
- **集成验证**：全量 Release 构建（`SunEyeVision.slnx`，61 工程，`--no-incremental -m:1`）0 错误 6059 警告；启动 `Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe` 验证通过（PID 2732，"SunEyeVision · 起始页"，进程响应正常）。本机签封配置 `installer\Release.Local.props`（gitignore，指便携 pwsh7）从 E 盘补齐。
- **未合入与未推送**：E 盘 worktree 的进行中改动（`SunEyeVision.slnx` 目录整理、两份执行链设计 HTML、`tasks/plan.md`/`todo.md`）属分支线在途工作，不在本次合并范围；main 领先 `origin/main` 172 提交，未 push。主目录 `src/Plugin.SDK/` 未跟踪目录来历未查清，保持未入库。
- **待人工验收**：合并后程序的工具加载、参数窗数据树、图像源拼接链（`ImageSourceStitchState` 拼接语义按分支实现，未做 VM 对照验收）。

**2026-11-17 框架地基与工具基线（架构最优批次）**：按“原厂设计为首要标准、除非不可达”的口径，先补地基再谈批量做工具。本批完成并提交 6 项：

1. **契约控制输出与兼容窗口**（`726c86efb`）：`ToolSchema.ControlOutputs` 声明分支能力（仅声明非空时进入 schema 指纹，故既有方案指纹不变）；`FoundationContract.IsCompatible` 统一兼容窗口（major 精确、minor 落在 `[OldestCompatibleMinor, Minor]`），程序集绑定与组件目录共用同一判据。
2. **控制流按原厂机制落地**（`cb88b6617` 为最终形态）：连线保持“节点对”语义（不以端口区分，与原厂执行边合同一致），分支选择在工具内部——工具在 `Describe()` 按顺序声明 `controlOutputs`，执行后用 `ToolOutcome.SelectedBranches` 返回选中序号（**不接触节点身份**），宿主把序号映射到该节点出边；未命中目标标 `Skipped`（非失败、不参与合格判定），越界序号判失败而非静默跳过。此前的 `control.<id>` 端口方案经取证判定为自研发明，已完整移除（含 `ControlPortPrefix`/`FindControlOutput`/`IsControlEdge`）。
3. **引擎 Skipped 语义**（`15bb6f937`、`d19082594`）：跳过链传递、上游失败仍为 `NotExecuted`、分支源未终结不判定、聚合排除 Skipped。
4. **新工具开发基线清单**（`docs/09-现行文档/新工具开发基线清单-20261117-033000.html`）：契约三级矩阵（必需 6 项 / 呈现 2 项 / 执行期按需）+ 缺省行为 + 分支写法 + 接入步骤（含“先建测试项目再建 UI”的载荷顺序）+ 10 项验收检查表 + 4 项已知限制。

**本批纠正的三处复核结论（有代码证据）**：
- **连续执行循环不在 UI 层**：`DocumentContinuousRun.RunCoreAsync` 的轮次循环本就在 Application 层，UI 的 `MainWindowContinuousExecution` 只订阅事件刷新界面，分层正确，无需下沉；
- **工具目录热插拔在本架构下不可达**：准入代次被三处持有（`App.xaml.cs:311` 进程级 pin、`ServiceInitializer.InitializeSolutionManager` 只初始化一次、`MainWindowViewModel.Toolbox` 只读属性），且发布时序先退役后发布无回退，等价实现需整体改造准入模型；
- **工具箱懒加载非必需**：当前工具规模下分类一次性构建无可感开销，登记为“工具数量显著增长后再评估”。

**验证**：UI Release 全量构建 0 错误；Execution.Tests 20/20（含 4 个分支用例）；Domain.Tests 27/27（含 5 个连线策略用例）；Blob 测试 128/129（1 个既有 discovery 问题）；`11111.solution` 端到端执行成功。**待办**：两工具契约的代码级收口（当前以矩阵文档固化判断，未改契约代码）；基线分批实现新工具。
**2026-11-17 原厂设计复核（完整使用链与生态基线，本批完成）**：按用户要求复核“以原厂为参照是否存在框架层严重偏离与设计错误”，采用双轴判定——轴一与原厂对照（一致/结构偏离/语义偏离/原厂不合理/静态无法确认），轴二架构正确性十判据（分层依赖、职责单一、所有权唯一、生命周期完整、数据流单向、扩展点充分、边界清晰、并发取消安全、可测试、失败可诊断），**原厂只作参照不作标准**。材料为 28 个托管程序集约 39 万行（VM 4.4.0）+ 原生模块；旧结论抽样回验 7 条证实、1 条新增发现（`Program.AppBeforStart()` 第二启动路径）、0 条推翻；样本外推一律标注。

**复核结论（分级）**：①**必须修（阻断新工具）**——执行引擎为纯 DAG“前驱全成功”模型（`WorkflowExecutionEngine` 第 86-87 行），无法按工具结果选择后继，`src/Domain/Workflows` 亦无条件/分支类型，分支类工具（If/IfBranch/BranchModule/BranchStringCpm）不能实现；组/嵌套类同样缺嵌套执行路径且 Group 迁移当前暂停。②**建议修**——连续执行的轮次循环在 UI 层（原厂在原生 Server 内，故自研每轮含托管开销）；工具箱无热插拔；工具箱分类一次性构建（工具变多会放大展开耗时）；图像区状态栏缺“模块/算法耗时 + 缩放档位”。③**保持（自研更正确，禁止改回原厂做法）**——两阶段打开、托管序列化+原子写+目标身份校验+按目标锁、画布作为文档投影、声明式结果投影与 RequiredOutputs 回传、图编辑与配置编辑分历史、工具窗口用调试会话对象、像素信息与状态栏按职责拆分。④**原厂不合理不照抄**——StatusBar 单控件承担 8 项职责、全局单例方案+TempVmSolution 中间态、保存仅布尔 `_isSaving` 无目标身份校验。

**生态基线就绪度**：图像处理/测量类工具七项基线能力（契约、参数、结果声明、UI、执行、保存、准入）齐备，可直接批量实现，但建议先用一个非 Blob 类工具端到端试点验证契约；分支类与组类为阻断项；脚本类缺执行宿主与隔离策略；通信类按用户要求本批不查（底座存在未核验）。

**待补证（本批未执行动态取证，已在报告登记顺序）**：原厂 `CloseSolution` 退役顺序、撤销粒度、流程页生命周期、连续执行回调时序、图像源控件逐项对照（优先级高，作为工具范式）、结果选中到图像高亮的逐事件链（优先级高）、第二启动路径适用模式、工具栏完整按钮集与禁用规则。

**产出**：主报告 `docs/01-方案设计/2026/11/17/原厂设计复核报告-完整使用链与生态基线-20261117-020000.html`；证据 6 份 `docs/collaboration/evidence/原厂设计复核-*.json`（材料可信度、启动与方案、工具箱与流程节点、显示结果与状态栏、执行与工具栏、生态基线就绪度）；文档索引与主题台账已重新生成。本批未修改产品代码。

**2026-11-17 启动预热与执行链常驻化收口（本批完成）**：按用户要求解决“图像源首次执行与后续差异大”，已完成并提交三个批次：`70e6f4891`（HALCON 平台预热 + 执行链 JIT 预热）、`9a361d656`（启动画面点亮样式）、`2c17f0cca`（执行链常驻化与运行态探针，34 文件）。实测（agent-desktop 自动驱动 `11111.solution`，同一进程连续执行 5 次）：整轮首次 570ms → 142ms，稳定态 46~50ms；ImageSource 首次 13ms、稳定态 4~5ms，`image-read-decode` 由 141~3134ms 降为 0ms；Blob 首次 77~83ms、稳定态 28~40ms。启动期后台完成 HALCON 平台装载（冷态 1~3s）与 4632 个方法预编译（294ms），不阻塞启动。

**2026-11-17 L3 Blob 优化取证结论（未实施，证据阻断）**：按“算法逻辑与设计对应、不为对齐而对齐”的用户决定，Blob 算法不再追平 VM。三条路线均被实测或证据否定：①按需跳过未请求输出不可行——`BlobResultDisplay.Descriptor` 的显示必需项包含 `region`、`outline-points`、`area`（缺失时结果图形不渲染），输出树节点含最小/最大灰度值与对比度（`BlobOutputDisplay.cs`），跳过会改变现有显示语义；②批量灰度统计不可行——`HOperatorSet.GrayHisto` 对区域数组返回合并直方图（256 桶），不是逐区域数组，实测 74 个用例失败后回退；③泛型收敛收益仅 2ms（`filter-sort` 首次 2ms、稳定态 0ms）。稳定态 Blob 36ms 构成为逐 Blob 灰度统计 9~15ms、逐 Blob 区域捕获 4~8ms、整体区域捕获 4~8ms、特征提取 2~6ms，均属每轮真实工作量，是 HALCON 逐区域算子调用的库级成本，与 VM 原生一次遍历的架构差距一致。

**2026-11-17 剩余性能项评估（本轮不做）**：执行链框架稳定态已降至 1~3ms（`accepted` → 首个 `module-session-created` 实测 1ms，快照复用与常驻化生效），参数物化与快照按需无优化空间；结果发布 8ms 属渲染实现选择，收益小且触及防闪烁护栏，按“不为对齐而对齐”原则暂不做。整轮 46ms = Blob 30~36 + 发布 8 + ImageSource 5~7 + 框架 1~3；对齐 VM 的 7ms 需 Blob 算法体原生重写（单独立项，本轮未评估工期）。

**2026-11-17 探针定位调整**：`BlobTimingProbe` 与 `RuntimeLifecycleProbe` 由“临时采证”转为常驻能力（前者受 `SUNEYEVISION_TIMING_PROBE=1` 控制、默认零开销空实现；后者记录运行态存活计数），后续性能核对直接复用，不再重复加装临时探针。

**2026-11-16 固定同图双轮快速定位**：Agent Desktop 批量关闭自动切换后，以同一 `1.jpg` 连续执行两轮。第一轮已有冷路径记录 `module-session-created`，PerfProbe `total=513ms (batch-capture48/batch-run443/result-publish9)`；随后两轮均为 `resident-plan-hit`、ImageLoad/BlobAnalysis `module-session-hit`，运行请求时间分别约 60ms 和 75ms，结果版本按轮退役/发布。当前日志未带 `ExecTimingProbe`，因为固定脚本从 PowerShell 启动 Agent Desktop 会被交互租约拒绝访问；不能把这次运行宣称为分段探针闭环。现阶段根因已定位到自研冷路径的会话创建/输入解码与正式执行链耦合，具体 ImageSource 分段仍需在带探针进程启动后补齐。`

**2026-11-16 有效本地图像证据审计（校正）**：按解码、帧复制、格式转换、输出资源发布、Blob 接收和分段耗时逐项审计。`test.sol` JPEG 路径的解码入口、ImageSource 发布、ModuleFrame 共享内存句柄登记、Blob 输入句柄/元数据和四阶段顺序已有证据；格式转换分支有明确未命中证据。帧复制只排除了已挂钩 CRT/Rtl/Win32 入口中的大块复制，专用复制、Blob 像素读取、内部资源分配和跨轮复用仍未闭合。最终审计为 `artifacts/evidence_completion_final_audit_20261116.md`，本阶段未修改项目源码。`

**2026-11-16 MVD JPEG 解码静态闭包补证**：Ghidra 闭合 `MV_MP_DecodeFromFileEx → FUN_180383ef0 → MV_MP_DecodeJpegEx/FUN_1803842d0`。原厂 JPEG 路径读取约 274–277 KB 文件内容，查询输出宽高/像素格式/字节数，将解码结果写入调用方输出缓冲区。当前 `test.sol` 动态未命中 `MV_MP_ConvertPixelTypeEx`、`ConvertPixelType`、`ConvertRgbToMono8` 或 `SetImageOutputMono8`，`SetImageOutput` 格式分类为 0，确认本次没有 RGB→MONO8 转换阶段。报告为 `artifacts/mvd_decode_static_closure_20261116.md`，已同步有效图像取证 HTML，未修改项目代码。`

**2026-11-16 ModuleFrame 间接调用复核**：进一步反编译确认 `VM_M_SetImageEx` 的直接路径是 `CUserModuleDelegate::SetImage → FUN_1800c9d80 → CSingleModuleRst::SetValue`，负责共享内存名称和资源句柄登记。`CSafePointerResultCache::SetImage` 的内部钩子在当前有效路径未命中，不能将其当作本次图像复制入口。该结论已同步静态闭包报告和有效图像取证 HTML，未修改项目代码。`

**2026-11-16 ModuleFrame SetImage 静态闭包补证**：Ghidra 闭合 `VM_M_SetImageEx → CSafePointerResultCache::SetImage`。该函数按共享内存名、数据指针、长度和模块索引管理缓存项、引用计数和容量，不执行 1228800 字节整帧 `memcpy`；反编译中出现的 `memcpy` 只复制字符串。结合动态 Trace/Frida，ModuleFrame 职责可固定为共享内存资源登记和缓存引用管理，图像字节生产/写入位于 ImageSource/MVD 图像对象或未解析共享内存边界。报告为 `artifacts/moduleframe_setimage_static_closure_20261116.md`，已同步有效图像取证 HTML，未修改项目代码。`

**2026-11-16 管理员 Frida 直接附加补证**：管理员 Frida 附加原厂 VisionMaster 两轮执行，确认目标模块调用者没有 `CreateFileMappingW`/`MapViewOfFile`，仅出现 6 次 `ModuleFrame` 的 4096 字节 `VirtualAlloc`。CRT 复制最大长度仍为 256 字节，ModuleFrame/ImageSource/Blob 分别出现 1383/12/2 次 `memcpy`（`memmove` 为同地址双重观察），实际 JPEG 读取为 `3.jpg`、`4.jpg`，单次约 274–277 KB。该证据强化整帧复制位于专用共享内存、跨进程或自定义路径的判断，不能证明其不存在。报告为 `artifacts/frida_direct_trace_evidence_20261116.md`，未修改项目代码。`

**2026-11-16 原厂 Trace 标记动态闭环补证**：按原厂日志等级工具真实实现临时创建 Trace 标记，在 `test.sol` 两轮执行中确认 ImageSource `VM_M_SetImage(Image,1228800,copyMode=0)`、Blob 输入 `VM_M_GetImage(InImage)`、Blob 输出顺序 `BinaryImage→BlobImage` 且均为 `copyMode=0`。第一轮 ImageSource/Blob module-process-algorithm=`4.469/4.458/4.064`、`6.164/6.157/4.516` ms，共享内存聚合 `0.036800` ms；第二轮为 `3.783/3.765/3.708`、`5.363/5.353/3.685` ms，共享内存聚合 `0.056700` ms，Procedure `9.330` ms。临时标记已删除，原始片段和集中报告已写入 artifacts 与有效图像取证 HTML，未修改项目代码。`

**2026-11-16 复制入口统计补证**：统计既有 `frida_original_copy_trace.jsonl` 的 1611 个 `copy_enter`，最大长度 256 字节，调用者为 ModuleFrame 1597、ImageSource 12、Blob 2，未观察到 1228800 字节 CRT/memmove/Rtl 整帧复制。该结果不能排除共享内存专用或自定义复制实现，已同步函数级动态集中报告和有效图像取证 HTML，未修改项目代码。`

**2026-11-16 函数级动态证据集中整理**：重新核对既有 Frida 记录，确认同一线程两次有效本地图像执行已取得 `MV_MP_DecodeFromFileEx`、`MV_MP_DecodeJpegEx`、`ImageSource.Process`、`SetImageOutput`、`ModuleFrame.VM_M_SetImageEx → ImageCachePublish → ImageHandleLock → ModuleImageSet`、`Blob.Process → ProcessAlg → LimitJudge → ProcessOutput` 的进入/退出和耗时。首次解码 1.38/2.74 ms、ImageSource 6.25 ms、Blob 4.86 ms；后续解码 1.28/4.52 ms、ImageSource 7.48 ms、Blob 4.24 ms。仍缺共享内存/自定义整帧复制进入退出、Blob 输入像素读取、解码内部格式转换和资源分配独立耗时。集中报告为 `artifacts/function_level_dynamic_closure_20261116.md`，已同步有效图像取证 HTML，未修改项目代码。`

**2026-11-16 WPR 原始采样地址复核**：对 `wpr_test_sol_dumper_50_70.txt` 的 SampledProfile 原始事件筛选，唯一直接归属 ImageSourceModule 的用户态采样地址为模块 RVA `0x226a9`，Ghidra 解析为 `CMyAlgorithmManager::MonitoringSubFolderChanges`，不是 `Process`。因此 WPR 只能作为模块级辅助证据，不能证明采样命中解码或输出函数，已在 `artifacts/wpr_test_sol_cpu_evidence_20261116.md` 更正，未修改项目代码。`

**2026-11-16 test.sol WPR CPU 采样补证**：管理员 WPR 在原厂 VisionMaster PID 17164 上覆盖两轮 `test.sol` 单次执行，ETL `artifacts/wpr_test_sol_cpu_20261116.etl`，xperf 栈报告 `artifacts/wpr_test_sol_stack_pid17164.txt`。模块汇总命中 ImageSourceModule=5、MVDMediaProcess=6、MVDImageCpp=13、ModuleFrame=3、IMVSBlobFindModu=34、BlobFindCpp=34。由于无可靠私有符号且采样为统计抽样，不能将样本数换算为函数耗时，仍不能拆出解码、帧复制、格式转换、输出资源创建或 Blob 像素读取。未修改项目代码。`

**2026-11-16 test.sol 重开并执行基线补证**：按用户要求关闭原厂 VisionMaster 后重新启动，通过最近方案打开 `C:\Users\Administrator\Desktop\test.sol`。主界面确认节点为“图像源1”和“Blob分析1”，两轮单次执行状态为“模块配置完成”，流程级耗时分别为 14.75 ms 和 8.11 ms。该证据只闭合有效方案和流程总耗时，不拆分解码、深拷贝、格式转换、输出资源或 Blob 像素读取，未修改项目代码。详见 `artifacts/test_solution_ui_timing_20261116.md`。`

**2026-11-16 CDB 函数级动态入口证据补齐**：已用 x64 CDB 先于原厂内部监控启动 VisionMaster，针对 ImageSourceModule、ModuleFrame、IMVSBlobFindModu 的 Ghidra RVA 设置断点，并对同一有效本地图像执行两次。两次均在同一线程 2554 命中 ImageSource.Process → SetImageOutput → ModuleFrame.VM_M_SetImageEx → Blob.Process → ProcessAlg → LimitJudge → ProcessOutput → 两次 VM_M_SetImageEx。命中次数为 Process=2、SetImageOutput=2、VM_M_SetImageEx=6、Blob 四阶段各=2。ConvertFileImage、ConvertPixelType、ConvertRgbToMono8、SetImageOutputMono8、VM_M_GetImageEx 未命中，说明本批实际走普通 SetImageOutput 分支。CDB 日志保存在 artifacts/original_cdb_trace_64f8_2026-11-16_08-20-13-260.txt。当前取得函数进入顺序和参数快照，函数返回时间仍需 TTD/返回断点补齐，未修改业务代码。
**2026-11-16 WinDbg 函数级取证工具安装完成**：winget 在本机不可用，已改用微软官方 WinDbg AppInstaller/MSIXBundle 安装。已验证 Microsoft.WinDbg 版本 1.2611.4001.0，安装目录包含 x64 CDB、TTD 组件和调试引擎；已复制 CDB 及依赖到 artifacts/windbg-tools/amd64，CDB 运行验证为 10.0.29680.1004。安装包已清理，未修改业务代码。
**2026-11-16 VM_M_GetImageEx 输入句柄解析静态闭合**：Ghidra 证据 ev_e460316b84e194f8a3606e058e4331ecfdac980b819eb9af5b2c972affa9012a、ev_4b7a933a19100128913d4ae2c925ed069a598ea170ec419d26648a297e086195、ev_5770eb04d94c82d827a636fe952fa803fdfe255b30eec14d78a6c0b4e208ce50、ev_11fe2e77469ab3a5ea5133c806506c4848c5ecb955561e04b2e83f37985dfa2d 闭合了 VM_M_GetImageEx→订阅查找→句柄 %d_%p_%s 解析→地址/长度返回链。静态链没有看到像素 memcpy，Blob 接收边界可具体化为句柄解析、地址/长度返回和算法调用；更低层映射实现及动态耗时仍未知。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 原厂 SDK Blob 输入合同补证**：VmModule_IO.cpp 的 VmModule_GetInputImageAddressAndDataSizeSharedMemoryName 直接调用 VM_M_GetImageEx，读取 IMAGE_DATA_V2 的 pImgData、nImgDataLen 和共享内存名，再复制共享内存名字符串；该辅助函数没有图像像素复制。结合 ImageSource SetImage[Image] 后进入 Blob ProcessAlg 的动态顺序，Blob 接收合同可表述为接收已发布图像指针和共享内存身份后进入算法。像素复制位于更低层资源发布或共享内存实现，函数级动态耗时仍未知。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 有效本地图像动态取证完整性审计**：已按解码/文件加载、帧复制/共享内存、格式转换、输出资源创建、Blob 接收、首次/后续分段耗时逐项核对证据。当前已确认入口、调用顺序、深拷贝合同、SetImageOutput 五元组、模块级冷/热和共享内存聚合耗时；四类子阶段的独立函数级动态耗时仍未确认。审计表已写入有效本地图像取证 HTML，当前阶段不能宣称原厂所有子阶段耗时全部闭合，未修改业务代码。
**2026-11-16 系统 CPU 采样入口复核**：已用提升权限启动 Windows Performance Recorder CPU profile，并在原厂 VisionMaster 打开 test.sol 的期间尝试采样；VisionMaster 进程在触发执行前提前退出。停止采样后 xperf 报告 There is no sampled profile data in the trace，未形成可用调用栈证据。采样文件已清理，原厂日志和 SDK 源码证据仍是当前有效基线，未修改业务代码。
**2026-11-16 07:14 Trace 共享内存聚合耗时补证**：对本次有效图像窗口检索映射/共享内存/复制关键词，首次只见三个 VM_M_SetImageEx（imgSize=1228800、copyMode=0），随后三个共享内存 execute count 0→1、SHM_CloseMapping 和 WriteSharedMemCount image size=3/use time=0.043500；后续对应 0→2、关闭映射和 use time=0.030900。没有独立映射创建、像素 memcpy、输入打开或复制完成事件。该 use time 只能作为三个共享内存结果的聚合边界，不能拆成解码、帧复制、Blob 接收或单个资源创建耗时。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 原厂 SDK 共享内存复制合同补证**：随附 SDK 的 SharedMemory.cpp Data2ShareMemory（约 444–484 行）明确执行文件映射创建、写入共享内存头和 memcpy 数据复制；VmModuleSharedMemoryBase::AllocateSharedMemory 负责映射节点分配/复用并返回数据区指针和共享内存名。该源码证明 SDK 具备共享内存复制合同和节点复用机制，但没有证明 ImageSourceModule.dll 本次单图路径直接调用 Data2ShareMemory；仅作为候选底层实现证据，不能替代函数级动态进入/退出和耗时。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 ModuleFrame 复制实现边界补证**：REA 搜索 ModuleFrame.dll 的 memcpy_s 仅解析到 MSVCR120.DLL 外部导入 ev_f521bc559e48cba21d39f4ef500840994ac87241ac2043f17879512b6c2ca8df，没有形成 DLL 内直接复制函数的完整调用者闭包。实际字节复制位置登记为 ModuleFrame 内部、共享内存 API 或更低层运行库的未解析边界；不能据此断言无复制，也不能把句柄发布时间当复制耗时。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 ModuleFrame VM_M_SetImageEx 复制模式静态闭合**：Ghidra 证据 ev_cf5da8707478cc38bbfbc2a6666ff78fb53d0500be6273c39ffa880eba99fdf2 显示原厂 VM_M_SetImageEx 读取 IMAGE_DATA 的 byCopyMode/长度，结合全局强制深拷贝状态计算有效 copyMode 后进入 FUN_180122e50；ev_420cb4bd2c9e10fff2128be4a5ba46f4520b4fa74f688b6bd531127228be9edd、ev_4777a2870333a06a728de00323d4c770d133ca0724fa5a1cc00fc68a9da3fee2、ev_efc98dabca47f6f63029f074f855d635b18ca0cf709a6ff6e20efafa8c4b563 进一步闭合结果缓存获取、锁保护句柄生成和 Module[N] SetImage 发布。结合 SDK 源码 0=深拷贝、1=浅拷贝及运行日志 copyMode=0，原厂有效链不能按零复制假设建模；实际字节复制函数和独立耗时仍未知。证据已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 原厂深拷贝合同补证**：补查原厂随附 SDK 生成器源码确认 IMAGE_DATA/IMAGE_DATA_V2 的 byCopyMode 语义为 0=深拷贝、1=浅拷贝；VmModule_IO 输出辅助函数将 bDeepCopy 写入 byCopyMode 后调用 VM_M_SetImageEx。07:13:07.467 ModuleFrame 记录 VM_M_ForceDeepCopyImage(force=true)，本次首次/后续有效本地图像的 Image、BinaryImage、BlobImage 发布均记录 imgSize=1228800、copyMode=0。可确认原厂链路启用强制深拷贝和深拷贝发布请求，不能按零复制假设设计；VM_M_SetImageEx 内部实际字节复制进入/退出及独立耗时仍未观测。证据已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 ConvertFileImage 冷路径静态闭合补证**：REA/Ghidra 证据 ev_ded871cb4fac8da2fbd0ffdc9fc8cc52af6feb19f2e2d98baad04e56826586a7 已确认原厂 CMyAlgorithmManager::ConvertFileImage 首次实例为空时调用 CreateImageInstance，经虚函数按路径加载文件，读取像素类型/宽高/数据指针并进行像素类型转换、类型设置和元数据记录；函数本身未见显式像素 memcpy，复制路径仍在后续共享内存/输出分支。该证据只闭合冷路径结构，不提供独立动态耗时，不能据此断言后续加载被缓存，也不转化为解码预热或缓存修改。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 句柄大小/别名边界补证**：`ModuleFrame.log` 中 ImageSource `Image`、Blob `BinaryImage`/`BlobImage` 句柄值均以 `1228800` 开头，与 1280×960 MONO8 字节数一致；但每次 GUID/地址段不同，只能证明尺寸一致，不能断言句柄别名或零复制。已写入有效图像取证 HTML，未修改业务代码。
**2026-11-16 ModuleFrame 动态句柄链补证**：Trace 日志已记录有效本地图像首次/后续执行的 `Module[0] SetImage[Image]` 输出句柄，以及 `Module[1] SetImage[BinaryImage]`、`SetImage[BlobImage]` 结果句柄；对应共享内存 execute count 从 `0→1`/`0→2`。这闭合了 ImageSource 输出句柄→Blob 输入/结果句柄的动态顺序，但仍没有 Blob 内部像素指针读取的函数级耗时。证据已写入有效图像取证 HTML，未修改业务代码。
**2026-11-16 SetImageOutput 五元组顺序校正**：重新读取原厂 `SetImageOutput` 汇编确认 Trace 五元组顺序为 `[总耗时, 像素格式映射, Width/Height/PixelFormat 写入, 输出描述准备, VM_M_SetImageEx 发布]`，不是“前四阶段、最后总耗时”。首次值 `0.036500,0.000000,0.026700,0.000200,0.009600` 中后四项之和约等于首项；已修正有效图像取证 HTML。未修改业务代码。
**2026-11-16 ProcessLocal Trace 有效性复核**：直接解析原厂 `Modules.log` 的首次/后续有效本地图像两条 `ProcessLocal` 记录，均为 67 个值，但每条包含 12 个绝对值超过 3,000,000 毫秒的正负异常值；仅前三项等少数值具备合理量级。说明原厂探针在本地图像分支存在未初始化、未执行分支或计时基准不一致槽位，不能把 67 个值整体当作可用分段耗时，也不能用异常值反推解码/复制阶段。证据已写入有效图像取证 HTML，未修改业务代码。
**2026-11-16 ProcessLocal 槽位计数纠正与分支边界补证**：重新按 Ghidra 反编译得到的原厂格式串计数，`ProcessLocal` 实际为 **67 个 `%f` 槽**，此前记录的 57 槽为误计数，已同步修正证据 HTML 和工程审查。`CMyAlgorithmManager::Process` 的 QPC 序列已对应到普通/Mono8 输出、`ConvertRgbToMono8`、共享内存清零/分配/两次 `memcpy_s` 等调用前后边界；仍不能把单个动态日志数值直接命名为某业务阶段耗时。未修改业务代码。
**2026-11-16 ProcessLocal 汇编顺序补证**：REA/Ghidra 完整汇编证据 `ev_2dbb09b0294dcc432a322ae5f83b85ecc1483ac69ff06a896a9df39def6a310c` 确认 `CMyAlgorithmManager::Process` 在 `0x180011ff2` 将多组 QPC 差值按固定栈/寄存器顺序传给 `ProcessLocal` 格式串 `0x18002f230`。这证明 57 个值是编译器展开的固定变参序列，但未保留原始局部变量名，仍不能逐槽赋予解码/复制/格式转换业务语义。已写入有效本地图像取证 HTML，未修改业务代码。
**2026-11-16 原厂 Trace 函数级动态证据补齐**：按原厂 LogLevelAdjustmentTool 实现创建临时 `sdk_log_t/server_log_t/proxy_log_t/frame_log_t/module_log_t`，通过 Agent Desktop 启动 VisionMaster 并打开 `C:\Users\Administrator\Desktop\test.sol`，对同一有效本地图像执行两次单次执行。新证据：`Modules.log` 出现 ImageSource `SetImageOutput` 五元组和 `ProcessLocal` QPC 序列；Blob 明确记录 `Process → ProcessAlg → LimitJudge → ProcessOutput` 起止；`ModuleFrame.log` 同时给出 ImageSource/Blob 的 SetImage 句柄、module/process/algorithm 时间、共享内存 execute count 和结果回调。首次 07:14:31：ImageSource 11.013/10.983/10.365ms，Blob 9.092/9.080/6.630ms；后续 07:14:50：ImageSource 4.667/4.659/4.633ms，Blob 4.441/4.435/2.906ms。ProcessLocal 的 QPC 序列尚未逐槽映射，不能擅自当作解码、帧复制、格式转换或输出资源创建的独立耗时；SetImageOutput 五元组已由 Ghidra 闭合其内部五阶段语义；该边界已写入 `docs/collaboration/evidence/VM4.4有效本地图像输入取证-20261116.html`。Trace 标记已删除，VisionMaster 已关闭；未修改项目代码。
**2026-11-16 原厂日志等级工具实现补证**：使用 ILSpy 对原厂 `LogLevelAdjustmentTool.exe` 只读反编译，确认 `LogLevelConfigUtil.Adjustment` 通过创建 `sdk_log_t`、`server_log_t`、`proxy_log_t`、`frame_log_t`、`module_log_t` 标记文件切换等级，`GetLangType` 读取 `Applications\LangCFG\LanguageSet.cfg`，`Load` 从当前工作目录读取 JSON。此前工具崩溃的直接原因是启动工作目录不正确导致 JSON `FileNotFoundException`；按真实实现创建 Trace 标记后启动原厂进程，但其窗口仍无法被 Agent Desktop/UIA 读取，未执行方案操作；Trace 标记已删除，原厂进程已结束。该证据补入有效图像取证 HTML，函数级动态耗时仍未取得。
**2026-11-16 自研软件与工具工程结构审查启动**：用户指出当前自研软件、工具工程和执行重构同样是在 Ghidra 之前依据表面语义形成，不能只重写 ExecutionServer。已盘点 `src` 约 1916 个 C# 文件/19.9 万行、`tools` 约 63 个 C# 文件/6100 行及项目引用图。初步确认工具 Runtime/WPF 二分可保留，但 `Core → Plugin.Editing.Wpf`、`Plugin.Infrastructure → Application/Plugin.Wpf/Plugin.Editing.Wpf`、`Infrastructure.ImageSources → Application` 属于 P0 反向依赖；ExecutionServer 仍是旧 WorkflowExecutionEngine 包装；ImageSource/Blob Runtime 仍包含未由原厂证据确认的复制、快照和探针路径。新增 `docs/09-现行文档/自研软件与工具工程结构审查-20261116.html`，主设计文档已加入附录入口。本轮只分析和维护文档，未修改业务代码。
**2026-11-16 原厂动态细分入口复核**：尝试启动原厂 `LogLevelAdjustmentTool.exe` 以切换 Trace/Debug 并取得 ModuleProxy frame/module 细分日志。配置确认可指向 `frame_log_`、`module_log_`、SDK 和 Server；但工具没有可访问窗口，Windows Application Error 反复记录 `.NET` 异常码 `0xe0434352`/`0xc000041d`（模块 `KERNELBASE.dll`），因此当前环境无法通过该工具获得函数级动态时间戳。该失败已记录为动态取证入口阻断，不等同于原厂不存在解码/复制/输出阶段；现有 ModuleFrame/PlatformSDK 的模块级顺序和首次/后续耗时仍有效，函数级分段继续保持未知。未修改项目代码。
**2026-11-16 主设计文档重审版更新**：按用户要求保留 docs/09-现行文档/执行引擎产品级架构重构设计.html 的原章节、流程图和验收矩阵风格，已将标题/状态改为“原厂反编译重审版（设计待证据闭合）”，新增 ImageSource/Blob 真实入口与当前源码差异基线，并明确 E0–E5 当前实现状态不等于原厂执行模型对齐或首次/后续耗时一致。旧文档不再作为历史孤立草案，而作为后续定稿的主设计载体；本轮仍未修改业务代码。
**2026-11-16 REA/Ghidra 证据边界补强**：从保留证据重新读取 ImageSource CMyAlgorithmManager::Process（x18000d820，证据 v_d6c18afad1d8f71ff46c383e59318424165019e1b2a32c5bc101e0db59e1fdc5），确认已解析调用包含 SetImageOutput、SetImageOutputMono8、ConvertRgbToMono8、OutputExtra、StitchImageOutput、UpdateImageFilesListFromDir。Ghidra 同时报告计算/间接调用未完全解析、值流达到默认上限，因此静态证据足以固定入口关系，但不能冒充完整动态调用图、运行时分段耗时或全部缓冲区所有权。该限制已写入 原厂反编译后执行链重新设计前置方案-20261116.html；项目代码未修改。

**2026-11-16 原厂反编译后重新设计前置基线**：Ghidra 已确认 ImageSource 的 Process → ConvertFileImage/ConvertPixelType → SetImageOutput* 和 Blob 的 Process → ProcessAlg → LimitJudge → ProcessOutput 真实入口。当前源码仍在 ExecutionServer → callback → WorkflowExecutionEngine 旧链上，ImageSource 正式路径含额外文件扫描/HImage复制/快照，Blob 每轮含整帧复制及多类结果资源物化；这些差异足以使旧执行引擎重构设计失效为实施依据。新增 docs/09-现行文档/原厂反编译后执行链重新设计前置方案-20261116.html，明确重新设计范围、三方差异矩阵、运行时所有权草图和实施硬门槛；本轮仍未修改项目代码，下一步继续补函数级动态证据并完成整体设计。

**2026-11-16 原厂独立 trace 目标搜索复核**：已对 `C:\Program Files\VisionMaster4.4.0\Applications` 及原厂安装目录的日志、文本、配置和二进制字符串检索 `ProcessLocal`、`ProcessCamera`、`SetImageOutputMono8`、`StitchImageOutput`、`hftest`。只有 DLL/模板中的静态字符串，没有发现独立运行时 trace 文件或函数时间戳；现有 ModuleFrame/PlatformSDK 仍只提供模块、流程和 SDK 回调级耗时。该结果进一步确认动态缺口不是漏读日志文件，项目代码未修改。

**2026-11-16 原厂证据完成性审计**：有效本地图像解码、MONO8 格式转换、当前帧选择、ImageSource→Blob 成功顺序及首次/第二次流程级耗时已由原厂日志证明；ImageSource/Blob 静态入口、QPC/hftest 位置、帧指针发布、Blob 点集/运行信息输出已由 Ghidra 证明。帧复制、输出资源创建和 Blob 输入句柄的独立运行时事件/耗时仍缺失，不能从 module/process/algorithm 总耗时反推。已修正文档中“Ghidra 未配置”的过时表述；当前目标仍未完成，项目代码未修改。

**2026-11-16 原厂反编译后设计前提纠正**：用户指出此前《执行引擎产品级架构重构设计》形成于 Ghidra 原厂 DLL 反编译之前，部分 VM 内部架构、缓存、资源发布和优化结论只是表面语义推断，不能继续直接指导实施。已在 `docs/09-现行文档/执行引擎产品级架构重构设计.html` 顶部标注为历史设计草案，并新增 `docs/09-现行文档/原厂反编译后执行链差异重分析-20261116.html`。源码对照已确认：ExecutionServer 仍包装旧 WorkflowExecutionEngine；SolutionRuntime 仍只有身份；当前 ImageSource 有额外整文件读取、HImage/快照复制；Blob 每轮显式整帧复制并创建多类结果资源；正式链仍开启会改变路径的 DecodeSplit/TimingProbe。下一步必须基于 Ghidra 真实调用链重新完成完整差异矩阵和整体实施设计，设计定稿前不修改执行代码。

**2026-11-16 原厂同一有效本地图像会话第二次执行补证**：使用 agent-desktop 在同一 VisionMaster 进程（ProcessID-18972）、同一方案 `C:\Users\Administrator\Desktop\test.sol`、同一 ImageSource 本地图像/MONO8 配置下再次点击“单次执行”。原厂 `ModuleFrame.log` 记录 executeCount=2：06:42:11.151 开始，ImageSource module/process/algorithm=4.513/4.511/4.482ms，Blob=3.498/3.497/3.394ms，流程 `free time=8.112ms`，06:42:11.160 结束；`PlatformSDK.log` 同批 `ProcessTime=8.111900ms`，状态回调在 06:42:11.151/06:42:11.160 完成。与同进程首次 executeCount=1（06:40:04.843–04.870：ImageSource 9.633/9.628/8.435ms，Blob 9.770/9.769/7.559ms，流程 20.384ms）对照，首次到第二次流程约降低 60.2%，ImageSource 和 Blob 均显著下降。该批仍未出现 `hftest`、`ProcessLocal`、`SetImageOutput`、`StitchImageOutput` 或独立帧复制/输出资源创建行；当前只能把动态顺序确认到 RunProcedureOnce→Module0→Module1→result dispatch→procedure free/SDK callback，不能把模块总耗时拆分为解码、帧复制、格式转换、输出资源创建或 Blob 输入句柄接收。未修改项目代码。

**2026-11-16 同一有效图像会话首次/后续完整执行补证**：原厂 ProcessID-5456 的 UI 日志与 ModuleFrame 已配对。首次 `executeCount=1` 前设置 `CurrentImagePath=12.jpg`（23:26:06.191），ImageSource/Blob module-process-algorithm=7.948/7.942/6.965ms、10.725/10.724/9.097ms，流程=19.420ms、结果回调=14.004ms。后续同一会话 `executeCount=2` 为 3.969/3.967/3.954ms 与 2.833/2.832/2.719ms，流程=6.897ms；随后 `executeCount=3` 仍处理 12.jpg，为 3.573/3.571/3.557ms 与 2.780/2.779/2.682ms，流程=6.453ms。UI 在每次完成回调后才切到下一张：2.jpg→executeCount=4 流程 5.670ms，3.jpg→executeCount=5 流程 8.524ms，4.jpg→executeCount=6 流程 5.955ms。该证据已写入有效图像取证 HTML，直接证明有效本地图像条件下首次与后续完整执行存在稳定冷/热差异；仍未把帧复制和输出资源创建拆成独立函数级耗时。
**2026-11-16 有效图像后的 Blob 后续自执行证据**：同一原厂进程在完整流程首次执行后于 22:18:46.221 调用 `VM_M_RunModuleSelf(moduleId=1)`，Blob 模块再次 `status=0(Ok)`，module/process/algorithm=17.846/17.842/9.296ms，结果回调=14.060ms。该证据支持 Blob 在同一进程可继续接收/处理已有输入并发布结果，但它不是完整流程第二次执行，不能替代 ImageSource→Blob 的冷/热对照。
**2026-11-16 有效本地图像动态链补证**：从原厂保留的 `UI\Ui.log` 与 `ModuleProxy\ModuleFrame.log` 找到 ProcessID-44256 的有效本地图像链：15:27:19.143–19.396 通过 `ModuleManager::SetParamValue(AddImage)` 加入 1–12.jpg；随后 `ImageData::GetImageDataFromFile` 对 1.jpg 等记录 `InitImage`、`PixelFormat=MVD_PIXEL_MONO_08`、`ConvertImagePixelFormat`、`GetImageData`、`ResizeImageData` 成功；15:27:29.402 设置 `CurrentImagePath=12.jpg`。同一次执行 15:27:29.438 的 ImageSource 模块返回 `status=0(Ok)`，module/process/algorithm 为 6.867/6.863/5.683ms；15:27:29.447 的 Blob 模块返回 `status=0(Ok)`，为 9.623/9.622/8.014ms；结果回调 12.240ms，流程 free time=16.889ms。该证据已补入 `docs/collaboration/evidence/VM4.4有效本地图像输入取证-20261116.html`。已证文件加入、解码、MONO8 格式转换、数据取得、缩放、当前帧选择、ImageSource 成功、Blob 成功和结果回调；帧复制与输出资源创建没有函数级独立计时，仍不得从模块总耗时反推。当前未修改代码。
**2026-11-16 有效本地图像输入取证仍未闭合**：原厂 ImageSource 配置窗口已直接确认“本地图像 / MONO8”，并观察到图像缓存、自动切换、最后一张停止、拼接使能和订阅图片文件夹等原厂状态。但同一方案运行日志仍出现 `CameraId=-1`、`SubscribeFolderPath: Image file not exist, path=`，主界面流程图像区保持空棋盘，历史执行记录为空；因此尚未取得有效本地图像解码、帧复制、格式转换、输出资源创建和 Blob 有效接收的运行时证据。新增证据文档 `docs/collaboration/evidence/VM4.4有效本地图像输入取证-20261116.html`。当前仍只取证，不修改代码；下一步必须先找出原厂本地图像文件的实际绑定入口并取得非零图像尺寸/非空路径，再分段记录内部链路。
**项目实施硬门槛（2026-11-16）**：所有功能、优化和重构统一遵守“原厂取证 → 完整链路分析 → 差异确认 → 形成实施设计 → 修改代码 → 按原厂链路验收”。原厂已有证据不足时，必须先使用逆向工具补充取证，再进入设计和实现；证据不足只能登记假设，不能直接改代码或把假设包装成优化结论。**2026-11-16 重构目标对齐审查纠正**：依据本轮代码审查交接，撤回此前“首次执行/后续执行已对齐”及“公共执行链首次耗时问题已解决”的表述。当前仅确认请求边界、生命周期骨架、工具会话复用、输入快照和结果版本机制具备；ExecutionCoordinator 仍只是 ExecutionServer 的调用包装，DocumentRunBatch 仍经 callback 进入 WorkflowExecutionEngine，SolutionRuntime 尚未实际持有执行服务、计划、模块宿主、输入工作区和结果存储，旧 CaptureRunBatch → WorkflowExecutionEngine → ResultPublication 生产链仍在。首次/后续耗时一致也未实现：ImageSourceRunSequence 的本地图像预热只覆盖选中图像，且可能把成本前移；图像帧复制、格式转换、灰度/拼接、输出资源创建及 Blob/HALCON 首次准备仍在正式执行链。后续必须先按代码链补齐运行时所有权和冷路径，再做耗时验收；不得再用 resident-plan-hit、module-session-hit 或局部日志推导目标已完成。方案切换暂缓。**2026-11-16 E3/E4/E5 headed Agent Desktop 实机收口**：当前 Release 路径为 E:\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe，使用官方 agent-desktop 1.3.1 headed MCP 在同一用户桌面运行。隔离验收根为 E:\SunEyeVision_V2\.agent-desktop-startup-test，方案为 Solutions\11111.solution。先选中 Blob分析1 节点后，模块结果页显示 Blob 数值表，当前结果页显示 5 行结构化结果，历史结果页显示 5 条带执行序号、时间和 Blob 数据的记录，帮助页显示“未提供工具帮助”；四类 UI 切换均未新增 RunRequest，当前结果行入口可正常驱动结果投影。E4 通过同一 headed 实例启动连续运行，50ms 内通过物理点击停止，最新日志观察到活动 run-request-cancelled，停止后按钮恢复“连续运行”；随后单次 Run 日志为 mode=formal、流程1：Success、run-request-completed、run-request-retired。随后从打开方案对话框选择隔离根内的 test1112.solution，日志记录 solution-open-transition-request、旧服务退役、resident-plan-invalidated、新服务创建和 solution-open-installed，迟到回调未写入新会话；该文件内部名称为空，因此 UI 显示“未命名方案”。最后关闭切换后的主窗口，日志 E:\SunEyeVision_V2\.agent-desktop-startup-test\Data\Logs\suneye.log2026-11-16.log 记录 module-sessions-retired、MainWindow.Closed、application-exit，最终计数 batches=0/engines=0/sessions=0/resultOwners=0/executionServers=0/executionRequests=0。本批未启动隐藏回退进程，也未创建第二个并行 SunEyeVision 实例。E3、E4 和 E5 五路径当前 Release 实机路径已收口。

**2026-11-16 agent-desktop 安装与启动退出链收口**：按用户授权安装 `agent-desktop` 1.3.1 Windows x64 到 `E:\SunEyeVision_V2\.tools\agent-desktop`，配置 `agent-desktop.cmd`、用户 PATH 和可信状态目录 `E:\SunEyeVision_V2\.agent-desktop-state-elevated`。agent-desktop 状态检查显示 Windows UI Automation 权限已授予，已读取 SunEyeVision 起始页控件树并确认真实按钮引用。首次启动发现 Release 载荷清单中的 `SunEyeVision.Foundation.dll` 封签与实际文件不一致，重新执行 UI Release 构建后载荷封签为 `E7B83BF58A95AB509DBBD774FEA54DEC8096211D3EC2A96155CEBBAEE01454CC`，Foundation 实际 SHA 与清单一致，程序进入起始页。源码补齐启动加载窗口右上角取消按钮与 Esc 取消，`LoadingWindow` 发出取消事件，App 通过启动取消令牌停止后续阶段并先排空插件目录，再正常退出；插件准入接口增加可选 CancellationToken 并在准入阶段检查。UI Release 构建 0 错误、359 个存量警告。当前已具备构建和 agent-desktop 控件树证据，真实慢启动点击取消的人工视觉与退出耗时仍待验证。\r\n\r\n**2026-11-15 执行节点快照缓存构建**：用户复测日志显示方案加载期间的预热批捕获约42ms，但首次正式执行仍重新构建 workflow-snapshots=52ms，证明前移预热只热 JIT 没有复用快照。已在 DocumentSession 按流程、节点和 ExecutionConfigurationIdentity 缓存不可变 ExecutionNodeSnapshot；节点配置身份变化时自然换键，方案关闭或运行目录退役清空缓存，正式执行的变量、宿主输入、图像和资源快照仍每轮重新冻结。全量 Release 构建成功，0 个错误、449 个存量警告。载荷封签 920502D8794F1085F0FC6EDC5277E3121842DC633C286F71161937222A99E038；SunEyeVision.UI.dll 13036B4DAFC16C7058EECFBC476EFCA49896C2C3987A3F53BDEC28346E518101；SunEyeVision.Application.dll 64D395B4AC7E318A3071EE47AA5BA81E809A8ACD4176282B160670EFD2B10309；SunEyeVision.UI.exe DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。待用户用该 Release 复测首次正式执行，重点确认 workflow-snapshots 是否降到接近 0，以及配置修改/节点切换后是否按新身份重新构建。

**2026-11-15 首次执行快照构建前移修复**：最新细分日志确认首次 atch-capture=99ms 中 workflow-snapshots=89ms，而结果发布仅9ms；Blob 首轮细分为灰度统计约11ms、逐 Blob 区域捕获约5ms、区域结果捕获约6ms。已在方案打开/新建完成后、加载遮罩仍有效的阶段执行一次无宿主输入的 WarmExecutionCapture，提前建立根流程执行快照结构；不执行工具、不推进图像游标、不发布结果，正式运行仍重新冻结输入和资源。全量 Release 构建成功，0 个错误、366 个存量警告。载荷封签 66FEC5E73E9E146C120B750C60E7CF92EA174B610E90AC7760FC4C91FB61A104；SunEyeVision.UI.dll BE4E487B11CA78AACC9068C30AD6AC6CDF4D0EA68A3696303DD3B4223FE8CAA6；SunEyeVision.Application.dll 8553A956FA0250A489C1B70359811130B64ECDD7E043AC69DFC37ACC5B6B0657；SunEyeVision.UI.exe DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。待用户用该 Release 复测，重点确认首次 atch-capture/workflow-snapshots 是否从约89ms降到接近后续水平；Blob 灰度统计和区域捕获暂未做猜测性改动。

**2026-11-15 首轮批捕获/Blob/结果发布细分探针 Release**：沿真实入口增加分段探针：DocumentSession.CaptureRunBatchCore 记录 workflow-ids、input-validation、document-state、workflow-snapshots、variable-capture、host-requests、batch-ready；Blob 记录逐 Blob region capture、boundary、gray statistics，以及 final/accepted region select、blob image、region capture；正式发布记录 batch-run-return、runtime-changes-drained、workflow-status-and-settled、formal-result-refresh。探针不改变执行、资源和发布语义。全量 Release 构建成功，0 个错误、374 个存量警告。载荷封签 C33B11C5AA8634BFDFD835FB27FA87C148B4A69A15FC8596A215CC8E1D9523CF；SunEyeVision.UI.dll AE7048791C4142E594338BA80FF28EC6933584B82462CFA61DD1B2E9F7A7F414；SunEyeVision.Application.dll 8553A956FA0250A489C1B70359811130B64ECDD7E043AC69DFC37ACC5B6B0657；SunEyeVision.Tool.BlobAnalysis.Runtime.dll 8DBC6B785E2A079C9A322EEC63B89337CA4B7D6B73DFC470026971ADEE4A1F36；SunEyeVision.UI.exe DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。本批已完成具体入口的细分定位准备，尚未基于新细分日志实施性能改动；需用该 Release 完成一次首次/后续执行后，再按具体段实施修复。

**2026-11-15 首帧/Blob 算子预热实测复核**：用户使用载荷 FD76574... 完成首次及后续执行。最新日志 C:\Users\Administrator\AppData\Local\SunEyeVision\Logs\2026\11\15\SunEyeVision_190301.txt 显示启动 Runtime.Warmup=23.5ms；首次 ImageSource 18ms（解码11ms、帧捕获7ms），首次 Blob 86ms/执行器93ms（阈值5ms、逐Blob测量20ms、区域快照11ms），首次引擎172ms，Perf 总计319ms（batch-capture100/batch-run183/result-publish11）。后续 ImageSource 5-10ms、Blob 25-39ms、引擎47-59ms。与上一版首次总计319ms、Blob 79ms相比，算子预热没有带来首轮业务耗时下降；它只证明 HALCON 基础算子已在启动阶段触发，真实首轮瓶颈仍在真实图像复制/结果资源创建、首轮 Blob 数据规模和调度/发布链，不能继续把静态算子预热当作已解决。下一步需沿首轮 atch-capture=100ms、per-blob-measure/region-snapshots 和首轮结果发布做更细分段。

**2026-11-15 首帧与 Blob 首次算子预热构建**：依据最新日志，新增 HALCON 图像管线预热（图像创建、复制、通道/尺寸/Domain 获取）和 Blob 算子预热（阈值、连通域、面积中心、外接矩形、旋转矩形、圆度、矩形度、周长），均在启动页运行目录预热阶段执行，不接触真实输入图像和结果。Release 全量构建成功，0 个错误、11 个输出警告；首轮构建中的二义性调用已修正。载荷封签 FD76574CCAB5A2199FBD8F6CF478EC9731230FC0FDCB7F002E491553A8F3D8B1；SunEyeVision.UI.dll 33C5336896DC782DD1513EE88233B3BA35E35BEFAA50E6F8560AF2A2CC783DFA；SunEyeVision.HalAdapter.Runtime.dll B73CF9B2D6A51687E1DCAEDED7BEDD2E638B24EDDCC813B9F7A3307E4B94E7F6；SunEyeVision.Tool.ImageSource.Runtime.dll 5BC3B202B6C5BB000CC6C4998D7BB5E97FF9025055DFE85F4BB3F289D829725E；SunEyeVision.Tool.BlobAnalysis.Runtime.dll 3BD4A26E93553676BEEC6274587656116D8B56C4F5C618BAF27CCA7A5748F118；SunEyeVision.UI.exe DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。待用户用该 Release 复测首次/后续执行，重点比较 Runtime.Warmup 时长、首轮 image-frame-capture 与 Blob threshold/per-blob-measure/region-snapshots。

**2026-11-15 公共执行链预热实测**：用户使用最新 Release 完成首次及后续执行并关闭程序。最新日志 C:\Users\Administrator\AppData\Local\SunEyeVision\Logs\2026\11\15\SunEyeVision_185639.txt 证实 Runtime.Warmup completed elapsedMs=30.5 tools=2；ImageLoad/Blob 两个工具首次与后续的 session-create 均为 ms，插件首次使用标记也未再形成执行期耗时。首次流程：图像源 22ms（读取解码 12ms、帧捕获 6ms），Blob 86ms（HALCON scope 0ms、图像复制 6ms、阈值 6ms），引擎 162ms，Perf 总计 319ms，其中 batch-capture=110ms、batch-run=174ms、结果发布=10ms。第二次：图像源 7-8ms、Blob 30-32ms、引擎 44ms；第三次：图像源 8-9ms、Blob 26-28ms、引擎 39ms。结论：公共执行链首次慢的问题已解决；剩余首轮差异来自首帧图像准备/解码与 Blob 算法首轮数据处理，不再是插件加载、HALCON 上下文或会话创建。当前未继续改算法，避免把数据处理耗时误判为公共框架初始化。

**2026-11-15 公共执行链首次耗时优化**：依据首次/后续探针分段，确认差异来自公共运行目录首次初始化、HALCON 进程上下文和每次执行都重新创建工具会话。已增加 IToolRuntimeWarmup 与 IReusableToolSession 合同；运行目录缓存准入定义和可复用会话，ToolRuntimeExecutor 正式执行与调试执行均通过运行目录会话租约，方案/插件目录退役时统一释放；Blob/ImageSource 在启动页阶段预热 HALCON 上下文和会话，方案加载与首次执行不再承担这轮初始化。全量 Release 构建成功，0 个错误、479 个存量警告。载荷封签 2C7569CC778791365E4CD31E7376004FD392FE469CCE4B2FBE88588BA05186EB；SunEyeVision.UI.dll SHA-256 33C5336896DC782DD1513EE88233B3BA35E35BEFAA50E6F8560AF2A2CC783DFA；SunEyeVision.Plugin.Runtime.dll A28C77C3A45AF05BDBF2C1FEA64B7B6F69BDDE8A50EDD7B14C5367505AE170E3；SunEyeVision.HalAdapter.Runtime.dll 5F4F38AF3B3E10B491E7CAE989167817E590A8D054A7334F17B22C93618C3E4A；SunEyeVision.Tool.ImageSource.Runtime.dll 9FE42C241FF5CD6143B3BFCEB911983C06F52D7D9F2A995430C76AF4A6589632；SunEyeVision.Tool.BlobAnalysis.Runtime.dll 64EA906625973E09596C37C029A76EB3A636D02B1616AF7E723A794CC323F6F8；SunEyeVision.UI.exe DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。本批完成构建验证，未做人工首次/后续耗时复测；需用该 Release 对比启动日志中的 Runtime.Warmup、首次执行与第二次执行的 session-create 和总耗时。

**2026-11-15 启动页品牌修正**：用户指出启动页误用原厂 HIKROBOT/Vision master 品牌。已沿用 SunEyeVision 自有 SunEyeVision.BrandLogo 资源，替换启动页左上角与中央品牌区，保留启动阶段结构、深色主题和进度提示。全量 Release 构建成功，0 个错误、366 个存量警告。载荷封签 72D2ECE6D51E171AD8BFF0CC595E623117DE2EA7DD60958695E188AFC70E9438；SunEyeVision.UI.dll SHA-256 43AD2A98BA691CC85EF0830118D5580687073B94D866BCC6F1B60DE985C58A7C；SunEyeVision.UI.exe SHA-256 DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB。本次为启动页单文件视觉改动，按规则只做构建验证，待用户实机确认品牌显示。

**长期做法·验证分级（2026-11-15 用户明确要求，跨对话持续有效）**：用户批评"随便一个小的更改都要跑一遍测试或回归测试，太浪费时间"。自本日起按改动风险面分级验证，**不再无差别跑定向/回归套件**：①单文件局部改动（短路、文案、样式、单点逻辑）→ 只做构建 0 错误 → 部署 → 用户实机复现，不跑测试；②触碰共享高频链（publication 生命周期、执行链、保存/重开、插件加载）→ 构建 + 对应 1-2 条定向，不跑全套；③改架构/接口/跨模块合同 → 构建 + 相关套件，且仅在确有回归风险时。禁止：每批机械跑 EditorDebugSession 52 条、每次 stash 做基线对照（该类套件对"看不见的清空/时序"类缺陷无拦截能力，实测多批真 bug 均由用户实机复现发现）。定位优先用临时探针采证（采证后删除），不以流水测试数量代替定位。用户实机确认即验收，不等待自动化全程通过再交付。

**2026-11-15 PresentSnapshot 同身份短路收口（c335bfc52，用户实机验证通过）**：前一批重绑定静默化后仍清空，改用**临时探针采证**（[ROITRACE] 日志埋 10 个决策点：编辑接纳/重绑定分支/Retire 调用方/PresentSnapshot 分支/Refresh epoch/Present/pane.Clear/scene 分支），用户实机点一次开关即钉死真凶：`RetireResultPublication from=MainWindowViewModel.PresentSnapshot`——策略编辑触发 RefreshFormalPublishedResult 走 retained 分支，`PresentSnapshot` 内部无条件 `RetireResultPublication()`，同一快照重复呈现也全量退役重建，epoch 循环推进→pane.Clear→Present 失败→场景 frame=False→ClearOriginal，图文层反复被清。修复：`PresentSnapshot` 同身份短路（当前呈现即该快照时直接返回，不退役不重建；新执行新 Identity 才退役重建，对齐原件 UpdateModuleViewResultShow 按数据变化更新）。**用户实机确认修复**；探针全部删除（净改动仅 +5 行）；正式载荷 `SunEyeVision.UI.dll` `57D88E6A49723C60B6B73F0AC0B79F58C51BEBE3A2C405BC9AFC7F4D3624A603`。方法论教训（已列入长期做法）：此类"看不见的清空"直接用临时探针采证，不靠推演撞墙；定向回归套件对该类缺陷无拦截能力，验证以构建+风险面判断+用户实机为主，不再每批机械跑全套。

**2026-11-15 同稳定节点重绑定静默化（765b8ed0c，待用户复验）**：用户复验单所有权批次 ❌——切自动切换开关后**整个视口空白**（背景图+图文全没，表/缩略图在）。根因：策略提交重建节点实例，`SelectedNode` 换新实例引用被选择管道误判为节点切换→`RetireResultPublication` 清空→重呈现时执行域已退役且无保留快照→落空声明发布。修复：①`SetSelectedNodeIdentity` 同稳定 Id 重绑定短路——静默替换实例引用，不退役发布、不推进 `_outputContractSelectionGeneration`、不重建结果视图（原件语义：提交重建实例对结果视图不可见，返回 false 不触发 `RefreshFormalNodeSelection` 递归）；②`CaptureResultViewOwner` 闭包按稳定 Id 判定归属并用当前实例复核，防后续 `EnsureCurrent` 因旧实例引用误退役。验证：EditorDebugSession+pane+presenter+三页 20F/105P 与基线名单一致零回归；全量 Release 0 错误。载荷：`SunEyeVision.UI.dll` `02760D3E28980378A0756C5D3EE90EECEF9C7A9CB57BD74408FA0EAEB359CB94`、`SunEyeVision.UI.exe` `AC86BC2C48394B1C0B0892D40E935153352AAE7B07DD4C12A7E16D45CBC54F5F`。复验清单同前批六条（自动切换开关为主项）。

**2026-11-15 结果显示层单一所有权重构落地（b61451167，待用户人工验收）**：用户实锤"点自动切换开关后 Blob 图形/文本被清空"，经 Codex 五层补丁（IsAcceptingEdit 豁免/冻结合同/保留凭证/EditVersion 豁免/在途识别）仍复现，判定为结构缺陷后成片重构（方案归档 `docs/01-方案设计/2026/11/15/结果显示层单一所有权重构方案-20261115-120000.html`）。核心：①场景重建/存活守卫剔除 publication 执行域当前性——配置编辑使发布变陈旧后背景帧与结果图文层全保留，新执行产生新发布对象才原子替换（原厂锚点：自动切换=参数节点 AutoPlay 普通绑定 LocalImageListView.xaml:11-45；参数变化仅 P_ValueChanged→UpdateModuleViewResultShow 用旧数据保持显示 ModuleResultView.xaml.cs.pending:473/:1344）；②`RefreshFormalPublishedResult` 陈旧保持（同一快照身份不重建、发布仍在显示时不清空）；③正式完成快照呈现即 Retain（最近完成执行第一类公民）；④`IResultPublication` 新增 `IsRetired` 两分合同：陈旧保留显示、退役（Dispose）释放借用归还资源（资源合同不变）；⑤拆除全部补偿层不留双轨：SourceDisplayPreservationScope（整文件删除）、PrepareDisplayRetention/DisplayOnly/ClearUnlessRetained/ClearRetained、ImageSourcePrepareDisplay、RetainCurrentResultForEdit、三个冻结显示字段、图像源编辑在途豁免窗口；图像源策略编辑统一轻路径不再触发工作区全量重建。验证：pane 契约 30/30（新增"陈旧发布场景保留+新发布原子替换"）、ResultPresentationPresenter 38/38、EditorDebugSession 19F/33P 经 stash 对照与基线名单一致零回归（注意：基线已随 Codex 批次从账本旧记 16F 漂移为 19F）、Blob 三页 4/5（失败条 `Real_three_pages_read_numeric_models_and_retire` 经 stash 基线甄别为 **Codex 批次已引入的存量回归**，非本批，待另行处置）；全量 Release 0 错误。载荷（2026-11-15 03:17）：`SunEyeVision.UI.dll` `9E1B15A58BA17719271426560A124F9E3AFAB2AF432AA14DB3E6C54F502D6CAA`、`SunEyeVision.UI.exe` `183C3E866B1DE91DE99EA94B12C9339A84FF5B563C50791AA194054AD9678AA5`、`SunEyeVision.Plugin.Wpf.dll` `06E81045AE13380F12EFA27C0AA16C7230AA99468EE18F37CCBBA7E29961FE7E`。待人工验收：①执行 ImageSource→Blob 形成图形/文本/表→切自动切换开关（开/关）→图形文本保留；②运行模式/运行勾选/缩略图切换同样保留；③改参数/ROI 不执行→保持；④重新执行→整体替换不叠加；⑤切节点/方案/关闭→按身份清理；⑥高亮/缩略图选中/帮助页/历史格式/ROI 链零回归。

**2026-11-15 正式 Release 重建封签**：用户关闭程序后重新执行全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false`，构建成功，0 错误、1 个本次输出警告（log4net NU1902）。最终载荷 SHA-256 `DFCE3CED92BE513DF30A8CEBD31091B97E5D6032ADB7C979443B9D13E2846037`；`SunEyeVision.UI.dll` `061E7EF89F950C9E4314A45556CCF3F0C6149D421F96EEC792C53A3D90427BCC`；`SunEyeVision.Tool.ImageSource.Runtime.dll` `B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`；`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。验收使用 `Release/SunEyeVision_v1.0.0/SunEyeVision`：执行 ImageSource→Blob 形成图形、文本和结果表；切换自动切换、运行模式、运行全部及图像源缩略图，确认图像更新但上一轮图形、文本和结果表保持；切换“图像源1·输出图像”与 Blob 输出来回确认结果层不丢；重新执行确认结果整体替换；切换节点/方案确认按身份清理。**2026-11-15 图像源输出切换结果图文场景保留修复**：用户复现“图像和当前结果表仍在，但切到图像源1·输出图像后 Blob 图形/文本消失”。全链路追踪确认直接清理点是 `ResultWorkspacePresenter.Present` 在来源切换前无条件销毁 `DisplayScene`；同时来源重绑期间显示描述可能短暂不可用，重绘异常路径又把旧场景清空。已改为输出视图仅替换背景帧，保留结果场景生命周期；新帧回执有效后原子替换图文层，短暂描述重绑失败时保留上一轮场景，正式执行、发布退役、节点/方案切换仍正常清理。UI Release 构建通过，0 错误、5720 个存量警告；无探针。Build payload SHA-256 `E5349F796917489A3D4EF32EE6A04BFBAC76C6275EE010EA250C54B926BA6994`；`SunEyeVision.UI.dll` `061E7EF89F950C9E4314A45556CCF3F0C6149D421F96EEC792C53A3D90427BCC`；`SunEyeVision.Tool.ImageSource.Runtime.dll` `B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`；`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。验收：执行 ImageSource→Blob，确认图形/文本/表格；切到“图像源1·输出图像”或其他缩略图，确认图像更新但 Blob 图形/文本/表格仍在；再切回 Blob 输出确认仍在；重新执行确认整体替换；切换节点/方案确认按身份清理。**2026-11-15 自动切换后结果图文层保留修复**：依据用户截图沿“图像源策略提交→DocumentSession.Changed→结果发布→ResultWorkspacePresenter.QueueResultDisplay”核对，确认结果快照和结果表仍在，但节点配置实例/显示版本更新使图形与文本显示层的旧快照被判失效并清除。修复在图像源编辑前同时冻结当前已发布结果的 `ResultDisplayDescriptor`、`ResultDisplayAppearance` 与几何快照；策略提交后的重绘继续使用该冻结显示合同，只更新图像游标，不重算或清空上一轮结果图形/文本。节点、方案或正式执行结果变化时仍通过既有 `ClearRetainedEditorDebugSnapshot` 释放冻结合同并整体替换。UI 定向测试 `ResultPresentationPresenterTests` 38/38 通过；无探针。全量 Release 构建成功，0 错误（本次输出警告 380，均为既有存量警告）。Build payload SHA-256 `0FC9E419F07333489A7A7DA559961C037E448156E785B6A555131F1DAF6DA1A`；`SunEyeVision.UI.dll` `483129CDF47E9F7A2D51A6C1AE2DA3C8ACA233F4DEF083146001BE0377E8C747`；`SunEyeVision.Tool.ImageSource.Runtime.dll` `B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`；`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。验收：执行 ImageSource→Blob 形成图形/文本/结果表；点击自动切换开关、运行模式、运行全部和缩略图切换，确认图像更新但上一轮 Blob 图形、文本、结果表不清空；修改 Blob 参数/ROI 不执行，确认仍保持；重新执行确认图形/文本/表整体替换；切换节点/方案或关闭重开确认按身份边界清理/恢复。**2026-11-15 ImageSource/结果状态边界重构首批**：依据 VM `ModuleResultView`/`RenderControlBase` 对照证据，确认自动切换、运行模式、缩略图游标属于图像源配置/显示状态，不能触发结果发布清空；上次执行结果由结果视图独立持有，直到下一次执行或节点/方案身份变化才替换。已在 ImageSource 提交前续租当前完整结果快照，并在策略提交通知窗口跳过 `RefreshFormalPublishedResult`/结果清空；ImageSource 的所有接受入口统一保留结果层（包括自动切换、运行模式、运行勾选和显式缩略图选择），重新执行才替换结果发布。无探针全量 Release 构建成功，0 个错误、366 个警告。Build payload SHA-256 `BF07883658DBF283C2EC740C469FCBC5372BE69B59A8E29E40CD9FA589F5848A`；`SunEyeVision.UI.dll` `EE9F6840111840936782787C8CDB7D742BC499E327DD0C6857B5FE735877E3D5`；`SunEyeVision.Tool.ImageSource.Runtime.dll` `B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`。ImageSource Runtime 定向测试仍为 39/45，6 条 `Missing validation root` 属既有夹具初始化失败。人工验收：执行 ImageSource→Blob 一次；切换自动切换、运行模式、运行勾选和缩略图，确认图像与 Blob 图形/文本/当前结果表保持；修改 Blob 参数或 ROI 后不执行，确认上次结果仍保持；重新执行后确认结果和文本整体替换；切换节点/方案或关闭重开时确认按身份边界清理并重建。**2026-11-15 自动切换提交在途刷新修复正式封签**：探针已删除。正式修复在文档变更观察者收到图像源策略提交时识别 `ImageInputSource.IsAcceptingEdit`，跳过接纳窗口内的 `RefreshFormalPublishedResult` 和结果显示清空，待图像源接纳回调完成后再按策略判断是否刷新。无探针 Release 构建成功，0 个错误、366 个警告。载荷清单 SHA-256 `082CD1A835353DC559DBFF7880D9E511C07DB36991832B312C3C8BF75FFE3B6C`，`SunEyeVision.UI.dll` `FCA7B78EF2C9F4040660346179F495BF66BE9FA69D3DE5EBB3C6CB92794D89AE`。验收：执行图像源/Blob后切换自动切换开关，确认图像、结果图形、文本和结果表不清空；再执行一次确认策略仍生效。
**2026-11-15 自动切换保持显示修复最终构建**：用户关闭程序后重新完成全量 Release 构建，成功，0 个错误、366 个警告。自动切换开关的显示保留凭证修复已进入正式载荷。载荷清单 SHA-256 `55783749C2A1FC43EE8D2A3F49C0609503786446B56B94047D819D65D90C1F39`，`SunEyeVision.UI.dll` `69386A094BE8289CB0107692632C3D0408674487DBAE014E1A9E9C57EE8C581D`。验收使用该 Release：执行并显示结果后切换自动切换开关，确认图像、结果图形、文本和结果表保持显示。
**2026-11-15 自动切换保持显示修复构建封签**：用户确认程序已关闭后，完整 Release 构建成功，0 个错误、60 个本次输出警告。自动切换策略提交的显示保留凭证已允许本次 `EditVersion` 单次递增，避免切换开关把当前图像和结果层清空。载荷清单 SHA-256 `26DFE87CB5733D44C43E73FCCDDAD671C886ADEA8C8AD463FE8EF78E56730C40`，`SunEyeVision.UI.dll` `CEB1C9195F37BBE0515853A110BC81C7E43243A88C39F69CFCF9C4129BB43171`。需人工验收：在已有结果显示状态下切换图像源自动切换开关，确认当前图像、结果图形、文本和结果表不被清空。
**2026-11-15 本轮人工验收通过**：用户确认当前 Release 下结果行与 Blob 图形高亮、图像源缩略图选中、右上角图像来源名称格式、Blob 帮助页以及历史结果中文 Blob 特征记录均符合预期。此前帮助页只读绑定异常和历史记录中的 `Module Status/BlobInfo` 差异已纳入当前实现。当前 Release 载荷以本文件上一条封签为准。
**2026-11-15 Blob 历史结果格式修复正式构建**：用户对照原厂截图确认历史记录差异来自 Blob 记录生产端。已移除 `Module Status` 汇总记录，改用原厂中文逐 Blob 记录格式 `Blob信息{编号}:面积...,周长...,质心X...,质心Y...,角度...`。程序关闭后重新完成全量 Release 构建，成功，0 个错误、1 个本次输出警告。载荷清单 SHA-256 `1EFD32857071E531409E257240BB2A18F74D0A7982BF2C67ADCFC3F35080AB43`，`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`，`SunEyeVision.UI.dll` `E362A7FE498F9399877B1D492DF92BDE42DDFA049C182B3A05B1B75FE72A6F17`。验收需重新执行 Blob 后打开历史结果，确认不再出现 `Module Status`，且记录显示中文 Blob 特征行。
**2026-11-15 帮助页绑定启动异常修复**：人工启动发现 `ResultHelpContent` 是只读计算属性，而 `ResultWorkspaceShell.HelpContent` 默认双向绑定，WPF 尝试回写只读属性并抛出 `无法对 ResultHelpContent 进行 TwoWay 或 OneWayToSource 绑定`。已将帮助绑定明确设为 `OneWay`，不再在启动阶段产生 UI 线程异常。全量 Release 构建成功，0 个错误、366 个警告。载荷清单 SHA-256 `AA1A11445D3FFC1C002B100D093CEF200A8F4615D06F52CBF1C3353505CBC7DC`，`SunEyeVision.UI.dll` `E362A7FE498F9399877B1D492DF92BDE42DDFA049C182B3A05B1B75FE72A6F17`。未新增探针文件；需用该 Release 重新启动并确认主界面无绑定异常、Blob 帮助页正常显示。
**2026-11-15 结果工作区工具对应内容与来源下拉格式修正**：按用户提供的原厂截图核对右上角图像来源下拉。当前额外拼接了工作流/节点序号和 `·`，原厂格式为“节点名.输出名”。已改为 `Blob分析1.显示Blob图像`、`Blob分析1.显示二值化图像`、`图像源1.输出图像` 等格式，未改变来源身份、顺序和选中状态。结果工作区帮助页已接入当前节点帮助内容，Blob 节点显示对应的功能和操作说明，未提供帮助的工具继续显示默认占位。历史结果仍沿当前会话和节点的正式历史读取链展示，不再使用独立探针缓存。刚才只运行了已有结果表单测，没有新增探针文件或探针实现。全量 Release 构建成功，0 个错误、366 个警告。载荷清单 SHA-256 `001683587CD734DC91219F923E6DE8B4C5621CCF55549F8C667FC7212C836F52`，`SunEyeVision.UI.dll` `5A14139F008DBD401EB4B778F5844C31247FFD7139C324D6D9CDE3924B54F881`。需人工验收 Blob 帮助页、历史结果和右上角下拉名称。
**2026-11-15 结果选中高亮可见性优化**：在保持原厂黄色选中语义 `#FFFF00` 的前提下，将被选中的 Blob 结果矩形和质心线宽提高到至少 3 像素，解决绿色正常结果和复杂图像背景下选中态不明显的问题。全量 Release 构建通过，0 个错误、366 个警告。当前载荷清单 SHA-256 `B2C06AFBCBEF449AAE9279DE29A1C1E296E5B8688FD6C533039AA07787A8CA63`，`SunEyeVision.UI.dll` `EB2711423F820F5342F33CEBB0A204037AAF2DF41E638E5D707E7EEAC4BEA313`，`SunEyeVision.Plugin.Infrastructure.dll` `CC925871CC2B2E713BA42DCE12C6D1CF17647513ECD6519B3D2AB33075F8B917`。需用该 Release 人工确认选中行对应图形使用黄色粗线并随行切换。
**2026-11-15 当前结果行与图形高亮真实断点修复**：用户再次复现后，通过现有结果表选择探针确认 `DeclaredResultTable` 能发出正确的 `element-id` 和几何目标，单测 `Highlight_borrows_only_the_selected_row_and_rejects_replaced_publications` 通过。继续追到宿主事件包装发现真实断点在 `DesktopToolPresentation.ResultLease.SelectionHighlightChanged`：包装器把内部 `BlobResultView` 作为 sender 透传，而 `MainWindowViewModel.OnDeclaredSelectionHighlightChanged` 按当前 `ResultLease` 身份校验，导致所有行选中事件被丢弃，`ResultWorkspacePresenter` 从未收到选择。已改为包装器以自身 `ResultLease` 作为 sender，再进入结果工作区高亮链。全量 Release 构建通过，0 个错误、367 个警告。正式载荷清单 SHA-256 `B10C6D5F3F5EB65A86D3AA28AC44D5560E47340137A295783E98C5D993AAE9FB`。`SunEyeVision.UI.dll` `F9C1BA9CBEB8FBE2AE9E858C82B0AE1BF4DB86A45F8B98093D943C6E2D2710E7`，`SunEyeVision.Plugin.Infrastructure.dll` `CC925871CC2B2E713BA42DCE12C6D1CF17647513ECD6519B3D2AB33075F8B917`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未完成实机人工复验，下一步使用该 Release 点击当前结果行确认黄色高亮与行切换联动。
**2026-11-15 当前结果图形高亮第二轮修复与链路探针结论**：用户人工复现确认结果表选中后图形仍保持绿色。沿 `DeclaredResultTable.OnSelectionChanged → MainWindowViewModel.OnDeclaredSelectionHighlightChanged → ResultWorkspacePresenter.Highlight → QueueResultDisplay → RefreshResultDisplayCore` 追踪，已确认第一轮修复仍在显示重绘阶段再次用 `PublicationStamp` 等值判断丢弃已通过当前性校验的 `_sceneSelection`。本轮取消该重复门槛，并保留结果表选择事件后的同步几何覆盖层，避免异步重绘未及时应用时没有即时高亮。全量 Release 构建通过，0 个错误、366 个警告。当前载荷清单 SHA-256 `D109D65625D50351178ACE713EC80C109460E5583560DC13D469BF8D7741B7A6`。关键程序集：`SunEyeVision.UI.dll` `F9C1BA9CBEB8FBE2AE9E858C82B0AE1BF4DB86A45F8B98093D943C6E2D2710E7`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试，需人工点击当前结果表行确认对应 Blob 矩形变为黄色或同步选中覆盖色，并确认切换另一行后高亮跟随。
**2026-11-15 两条动作入口修复正式 Release 构建**：按原厂证据沿完整动作入口复核并完成两处集中修复。其一，工作流先执行、随后才选中节点时，节点选择会消费当前会话仍有效的图像读取回执，使 `ImageInputSource.CurrentIndex` 与图像源缩略图 `SelectedIndex` 同步。其二，当前结果表行的有效 `element-id` 不再被结果显示投影重绑或帧差异错误丢弃，显示层恢复后继续应用对应 Blob 图形选中颜色。全量命令 `dotnet build-server shutdown; dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 已通过，0 个错误、60 个本次输出警告。Build payload `2D65275BE48D77ADDCC9E420CEB2384924B5AAD5DC468A50196D3A78D8EE5906`。正式载荷目录 `Release/SunEyeVision_v1.0.0/SunEyeVision`：`SunEyeVision.UI.dll` SHA-256 `4EAE3E7D58F8EFE4E79AACED1F70CC7737BFB65AE316B00383E4D03757CF9842`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试，以下两条仍需人工验收。
**2026-11-15 两条动作入口链统一追溯**：按用户要求从动作入口逆推而不是只补控件状态。链一为“工作流执行按钮→RunFormalWorkflowsAsync→ImageSourceHostInputFactory 发布完成读取回执→RefreshFormalImageReadReceipts→ImageSourceProjectionBinding.TryPresentCompletedRead→ImageInputSource.CurrentIndex→缩略图 SelectedIndex”，已补节点后选时消费未处理的当前代回执。链二为“当前结果表行 SelectionChanged→DeclaredResultTable.ResultSelection→BlobResultView→MainWindowViewModel.ResultWorkspaceSelectionChanged→ResultWorkspacePresenter.Highlight→结果显示层 selectedId/选中颜色”，已取消显示配置重绑期间对有效 element-id 的错误丢弃。源码链已完成，正式构建因用户重新打开程序被 `SunEyeVision.UI.exe` 文件锁阻断，待关闭后重建。**2026-11-15 当前结果表选中高亮第二轮修正与 Release 构建**：继续沿 VM/本方链路复核后确认，结果表的 `element-id` 已由 `DeclaredResultTable` 生成并通过 `BlobResultView → MainWindowViewModel → ResultWorkspacePresenter` 传递，但 `ResultWorkspacePresenter.Highlight` 又以一次可替换的结果显示配置回读作为是否接受选中身份的门槛。编辑器重绑或结果显示投影短暂不可用时，该回读返回空，选中身份被丢弃，后续显示层自然不使用选中颜色。已改为同一结果发布内只要 `element-id` 有效就保留场景选中状态，显示配置恢复后由渲染层应用 Blob 选中颜色。全量 Release 构建通过，0 错误、366 个本次输出警告。Build payload `CFD20A735B53923320E5CABF9E8B6664F237AE0B68610A2C779CA97B67AE8C93`。载荷：`SunEyeVision.UI.dll` SHA-256 `B538F37057F40DEBAA702C4E0717DED74D00EC4839C723392F0AD80084BA62D1`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试。**2026-11-15 缩略图选中态修复正式 Release 构建**：用户关闭程序后解除正式载荷锁定，重新执行全量 `dotnet build-server shutdown` 与 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false`，构建成功，0 个错误、60 个本次输出警告。Build payload `420761028F3F8B31B6FF948BF426D5EB89DB8FD0A380E40FEDFDAEA31060E3B3`。正式载荷：`SunEyeVision.UI.dll` SHA-256 `92A42C15FE5A06CE5B48594A73059A139A2369AB3F73989BBAF6BC79873DA609`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试。人工验收使用该 Release 程序：打开方案后执行工作流，确认图像源缩略图对应当前输入项显示橙色选中框，再切换另一缩略图确认选中框和显示图像同步，重新执行后仍保持当前项。**2026-11-15 工作流执行后图像源缩略图选中态**：用户提供 VM 原厂截图，原厂图像源列表的当前缩略图有橙色选中边框。逆向源码 `ImageListPanel.xaml` 明确 `ListBox.SelectedIndex` 双向绑定 `CurSelectedIndex`，`lst_SelectionChanged` 写入当前图像并执行。当前实现对照发现 `ResultWorkspacePresenter.ResolveInputChoice` 在单图模式直接返回 `null`，导致输入缩略图有数据但 `ResultWorkspaceShell.SelectedInputItem` 不回写，执行完成刷新后没有选中边框。已改为单图和双图都按当前输入键、主输入键或首个输入源恢复 `SelectedInputItem`，保留双图关联输入行为。当前源码修复已完成，但用户程序仍在占用正式 Release 的 `SunEyeVision.UI.exe`，本次全量 Release 构建被文件锁阻断（MSB3021/MSB3027，8 个错误），未强制结束用户进程。待用户关闭程序后重新执行全量 Release 构建，再验收“执行成功→对应缩略图橙色选中→切换缩略图仍同步”。**2026-11-15 Blob 当前结果行选中高亮修正**：复核本方完整链路确认结果表行选中已通过 `DeclaredResultTable.SelectionHighlightChanged`、主窗口 `ResultWorkspaceSelectionChanged` 到 `ResultWorkspacePresenter.Highlight`，Blob 显示声明也已有 `element-id`、旋转矩形与选中颜色。缺口在结果显示投影层：`selectedId` 只有在选中几何的 `ImageFrame/CoordinateFrame` 与当前视口回执完全相等时才接受行 ID。结果区域来源在帧转换、派生显示或坐标重投影后可能携带源帧，导致行选中事件有效但图形层把 selectedId 清空。已按发布结果身份与行 `element-id` 修正为同一 publication 下直接使用选中 ID，几何 targets 仅负责叠加高亮，不再决定是否接受选中。全量 Release 构建通过，0 错误、366 个本次增量输出警告，Build payload `D4872FA2042F7A15C7DA340305FD4ECDAC99EA0928C38C754B981951EA9FB3A6`。当前正式载荷哈希：`SunEyeVision.UI.dll` `1276E41AC543580051B00E092172B01B6FE71C50341D94C2BE76D45E94334EE8`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。按用户要求未运行自动化测试，待人工确认点击当前结果表任意 Blob 行后对应矩形/质心变为选中颜色。**2026-11-15 项目级证据门槛与 ROI stale 回执保护**：用户明确要求该规则适用于整个软件开发与优化，不再重复提醒。后续所有设计和修复必须先核对原厂证据、当前实现差异、完整生命周期和验收出口，再实施。针对用户提供的 `NativeRoiSession.Capture` 未处理异常堆栈，现有接纳回执在编辑期间遇到图像/范围换代时会把可恢复的 stale 回执当作违反合同并抛出，导致 UI 线程异常。对照当前 ROI 编辑租约和换代边界，已在创建/替换接纳发布前识别范围、物理帧或版本倒退，取消本次候选并保留当前会话，不再向 UI 抛出该类可恢复异常。另依据 VM 原始组合摘录 `VM-Blob组装与输入组合原件摘录-20261110.xml` 中 `SelectWay` 的 `CurValue=0`、`DefaultValue=0` 与三项枚举证据，修正 Blob 位置修正模式投影：可选输入未设置时仍投影默认 0，避免启用位置修正后三个单选项全部失去选中态。全量 Release 构建通过，0 错误、5679 个存量警告，Build payload `BA98B29C939B72512421AFD4A5844370BFC03D162C4E718E8AAE9C8C7C78E67A`。正式载荷 `Release/SunEyeVision_v1.0.0/SunEyeVision`：`SunEyeVision.UI.dll` SHA-256 `5B0FCD23A074DDCA722D1FBDFDB9D96F1A89535458530100DEADA2F05C0587A3`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `240F4975F5682EBDE29FC76E4CEACCB867527BD81FA95D050FAEDB57C5045D1F`，`SunEyeVision.Plugin.Wpf.dll` SHA-256 `74957CC0A7DE21140A695CDF9E464041E14CDA29C977EFB41D27B7E245A41840`，`SunEyeVision.UI.exe` SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。按用户要求未运行自动化测试，仍需使用该正式载荷人工核对三项单选可切换、stale ROI 操作不再弹未处理异常。**2026-11-14 执行完成后 ROI 编辑层补链**：用户人工验收确认运行后只剩 Blob/检测区域结果图形，ROI 没有橙色控制点且无法命中。完整调用链复核发现，ROI 帧通知只依赖结果工作区计划是否换代，执行轮次保持同一输入图时该路径会被 `DisplayUnchanged` 跳过。已在调试执行完成事件接入 VM 对应的执行后 ROI 更新，在视口 `DispatcherPriority.Loaded` 阶段单独触发 `NotifyRoiFrameAvailabilityChanged`，让 `BlobLocalRoiOwner` 重新借帧并安装编辑 attachment，同时保留结果显示层。用户当前程序占用 Release DLL 时构建被阻断，程序关闭后已执行 `dotnet build-server shutdown` 并完成全量 Release 构建，0 错误，Build payload `82240F024C8FE68FFEADC4A62821D0EB60A9A0102A4195702A0B3A1696BEE584`。未运行自动化测试，待用新载荷复验结果层与 ROI 交互层同时存在。

**2026-11-14 Release 载荷核验**：当前正式载荷中的 `SunEyeVision.Tool.BlobAnalysis.Wpf.dll` 生成时间 `13:18:09`、SHA-256 `3678F84A6964ED0DE672EFAD7630D565667B02CCEC9BE4287A3AF0487AF215FC`；`SunEyeVision.UI.dll` 生成时间 `13:18:18`、SHA-256 `126EA8778739A2E184C69FDCB5465B58F21D8A0E744C5DA9DD6AE6333DB9B70E`。载荷文件与本轮 ROI 竞态修复编译时间一致，未运行测试。

**2026-11-14 ROI 异常日志复核**：修复后的正式日志没有新增 `ROI编辑会话与实际视口的物理帧、坐标或尺寸不一致` 错误；现存两组 ERROR 均来自 00:44:02 的历史运行记录，位于本次重试修复之前。该结果只能说明当前日志未再出现同类异常，不能替代用户对形状命中和运行重投影的人工确认。

**2026-11-14 ROI 命中竞态实锤修正**：从正式运行日志 `suneye.log2026-11-14.log` 取得真实异常：用户命中形状按钮时，`NativeRoiFrameLease` 借帧后视口在 `BeginRoiEditing` 前完成换代，导致物理帧/坐标校验抛出异常并中断交互。沿 `BlobLocalRoiOwner.Activate → NativeRoiFrameLease.BeginEditing → NativeResultViewport.BeginRoiEditing` 对照 VM 换代重投影语义，已在 `ActivateCore` 仅针对帧或会话已失效的异常退役旧帧，并在 `Activate` 重新借当前视口帧后限次重试；真正的参数错误仍继续抛出。全量 Release 构建通过，0 错误，Build payload `2B2209C30B6A499E0B57924246D0920888D6ED9F56387EA510D6D906723218C4`，正式程序生成时间 `2026-11-14 13:18:18`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试，待人工复验命中形状、运行后重投影和关闭调试结果。

**2026-11-14 Release 启动链运行证据**：一次正式载荷启动记录已写入 `C:\Users\Administrator\AppData\Local\SunEyeVision\Logs\suneye.log2026-11-14.log`。日志显示运行身份来自 `E:\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`，完成插件目录、服务、方案管理器和工作区装配；`MainWindow.Activated` 记录可见最大化窗口，ImageSource 节点绑定本地图像 12 张，随后 Blob 参数视图完成基本参数页装配。CUA 当前无法列出该 WPF 窗口，不能据此替代用户人工交互验收；启动链本身已有运行证据，ROI/结果/保存链仍未验收。

**2026-11-14 正式 Release 启动检查观察**：从 `E:\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe` 启动一次后，进程在约 8 秒内保持响应但 `MainWindowHandle=0`、没有可见主窗口，当前 CUA 状态也未列出该窗口；随后已结束该进程，未进入方案或执行交互。该现象暂记为启动环境/起始页可见性待核对，不把它归因于 ROI 链，也不把启动检查当成人工功能通过。

**2026-11-14 完成审计证据矩阵**：现行 HTML 清单新增“当前证据等级”，将 ImageSource→Blob 输入、运行后 ROI 可编辑、检测区域结果层、调试关闭结果延续、保存重开/取消关闭/资源释放分别标为源码闭合或待人工，并写明每项所需的端到端证据。这样不会把构建、静态调用图或局部截图误记为完整生态链通过。

**2026-11-14 保留快照身份门槛修正后构建**：修正调试快照与正式结果共用同一 `DocumentSession.ResultOwnerId` 导致的错误拒绝条件。全量 `SunEyeVision.slnx` Release 构建通过，0 个错误，Build payload `13535DB759D3791185F88AEDFC2138A11B1F3BFD03D9FF6425126392488D129C`；正式程序生成时间 `2026-11-14 13:10:50`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。按用户要求未运行自动化测试，关闭调试窗口后的结果接续仍待人工验收。

**2026-11-14 调试关闭结果断点修正**：继续审计保留快照的身份合同后发现，调试执行与正式执行都由同一 `DocumentSession` owner 生成，`DeclaredOutputSnapshot.Identity.DocumentSessionId` 必然等于 `DocumentSession.ResultOwnerId`。原 `IsRetainedEditorDebugSnapshot` 额外要求两者不相等，导致调试窗口关闭后已保留快照永远被 `PresentSnapshot`、`CaptureCurrentResultDisplay` 和当前性判断拒绝。已删除该错误门槛，保留快照改按对象身份、会话、节点和流程作用域校验；快照来源只由调试完成事件写入，正式结果替换/节点切换/方案关闭仍会清理租约。待 Release 构建后人工核对关闭调试窗口结果延续。

**2026-11-14 全入口静态消费者审计**：逐项复查 `ResultWorkspacePresenter`、`BlobResultGeometry`、`BlobResultProjection`、`BlobDisplayUi`、`BlobLocalRoiOwner`、`NativeRoiAttachment` 与主窗口调试/正式结果发布路径。确认检测区域、扇环、屏蔽区均由同一 `ResultGeometrySnapshot` 进入结果场景层，ROI 编辑层经独立 `NativeRoiFrameLease` 进入 `NativeRoiSession`；结果下拉、双图切换、参数窗口页签、运行刷新和调试关闭均已有消费者通知。没有发现可以凭静态证据安全增加的第二套渲染或自动切源逻辑，因此本轮未添加猜测性补丁。缺口继续保持为正式 Release 的端到端人工证据，而不是用局部源码命中替代。

**2026-11-14 全入口闭链批次最新构建**：沿“调试执行完成 → 结果快照保留租约 → 图像结果/Blob 结果层 → 可编辑 ROI 借帧 → 调试窗口关闭 → 正式结果替换/节点切换/方案关闭释放”完整链路复查后，全量 `SunEyeVision.slnx` Release 构建通过，0 个错误。最新 Build payload `5E79580B07EFF9D729DA314715BC2BE2297E8A11B3EBF80E0769CF09F72763B4`，正式程序生成时间 `2026-11-14 13:00:53`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。本批没有运行自动化测试，构建只证明源码和正式载荷一致；ROI 可编辑、检测区域结果显示、调试窗口关闭后的结果保留、保存重开和资源释放仍由用户统一人工验收。`anti-flicker-wip.patch` 仅保留为历史参考，不属于正式载荷。

**2026-11-14 最终人工验收入口固定**：本批正式程序为 `E:\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`，生成时间 `2026-11-14 12:12:18`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。后续人工验收必须使用该 Release 目录程序，不使用旧 C 盘目录或历史载荷。

**2026-11-14 ROI 运行后形状丢失根因修复**：定位到参数面板运行刷新期间，ROI 控件经历 `Unloaded/Visible=false` 后 `RetireFrame()` 清空交互会话和编辑器绑定；恢复可见时原有预览队列不会在本地图像 ROI 模式重新建立交互会话，最终留下 `Bind(null)`，导致运行后形状不可见且再次绘制无会话。依据 VM 的 `SetRoiVisible`/`UpdateROI` 恢复语义，在 `BlobLocalRoiOwner.Preview.cs` 增加可取消的 `DispatcherPriority.Loaded` 阶段 `RefreshFrame` 重建，加载/恢复可见时补建，卸载/隐藏/Dispose 时取消，未新增显示刷新到 ROI 的跨路径通知，避免已实锤的反馈死循环。UI 项目 Release 构建通过，0 个错误、5655 个存量警告；本轮按用户要求不运行自动化测试。待用户在正式 Release 程序中人工验收“绘制 ROI→运行→形状仍在→再次绘制”。

**2026-11-14 运行后 ROI 可见但不可命中根因补证**：用户进一步确认形状仍显示但不能再次命中编辑。对照本方 `NativeResultViewport` 与原厂 `ImageViewBox.ChangeHitTestVisible`（`ROIVisible && !IsStateForDrawROI`）确认：结果视口初始化把 `ROIHitTestVisible` 固定为 `false`，新建 `NativeRoiAttachment` 时只装载 ROI 列表，没有恢复可编辑命中门，故结果层可见而 ROI 层不可命中。已在可编辑 attachment 初始化时按当前可见/绘制状态恢复 `ROIHitTestVisible`，退出 attachment 时恢复只读状态；不改变右上角结果显示下拉的结果图形层。UI 构建首次因正在运行的 SunEyeVision 进程占用 `Release\...\SunEyeVision.Plugin.Wpf.dll` 失败，等待用户关闭程序后重建部署。用户新截图同时作为结果显示层与 ROI 编辑层分离的原厂证据，原厂下拉条目是 Blob/二值化/掩膜等结果源，不能作为 ROI 编辑命中依据。

**2026-11-14 结果显示标题重复根因补证**：用户截图中结果显示设置出现两行“Blob”。`BlobResultDisplay.Build` 对质心组使用 `DialogGroup=display.centroids`，但 `DialogTitle` 映射未包含 `Centroids`，命中默认标题 `Blob`；已补为“质心”。这只修正显示标题，不改变结果数据或绘制链。用户同时报告“检测区域”设置存在但图像区只见扇环，当前继续区分 Blob 结果矩形、检测区域 ROI 与可编辑 ROI 三层，待当前结果快照确认缺失层后再改。

**2026-11-14 本轮修复构建封签**：用户关闭 SunEyeVision 后，先执行 `dotnet build-server shutdown` 释放残留编译服务，再完成 UI Release 构建和全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false`，均 0 个错误。全量 Build payload `E94CD2D119C085390568DD5051D29D9D65B73CAF540D082F84683ACFA0297641`，正式程序生成时间 `2026-11-14 12:31:55`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。本轮未运行测试，等待用户使用该 Release 程序人工验收三条链路：运行后 ROI 可命中编辑、结果检测区域显示、关闭调试窗口后 ROI/结果不丢。

**2026-11-14 图像结果入口触发 ROI 编辑层**：用户补充原厂交互语义：形状按钮先选中，点击“图像结果”入口后才显示当前输入图上的可编辑 ROI。现已在 `ResultWorkspaceShell.SelectedResultTabIndex` 增加 `ResultTabSelectionChanged`，由 `ResultWorkspacePresenter` 转发 `NotifyRoiFrameAvailabilityChanged`，让 Blob ROI owner 在图像结果入口切换后重新借用当前输入帧并安装编辑层。该变更只触发 ROI 生命周期恢复，不改变结果图形层或图像来源选择。

**2026-11-14 交付状态纠正**：前述“完整生态链清单已落地”和 Release 构建只能证明文档覆盖与代码可编译，不能证明 VM 每个操作入口、交互状态、结果投影、关闭恢复和资源生命周期已经闭环。用户实机仍报告形状按钮选中后必须点击图像入口才出现可编辑图形，说明 ROI 生命周期合同尚未完成；检测区域结果显示和调试关闭后的保持也尚未取得端到端实证。本任务恢复为未闭口状态，后续按 VM 完整操作链逐项取证、实施和统一验收，不再用清单、局部构建或单点截图宣称完成。

**2026-11-14 全入口全链路追溯规则**：用户明确要求所有实现入口都必须沿完整生态链追溯分析和优化。入口只作为调用图起点，不能作为交付边界。每个入口必须继续追踪创建者、状态源、全部消费者、图像/数据身份、同步与异步通知、渲染层、执行快照、持久化、取消关闭、资源所有权和失效路径，并以同一 VM 操作的端到端证据完成验收。任何只修改局部入口、只通过构建或只通过局部测试的结果均不得标记为功能完成。

**2026-11-14 调试结果关闭后丢失的跨链断点修复**：追踪 `RuntimeToolEditorWindow.ToolExecutionCompleted` 的全部消费者发现，调试执行完成事件没有接入主窗口结果发布链。结果仅存于 `ProductionEditorDebug.CurrentSnapshot`，关闭时 `RetireAsync` 释放快照，`RefreshFormalPublishedResult` 只能恢复旧的正式发布，因此调试运行产生的 ROI/检测区域结果会消失。已接入主窗口保留链：执行完成 → `DeclaredOutputSnapshot.Retain` → 主工作区结果发布/图像来源捕获 → 调试关闭后继续使用保留快照；节点/会话不匹配时由当前性校验拒绝。该修复尚未完成人工运行验证，需与 ROI 编辑层、结果几何和关闭释放一起验收。

**2026-11-14 调试结果保留租约释放补齐**：对上一条保留链做反向生命周期审计，补齐方案会话切换和主窗口退出路径的 `ClearRetainedEditorDebugSnapshot`，避免保留快照租约跨文档或跨主窗口泄漏。该项已通过 UI Release 构建，仍需与关闭窗口后的结果恢复一起人工验收。

**2026-11-14 当前 UI Release 产物**：本轮快照保留与释放修复构建通过，0 个错误。正式程序生成时间 `2026-11-14 12:50:18`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。未运行自动化测试，当前仅具备构建证据，未具备 VM 端到端人工验收证据。

**2026-11-14 保留快照随节点身份切换释放**：继续沿节点选择和删除路径追踪，补齐 `SetSelectedNodeIdentity` 中的保留快照清理，避免调试结果租约在节点切换后继续占用资源。UI Release 构建 0 个错误，未运行测试，仍待人工验证关闭恢复和跨节点释放。

**2026-11-14 全量 Release 封签**：主窗口结果保留链和节点切换释放补齐后，全量 `SunEyeVision.slnx` Release 构建通过，0 个错误。Build payload `B236DF0471AFDB06F6DF8D3CBFC2B10E45EB334765CDEAE048CAA77A65616167`。全量构建只证明代码和载荷一致，不替代 ROI、结果显示、调试关闭、保存重开和资源释放的人工 VM 验收。

**2026-11-14 正式结果替换规则补齐**：保留调试快照只服务于关闭调试后的结果延续；当同一节点收到新的正式运行结果时，`FormalResultAvailable` 先释放保留调试快照，再刷新正式发布，避免旧调试结果遮住新执行轮次。UI Release 构建 0 个错误，Build payload `CB95A0A76FB3EE78EB0CA73B07B5DC59C92727AB7AD1FA952525643752451977`。

**2026-11-14 持久化静态链核对**：`DocumentSession.CaptureForSave/Save/SaveAsCore` 先执行 `AcceptEditorsForSave`，再由 `SolutionDocumentWriter` 捕获节点配置字段、显示字段、选择来源和 `DisplayOverlays`；Blob ROI 作为 `input.roi.local` 的 `RecordValue` 配置字段由同一字段快照写入。打开时 `DocumentSession.FromPersistenceCandidate` 按节点配置恢复，并重新执行声明指纹与绑定校验。该链具备源码证据，保存重开后的真实 ROI 重投影和运行结果仍未人工验收。

**2026-11-14 VM 原生模块导出边界补证**：REA 对本机 `ImageSourceModule.dll` 和 `IMVSBlobFindModu.dll` 完成 PE export inventory。ImageSource 原生入口明确包含目录监听 `Add_WatchSubFolder/Del_WatchSubFolder`、文件夹初始化/变化处理、文件枚举排序、`SetImageOutput`、`SetImageOutputMono8`、`StitchImageOutput`、`Process`；Blob 原生入口明确包含 `Process`、`ProcessAlg`、`ProcessOutput`、`LimitJudge`、`ResetMember`、`SetParam`。这补齐了托管包装层以下的原生边界证据，但导出表不能证明参数布局和运行时顺序，未据此猜测代码。证据已追加到 `docs/collaboration/evidence/VM原厂本地图像与ROI刷新逆向取证-20261114.json`。

**2026-11-14 全量 Release 构建封签**：首次构建被残留 MSBuild/VBCSCompiler 和输出文件占用导致的 UnauthorizedAccessException 阻断，执行 `dotnet build-server shutdown` 后按同一命令重建成功。`dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 通过，0 个错误、5750 个存量警告；Blob、ImageSource、UI 与验证载荷均生成，Build payload `533BCB1AF516227ED5548C92B45E63AA96486879BCC605517C7C6CAFC1346D26`。本轮按用户要求不运行测试。

**2026-11-14 VM ImageSource/Blob 结果合同第三轮取证**：继续检查本机原厂 `ImageSourceModuleCs.dll`、`ImageSourceModuleAlgorithm.xml`、`ImageSourceModuleAlgorithmTab.xml`、`ImageSourceModuleDisplay.xml` 和 `IMVSBlobFindModuDisplay.xml`。证据确认 ImageSource 原厂入口包括 `Run`、`SetImagePath`、`SetImageData`、`AddInputImageByPath`、`DeleteInputImageByPath`、`ClearAllInputImage`，结果通过 `ModuResult` 的查询与结果管理器填充，正式输出包含图像、灰度图、当前路径/名称、帧号和 SN。Blob 原厂显示 XML 明确 Blob 矩形、内接矩形、ROI、轮廓点、基准/运行点、文本、结果列表、二值图、Blob 图、掩膜和扇环的映射。已把这些原厂文件摘要追加到 `VM原厂本地图像与ROI刷新逆向取证-20261114.json`。当前没有依据证明需要改 ImageSource/Blob 执行入口，暂不做猜测性改动。

**2026-11-14 第二批 VM 原厂入口取证补齐**：按用户要求继续自行逆向，不只复用已有截图和证据。REA 已检查本机 `ImageSourceModuleControl.dll`（SHA-256 `04a73b135d18641b65b2815a292b795b922cb675122d6555f61947f372958906`、MVID `4298c0d6-ba00-4d57-8925-21ea9618117f`）的 `ImageSourceModuleControl.LocalImageListView`，确认本地图像路径和运行模式从同一个 `AlgorithmTabRoot` 正式参数节点读取，模式下拉写回 `UserStringData[ExecuteType]` 并通知列表面板；同时检查 `IMVSBlobFindModuCs.dll`（SHA-256 `3106af2e9344a97288553bfbbfa86afd81e2384188b452734f56825f63e0a78d`、MVID `1d550689-0379-4822-8b69-8713d2324342`），确认 Blob 输入、ROI 管理、质心/矩形/轮廓/图像结果和结果管理器填充入口。原生算法 DLL 无 CLI 元数据，已明确记为 REA 静态覆盖边界，没有用猜测替代。证据已追加到 `docs/collaboration/evidence/VM原厂本地图像与ROI刷新逆向取证-20261114.json`。当前只继续按这些入口对照实现，不先做无证据设计。

# SunEyeVision_V2 当前任务账本

**2026-11-14 当前阶段范围再次确认**：用户明确要求先闭口本地图像文件/本地文件夹 → BlobAnalysis → 结果显示/ROI/保存重开/取消关闭的完整生态链，相机与 SDK 接入后置。现行清单已增加“按实现入口反向追踪”及“本阶段本地图像闭口入口”两节。当前先沿 `SubscribedImageFolder`、`LocalImageSequenceService`、`ImageSourceHostInputFactory`、`BlobSession`、`ResultWorkspacePresenter`、`BlobLocalRoiOwner`、`DocumentSession.Save/Open/Dispose` 形成证据和回归矩阵，不把相机缺口计入本阶段阻断。用户已明确后续不再逐条跑测试用例，最终由用户统一人工验收，实施以 VM 逆向证据为准直接落代码。

**2026-11-14 VM 原厂第二轮逆向取证并落地运行后 ROI 换代通知**：按用户要求不再只复用既有截图和文档，使用 REA 对本机 `VMControls.WPF.Release.dll`（SHA-256 `1dd76c5157a7d6ec9229a5f59275337312625cbd481cd483ba2b2891ead91332`、MVID `60eb9c1d-902a-4e2f-8266-75e5c188c129`）完成成员/CIL 取证，并用同版本 `VMControls.WPF.dll`、`Apps.XmlParser.dll` 的本机反编译交叉核对。证据文件：`docs/collaboration/evidence/VM原厂本地图像与ROI刷新逆向取证-20261114.json`。原厂确认：运行回调按图像对象变化决定是否重设图像，结果图形独立重建，回调末尾调用 `ModuleSource.UpdateROI()`→`AlgorithmTabRoot.UpdateROI()` 遍历 ROI 选择器重投影；有效显示切换只在空选中时清空，ROI 资格按当前显示图是否属于模块输入图判断。依据该证据，`ResultWorkspacePresenter.Refresh` 现在只在视口真正换代完成后单次调用 `NotifyRoiFrameAvailabilityChanged`，让 `BlobLocalRoiOwner` 重新借当前帧，避免在清空/重建中途借半成品帧。未改变自动切图语义，未进入相机/SDK。用户最终统一手动验收，后续不再逐条跑测试用例。

**2026-11-14 本批源码构建**：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 通过，0 个错误，正式 Release 载荷已重新封签。仅记录构建结果，不运行自动化测试，等待用户统一手动验收。

**2026-11-14 Blob 质心结果图形链补齐**：依据 VM 结果分发把点形状交给同一结果视图的原厂证据，补齐 `ResultDisplayDescriptor.Centroid`、Blob `centroid` 输出路径和“质心”显示设置，`ResultWorkspacePresenter` 按每个 Blob 的判定颜色绘制点图形。结果矩形、轮廓、质心现在共用同一帧回执和结果图层，未改变算法值或相机链路。本批 UI Release 再构建通过，0 个错误，未运行自动化测试，待用户手动核对结果图中的 Blob 框、轮廓、质心和文本。

**2026-11-14 ImageSource → Blob 完整生态链对照清单已落地**：按用户最终范围重新整理本地 HTML 清单 `docs/09-现行文档/ImageSource-Blob完整生态链对照清单-20261114.html`。清单以完整生产链为主线，覆盖方案/流程/节点操作、ImageSource 本地/文件夹/相机/SDK/拼接/灰度/缓存、Blob 输入/ROI/掩膜/阈值/判定/执行、图像显示五类下拉、图像交互、ROI 子链、保存重开、取消关闭、节点生命周期、后台资源释放和验收矩阵。当前只完成清单文档，没有修改代码，运行后 ROI 图形清空和完整人工验收仍保持未闭口。

**2026-11-14 阻断态形状选中态修复通过 + 运行清空图形新实锤，移交 Codex（交接单 HANDOFF_ROI选中态批次与运行清空图形-20261114.md）**：用户确认两段式通过后报"阻断态点形状按钮样式不变（原厂有选中样式）"。根因：借帧失败→`Bind(null)` 每次重建 `UnboundContext` 且形状 setter 全空。修复：`NativeRoiEditor` owner 增加 `UnboundShape` 单一事实源，`UnboundContext` 实现 INPC 四形状属性 radio 互斥，跨重建保持选中样式；`Bind(session)` 时清除由 session `ActiveDraw` 接管。构建 0 错误已部署，**用户人工验证通过**（跳过自动测试，用户指示）。用户随即新报实锤：**点击运行后可交互图形被清空**（截图：Blob 调试窗口显示图像源1、输入图上圆 ROI，运行后图形消失）——未定位未修，列为 Codex 主任务，初步线索（运行→epoch+1→租约死亡→退役/重建借帧链）已写入交接单。工作区未提交改动仅 `src/Plugin.Wpf/Presenters/Regions/NativeRoiEditor.cs`，接手方先按用途提交固定基线。遗留：下拉"显示X"命名链、16 条存量失败甄别、变量 ROI 显示位（P4）。

**2026-11-14 阻断提示两段式（原厂交互实锤：按钮只选中，落笔才弹框）（1971f83a4，待用户复验）**：用户对照 VM 实机指出设计差异——原厂点形状按钮只选中形状，"无效的绘制操作！"提示在用户**图像窗口落笔**时才弹；本方点按钮立即借帧+弹框。已拆两段：①按钮点击→PrepareInteraction 失败且阻断来自显示选择→置 `DeferInteractionFeedback`，命令路径跳过弹框；②显示视口 `PreviewMouseLeftButtonDown`（仅 `RoiDrawBlockReason` 非空时）经新通道 `NotifyRoiInputViewportClicked`（presenter→主窗口→调试控件→`NativeRoiFrameSource.InputViewportClicked`）送 owner，落笔重试激活：显示已切回则武装，仍阻断才弹原厂提示（`ShowInteractionFeedback` 开放 public——Plugin.Wpf 无对工具/UI 的 InternalsVisibleTo）。正常路径（显示=输入图）不变：按钮点击即武装，拖拽绘制。52 条与基线一致零回归，全量 Release 0 错误已部署。

**2026-11-14 原厂语义终定案：阻断+提示（用户VM实机截图）+ 显示来源按模块序号对齐（8c9fbacbb，待用户复验）**：用户出示 VM 实机截图——显示图不是输入图时点形状按钮，VM 弹"**无效的绘制操作！请检查绘制图像是否为模块的输入图像**"，由用户自行切换显示。一击命中自动切换（5242612b7）据此撤销，改回阻断+原厂文案；EnsureRoiDisplayInput 删除（无消费者不留死代码）。**P1 语义经三轮翻转终定案：阻断+提示**，此前"执行后没法命中"的真凶是崩溃异常（已修 f5a9bf36b）与切回后会话不恢复（已修自动重建），不是缺自动切换。保留底座：回执全等锚定、自动重建、非交互不落离屏视口。另按用户下拉对照截图对齐：显示来源节点按**模块序号**（GlobalIndex）排列（不按名称——拉丁名插中文前）、条目带模块号前缀（"1 图像源1 · 图像"）、掩膜等辅助图**回组内**（撤销挪队尾）。遗留登记：本方下拉存在"显示X"旧本地化命名链（zh-cn.xaml Display Blob Image 键），待定位具体下拉面后对齐；16条存量失败甄别；变量ROI显示位（P4）。52条回归与基线一致零回归，全量 Release 0 错误已部署。

**2026-11-14 绘制一键命中成片实施 + 反馈死循环实锤回滚（5242612b7，待用户复验）**：用户连续反馈"执行后没法重新命中/必须点两次形状按钮""不要逐点手动测试反馈，按 VM UI 交互完整链路逆向优化""测试用例是摆设"。实施转向定案：①恢复 P1 一击命中（4972ba4a2 的"阻断+提示"按用户最新指令废弃）——`BorrowRoiFrame` 交互请求在显示≠输入图时**本调用栈内**完成"切显示（单图临时态/双图输入选择）→同步 Refresh→PushFrame 有界等待呈现（800ms 上限）→同栈借帧"，request/epoch 刷新后重捕获；②**实锤回滚**：曾试"呈现完成通知"形成"显示刷新→ROI重建→编辑事件→再刷新"的 Render 队列反馈死循环，UI 线程冻结（用户实机窗口冻死+测试挂死），已回滚——**显示刷新→ROI 的跨路径通知列为禁区**，呈现等待只能调用栈内有界进行；③非交互借帧对有显示位的输入图不再落离屏视口（离屏会话用户无处可画），变量图像等无显示位来源保留私有呈现（P4 订阅链显示位缺口另批）；④PendingActivate 兜底保留（内联超时后由自动重建补齐）。测试按用户批评重造为行为链断言：`Roi_draw_request_on_non_input_display_switches_display_and_arms_in_one_click` 断言单击命中+显示切换+正式选择不变+武装在可见视口（反射 OutputPane/Viewport/_roiAttachment）+跨刷新存活，通过；夹具加 `Prop<T>` 属性反射助手。EditorDebugSession 52 条 16F/36P 与基线一致零回归；全量 Release 0 错误已部署。待用户复验：①执行后直接点形状按钮单击命中；②切图/切页/关窗不丢形状维持；③Blob 结果图形显示维持。遗留：16 条存量失败甄别、变量 ROI 显示位（P4）。

**2026-11-14 执行后点击绘制报"ROI编辑会话与实际显示标的不一致"根因修复（f5a9bf36b，待用户复验）**：用户实机截图+堆栈实锤：执行后点击绘制按钮抛 `InvalidOperationException`（NativeResultViewport.BeginRoiEditing:35）。根因：`NativeResultFrameReceipt` 是 record，相等含 OwnerId/Epoch/Frame/Coordinates，视口每次重绑定（执行/刷新 BindImage）epoch+1；上一批 P2 把发布路径租约货币放宽为**只比 Frame 身份**（RoiEditing.cs:102），而 BeginRoiEditing 校验**完整回执** `Current(receipt)`（NativeResultViewport.cs:106）——执行后"活租约撞失效回执"，Activate 的 FrameCurrent 为真跳过重新借帧，BeginEditing 必炸。修复：发布路径租约改 `pane.CurrentFrame==accepted.Receipt` 全等锚定（与变量路径 :88 一致），重呈现即失效，由 RefreshFrame 自动重建会话重投影（P2+自动重建组合=原件 UpdateROI 语义）。归据：修复后 EditorDebugSession 52 条仍 16F/36P 与基线一致，零回归；全量 Release 0 错误已部署。附带语义确认：显示图不是 ROI 输入图时点击绘制按 P1 实现语义给出阻断原因文案（"请先将单图显示切换到该模块的输入图像后再绘制区域"，原件 IsCanDrawROI+RoiDrawTips），不是静默失败。

**2026-11-14 跨显示图像丢结果图形根因修复 + ROI 会话自动重建（4364645ea，待用户复验）**：用户实机报"切换可编辑图像形状消失、感觉整个链路生态有巨大问题"。归因实锤两层：①`ResultWorkspacePresenter.Display.cs` 的 sameDomain 门（`frozen.Coordinates == receipt.Coordinates`）用**声明域常量**（BlobSchema.ImageFrame="image:input"，合同标签）与显示帧**实际来源 id**（如"image:<sourceId>"/"camera:*"）做 record 相等比较，恒假→运行后模块几何整组被拦（截图佐证：文字渲染通、状态栏报坐标域不一致警告）；②`BlobLocalRoiOwner.RefreshFrame` 帧死亡分支只退役不重建，切换可编辑图像后会话退役后无人重建，需再点绘制按钮（即用户"切图丢形状"）。原件语义早已在案（ModuleResultView.xaml:142 ShapeList 始终绑定模块结果、imageListCombobox_SelectionChanged :2152-2153 换图从不重建/隐藏）——**对照结论正确但实施时被我加的 sameDomain 门违背，属实施失职已拆除**。修复：模块 ROI/矩形/内接矩形/轮廓一律按当前显示帧物理绑定+重声明到显示坐标域（`receipt.Frame`/`receipt.Coordinates`），`NativeRoiResultProjection` 帧校验改物理帧+像素域、点形状重声明；RefreshFrame 退役后按可见性自动 `AcquireFrame(interactive:false)`。附带：`ThemeMessageDialog` 加 `TestAutoRespond` 钩子（与 NativeMessageBox 同型），测试夹具挂无监督应答（保存方案→否），**无人值守测试不再被"保存方案"模态阻塞（用户不用再点）**。归因证据：EditorDebugSession 全量 52 条在基线 f49923752 与修复后均为 16 失败/36 通过——零回归，16 条为账本已记录的存量失败（:19 甄别条目），另行处置。全量 Release 0 错误，产物已部署。待用户复验：①运行后 Blob 图形（轮廓/外接框/十字标/标注）显示；②切换可编辑图像形状保持；③切页/关窗不丢形状维持。

**2026-11-13 A4/#11 按原件对齐提交 + Blob 结果图形投影实锤未实现（下批主项）**：用户实机走查确认 P1 阻断提示/切显示绘制/双阈值极性行显隐三条通过（1-3 OK），并报两个新缺陷已修（e856973ea）：①切参数页签丢形状——RefreshFrame 仅帧身份死亡才退役会话，恢复可见自动重建会话显示（原件 SetRoiVisible :2252 只隐藏不销毁+UpdateROI :604-608）；②关窗丢形状——关闭流程 CloseDebugAsync 前尽力 TryCommitEditor（原件形状=参数对象写入即持久，无草稿丢弃边界）。A4 双图入口对齐原件 RefreshInputImageList（:1453-1463 关联IO匹配项→否则[0]）+#11 模式切换显隐（原件 0x3 双阈值 Trigger IMVSBlobFindModuAlgorithmTab.xml:160-173 隐藏 Polarity 行——原厂 Blob 参数 XML 实机在案，后续页内对照直接以它为准）→ 3b04ca63d。**新实锤缺口：Blob 结果图形投影整体未实现**——用户截图对照（原厂结果图上每个 Blob 有绿色轮廓/青色外接框/十字中心标/面积质心标注，本方只有 ROI 框+"0"标签）：`presentation.graphics.blobs/centroids/contours` 显示设置字段已声明（结果显示页可见）但全仓无任何渲染消费方，且调试/正式执行未请求 Boundary 输出（BlobOutputRequest.None → BlobResultGeometry 不生成轮廓）。下批主项：实现 Blob 结果图形投影——①调试/正式执行请求 boundary 输出；②按 presentation.graphics 设置渲染每个 Blob 的轮廓/外接框/十字标/测量标注（原厂锚点：ModuleResultView UpdateShapesWithCallBackData/DrawCurModuleUIResult 图形分发）；③算法数值差异（原厂3个/本方2个 blob，两边 ROI 形状不同不可直接判定）需同 ROI 同阈值对照后再判。另：A4/#11 已提交（3b04ca63d），清单已同步（0210d2048、98bd75b5f Blob 专用入口 17 项全覆盖核对：已对照 8/未对照 9）。

**2026-11-13 数值红框根因修复并闭口（用户实机确认消失）+ P1 显示切换落库**：按用户"从 UI 反推"方法，先确认红框控件链（查找个数/字号行 = Interaction.ListView.NumericControl + PcUi/RangeRule.cs:33，与 AppsXmlUi 版是两个类——首轮埋点文件选错），再以真实程序采集（redframe.log）实锤：红框出现时 ValueText 文本为 '0'（PcUi NumericControl Text DP 默认值，:36），0<Min=1 越界挂红；真实值经 WriteText→SetCurrentValue 落地**绕过绑定校验**，红框残留（F9 扫描实锤：ParamListView 上孤儿 TemplatedAdorner，祖先链=Control<-TemplatedAdorner<-AdornerLayer）。修复三件：WriteText 成功落地后强制 RevalidateRange；RevalidateRange 合法分支强制清扫孤儿装饰器；RangeRule 加 ValidatesOnTargetUpdated。用户实机复验红框消失。附带确认：主窗口 BoundingRectangle 红框为 Collapsed 调试元素（死代码 ToggleBoundingRectangleOnCanvas 无调用点，误报排除）。P1 同批落库：交互借帧标记（NativeRoiFrameSource borrow 加 interactive 参数）+ EnsureRoiDisplayInput 内联切换后立即重试借帧（单击完成切换+借帧+激活），非交互借帧（预览）不改显示；修复了由此产生的 ROI 保存重开回归。回归 13/13（P0 三条、Blob 三页、变量帧、保存重开、显示切换、命令门控）；全量 Release 0 错误。提交 c24b11dc2（红框）、783d67a64（P1）。夹具局限记录：FontSize/Roi_shape 两个夹具用例因页面物化与调度时序无法复现真实状态已删除，此类场景验收以真实程序人工核验为准。P1 剩余人工验收：ROI 绘制全链（用户实机）；P2 租约全路径帧锚定、P3/P4 待下批。

**2026-11-13 ROI 生态链完整逆向对照完成，优化方案定稿（CodeBuddy 接管，按阶段实施）**：按用户要求先完整逆向原厂 ROI 生态链再设计。方案归档 `docs/09-现行文档/ROI完整生态链逆向对照与优化方案-20261113.html`：原厂链 6 条（单绘制面=结果视图 ImageViewBox+ROISelecterHolder 形状共享，ROISelecterHolder.cs.pending:15-193；绘制前置=显示图必须是输入图，IsCanDrawROI ModuleDisplayDecorator.cs.pending:446-480；关联输入变化自动切显示 OnRelatedIOChanged :500-524；ROI 跨刷新 UpdateROI 重投影 :604-608；掩膜不参与显示切换 :1180-1186；RoiDrawTips 反馈 :2514-2521）。本方三个实锤断点：①显示图≠ROI输入图时借帧落**离屏视口**（privatePane 不在可视树），用户无处可画且无反馈（ResultWorkspacePresenter.RoiEditing.cs:25-44）；②租约锚定刷新代次致会话反复死亡重建（roibtn.log gen 0→1→2）；③不可绘无反馈。方案分四阶段：P1 进入绘制即把显示切到 ROI 输入图（借帧命中可见视口）、P2 租约货币全路径帧锚定、P3 RoiDrawTips 等价反馈、P4 订阅/继承链同规则，P5 生态验收（绘制→提交→执行→重投影→保存重开→关闭释放）。未取证项如实标注（RoiSelecterCombine.xaml 绑定细节无 .pending）。CodeBuddy 接管写入（用户确认 Codex 已停），按阶段实施，每段独立验收。

**2026-11-13 Blob 红框第二次根因收口**：用户最新截图确认上一版仍显示红框。继续追踪后确认错误有两层：`AppsXmlUi.NumericControl` 的 WPF 范围校验，以及 `NativeNumericCommit.ValidateInitial` 在字段范围绑定前把默认上限 100 的错误写入字段状态。现已移除过早的初始状态校验，改为在原参数行加载、正式 `Minimum/Maximum` 绑定完成后的 `DataBind` 阶段执行；新增回归同时断言 `Validation.GetHasError`、字段 `Error` 和 `DeclaredSettingsView.HasInvalidInput`。最新 Release UI.Tests 定向回归 3 条中数值红框和拼接链通过 2/2，当前 E 盘正式程序时间戳为 `2026-11-13 15:02:40`。用户若仍看到旧红框，应先关闭旧进程并启动该 E 盘程序验证，截图可能来自重建前进程。上一版仅清控件验证、未清字段状态的问题已修正。

**2026-11-13 ImageSource 投影引用与条件字段时序修复**：订阅目录和拼接条件字段按 VM 的可见条件虚拟化，测试改为先切换条件再读取真实行。ImageSource 显示投影此前直接对完整 `FieldSettings` 做空解析，遇到 `stitch.clear-value` 的引用就阻断整个投影；现为投影阶段给未使用的引用字段保留空解析值，执行层仍按正式引用合同解析。最新 Release UI.Tests 定向 3 条中数值红框和拼接引用链通过 2/2；订阅目录场景剩余失败是保存重开前测试会话仍持有“通讯服务及驱动”后台租约，属于关闭顺序待收口项，不能计作资源释放完成。

**2026-11-13 Blob 数值输入红框根因修复**：用户反馈运行参数“查找个数 100”和结果显示“字号 10”显示红框。两者都使用原厂 `AppsXmlUi.NumericControl`，该控件在字段范围绑定前先按默认上限 100 执行 `RangeRule`，范围绑定到 `1..20000` 或 `6..72` 后没有重新清理旧验证状态。现补齐与另一套原厂数值控件一致的 `Minimum/Maximum` 范围变化回校验，保留真实范围合同和错误校验。新增真实 Blob 参数窗口回归，核对查找个数与字号初值无验证错误，1/1 通过；UI.Tests 构建 0 错误。人工需用最新 Release 窗口复核两处红框消失及超范围输入仍能报错。

**2026-11-13 Blob 掩膜继承执行链甄别**：Blob 运行回归中发现的二值图不一致不是生产逻辑证据，而是测试输入几何不一致：本地屏蔽区使用三角形 `(2,2)-(8,2)-(2,8)`，继承输入此前使用矩形 `(2,2)-(4,2)-(4,4)-(2,4)`。已将测试改为同一三角形坐标，保留本地 8 个与继承 9 个数量上限断言。全量 Release 重建后重新封签，定向执行链回归 1/1 通过；随后 ROI/变量帧/保存重开及 Blob 三页定向回归 9/9 通过。该项结论仅关闭误报，不代表 Blob/ImageSource 完整生态已闭口；正式剩余项仍按 ROI 借帧、提交、重投影、保存重开、关闭释放和 ImageSource 存量失败继续核对。

**2026-11-13 ROI 完整链路逆向定位**：沿 VM 的 ROI 按钮、SelectedROI/ROIList、ImageViewBox、参数 ValueChanged、UpdateROI、RoiDrawTips 链路完成源码对照。已用新增回归确认本方五个 ROI 形状 ToggleButton 全部初始 IsEnabled=false，根因是 NativeRoiEditor.RequestCommand 将 _suspended 等加载暂态直接用于 WPF CanExecute，且 CanExecuteChanged 为空，首次绑定禁用后不会重新查询。原厂 ROI 命令入口保持可执行，绘制资格在 IsCanDrawROI 和结果视图层判断。本方已按该原证修正命令入口，新增回归通过，并验证矩形命令实际进入绘制状态；相关 ROI 回归 4/4、Blob 三页 5/5 通过。该修正只解决命令门控，完整链路仍需继续核对借帧、会话、参数写回、结果重投影、保存重开和关闭释放。临时借帧探针已清理。
**2026-11-13 ROI 根链合同第一阶段**：按原厂 RoiDrawTips 语义，ROI 命令入口现在保持可点击，加载暂态和会话失效由统一执行路径处理；失败时 NativeRoiEditor 显示原厂资源文案的警告反馈，BlobLocalRoiOwner 可提供具体错误信息。Release UI.Tests 构建 0 错误；ROI 形状命令、同帧绘制、ROI 保存重开和变量帧相关定向回归 4/4 通过，Blob 三页 5/5 保持通过。当前未闭口仍是帧借用全路径、各种形状提交、刷新重投影、保存重开和关闭释放的完整生产链，不能计作总目标完成。
**2026-11-13 完整回归甄别**：ROI 专项链和 Blob 三页保持通过。编辑器调试全套与 SourceAndVariables 全套仍有多条存量失败，主要表现为参数页/结果树/主题数据未物化、调试执行入口未就绪或来源列表缺项，需按基线逐项甄别，不能混入 ROI 根链结论。SourceAndVariables 本次 15/17 通过，2 条失败在图像源拼接/文件夹订阅场景；当前不宣称 ImageSource 全生态闭口。

**2026-11-13 ROI 形状按钮缺陷移交 Codex**：探针证实点击从未进入业务借帧链（roibtn.log 不存在），失效点在 NativeRoiEditor 命令门控层（_suspended/IsCurrent/generation 三个候选门）。交接单 `docs/collaboration/HANDOFF_ROI形状按钮不可点-20261113.md`（含根因候选、命令门控分析、原厂基线锚点、修改文件清单、构建测试命令、回归红线与禁止事项）。本对话批次成果（P0/显式改选/变量租约/差异矩阵）随交接单一并临时提交。

**2026-11-13 图像结果与 ROI 绘制交互全量原厂对照完成（差异矩阵已归档，待日志定位按钮缺陷）**：按用户要求先完成逆向全量对照再修。差异矩阵归档 `docs/09-现行文档/图像结果与ROI绘制交互原厂对照-20261113.html`：图像结果工作区 8 条（A1 模块切换/A2 单图改选/A3 关联输入/A4 双图入口/A5 数据刷新/A6 参数窗口开合/A7 来源列表构成/A8 不可绘制反馈）+ ROI 绘制 5 条（B1 判定/B2 载体/B3 刷新共存/B4 反馈/B5 绘制中编辑），每条含原件锚点（ModuleResultView/ModuleDisplayDecorator/RenderControlBase/DisplayImageListDecorator *.pending 行号）与本方差异。已对齐：A2/A3/A5/A6/B3/B5；差异待办：A8+B4 不可绘制必须给用户明确反馈（原件 RoiDrawTips 弹窗，本方静默 no-op）、A4 双图入口待用户确认、A7 来源列表构成逐项核对。ROI 按钮不可点的候选根因 H1 借帧失败无反馈 / H2 会话过期禁用 / H3 组合器不可见已列判据，一次性定位日志（roibtn.log：Activate 门槛/借帧拒绝/宿主借帧门槛）已埋点待用户配合点击采集。本批不先修，等日志定位后按矩阵成片实施。

**2026-11-13 调试窗口打开期间显式改选显示来源空白已修（按原件基线去除正式像素压制）**：用户人工反馈并确认调试窗口内输入联动正常，但主窗口图像结果下拉显式选择『2 Blob分析1 · Blob图像数据』视口空白（关闭调试窗口才显示、重开又消失、来回切输入源恢复）。原件基线：`UpdateVMResultShow`（RenderControlBase.xaml.cs.pending:1127-1154，由 imageListCombobox_SelectionChanged:1998-1999 调用）在用户改选后立即读取所选图像对象当前值显示，与参数窗口开合无关；据此**删除** `CaptureNativeImageSources` 中"私有编辑作用域+有持久化显示选择即压制正式像素"的规则（`retainFormalImage` 恒为 true，调试执行产生私有快照后自然切换私有结果），P0 用例中编码旧行为的 `HasImageValue=false` 断言同步翻转为原件基线。新增合同用例 `Explicit_display_choice_shows_the_selected_modules_formal_pixels_while_the_debug_window_is_open`（含用户场景：先选上游图、开调试窗口、再显式选 Blob图像数据、关闭后保持）。验证：定向 9/9 通过（含 P0 三条、Blob 三页 5 条）；全量 Release 0 错误。回归补测：ROI 保存重开 `Confirmed_blob_roi_threshold_and_display_reopen_on_the_same_image_source_connection` 通过；`ShutdownLifecycleProbe wait` 通过（探针目录部署重建后运行，临时数据已清理）；`ImageSourceSessionTests` 37/38，唯一失败 `Camera_acquisition_resolves_exposure_and_gain_from_the_formal_upstream_result` 报"普通参数来源必须属于当前流程的控制前序"（Application 层 `CandidateInputValidation`，不经过本批任何改动文件，疑似存量，待全量封签与基线对照甄别）。遗留：UI.Tests 全量封签、图像结果完整交互矩阵（图像源条/自动切换/模块切换）对照另批立项。改动未提交。

**2026-11-13 ROI 形状绘制失效修复（待用户重建验证）**：用户人工反馈 ROI 形状绘制按钮完全不可用。根因：本批输入页写回通知对**所有**输入执行 `_debug.OnDraftEdited()`，而 ROI 每次形状接受都是 `input.roi.*` 写回 → `InvalidateDraft` 杀死调试会话/帧租约 → 绘制报废。原件语义（ModuleResultView.xaml.cs.pending:604-608 `UpdateROI`、`P_ValueChanged→UpdateModuleViewResultShow`）：区域/参数编辑只重绘结果并重投影 ROI，从不终止模块会话。修正：输入页写回不再失效调试会话，仅 IMAGE 输入触发 `_debugResultChanged`（OnRelatedIOChanged 语义）。定向 10/10 通过（ROI 相关回归全绿）；用户运行中的程序占用 Release 导致该修复尚未部署，待用户退出程序后重建再人工验证绘制与显式改选两条。

**2026-11-13 P0 闭口 + 变量 ROI 租约按原件语义修复（待提交）**：P0 用例 `Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice` 由失败转通过（781/801/804 三处断言全过）。缺失接缝是**输入页字段接受后不通知宿主**：`DeclaredInputFieldsView` 写回只应用草稿不触发宿主，`input.image` 改选后结果工作区从不刷新；原件 `ModuleResultView.P_ValueChanged`/`OnRelatedIOChanged` 直接驱动结果视图，现按端口类型通知（IMAGE 输入触发结果刷新，其它输入只失效调试草稿并重绘结果）。配套：调试临时显示态收敛为单一 `EditorRelatedImage(Key,PortId,EditorScopeId)`；`ResolveOutputChoice` 为重建与不变判定共用的唯一解析；双图模式会话存续期间保持单图选择（原件 `OnIsShowDoubleImageChanged` 不动 `SelectedImage`）；`ItemsSource` 替换期置空不清除已选来源；变量图像比较改按帧身份（对应原件 ImageDataSource 守卫）；空/无输入的中间捕获计划不清空已呈现视口（原件无新数据不动视口）。变量 ROI 回归根因与修复：`ResultWorkspacePresenter.RoiEditing.cs` 变量租约谓词误含 `temporarySource`，输入面板真正选中变量来源后走真实视口路径时租约创建即死；按原件 `UpdateROI` 语义（ModuleResultView.xaml.cs.pending:604-608 数据刷新后把既有 ROI 重投影到当前帧、不销毁会话）改为锚定“同一帧仍是该视口的呈现帧”。验证：定向 8/8 通过（P0 三条 + Blob 三页 5 条）；全量 Release 构建 0 错误（657 警告为存量；期间 174 个 MSB3027/3021 为扩展宿主崩溃残留 testhost 锁文件，结束进程后 0 错误，非代码错误）。未做与待验：ROI 保存重开、上游失败恢复、`ShutdownLifecycleProbe wait`、全量测试封签、人工核对（单图下拉切换后右侧联动、关闭后回正式选择、变量 ROI 绘制提交与保存重开）。改动未提交，涉及 `ResultWorkspacePresenter(.RoiEditing).cs`、`MainWindowEditorDebug.cs`、`MainWindowRuntimeEditing.cs`、`MainWindowViewModel.NativeResults.cs`、`RuntimeNodeConfigurationControl.cs` 及已登记证据 `evidence/VM编辑器相关输入单图联动原件对照-20261113.json`。

**2026-11-13 P0 输入改选→单图联动：先补原证再改实现**：用户质询本轮改动是否先取证，如实结论是「方向有原证、机制属自研推导」。已逐行核对仓内 `.pending` 保留源并新登记 `docs/collaboration/evidence/VM编辑器相关输入单图联动原件对照-20261113.json`：`ModuleResultView.OnRelatedIOChanged`（:500-524，仅单图模式、按 DisplayValue 命中后同步写 SelectedImage、随后 RefreshInputImageList 与 SetRoiVisible）、`ModuleDisplayDecorator.SelectedImage/SelectedInputImage` setter（:319-372、:282-314，纯视图状态、无持久化写入）、`RefreshInputImageList`（:1432-1468，双图选匹配项否则首项、单图清空输入选择）、单/双图下拉绑定（`RenderControlBase.xaml.pending:47/:28`）、用户改选处理（`RenderControlBase.xaml.cs.pending:1934-2007`，只改视图与叠加层、不写保存）、双图开合（:2225-2260，不动 SelectedImage）。差异与决定已登记：D1 匹配键改端口/Key（语义适配）、D2 双图入口保留当前合法输入（不按原件 `[0]`，标运行未确认）、D3 临时态随编辑会话退役清空、D4 ROI 可见性不在本批、D5 参数根切换不迁移。据此把无原证的 6 个补偿字段（ActiveEditorInputKey/PortId、_editorInputDisplayKey/PortId、_pendingTransientInputPortId、两处 ContextIdle 回推）收敛为单一 `EditorRelatedImage(Key,PortId,EditorScopeId)`，`Refresh` 与 `DisplayUnchanged` 共用唯一 `ResolveOutputChoice`。P0 失败事实：`EditorDebugSessionTests.cs:781` 期望 `output.gray-image` 实际仍为二值字段；一次性追踪显示下拉事件被接纳且切换逻辑执行，但视图选择未在同一通知内落地（追踪代码与日志已删除）。当前改动未构建未验证，下一步构建 UI.Tests 并复跑 P0 三条。

**2026-11-13 VM 源码级联动取证与当前状态**：已从仓内 VM 反编译保留源 `ModuleResultView.xaml.cs.pending`、`ModuleDisplayDecorator.cs.pending`、`RenderControlBase.xaml.pending` 核对原厂链路。单图调试时 `OnRelatedIOChanged` 直接将 `SelectedImage` 切到关联输入，随后回调从 `RenderDataCenter.SelectedImage` 刷新结果视口，正式显示选择不在该调试切换中改写。本方已将编辑器图像字段变更接回结果工作区刷新，并保留调试输入的临时来源身份跨计划重建；UI.Tests 编译通过，三条定向回归仍为 2/3，Blob 主图切换断言仍失败。进一步确认失败发生在编辑器上下文重绑定后的选择恢复，当前仍不能宣称链路闭口。已补充独立的临时输入选择通知与窗口关闭清理边界，回归仍未通过。临时调试文件已清理，下一步继续核对结果工作区选择对象与 WPF 选择绑定的最终覆盖顺序。

> 本文件只回答「现在在做什么、卡在哪、下一步做什么」。历史批次已按月切到 `history/CURRENT_TASK_ARCHIVE_<年-月>.md`（只追加、不改写），2026-11-05 之前共 2206 个批次已归档。
> 找文档先看 `docs/00-索引/主题台账.html`；找具体文件用 `docs/00-索引/文档索引.html`。

**交接入口（2026-11-11）**：本批暂停/清除后的基线、已改文件、构建证据、人工验收边界和下一步统一见 `docs/collaboration/HANDOFF_VM原件化工具调试窗口全链迁移-20261111.md`；该交接单不代表目标完成。

**CodeBuddy 交接摘要（2026-11-11）**：本轮 Codex→CodeBuddy 的目录、基线、未提交手工修复、构建阻塞和接续顺序已追加到上述交接单末尾的"2026-11-11 Codex → CodeBuddy 交接摘要"。


**2026-11-12 欢迎页链路交接（供 Codex 接手）**：本批剩余核心问题="打开已保存方案后流程画布节点与小地图视觉空白（数据层确证 2 节点）"，交接文档 `docs/02-实施总结/2026/11/12/欢迎页链路与方案打开画布空白-实施总结-20261112-195757.md`（含证据链、已改文件清单、未验证的候选修复、诊断步骤、回归命令与踩坑提示）。已修并手测通过：保存即命名、关闭欢迎页进入可编辑方案；已修未复测：欢迎页拖动。构建当前 0 错误。本批改动未提交。
**2026-11-12 画布空白修复（Codex 接手）**：按已有 VM 生命周期证据和本方运行日志完成一次性根因收口：将当前 Tab 显式绑定到画布节点/连线 ItemsControl 与缩放投影，Shell Loaded/Tab 切换共用 `SynchronizePresentation`，并把回归用例改为断言真实节点容器和小地图内容。此为有原证约束的 WPF 语义适配，不宣称直接迁移 VM 内部控件。未启动程序或自动测试。执行 `dotnet build-server shutdown` 释放占用后，完整 Release 构建已通过，0 错误。新程序路径、生成时间和哈希见实施总结第9节，现可交人工验收。
**2026-11-12 画布空白二次根因修正**：用户截图探针显示 `selectedTab=流程1 nodes=2` 但 `canvasRef=null canvasNodes=-1`，确认方案文件和会话数据完整，真正漏通在欢迎页空态清理画布引用后打开方案未重新接回。`RefreshMiniMap` 已补充当前视觉树重新发现、ScrollViewer 恢复和 `SynchronizePresentation`，完整 Release 已再次通过，0 错误。新程序仍为同一 C 盘 Release 路径，待用户重新打开方案验收。
**2026-11-12 画布空白人工验收通过**：用户使用 C 盘 Release 重新打开方案后确认画布节点和小地图均正常显示，当前测试没有发现问题。该项完成人工验收，后续继续观察方案切换和关闭重开回归。
**2026-11-13 持久化阻断已解除**：`EmbeddedFileCatalog.Parse` 的 JSON 数字节点读取已统一为严格 `Int64` 令牌校验并保持 `JsonException` 语义；清理旧 `dotnet/testhost` 占用后，以正式 Release 程序集和 `S2_CASE_ROOT=E:\\SunEyeVision_V2\\Release\\_validation` 重跑 `SunEyeVision.Persistence.Tests`，56/56 通过。当前未闭口的是三条真实 UI 回归：方案切换前"通讯服务及驱动"租约未排空、Blob 重开后主图来源仍带图像值、屏蔽区继承保存重开断言失败；已拆成独立链路继续修复，不再归因于权限或旧 DLL。
**2026-11-13 关闭资源阻断修复**：主窗口插件 drain 超时后原实现只显示"资源释放后自动关闭"提示却没有重新执行关闭链，导致 `wait` 场景一直停在"等待资源释放"。现已保留同一 drain 任务，在其完成后回到 UI 线程自动重试 `CompletePreparedCloseAsync`；正式 Release `ShutdownLifecycleProbe wait` 通过并输出 `Actual MainWindow shutdown scenario passed: wait`。通讯服务、相机和会话租约的所有权顺序未改变。
**2026-11-13 位置修正 UI 投影修复**：关闭 Blob 位置修正开关时，原实现尝试向不含投影字段的工具编辑草稿直接写入，导致可视行仍显示旧来源或抛未知字段异常。现改为沿真实 `input.pose` 参数行宿主写回 `UnsetSetting`，由现有输入转换和提交链维护同一草稿状态；`Disabling_position_correction_clears_its_source_in_the_same_undoable_edit` 1/1 通过。
**2026-11-13 Blob 三页回归复测**：位置修正清理修复后，`BlobThreePageSessionRegressionTests` 为 2/5 通过；剩余失败已缩小为 Reset 对话框观测、真实三页数值模型查找、继承 ROI 预览层路由/颜色三条，未宣称三页闭口。
**2026-11-13 ROI 预览层修正（待干净复测）**：继承/区域模式的变更现在会触发 `BlobLocalRoiOwner` 预览刷新；只读 ROI 层新增与结果视口一致的分组形状投影，避免蓝色输入 ROI 只作为未分组形状落在 `ShapeList` 外。复测时测试夹具在断言后直接 `SolutionManager.Open`，仍被"通讯服务及驱动"租约阻断，当前只记录实现变更，不记录通过。
**2026-11-13 三页参数首屏物化修复**：真实三页回归暴露初始页索引为 0 时 WPF 不触发 `SelectionChanged`，导致 Blob 基本参数页的 Assist 控件未进入实际视觉树；`DeclaredFieldsView` 现在显式物化初始选中页。Blob Assist 行取消重复条件门控，改由正式位置修正开关同步可见性，并在关闭状态首次打开时先物化一次再折叠，保留 VM 的隐藏语义。`Real_three_pages_read_numeric_models_and_retire_with_the_actual_editor` 已 **1/1 通过**。当前 Blob 三页套件为 **3/5 通过**；剩余 Reset 对话框观察和 ROI 测试直接 `SolutionManager.Open` 通讯租约问题均属测试入口/钩子，不能冒充产品链通过。
**2026-11-13 测试入口与嵌入文件持久化收口**：Reset 测试钩子对该确认/成功消息改为进入真实 owned native dialog，由测试安排的 No/Yes/Ok 按钮驱动；Blob 三页回归现 **5/5 通过**。直接重开前统一排空通讯租约。`SolutionDocumentValidator` 允许当前持久化信封写入的根级 `EmbeddedFiles`，ImageSource 保存重开两条回归 **2/2 通过**。另发现独立 ROI 调试回归的真实产品缺口：窗口初开时结果工作区仍选 Blob 输出，`NativeRoiFrameSource` 因未选上游输入而拒绝借帧；未用试探性自动切换冒充 VM 行为，保留为下一批按原证补齐的输入图像选择链。
**2026-11-13 变量图像源 ROI 阻断仍未闭口**：已确认不是权限或通讯租约，而是编辑器选择变量后，双图切换期间 `SelectedEditorContext` 会短暂失效，结果工作区随后按空输入重建，ROI `PrepareInteraction` 无法取得同一帧。已加入同会话/同节点草稿保留和私有计划空输入过渡保护，但定向回归 `Stored_image_variable_uses_the_same_frame_in_editor_roi_preview_and_formal_blob_execution` 仍失败（ROI 会话未成为 current），因此不能宣称该阻断已解决。下一步需沿"编辑器节点重绑定→草稿作用域→结果计划捕获"补齐稳定身份合同，再复测变量图像源、ROI、Blob 执行及保存重开全链。
**2026-11-13 ROI 阻断继续定位**：编辑器当前性已改为按 workflow/node 稳定 ID核验，且结果绑定不再因节点对象重建立即失效；UI.Tests 构建 0 错误。但定向变量图像源回归仍在 `PrepareInteraction` 失败。当前新增的变量帧直接借用路径已能从方案变量捕获同一帧，剩余缺口集中在私有未加载视口的 ROI 安装/会话接纳，尚未通过回归，不能计作闭口。
**2026-11-13 ROI 作用域复用根因进一步收窄**：结果工作区在正式发布计划与私有调试计划的来源集合相同、但编辑作用域已变化时，原 `DisplayUnchanged` 判定会错误复用正式计划；现已增加 `EditorScopeId` 边界判定，Blob 三页回归保持 **5/5**。独立 ROI 回归仍在 `PrepareInteraction` 入口失败，当前未宣称闭口；下一步需实现不改正式结果选择、不重置调试草稿的宿主私有帧借用路径。
**2026-11-12 欢迎页全链路开口（取证完成，待实施）**：用户三截图对照 VM 提出三条链路问题：①VM 欢迎页可移动、本方不可；②关闭应用→保存解决方案→弹窗命名后，最近打开列表显示的仍是"未命名方案"；③直接关闭欢迎页后本方主窗口"暂无流程"不可编辑，VM 关闭欢迎页即可直接编辑。已按门禁完成取证（ilspycmd 反编译本机 VisionMasterMain.dll + 用户实机截图），证据 `evidence/欢迎页与方案生命周期原件对照-20261112.json`：VM 欢迎页=VisionMaster.AppMainWindow.GuideWindow（BaseWindow 独立窗口）+GuideHelper（ShowEnable 不再显示配置），模板卡只 SaveConfig("ToolBoxType") 后 Close，不创建/销毁方案——VM 主窗在欢迎页之下已持有可编辑方案；最近列表显示名=方案文件名。本方根因：②SaveCurrentDocumentWithPrompt→SaveAs 后 RecordRecentSolution 记录的是 session.Document.Name（CreateNew 默认"未命名方案"），未同步文件主名；③App.xaml.cs 关闭起始页=EmptyWorkspace→不建方案进空态，仅 NewEmptySolutionAsync 才建；①拖动代码已存在（PreviewMouseLeftButtonDown+DragMove）但用户实机不可移动——失效区域待实机复现定位，不猜测改。修复已实施（2026-11-12）：②DocumentSession.SaveAsCore 在 updateBaseline 时把 Document.Name 同步为文件主名（保存即命名，方案名随文件名落库，副本保存不改名）→ 最近列表/主窗标题随实名；③App.xaml.cs 启动期关闭欢迎页与运行期复显关闭（无打开方案时）均触发 NewEmptySolutionAsync，主窗口进入可编辑空白方案不再落空态；①StartPageWindow.IsInteractiveSource 精确化（只拦 ButtonBase/TextBoxBase/ComboBox/ListBoxItem/Slider，Label/TextBlock/Border 放行拖动）。全量 Release 0 错误；定向回归 26 用例 25 通过（唯一失败为存量超时型环境失败）。待人工核对三条：欢迎页任意空白可拖动（如仍不可动请指明拖动区域）、关闭应用→保存命名后最近列表显示所命名、直接关闭欢迎页后画布可直接新建/编辑流程。**2026-11-12 双击调试窗口闪烁与拼接高度红框修复批（用户手测两项均确认修复）**：①闪烁：按门禁先用 ilspycmd 11.2.0.10050 反编译本机 `myLibs\VMControls.WPF.dll`（SHA-256 7F47D5B2…，与仓内第一批登记 99FFA4…不一致故未复用旧产物）取证 `ModuleResultView`：原件结果视口按数据变化更新——图像仅在对象变化时经 `ImageDataSource` 守卫（ModuleDisplayDecorator.cs:524）重设，参数窗口开合只切 ROI 可见性（`IsOpenParams`→`SetRoiVisible`），证据 `evidence/VM调试窗口结果视口更新路径-20261112.json`。据此改 `ResultWorkspacePresenter.Refresh`：重建计划与当前显示完全一致时跳过清空重建（判定覆盖来源集合 Key/运行身份/HasImageValue/显示名、选中节点、叠加勾选计划、按重建规则只读解析的输入/输出选择）；捕获 currency 只挂 `_disposed`（plan 闭包须跨重建 epoch 自增有效——首版把 epoch 后置导致来源 Current 全假、图像不呈现，已修）；并在 `CaptureNativeImageSources.Owner` 移除 `SelectedEditorContext` 绑定（原件语义：正式结果显示不随参数窗口开合变化；私有调试快照当前性由 `CurrentEditorDebugSnapshot` 内部校验保证）——这是"打开窗口后正式结果被清"的根因。②红框：用户实机定位"拼接高度 2000 红、改 100 不红"锁定 NumericControl 默认上限 100 误判；根因两处——迁移丢失原件 `Integer_Sub`/`Float_sub` 的 `Minimum="{Binding MinValueA}" Maximum="{Binding MaxValueA}"`（原件 paramusercontroldatatemplate.xaml 551/803 行，已补回含 SliderTickFrequency），及 `SliderArrangeBase.MaxChanged` 未按单值模式语义同步 `EndValueF`（T1.Maximum 源，恒停默认 100）；另加 `NumericControl` 范围变化重跑 `RangeRule` 清除残留红框。③测试基建：`NativeMessageBox` 加 internal `TestAutoRespond` 钩子（Plugin.Wpf 对 UI.Tests/CanvasLayout.Tests 开友元；生产不设置行为不变），测试宿主自动应答模态弹窗——此前弹窗会阻塞测试派发线程。新增合同用例 6 条（显示不重建、图像值消失/恢复、掩膜清空/恢复、选择计划更替、红框简单物化+会话级守卫）。验证：全量 Release 0 错误；定向用例通过；全量 361 用例 309 通过/52 失败——经 stash 基线对照 EditorDebug/NativeResultComposition 两类基线失败 19 条与本批清单一致，`Failed_execute…native_dialog`、`Overlay_commands_restore…` 2 条疑似本批回归**待甄别**，其余为存量夹具/主题资源环境失败，下次封签复核。人工验收：双击打开调试窗口图形/文本不动（用户确认）✓、拼接高度 2000 红框消失（用户确认）✓、正式结果叠加不消失与图像源切换图形保留（用户确认"没有太大问题"）✓——四条人工验收全部通过。未做：2 条疑似回归甄别、全量封签复核、Pack-Release 本地打包（缺 Inno Setup 6 非阻塞）。本批改动未提交，工作区含 anti-flicker-wip.patch（首版参考）与两份证据 JSON。

**2026-11-12 双击调试窗口闪烁：用户反馈"图形和文本重绘闪烁、VM 不会"，按门禁先 REA 取证再修复（下一轮启动依据）**：用户反馈双击打开调试窗口时主窗口结果视口的图形与文本会重绘（闪烁），VM 无此现象；模块结果树部分用户确认"目前看的还行"。已定位本方根因（代码事实）：打开调试窗口 → `MainWindowEditorDebug.RefreshEditorDebugResult` 因 `contextChanged` 广播 `ResultWorkspaceChanged`（MainWindowEditorDebug.cs:60）→ `ResultWorkspacePresenter.Refresh`（src/UI/Services/Images/ResultWorkspacePresenter.cs:237 起）**无条件清空两个视口图像+图形层并全量重建**（清空→重设同一张图=可见闪烁）；另有同族隐患：`RefreshFormalNodeSelection`（MainWindowRuntimeEditing.cs:52）无条件 `ReleaseActiveResultView+ClearResultDisplay`，使 `MainWindowResultPublication.cs:129` 既有防闪护栏（"同一身份不重建，原件按数据变化更新"）在该路径永不生效。已尝试"计划未变化则跳过重建"的首版实现并回滚：全量测试 47→50 失败，定向用例（Actual_result_workspace_clears_and_restores/Blob_output_overlay_retires_on_a_new_choice_plan 等 6 条）证明判定条件不足——至少漏了每来源 `HasImageValue` 变化（同一 publication 下图像值从有变无）与"该清空/该换计划"的强制重建语义。**回滚后工作区=已验证提交 `2e0e1e174`；首版改动存于仓库根未跟踪文件 `anti-flicker-wip.patch`，可作参考但不得直接套用。**用户明确要求：先找 VM 逆向证据再优化，不得自行推导实施。VM 4.4.0 已定位：`C:\Program Files\VisionMaster4.4.0\Applications\`（主逻辑在 Qt DLL，不在 1.4MB 的 VisionMaster.exe）。下一轮步骤：①REA 打开 VM Applications 下承载结果视图的模块 DLL（顺既有类名 `AlgorithmTabRoot`/`ModuleResultView` 定位），取证"参数窗口打开时结果视图的更新路径"——是否复用已呈现场景、是否仅数据变化才重设图像、叠加层归属；②产出 `evidence/VM调试窗口结果视口更新路径-<日期>.json`（类名/关键函数/调用链/结论）；③按证据修正判定条件（补 `HasImageValue` 比对等），先补合同用例（显示未变不重建 + 三个反例：图像值有无变化、掩膜清空恢复、选择计划更替）再改实现；④全量构建封签后 `--no-build` 测试不得劣于基线 47 条失败；⑤人工核对"双击打开调试窗口图形/文本不动、正式结果叠加不消失、图像源切换图形仍保留"三条。

**2026-11-12 模块结果树按显示声明重建（投影方向重构落地，待人工核对）**：按 `HANDOFF_Blob结果树投影重构-20261112.md` 完成。①`OutputDisplayNode` 增加目标端口（`portId`）与成员路径（`memberPath`）：根节点=端口行，子节点默认继承父端口并追加自己的 FieldId，跨端口分组显式给 portId；`Validate` 按端口+成员路径解析（记录字段或几何伪成员 count/x/y），失败直接抛异常；`FindOutputDisplay` 增加深度查找，只作为分组子行出现的端口仍能取到显示名。②顺序权威回归显示声明（深度优先），未声明端口追加末尾、声明过的端口不重复；`BlobSchema.Outputs` 顺序未动（其顺序参与 Fingerprint），16 个清单外端口仅以 `displayInResultPanel:false` 隐藏。③`ResultPresentationModel` 按声明树建行并实现成员转置取值（序列端口每元素取该成员，单行分号拼接、最多 64 个值后加"…"，空序列显示"空序列 (0)"）；删除 `CreateAllMembers`/`[All]` 行/`[i]` 样本行/「加载下一页」分页行（presenter 与只读属性树同步删除）。④`BlobOutputDisplay` 按证据 JSON 逐项重写：25 个可见顶层项与 12 个分组的名称/层级/成员路径与原件一致（名称修正：对比度/检测区域/ROI圆弧/轮廓点集/主轴角度/圆形度），「Blob矩形框」跨 4 个端口组织子行，「屏蔽区多边形」点集走几何成员投影。⑤用例重写：旧分页/All/样本索引用例改为成员转置与行数合同（先改断言再改代码），新增 `BlobResultTreeTests` 以证据清单逐项断言声明树；`ResultBindingInfo.CanBind` 对几何伪成员返回 false（不再抛异常）。已知缺口（不伪造）：原件「分数」无本方 Blob 级 score 数据来源，未声明该行；「屏蔽区多边形」点集数值语义标"运行未确认"。验证：全量 Release 构建 0 错误；全量构建封签后 `--no-build` 跑测试——UI 357 用例 310 通过/47 失败（基线同套件 288 通过/69 失败，本批净修复 22 条，失败项经 stash 基线对照均为存量夹具/主题资源环境问题），Blob 契约 129 用例中结果树与参数契约容器全部通过（Runtime.Tests 容器存在存量 xUnit 发现器程序集加载失败，与基线一致）。待人工验收：用户按 6 张原件截图逐项核对项名/分组/顺序/值形式，及双击调试窗口不闪烁。

**2026-11-12 关键结论：模块结果树"投影方向错了"（下一轮启动依据）**：用户提供 6 张原厂 Blob 模块结果截图（证据 `evidence/Blob模块结果树原件结构-20261112.json`）后确认：VM 树里的 `面积/周长/质心X/质心Y/角度/长轴/短轴/圆形度/矩形度` 全部是 **`BlobsPort` 记录的成员**（由 `BlobDisplay.xml` 的 member 定义佐证），VM 是"把 Blobs 记录按成员**转置成列**：每个成员一行、值为全 Blob 数组（分号拼接）"；我方是"每个成员一个顶层端口 + 手工起名"（`Field(17)` 叫灰度直方图、`Field(19)` 叫面积…），**编号与名称本身错位**。因此此前反复改名称/顺序/格式都无法对齐——问题在数据模型到树的投影方向，不在渲染。正确改法（结构性，一次性）：①`OutputDisplayNode` 支持 member-path 行（可带分组，如 质心点→centroid-x/y）；②树的取值按 member 从 `BlobsPort` 投影为数组行；③顺序/分组/名称全部来自该声明；④结果表继续沿用 `BlobDisplay.xml`。本轮已完成：树值回裸值（模块状态=1）、标量数组一行分号拼接且无子节点/分页行、结果项顺序改回声明序、调试窗口保留正式结果叠加、换显示图像保留矩形与文字（用户已验证第 1 项正常）。

**2026-11-11 CodeBuddy 接手交接批：图像来源空项/资源引用行核对 + 结果树语义格式成片收拢**：按 `HANDOFF_VM原件化工具调试窗口全链迁移-20261111.md` 接续。修复1按 VM 原证纠偏：D 盘参考的"未设置"文案在 VM 4.4.0 中文资源中查无，图像来源空项改为原件空项惯例 `RunParam_None`（zh"无"/en"None"，`DeclaredFieldRow.ImageSource.cs`），新增合同用例；修复2（资源字段→引用模板路由）经核对在本 worktree 已有等价实现，只登记不重做，证据 `evidence/图像来源空项与资源引用行原件对照-20261111.json`。结果格式块新增 `ResultSemanticText` 共用语义投影：模块状态/单体状态 0/1→失败/成功、阈值方式五种原厂中文、极性 Binary→亮于背景/BinaryInv→暗于背景，主结果树（`ResultPresentationPresenter`）与只读属性树（`DeclaredPropertyList`）统一接入，序列元素节点沿声明地址回溯根端口取显示名；属性树 Float32 改单精度自然位数（C03 原证），主树/属性树既有 Culture 合同不动；结果表 Blob 列已是 float32 与原证一致未改。分组标题与空值文案无 VM 原证，按门禁不发明，保留待办；证据 `evidence/Blob结果格式语义投影原件对照-20261111.json`。全量 Release 构建 0 错误（本机缺 PowerShell 7 阻断 UI 载荷封装，经用户授权以官方便携 ZIP 装至 `C:\Users\Administrator\.local\pwsh7` 并配置 git-ignored 的 `installer/Release.Local.props` 解决；winget 安装因系统服务禁用失败）。回归（用户授权全量）：UI 224/355、Blob 103/121，本批相关用例全部通过；失败项经排查均为该 worktree 既有的夹具/部署问题——`DocumentSession.Dispose` 报"方案后台活动尚未排空"、测试 bin 主题资源缺失、共享 bin 陈旧程序集/清单哈希不匹配（测试程序集烘焙路径与独立构建测试项目会使已封装清单失效，必须全量构建后立即 --no-build 跑测试），以及一个既有 de-DE Culture 漂移用例；均与本批改动无交集。运行态体感（空项显示、结果树文本、关闭释放）留用户人工核对。分支：`feature/param-window-datatree` 被 D 盘 worktree 占用，本批提交落同源兄弟分支 `feature/param-window-datatree-codebuddy`（基线同 c5e8901df，可快进合并）。

**2026-11-11 全链路差异矩阵重新开口**：用户反馈"否"关闭方案后仍停留在等待资源释放、图像缩小后无法平移、图像源切换后 Blob 图形与文本消失，且模块结果页的分组和格式仍与 VM 不一致。前期工作只完成局部字段、结果格式和若干生命周期接缝，没有完成"VM 原证→当前消费者→状态/身份→资源所有权→关闭退役"的统一矩阵，不能声称全链路已完成。现已建立 `docs/09-现行文档/VM-ImageSource-Blob全链路差异矩阵-20261111.html`，按框架生命周期、数据/资源所有权、控件交互和窗口组装四层归因，后续按矩阵成片修复并集中构建，不再逐点补丁。

**启动安全边界**：启动阶段只对照 VM 的流程时序和用户可见状态。加密、权限、授权策略、密钥和安全弹窗均采用本方自研设计，不逆向或复用原厂实现、协议、资源或名称；差异矩阵单独记录流程一致性与安全实现来源。

**2026-11-11 图像图层保留与结果视口交互修正（待人工核对）**：定位到图像源切换后 Blob 图形/文本消失的实际断点：`ResultWorkspacePresenter`虽按坐标域保留场景，但`ResultImagePane`清空视口及`NativeResultViewport.BindImage`仍无条件退役Shape层，导致下游重呈现前图层已被删除。现将输出视口的清空/绑定改为可选保留Shape层，并把结果视口图形设为只读命中区域、让平移/缩放继续由原件`ImageViewBox`处理；ROI编辑仍由独立`NativeRoiViewport`负责。全量Release重新构建0错误、3302警告、22.04秒，未启动程序、自动测试或桌面控制。图像源切换后图形/文本是否持续、缩小图像是否可平移，以及关闭释放链仍待用户统一人工核对，不能据此宣称整链完成。

**2026-11-11 关闭资源代次释放顺序修正（待人工核对）**：继续沿"否关闭→子窗口排空→正式结果/视图→方案会话→插件目录"的生命周期矩阵复核，确认`ReleaseDocumentHost`此前只解绑宿主和视图，却未在插件目录退役前关闭`DocumentSession`；正式结果reader/执行代次可能因此继续持有租约，造成"等待资源释放"后只能结束进程。现将宿主退出专用的`DocumentManager.CloseCurrent(discardPendingEdits:true)`加入`ReleaseDocumentHost`，方案切换路径不复用该方法，不改变切换语义。全量Release构建0错误、5945警告、26.71秒，未启动程序、自动测试或桌面控制。实际"不保存后退出"和迟到回调仍待用户人工核对，不能宣称关闭链完成。

**2026-11-11 图像层回归合同补强（待运行）**：在`ResultImagePaneTests`增加输出图像重新绑定时保留原生Shape层、等待新场景回执替换的STA合同；该用例验证的是图像源切换下图形/文本不被底层清空，不将截图或算法结果替代视图生命周期证据。全量Release重新构建0错误、3302警告、18.83秒；按用户要求未运行测试或程序。

## 一、当前主线\n\n**2026-11-11 结果语义格式化首批验证**：`DeclaredPropertyList` 已沿结果声明将模块状态值1/0投影为"成功/失败"，将阈值方式内部值投影为中文语义；不改变算法结果或端口身份。Plugin.Wpf编译0错误，全量Release编译0错误、5964警告、28.87秒；未运行测试或程序。图像源切换后的图形/文本保留、Blob专用结果分组和原厂数值格式仍未完成。\n\n**2026-11-11 Blob结果展示合同重新开口**：用户人工截图确认Blob算法结果值与原厂高度一致，但模块结果页的分组、状态文本、数组/数值格式，以及图像源切换后Blob图形和文本的保留行为仍明显不一致。已固定证据 `evidence/Blob结果展示合同缺口-20261111.json`。当前结论是算法结果链具备基础，结果声明/格式化/图像publication上下文尚未闭合；不进入完成或验收结论，下一批按三层合同成片修复。\n\n**2026-11-11 原厂依赖边界与正式位置修正字段收口**：产品运行时不直接引用原厂 VM DLL，原厂程序集、Qt/C++ 类型名、资源路径和算法身份只保留在逆向证据与映射材料中。位置修正模式依据同版本 Group.xml 的 SelectWay 0/1/2 静态证据，改为中性正式输入 `input.pose.mode`，进入参数提交、保存重开和执行快照；辅助组合器只保留宿主绑定、可见性和生命周期接缝。全量 Release 编译0错误、3302警告、15.73秒；未启动程序、自动测试或生产探针。当前进入人工验收阶段，重点核对 Blob 三页、位置修正模式、参数保存重开、执行结果/图像叠加和关闭释放，未反馈项不计作通过。\n\n**2026-11-11 删除字段视觉占位分支**：按原件优先门禁，`DeclaredFieldRow.BuildSlotContent` 不再生成通用记录子行、"未设置"文本或"由专用区域处理"文本。生产已接入的颜色记录继续借用原件颜色控件；未覆盖的记录、几何和集合字段现在明确抛出未完成依赖，避免伪造可用视觉。全量Release编译0错误、5992警告、23.92秒；未启动程序、自动测试或生产探针。Blob ROI组合器、辅助组合器和页面组装链仍待按原件闭包替换。\n\n\n**2026-11-11 中性命名与原厂依赖边界清理**：确认产品代码仍有 `BlobVmFindParameters`、`BlobVmResultProjection`、`VmParameters`、`VmResult` 及 `BlobVm*` 枚举等逆向来源命名，且 Blob 视觉仍由 `BlobUiComposition`、`BlobRoiUi`、`NativeRoiUiDraftControls` 等本方组合器部分承担。已将运行时类型、属性和枚举改为中性命名，未改变字段身份、序列化键、结果端口或算法语义；新增原厂依赖边界证据。全量Release编译0错误、3308警告、22.20秒；未启动程序、自动测试或生产探针。视觉组合器和字段记录/未设置占位仍待按VM原件证据成片替换。\n\n\n**2026-11-11 结果树根顺序按VM显示声明收口**：VM结果页的根字段顺序来自原件显示声明，当前 `ResultPresentationModel` 原先按 `ToolSchema.Outputs` 顺序生成根节点，导致Blob耗时、模块状态和统计字段顺序与原厂不一致。现按 `OutputDisplay` 根节点顺序投影，未列入显示声明的端口追加到末尾，端口身份、publication、结果表和资源借阅不变。已补充结果树顺序合同与回归用例。全量Release编译0错误、5992警告、21.88秒；未启动程序、自动测试或生产探针。\n\n\n**2026-11-11 模块结果摘要行全链修正**：基于 VM 原厂结果页证据，确认 Blob 的"耗时（ms）"和"模块状态"属于工具正式输出，不应与宿主无条件追加的"执行状态/耗时"摘要混排。已在 `OutputDisplayNode` 增加原件模块摘要标记，Blob 正式输出树显示耗时与模块状态，宿主仅对未声明原件摘要的工具追加通用摘要。全量 Release 编译0错误、6089警告、54.24秒；未启动程序、自动测试或生产探针。运行顺序、视觉尺寸、结果树展开和关闭释放仍待人工核对。\n

**VM原件优先门禁（持续有效）**：任何新的代码修改必须先登记同版本 VM 原件证据、当前生产消费者、可见/交互/数据/生命周期差异、迁移或语义实现决定及验收出口。没有 VM 源码、模板、资源、XML 或运行证据的行为只能标记"运行未确认"，不得按本方现状猜测，不得以构建、测试或截图替代 VM 对照。先完成全链设计审计，再集中实施，禁止发现一个局部问题就直接追加补丁。

本批模块结果树与当前结果表的实施门禁记录见 `evidence/VM-Blob模块结果树对照与迁移决定-20261111.json`。后续每个跨链批次沿同一格式登记，不再只在代码注释或口头说明中保留 VM 对照结论。

**2026-11-11 结果与生命周期全链设计漏通（当前最高优先级）**：用户连续反馈模块结果树与VM差异大、Blob节点选中后图像/图形/文本和结果表不一致、打开调试窗口后正式结果消失，以及选择"不保存"关闭后卡在"等待资源释放"并需要任务管理器。它们共同说明输出声明、结果表、模块结果树、publication、图像/叠加、调试上下文和插件代次释放没有形成同一全链合同。暂停继续追加局部视觉补丁，先按 `VM调试窗口体感全链设计审计-20261110.html#full-chain-gap-20261111` 完成 VM 原证→当前消费者→状态/身份→资源所有权→关闭退役的统一矩阵，再成片实施。当前问题未闭合，不能以已有构建、单个页面正常或结果表局部可见宣称目标完成。

**2026-11-11 参数绑定提升为核心闭包**：用户明确要求参数绑定完整链作为参数传递核心。已纳入同一审计矩阵 `#parameter-binding-chain-20261111`，覆盖声明、候选、引用/变量编辑、接纳、保存重开、执行快照、结果来源、失效重绑定和退订释放。现有 B08 数值引用证据只作为来源，不能把局部引用控件通过当作 ImageSource + Blob 绑定链完成。下一批实施必须把图像来源、ROI、结果显示来源和普通数值绑定按同一身份、代次和顺序合同统一核对。

**2026-11-11 插件代次租约诊断与跨链修正构建**：运行代次现在记录长期租约持有者，主窗口关闭超时提示会显示"参数视图/结果视图/调试执行/正式执行"等消费者计数，用于定位资源释放断点。调试窗口打开时保留当前正式 publication 的代码修正已纳入本批，生命周期语义定为"首次打开保留正式结果，执行参数改变后清空失效结果，调试执行后替换为私有结果，关闭后恢复正式结果"。Blob模块结果元数据已按VM证据隐藏重复Blob明细和模块耗时根字段，保留单体状态命名与当前结果表，补充合同用例。关闭链异步等待和结果工作区释放继续沿同一退役顺序。最新全量 Release 构建0错误、3302警告、17.50秒；未启动程序、自动测试或生产探针。最新构建只证明代码可编译，不能替代人工关闭和结果体感验收。

**2026-11-11 首轮人工界面反馈**：用户按C盘Release手动核对输出掩膜、枚举展开和Blob三页切换/关闭，反馈"正常了"。本次确认覆盖输出掩膜无×、枚举中文呈现及三页基本导航/关闭。图像来源候选失效、ROI绘制/继承、阈值执行、结果图层、保存重开、取消/在飞排空、性能和迟到回调仍未逐项确认，不能据此宣称ImageSource+Blob完整目标完成。人工反馈已纳入现行验收口径。

**2026-11-11 Blob执行回归修正（待构建）**：用户提供的生产结果截图显示Blob执行失败，错误为`Missing required port input.roi.output-mask`。原因是上一轮为去除输出掩膜清除叉号，将该字段错误改成Required=true，旧配置没有写入该字段。现恢复Port可选，保留VM的false默认值语义，在Blob正式输出掩膜行通过`showClearButton:false`隐藏清除按钮，普通字段的清除规则不变。用户未授权桌面控制，未启动程序或自动测试。本次全量Release因C盘DLL仍被进程占用在复制阶段失败，等待用户手动退出后重建并由用户人工复核执行链。

**2026-11-11 关闭链逆向对照与阻塞修正**：REA现有调试会话证据确认VM关闭顺序为停止连续执行、解绑运行回调与设备/通信服务、`AlgorithmTabRoot.Dispose`、`DoFinish`，参数树随后释放。当前宿主的调试窗口本身沿"取消→等待在飞→排空→Dispose"实现，但主窗口关闭时曾同步调用子窗口`Close()`后立即检查窗口是否仍存在。RuntimeToolEditorWindow的`Closing`本来就会异步等待排空，因此该检查把正常的异步关闭误判为失败，主窗口停在退出准备状态，用户只能用任务管理器结束进程。现将主窗口关闭准备改为异步等待RuntimeToolEditorWindow.CloseAndDrainAsync，并复用插件目录排空任务，超时返回可重试状态，不再无限等待。全量Release构建0错误、3355警告、23.30秒，未启动程序或自动测试。原厂动态关闭回调和设备服务实机顺序仍标"运行未确认"，不能把本方修正冒充VM事实。

**2026-11-11 工作流与执行引擎独立逆向闭包**：按用户要求，工作流、节点/连线、输入快照、执行调度、结果发布、调试会话和关闭退役从工具迁移中独立出来，形成现行方案 [工作流与执行引擎逆向闭包方案](../09-现行文档/工作流与执行引擎逆向闭包方案-20261111.html)。ImageSource + Blob 作为真实载体验证，不再把全工具落地作为引擎逆向前置。原厂证据沿 `DynamicParamsTabControl`、`BottomCommand`、`VmModule.Run`、`ModuleManager.SelfRun`、`Window_Closed`、`AlgorithmTabRoot.Dispose` 和 `DoFinish` 建立映射；原生上游准备、算法中止、设备释放和事件线程仍标"运行未确认"。

用户随后提供了基本参数页截图，画面中输出掩膜没有×，ROI区域的图像类型、创建方式、形状、屏蔽区创建和位置修正均可见。截图窗口处于较小尺寸，字体与间距按窗口整体缩放，不能据此判定原件设计尺寸差异；该截图作为人工反馈留存，完整尺寸、交互和其余链路仍按待验项记录。

**2026-11-11 输出掩膜回归处理**：用户生产截图确认上一轮Required=true修正导致Blob执行缺少`input.roi.output-mask`。该字段已恢复为可选，正式Blob行以false投影并隐藏清除按钮，保留VM开关无Unset分支的视觉。当前代码待用户退出占用进程后重新构建，未启动程序或自动测试。

**2026-11-11 用户反馈ROI输出掩膜出现原厂没有的×**：已按用户原厂截图和同版本AlgorithmTab.xml确认OutputMaskImage是普通RW Boolean、默认False、无Unset分支；本方×来自Boolean原件模板对可选Port显示ClearVisibleA，而BlobImageRoi.OutputEnabledId此前错误声明Required=false。现改为Required=true，默认值、字段身份、切换提交和执行输入不变；新增合同用例断言Required/Constant。修正源码尚未构建或运行，证据 `evidence/Blob输出掩膜清除按钮原件对照-20261111.json`、主审计roi-output-mask-clear-action-20261111节和控件台账已同步。用户手动核对项：输出掩膜和位置修正都只有开关，不出现×；保存重开后状态一致。当前Release仍可能被用户程序占用，不擅自关闭，待用户退出后再构建。

**2026-11-11 生产探针正式宿主资源覆盖补强（待构建）**：在不启动程序/测试的约束下，修改RuntimeEditorProductionProbe：初始化正式`SunEyeVision.UI.App`资源图，而不是只加载Plugin.Wpf共享资源；对ImageSource与Blob每个实际页增加可见文本旧占位/Entry全文检查、高级参数默认折叠检查、枚举展开项中文标签和打开列表不改变接受值检查。既有探针的正式节点、字段、捕获/回执、结果reader、底栏命令及关闭排空覆盖保持。当前C盘Release的Plugin.Wpf.dll仍被用户运行中的程序占用，本轮全量构建失败MSB3027/MSB3021，故探针增量未编译、未运行，不能沿用旧结果；不操作用户电脑，等待用户自行退出后再构建。总目标仍未完成。

**2026-11-11 图像来源/关闭链与集中人工验收入口**：复用原GetFrontParamItemsOperation/GetSelectedCombinationOperation/SetCombinationSourceOperation及ImageSourceGroup静态证据，确认前置IMAGE候选与接受订阅投影不等于刷新时自动首选或写值。正式ConnectionBindingPolicy记录自动连线OriginId，删连线保留重绑定原因；DeclaredFieldRow.ImageSource按候选快照、接受值、代次及退役门禁投影，候选失效不改引用。窗口关闭沿CloseAndDrainAsync→CloseDebugAsync→DisposeAsync，事件/模板来源按已有退订释放链；失败/取消/实际排空仍运行未确认，不冒充VM行为。本批未改代码、未构建或运行任何程序/测试/桌面读取。原主方案current-release-manual-chain-20261111节已合并六阶段完整人工路径：节点/来源、三页/高级、数值/引用、ROI/修正、执行/图像/结果、保存重开/取消关闭；各项未反馈均保持待验。后续按同一C盘Release收集完整反馈，运行期间不覆盖产物，不以静态映射或前批构建宣称总目标完成。

**2026-11-11 结果显示静态生产链复核**：七组可编辑图形的默认显隐/OK与NG颜色/透明度/线宽与同版本Display.xml一致，文字字号6..72步长1、透明度步长0.1有Group.xml原证。沿BlobDisplayUi→正式显示字段→AdmittedToolConfiguration/ResultDisplayFields.Capture→DocumentSession.GetNodeResultPresentation→MainWindowResultDisplay→ResultWorkspacePresenter的方案/节点/publication/帧/坐标/代次校验→NativeResultViewport原件投影复核，结果表和高亮沿同一publication借用，复用正确实现。BlobResultDesignData有设计器门禁，未发现生产因示例重复建表，不删初始化。原英文文本XML与中文截图的动态桥接、轮廓声明线宽与点集绘制关系仍运行未确认，未凭XML局部差异改画法。本批仅固定消费者与证据，代码未改，不重复构建；未运行程序/测试/探针或桌面读取。原主审计result-display-consumer-closure-20261111节、控件台账及 `evidence/Blob结果显示默认声明与生产消费者-20261111.json`同步。实际图像/二值图切换、即改即显、保存重开、节点切换、关闭释放与性能仍待整链验证。

**2026-11-11 默认值/边界/调节步长集中修复**：按同版本AlgorithmTab核对104字段的302项默认值、上下界、步长及27个范围父级，发现结果判定12组24端点统一0.01与原件不符，沿原FloatBettween_sub.SliderTickFrequency及本方DeclaredRangeRow正式消费者确认影响调节交互。现面积/总面积、质心/中心、Box宽高、周长、长短轴步长改为原件1，Box角度改0.1，显示两位小数及边界/默认值不变。原XMLMinAxisRatio两个相同MinValue=0.01已区分记录，不误作本方异常。修正后上述302项和27父级静态一致，正式参数合同用例增加步长与精度断言，编译未运行；全量Release0错误、3308警告、18.34秒。原主审计numeric-original-step-closure-20261111节、控件台账和 `evidence/Blob默认值与数值边界原件复核-20261111.json`同步。未操作桌面、未运行程序、测试或探针；实际箭头/滑条行为、性能及总链验收未完成。

**2026-11-11 ROI完整组装与正式输入静态复核**：原RadioSelecter语言列宽为中文89/英文165，Ex2明确复用同一模板；本方两槽单选硬编码110导致ROI区域类型、检测/屏蔽创建错列，已改按原件语言列宽。沿InputMaskImageGroup/ExternROITypeGroup/AssistGroup→NativeRoiUiDraftControls/BlobSupportedWindowState→BlobTool.ExpandChanges/SelectExecutionInputs→AdmittedToolConfiguration正式修改/准入调用复核，保留正确的图像模式排除输入、检测继承隐藏清源、屏蔽隐藏保留但切绘制清源、修正隐藏清叶字段及交互退役；不无证统一清空隐藏来源。真实单选选择/退役用例补列宽断言，已编译未运行；全量Release0错误、5945警告、24.59秒。原主审计roi-radio-column-and-transition-20261111节及 `evidence/BlobROI组装与正式输入复核-20261111.json`同步。未操作桌面、未启动程序/测试/探针；完整ROI实机交互、性能和生命周期仍未验，不宣称完成。

**2026-11-11 别名字段及全部枚举名称闭包**：沿BlobVmFindParameters.FromSettings/ApplyTo和AssistGroup/Assist0核对剩余9个非同名声明字段，连同上批95项共104个字段名称有原件来源；29个枚举项逐项记录原件Name/Value/IsImplemented/DisplayName，原件均可实现，没有新增隐藏或禁用。修正二值化图输出、圆形度使能、输出无序/有序轮廓及排序项目的自拟缩写，正式键、顺序、算法转换不变。当前XML与104字段标签、27范围标题、29候选标签逐项静态一致；该名称闭包不等于控件全行为闭包。最终全量Release0错误、3302警告、19.74秒；未运行程序、测试、探针或桌面读取。主审计native-alias-choice-label-closure-20261111节、控件台账及 `evidence/Blob别名字段与枚举项原件映射-20261111.json`同步；图像来源/ROI交互、结果显示、异步及性能仍须继续整链核验，目标未完成。

**2026-11-11 原件标签与范围标题集中修正**：按当前同版本Blob AlgorithmTab.xml字段bindingName/Name及原件zh-cn资源逐项匹配，校正RECT宽/高使能、轴比范围、Blob个数为0、Blob总面积等自拟文案。27个范围组按原件IntegerBettween/FloatBettween真实父节点独立迁入显示名，端点仍保留原字段名称；Blob XML增加pairLabel，沿既有FieldLayout.PairDisplayName进入DeclaredRangeRow，删除中文"下限→范围"推导，不新增参数值模型或视觉链。公共声明拒绝无PairWith或空范围标题，Blob原范围合同用例补充范围/端点名称分离和关键原厂标签断言，已编译未运行。全量Release0错误、6089警告、33.68秒；未操作桌面、未启动程序、测试或探针。原件仍把运行参数声明为普通Category，当前无删除页内标题的动态证据，保留并标明截图滚动/动态状态未确认。原主设计native-label-and-range-contract-20261111节、控件闭包台账及 `evidence/Blob标签与范围组原件映射-20261111.json`已同步；整体体感、交互及性能待用户后续手动核对，总目标未完成。

**2026-11-11 枚举样式集中修复构建**：用户确认已退出程序后，执行当前worktree全量Release构建，0错误、5945警告、22.23秒。下拉条目共享模板与归档原件ControlTemplate XML逐字一致；正式枚举行不再继承宿主隐式ComboBoxItem样式，禁用及说明扩展保留。图像来源仍继承NativeInputFactory内的原件样式。已补充"宿主隐式条目模板存在时，正式弹出项显示中文、无Entry全文、选择写回正式枚举键"用例，并与ROI默认投影、构造失败清理、底栏父级/命令用例一起编译；全部未运行。证据与本次Release控件DLL哈希见 `evidence/Blob枚举弹出项原件样式修正-20261111.json`。未启动或操作程序；待用户手动核对阈值方式、极性、轮廓类型的展开列表、选择回显和关闭重开。此结果仅为本批构建通过，不代表Blob整窗或总目标完成。

**2026-11-11 用户停止桌面控制（最新约束）**：用户明确要求"不控制电脑，需要什么由用户手动测试反馈"。立即停止窗口读取、激活、点击、程序启动等全部桌面操作；后续仅做源码/原证分析、代码修改、构建及测试用例维护，不运行自动测试。上批底栏循环修正后全量Release0错误、5945警告、22.52秒；重新打开的C盘进程及Blob运行参数页已由此前授权范围内的窗口清单和可访问性树确认，窗口不再停在原错误对话框，但不得据此宣称三页和全部生命周期通过。用户新截图确认阈值方式/极性弹出项显示Entry对象全文：本方枚举模板为支持不可选项覆盖ItemContainerStyle，错误继承宿主隐式ComboBoxItem样式，退出原件条目模板。现从已有原件SourceComboBoxes样式提取同一条目模板为SourceComboBoxItemStyle，原ListViewComboBoxStyle及枚举扩展样式共用，保留禁用/提示绑定、ValueA身份和DisplayNameALang显示。该增量XML语法已检查，尚未重新构建、运行或人工核对；不操作仍运行的程序。

**2026-11-11 C盘正式窗口阻断定位与修正**：已成功启动本worktree Release，原生窗口清单确认完整C盘路径；实际错误对话框与用户截图一致，不能再把问题仅归于D盘旧产物。实机同时暴露ROI区域类型原整数单选声明拒绝、未完成字段行清理的空宿主委托异常，以及WPF逻辑树深度异常。现已修正显式默认值单选的编辑器Required投影（正式可选Port和写入回调不改）、构造早期失败清理的空检查、底栏Reset内容错误引用祖先框架的父子循环（原件模板直接绑定祖先命令/可用状态）。补充三项生产消费者/真实窗口回归用例，未运行。首次构建因运行进程锁DLL失败；用户已明确关闭程序，随后单选/清理修改全量构建0错误、3349警告；底栏修正后的最终构建正在进行，三页尚未重新核对。原主设计的release-window-startup-failures-20261111节记录归因和边界，不宣称整个窗口完成。

**2026-11-11 当前实机与启动核对**：用户明确允许启动当前worktree的Release，仅核对Blob三页界面，不运行自动测试或算法；该授权覆盖下文历史"不启动程序"表述。用户手动启动并确认的路径是 `D:\MyWork\SunEyeVision_V2-param-window-datatree\Release\SunEyeVision_v1.0.0\SunEyeVision`，原生窗口清单亦确认该路径；它不是本C盘worktree产物。C盘Release最新全量构建0错误、3385警告，尚无成功显示的C盘窗口证据。启动源码确认SingleInstanceCoordinator按Windows用户与Session标识单实例、不按安装目录区分；第二个实例会向已有实例转发激活并退出，符合本次C盘进程短暂出现后退出的现象。不能把D盘界面当作C盘构建的视觉验证；保留用户D盘会话，不擅自关闭或丢弃未保存内容。后续需退出D盘实例后再打开C盘正式Release核对，不使用隔离测试模式冒充正式启动。

**2026-11-11 用户实机纠偏（最高执行优先级）**：用户反馈工作11小时后，打开程序的Blob分析UI仍无改善。最近推进集中在图像源存图、诊断和资源接缝，不能算作Blob整窗原厂化交付。停止这些旁支的新增扩展，返回用户三页原图与实际Blob窗口的完整差异归因及集中实施；先核对正在使用的产物，再沿入口、组装、资源、模板及消费者查明可见差异。当前进程路径未核实，CIM读取拒绝访问，Get-Process未取得SunEye进程；不得据此归咎用户启动旧版本。保持不启动测试或程序的要求，可观察用户已打开的窗口。此前构建成功不代表UI改善。存图逻辑引用身份修补尚有本轮未最终构建改动，不沿用上一轮编译结果覆盖；保留为进行中，不再扩展其功能范围。

| 事项 | 状态 | 关键结论 |
| --- | --- | --- |
| 统一开发基线 | 已建立 | `main` 指向合并提交 `b895d697e`（双父：重构分支 `f5e6b8fde` + 旧 main `d44904f38`，164 个旧提交全保留）；回退点 `backup/main-before-sync`；`origin/main` 尚未同步 |
| 数据目录启动门禁 | 实施中（代码未修） | 已定案：数据根写程序目录 `App.ini` 的 `[Data] Root`，不弹「选择软件数据目录」；退出 `data-location.json`、`suneyevision-data.json` marker、`schemaVersion` 协商 |
| main 控件原件回补 | 分批进行 | 按原件映射表回补 55 个控件提交 + 72 个验证夹具提交，不做逐文件人工合并 |
| 工具身份合同 | 已定案 | 发布态才有 `release-payload.json` + `ToolGroups.xml` 强准入；开发态 `dev\\Debug\\plugins\\<Tool>\\` 只有 dll/pdb/deps.json |
| 旧 Plugin.SDK 退役 | 已完成 | 现行解决方案消费者 0，工具消费者 0，源码实时扫描 0，`src/Plugin.SDK` 目录条目 0；门禁证据见 `docs/collaboration/evidence/PluginSDK退役门禁-20261108.txt` |

## 2026-11-10 参数页原件化重构（任务卡：一次性执行完，不再调研）

**目标**：参数页行内容改为原件模板驱动（XAML DataTemplate + 模板选择器），删除自研逐字段 C# 控件构造；完成后统一验证，再交人工验收。**执行时不做新的调研与决策，照卡施工；每步构建闭环。**

**新增最高验收门禁**：调试窗口整体使用体感必须沿 VM 原厂完整链核对。入口、首屏、页签切换、模板物化、引用/ROI 交互、提交回执、关闭排空和资源释放分别记录架构、组装时序、控件映射三类差异。局部测试通过不等同于体感达标；没有 VM 同版本动态证据的行为标记"运行未确认"。

**用户最新纠偏（优先于下文历史施工与测试安排）**：现有测试通过但成品与 VM 实现和体感差异较大，说明现有用例不足以证明迁移一致性。先完成全链设计差异归因，再集中实施；预期行为必须可追溯到 VM 同版本源码、模板、资源或运行证据，不能从本方当前实现反推预期。覆盖布局与组合、默认与联动状态、鼠标/键盘/焦点、引用及 ROI、切页状态保持、算法输入输出与边界、取消关闭及响应耗时；性能绝对门限只证明本方预算，不证明 VM 等效。按用户最新要求不启动测试或程序，保留完整验收用例和未验状态，实施完成后由用户统一人工验收。不得以构建成功、绿测或文档齐备替代原件一致性结论。

**Blob 闭口口径补充**：本批的 Blob 交付单元同时包含 UI 与算法双闭包。UI 的窗口、参数页、引用、ROI、运行参数、结果显示和生命周期，必须与 Runtime 的输入快照、阈值/软阈值模式、检测/屏蔽区域、位置修正、筛选排序、输出与结果 reader 使用同一字段身份、资源身份和代次合同；不能把"Blob Runtime 测试通过"或"窗口能打开"单独记为 Blob 全量完成。VM 算法动态未取得的部分标为"运行未确认"，不以 HALCON 结果猜测原件语义。

**两工具共用链路**：用同一方案中的 ImageSource → Blob 验证后续工具的架构接入基线。整链从工具准入、工具箱创建、节点样式及交互、连接和来源订阅开始，贯通设置工厂、三页原件组装和 ROI、执行捕获、阈值算法、结果 reader 与图像显示、底栏命令、保存重开、取消关闭和资源释放。每项追踪到正式生产消费者，并区分本方回归、原件静态对照和人工体感验收。

**用户再次确认的交付目的**：以 ImageSource + Blob 的完整生产链奠定后续工具开发基础。公共合同、声明驱动控件、执行与显示、编辑和资源生命周期必须通过这两个实际消费者贯通；不以两个独立窗口能打开代称架构完成。原主设计已补充两工具架构基线及后续复用要求。本次仅固化范围，未新增代码、构建或运行结果；不启动测试、程序和探针的最新要求继续有效。

**模块状态出口实施（2026-11-11）**：已按归档 Process 入口置0、统一出口按返回值置1的链接入 ImageSource 的 Int32 模块状态。特别保留采集失败计数写入成功后模块状态1、宿主仍Failed且无图/SN的区别；处理失败0、处理成功1，准入/身份/取消/抑制不造值。原件 CustomVisible=false 通过公开输出显示元数据贯通声明预览、正式快照和publication，值仍可由reader读取，不新增可见参数行。原证哈希、条件、消费者及未确认项见 `evidence/ImageSource模块状态出口与消费者-20261111.json`；新增用例未运行；最终全量Release 0错误、3302警告、13.90秒，命令和源码哈希已记入同一证据。原生错误码全映射、原件动态、完整窗口体感及方案存图等剩余项不因本批收缩。

**拼接原码实施（2026-11-11）**：归档 StitchImageOutput 与本机安装DLL静态指令共同确认，历史宽度不符及裁剪高度越界返回0xE0000502；外部clear-width-rejected/camera-invalid-stitch记录作交叉支持。现沿工具内部处理异常→Diagnostic可空NativeErrorCode→同一失败输出/快照→publication传递，宿主诊断Code仍独立。采集、准入、身份、未分类读图/内存/清理异常不套码，不将原码缺失写成0。已补本地宽高拒绝、相机拼接、非映射异常及快照/publication原码保留用例，未运行；全量Release 0错误、6087警告、23.01秒。原件正文、当前安装DLL哈希/指令及消费者见 `evidence/ImageSource拼接错误码原证与消费者-20261111.json`。未启动测试、程序或探针；完整错误码表和整链动态验收仍未完成。

**目录初始列表实施（2026-11-11）**：核对方案存图消费者时确认归档SetParam在接纳SubscribeFolderPath后即更新列表，本方投影却总置空且普通刷新清空回执列表。现初次绑定/目录接纳变化复用正式取图的纯过滤与自然排序，来源/目录/订阅字段变更戳区分重置；普通刷新保留行，正式回执继续遵守RefreshFolder。没有新增watcher、取帧、SN或游标推进；目录错误走既有呈现错误回调，退出恢复手工列表，回调退役拒绝安装。新增实际UI会话用例已编译未运行。首次编译漏IO命名空间已修正，最终全量Release 0错误、3302警告、14.28秒；未启动测试、程序或探针。原件摘录和消费者见 `evidence/ImageSource目录初始列表原证与消费者-20261111.json`；方案存图仍未实现，原机初始选择及完整体感仍未确认。

**存图开关扩展依赖（2026-11-11）**：发现SolSaveImageData使用ExtensionParamUserControl的BooleanTips外部字典，其提示是原开关旁的状态文字，并非普通Boolean的标签ToolTip。现已将真实模板、既有BooleanControl/两张switch图、原本地化绑定、提示布局和FieldRowModel已接纳值投影接入统一模板链；普通布尔与带提示布尔均在拒绝提案后重发接纳值，修正TwoWay目标可能残留新状态的问题。冲突声明在物化前拒绝。固定安装件SHA/MVID与原件XAML/模型/字典哈希，见 `evidence/BooleanTips原件模板与消费者-20261111.json`。实际控件初值/切换/拒绝/退役及声明用例已编写未运行；最终全量Release0错误、3302警告、14.17秒，命令和源码哈希记入该证据。ImageSource生产存图字段和完整持久化消费者尚未接入，没有放出假开关；本批是已查明依赖的迁入，不代表方案存图完成。

**显式列表存图生产实施（2026-11-11，进行中）**：已将local.save-images接入原件BooleanTips、公开资源声明/指纹、保存副本捕获、编码附件校验、候选准入、会话资产拥有者及统一路径解析；图像条/正式输入/私有调试沿既有解析器消费，Runtime与Blob无新增资源模型。原地址与内容共同确定身份，同一外部文件一次捕获只读一次；前120项收集附件，余下独立文件保持外部引用并由现有保存回执提示。关闭存图只在输出副本恢复外部地址，缺源有保存回执；保存/自校验不落临时文件或改活字段/历史。候选完整准入后、发布前统一写出附件并持有读锁；既有输入排空后关闭读锁、清空编码载荷及删除自有目录。新增损坏/拥有者/数量/预算/转移释放、缺源保存不替换文件、跨目录重开与关闭存图、实际BMP图像源→Blob用例，均已编译未运行。最终全量Release0错误、3302警告、16.07秒；未启动测试、程序或探针。证据 `evidence/ImageSource显式列表存图生产链-20261111.json`。目录运行列表快照尚未接入，当前在存图捕获明确拒绝该模式；跨方案复制资产转交、性能及完整视觉/交互仍待核，不能据此宣称方案存图或总目标完成。

**统一验收测试包口径**：实施阶段不逐点启动测试；完成整批代码后一次性运行。测试包必须覆盖：ImageSource + Blob 打开/首屏/重复 Tab/换节点/保存重开；字段编辑、枚举、数值上下界、范围双端、引用候选/清除/变量、ROI 本地/继承/位置修正；执行/取消/结果 reader/结果显示/底栏命令；窗口关闭、退订、在飞任务排空、迟到回调拒写；以及首屏、首切页、重复切页、整页物化的性能阈值。测试通过仍只是本方行为门禁，必须另有 VM 原件静态映射和截图/运行对照，不能以绿测替代 VM 体感验收。

**已定结论（证据齐，勿重查）**：
- 原件机制：行内容由 `MyDataTemplateSelector`（`x:Key="mydt"`）按参数类型从 `paramusercontroldatatemplate.xaml`（3995 行，键：`Default/Boolean/BooleanCheck/AddButton/TextLeft` 等）选 DataTemplate 实例化；原件列表/行壳 `ParamListView`/`ParamUserControl` 已迁移到我方 `Themes/Native/Composition/ParameterPresentation/`。
- 我方缺陷：`DeclaredFieldEditor`（`src/Plugin.Wpf/Fields/DeclaredFieldEditor.cs`，506 行）构造即 `Render()`（:167），内建 `NativeReferencePresenter`（:208）；范围行 `NativeRangePresenter` 每组再组合两个输入。实测：编辑器 16ms/字段、范围 22ms/组；Blob 运行参数页 35 字段 rows=1076ms，结果显示 52 字段 rows=1770ms；三级计时（`LastEditorCtorMs/LastRowAssemblyMs`）可由探针复测。
- 已完成的准备（勿回退）：判定组已归位结果显示页（F1，测试 45/45）；死代码草稿面板已删（`NativeThresholdUiDraftControls/NativeUiDraftRange`）；空闲预热已删（按原件按需建页）；`NativeDeferredContent` 已加（行内容延迟实现，模板化后删除）；`ParamListView.xaml` 已启用 Recycling 虚拟化 + 分组虚拟化；性能埋点保留（PROBE-KEEP）。

**实施步骤**：
1. 迁移 `docs/07-工具逆向/decompiled/XmlUi/styles/paramusercontroldatatemplate.xaml` → `Themes/Native/Composition/ParameterPresentation/resourcedictionaries/ParamRowDataTemplates.xaml`；先只保留 Blob 实际用到的模板族：`Default`（数字/文本）、`Boolean`/`BooleanCheck`（开关）、范围组合（IntegerBettween/FloatBettween）、枚举下拉、引用链接；其余模板不入库（YAGNI，等其它工具接入时再补）。
2. 迁移选择器与依赖转换器：`MyDataTemplateSelector` + Blob 用到的转换器子集（`FgConverter/WidthConverter/HeightConverter/Boolean2VisibilityReverse/EqualConverter/NumCompareConverter` 等），命名空间归 `SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation`，数据上下文改绑 `DeclaredFieldState`+`FieldPresentation`（值/校验/引用提交逻辑保留在 C# 状态类，视觉全部进模板）。
3. `DeclaredFieldsView` 行内容改由选择器生成：`BorrowRow` 的 `view` 参数改为模板宿主（ContentControl + ContentTemplateSelector），删除 `new DeclaredFieldEditor(...)`/`new NativeRangePresenter(...)` 的视觉构造调用。
4. 删除：`DeclaredFieldEditor` 的视觉构造路径（状态/校验/提交/引用逻辑改为纯逻辑类保留）、`NativeRangePresenter` 视觉组合、`NativeDeferredContent`。不留双轨。
5. 构建闭环：`dotnet build SunEyeVision.slnx -c Release --no-restore`（注意：测试须先构建再 `dotnet test --no-build`，否则 SHA 封签 mismatch）。
6. 统一验证：`dotnet test`（Blob 45/45 + 全量 UI.Tests，已知既有失败白名单：EditorWindowSessionTests.Actual_tool_editor、AboutWindow/BrandingLogo 主题资源、ShellLayout 载荷根）；剖析程序复测 `dotnet run --project tests/RuntimeEditorProductionProbe -c Release --no-build -- <仓库根>`。

**验收标准（人工验收前必须全过）**：单字段 ≤5ms；Blob 运行参数页 rows ≤300ms；开窗 ≤600ms；切页 ≤150ms；外观与原件截图一致（`docs/collaboration/evidence/VM_BLOB_USER_UI_SS01~08_20261026.png`）；测试白名单外 0 失败。完成后输出统一验证报告 + 人工验收清单，等用户验收，不自动进入方案测试技能。

**2026-11-09 GitHub 版本同步、Release 构建与启动验证**：本地 `main` 从旧基线 `9603e18d` 快进同步到 GitHub `origin/main` 的 `821858528aa60e4a9d502dfb8d1726bdd97a9e09`，同步前创建备份分支 `backup/main-before-github-sync-20261109`（备份提交 `9603e18d4429afe8db01ca84783356b05c816c2b`）。全量 Release 构建（先补 `dotnet restore` 再 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1`）成功，产物位于 `Release\SunEyeVision_v1.0.0\SunEyeVision\`；启动 `SunEyeVision.UI.exe` 验证通过，窗口显示"SunEyeVision · 起始页"，工具加载由用户人工确认正常。`log4net 2.0.17` 的 `NU1902` 警告及原有 nullable/XML 文档警告未作阻断项，本轮无编译错误。

**2026-11-10 一次性批次回归**：全量 Release 构建 0 错误；Blob Runtime `45/45`。UI 三项 Blob 调试窗口用例中 1 项通过，2 项失败，分别暴露 ROI 视图中重复引用文本控件和正式提交时数值范围校验失败。该结果只作为整体修复输入，不记作全链完成；生产探针需在下一轮整体修复后重新执行。

本轮按 VM 对照先修正两处设计边界：数值行模板同时把业务范围投影到原件控件的 XML 范围属性，避免 `ThresholdSoftness=254` 被控件默认上限 100 拒绝；结果页移除"基准点/运行点"旧自研显示行。历史判断曾将默认截图未显示解读为无对应声明，后续静态复核已更正。按用户要求，本轮不启动测试。

随后又清理了 Resource 字段的自研资源文字占位：资源端口按 VM 引用模板分类，图像来源仍由来源组组合槽承载；本轮完整 Release 构建复核 0 错误（3298 个既有警告）。测试、生产探针和人工 VM 视觉验收均保持未执行。

参数闭包续查发现：轮廓类型在 Freeze/ReadSettings 中被布尔开关覆盖，ApplyTo 又将类型合并回开关；已依据原件 BlobFindParam 独立字段声明改为分别往返，并编写六种组合的参数物化/冻结回归用例（未运行）。已移除窗口对 Runtime 已消费的轮廓输出和结果判定的过期拒绝；位置修正、本地 ROI 组合及最大内接矩形的真实缺口仍保留，未宣称接通。设计审计已同步差异、源码定位及证据边界。本次增量未构建、未启动测试或程序，上一轮构建结果不能覆盖本次增量。

ROI／Assist 整链静态复核已定位结构性差异：VM 按信息选择 FIXTURE 组合、按点选择 POINT 组合、按坐标选择 float 来源；本方把按信息展开为 Affine2D 内部字段，另两方式为不写草稿的 ui-only 字段。VM InputMask 另为 IMAGE 组合，并非本方 Region 端口。原件八个相关组及两种输入组合已按文件哈希固定在 `evidence/VM-Blob组装与输入组合原件摘录-20261110.xml`，设计审计记录完整替换边界；需沿同一正式输入合同实现三方式及掩膜消费，不以局部控件去重或移除保护代替。SetCombinationSourceOperation 已核到 RelateIO/RelateIndex 写入，关闭的 CurValueA setter 副作用还需查证。本轮未启动程序、测试或构建。

Assist 绑定语义续查：已确认 CurValueA→OnPropertyChanged→TriggerBusiness→Trigger→SetCombinationSourceOperation 的置空解除链（值变化、触发未抑制、Owner/目标 IO 有效为条件）；GetSelectedCombinationOperation 的初始化是无触发读回，并区分直接订阅和派生只读来源。原设计审计新增八项原件行为验收合同及宿主复用位置。十项修正输入的算法缺省值、旋转/尺度顺序仍未确认，不推定等于现有仿射矩阵；未执行运行验证。本工作区实际仍为 c5e8901df detached HEAD，指定 feature 分支被 D: 原 worktree 占用；本轮没有切换、修改该工作区或 main。

本批模板物化继续实施：输入页本已强制仅引用，但 BuildSlotContent 按 Constant Record/Unset 再造子控件，越过了选择器。现统一以已选模板决定视觉路径，已注册模板的全部值状态不再进入 C# 槽位构造；显式组合槽、图像来源及区域来源保留原入口。由此修正引用记录内部字段展开和普通未设置字段覆盖 XAML 模板的问题。三方式及掩膜的原始语义缺口仍未闭合；旧记录引用的 D:/MyWork/REA逆向测试 两份 B08 定点补证原文在本机路径不存在，未将历史摘要当作取得原文。未启动构建、测试或程序，主设计同步实施边界。

交互接线续批：IntegerRadio 仅改变外观别名但值投影/Checked 接纳识别的是 RadioSelecter，因此统一解析到 RadioSelecter/RadioSelecter3，移除额外包装模板；图像来源重配增加旧选择器退订、代次及退役检查，防止来源回调重入后旧控件重新安装。新增 ParameterTemplateInteractionTests 覆盖选择写回、退役拒写、来源重入替换（未构建/未运行）。同时发现并确认 DeclaredFieldsView.Assist 已有十五角色、批量清除与完整性逻辑，正式 options 接线亦已存在；Blob 尚未消费，不能再称缺公共 Assist 能力。主设计已更正复用决策，后续接入现有声明树而非新建控件。

上述集中批已全量 Release 编译通过（0 错误、5996 警告、28.66 秒；带 HalconSourceRoot，命令/源码哈希见 `evidence/参数模板集中实施-Release编译-20261110.txt`），没有启动测试、探针或程序。已定位迁移后的逆向仓库 D:/MyWork/Rea逆向，核收 FIXTURE 字段/缺省尺度与 Blob ROI 分支的既有报告，原文及哈希见 `evidence/Blob-迁移后逆向仓库定点回件-20261110.json`；四个尺度公共缺省为 0，不可沿用示例默认 1 冒称原件；实际变换公式、零尺度执行策略和掩膜像素规则仍未证。报告引用的 native C 原文件在迁移路径缺失，未把报告误记为重新反编译或运行通过。

模板生命周期续批：修复 StringConfig_sub 与 ButtonSelecter_sub 共用 SubDataA 导致文本会话覆盖引用会话的问题，两个模板分别绑定 TextDataA/ReferenceDataA，仍共用唯一字段状态；引用输入随模板物化重新挂接，文本弹层按自身会话定位。关闭挂起与子行释放纳入异常聚合清理，单项失败不跳过其他释放。新增三次模式切换、实际控件 DataContext、常量恢复及释放投影清空用例。此增量尚未构建/运行；不沿用上一构建结果宣称通过。完整 Assist/FIXTURE、图像掩膜及整窗 VM 对照仍未完成。

按用户要求启动全部 Blob 消费控件的原件复核：原 AlgorithmTabRoot 递归展开 9 个 GroupLinkItem 引用组，固定 247 条命名声明（含页签、分组、控件与条目，不是独立控件数或完成数），原文件哈希/层级/操作名见 `evidence/Blob控件原件声明展开清单-20261110.json`。已重建 `docs/03-测试报告/2026/11/Blob控件依赖闭包台账-20261110.html` 的现行结论和完整消费者清单，旧"参数页无需动作、pending 均无关"的推断转入历史保留。用户提供的 D:/MyWork/Rea逆向/docs/reverse-plan/index.html 已读作参考，并核对 UI-C05–09/C12 关联正文；目录加载和 FrontendEdit 设计器证据不得转签为 Blob 参数窗证据。模板资源/代码继承与事件的逐项闭包仍待核，不以声明枚举完成代称全控件复核完成。

底栏连续执行实施：依据 BottomCommand 静态源码，将硬编码禁用的连续命令接入 DocumentDebugSession 串行循环，复用正式捕获/执行/结果接纳；停止取消、关闭等待单次及循环排空，回调重入取消后禁止新捕获，重复 Loaded 不重复订阅。补充真实输入准备期间停止/重启、关闭拒写、准入重入用例（未运行）。全量 Release 编译0错误、6028警告、32.68秒，覆盖本次及上一文本/引用模板增量；证据见 evidence/连续调试与模板会话-Release编译-20261110.txt。宿主 await 完成后300ms与VM独立轮询的实际相位不保证一致，未宣称动态等效。主设计、控件台账同步；未启动测试、探针或程序，Assist/FIXTURE及图像掩膜等剩余范围不变。

Assist完整输入树续审取得新的原件正文：迁移仓库 docs/architecture-recovery 的 VmIO.RelateIO、VmModule.Subscribe/UnSubscribe 证明组合递归写叶，组合读取由叶重建，混合来源按只读摘要呈现，索引递归；证据固定 evidence/Assist组合订阅递归原件证据-20261110.json。主设计修订为十叶唯一来源事实、根/点选择投影和原子接纳，不能直接将已有十五独立字段组合视为已等价。当前正式输入与设置视图分离的跨边界提交亦已定位。另核到 BlobSession 仍合并轮廓类型输出门控且 ReadBoundary 无模式参数，参数往返修正不等于有序/无序算法闭合。此轮为完整设计与证据进展，未修改产品、未运行测试/程序或重复构建。

活动XML轮廓链清理：确认原件 BolbOutLineEnable 控件处于注释，正式窗口仅有三值 BlobContourType；同步删除本方 OutputBoundary 声明/设置/旧布尔投影与执行门控，消费者统一枚举，顺序改为不输出/无序/有序。此前六组合独立往返结论由本次活动树证据替代，原历史保留。全量Release编译0错误、3308警告、23.94秒；未启动测试或程序。集中核对六组枚举，五组声明顺序对应，排序特征仍缺原件五项且现有选择器拒绝负键，缺口已列主设计和 Blob六组枚举原件对账-20261110.json；有序/无序提取语义仍未完成。

用户新增原厂视觉证据：9张Blob三Tab截图，明确"原厂3个tab展开高级参数的截图 默认是不展开的"。已原样归档 evidence/VM-Blob原厂三页截图-20261110/（含SHA256清单与HTML图集）。默认高级参数收起；展开截图用于完整内容/滚动位置对照，不作为默认首屏。跨Tab或重开是否保留展开状态仍未确认。排序续批补齐五项声明与同一候选特征消费者，角度容量选择允许有限负键；原件参数注释定义Box角度/宽高、二阶中心矩角度、Box短长轴比，测量沿既有HALCON适配，非VM数值等效声明。新增13特征生产链及正负角度限数用例（未运行）。首次编译发现RequireKey签名遗漏已修，最终全量Release 0错误、3308警告、15.39秒；未启动测试/程序。

原厂截图组装纠偏实施：NativeAdvancedPresenter原本默认false，问题为XML把高级字段放入普通组。现运行页19个普通声明/38个高级声明，结果判定10个普通声明/36个高级声明（均含条件字段，不是同时可见行数）；保留全部104字段身份。填充阈值移至查找个数后，轮廓类型移至面积范围后，Blob/二值图像随后；圆度/矩形度移至长轴后、质心偏移前。结果页从质心X起归高级判定。同步将BlobRunUi过期8项排序准入改为正式枚举，避免新增5项仍被UI拒绝。补充原厂三页分组/顺序用例，未运行。全量Release0错误、3308警告、17.13秒；未启动测试、探针或程序，实际WPF外观待统一验收。

三页数值声明续审：固定Blob参数数值声明对账-20261110.json（实施前原始字符串快照，十六进制/小数写法差异不代表数值差异）。活动XML确认六处真实默认值差异，现XML及BlobVmFindParameters同步改为矩形宽/高100–200、周长/长轴下限10，稳定字段ID不变；新建及正式参数往返用例已编写未运行。原证见Blob六项筛选默认值原证-20261110.json。轴比上限最小值、Blob个数上限最小值涉及父级范围约束，未据叶声明贸然改变。此增量未构建，不沿用上一构建宣称覆盖；未启动测试/程序。

范围父子约束续审：原件IntegerBettween/FloatBettween模板分别绑定父级Minimum/Maximum及左右叶极值；数值上下箭头还使用动态对端值，整数启用XML极值交集。证据固定Blob范围父子约束静态核对-20261110.json。因此轴比上端/数量上端XML最小值0不直接等于常量编辑可接受0，本轮未改这两字段。另记录当前DeclaredRangeRow以端点包络推父级范围，未显式保留原件父级元数据；引用/常量切换及反向范围仍需完整核对，不能以Runtime反向面积支持替代UI原证。六项默认值增量仍未重新构建；未启动测试/程序。

范围父级合同实施：在现有FieldLayout补充PairMinimum/PairMaximum（仅范围起点可声明完整有限父级上下限），Blob XML全部27个PairWith按原件父级提取并固定映射；DeclaredRangeRow将父级值和左右叶值分别传入原件SliderArrange。同步保留轴比上端/数量上端叶最小值0、父级分别0.01/1，不再靠端点包络推原件父范围。104字段身份不变。补充27组与父子差异用例，未运行。全量Release 0错误、6085警告、26.65秒，覆盖本批及此前六项默认值增量；未启动测试/程序，常量/引用切换与反向范围的真实交互仍待验。

用户明确节点是工具测试载体。验收从真实画布的 ImageSource → Blob 开始，覆盖创建和样式状态、选择拖动及连线、调试窗身份、提交执行与结果回显、换节点、复制删除与撤销重做、保存重开和关闭释放。现有独立编辑器探针继续作为控件与性能证据。扩展真实节点会话用例，核验 Blob 执行后切换到 ImageSource 的会话代次和旧提交拒绝。并行只读复核继续。当前实现按原件修正六种阈值模式可见集合、排序准入、总体及逐体判定合成和窗口重新加载时的底栏绑定。全量 Release 0 错误、3308 警告、15.66 秒。运行验证待用户统一验收。隐藏引用、重置运行参数、图像变量来源、15组判定精度、FIXTURE和掩膜仍待闭口。

用户明确首批工具相关完整链路均纳入复核，包括图像源控件、阈值关联图像和结果显示。扩展真实节点会话用例，对同一上游图像调整低阈值并分别执行，核对二值图、Blob 图、数量及结果发布身份，随后切回基本参数并继续验证预览与切节点退役。全量 Release 0 错误、3301 警告、14.67 秒。画布实际叠加、结果图像控件交互和原厂视觉对照待验，测试和探针未运行。

用户明确用 ImageSource 和 Blob 打通可供后续工具复用的完整架构。验收按图节点与宿主身份、声明和参数控件、输入资源、执行与取消、输出发布、画布和结果消费、保存与退役分层。Blob 算法规则留在 Runtime。按原件 LimitJudge 15 组映射接入整数、float32 和 double 边界规则，保留有限值策略与现有 HALCON 总面积来源。新增质心阈值内外用例。全量 Release 0 错误、3308 警告、15.46 秒，运行待验。节点只读复核发现边框转换器绑定类型不符和普通节点运行状态缺口，后续需落实。

节点复核确认原件 DiagramItem 在命中且工作区未锁定时才挂端口。WorkflowNodeView 已接入当前流程运行和主窗口 CanEditWorkflowCanvas，禁用、运行、参数窗锁定及方案切换时隐藏端口。旧评审的边框参数类型结论已纠正，实际 XAML 用 CachedNodeBorderConverter，该转换器接受布尔 IsSelected。原件 NORMAL StateFlag/errorBorder 的运行、配置错误、禁用与选中优先级仍待迁移。全量 Release 0 错误、3301 警告、15.04 秒，运行待验。

运行参数重置按钮已接入正式草稿。确认后从当前节点准入定义获取运行页默认值，以单一提案替换常量及该页订阅，范围双端按完整有序提案验证。通过现有字段写回和读回链，不新建控件，执行或窗口退役时禁止。原件 ResetRunParamBind 遍历全部页的 ISubInfo，当前接入边界为运行页，跨页订阅与原厂 ResetParam 实际覆盖仍待对照。全量 Release 0 错误、6028 警告、19.93 秒，未运行测试或程序。

阈值订阅休眠已接入正式执行及节点调试。Blob 共用阈值活动字段定义，隐藏订阅保留在持久配置，准入冻结执行默认值，仅执行快照替换且跳过绑定解析。模式恢复时按正常准入检查来源。已移除旧窗口隐藏绑定拒绝。新增正式会话用例验证隐藏绑定不读取、原配置保留、激活缺失来源拒绝。该策略为宿主语义适配，原厂动态行为未确认。全量 Release 0 错误、6085 警告、21.85 秒，测试及探针未运行。

图像来源正式捕获时重新核对候选，已接受绑定保持原身份。来源移除时拒绝捕获，来源恢复后可再次捕获。下拉选择器代次在捕获刷新前后验证。新增移除与恢复用例，验证刷新不写入字段。当前图像变量仅具备 StoredRaster 持久声明，VariableValueDeclarations.TryGetPort 和执行绑定尚无资源租约接入，变量路径待完整实现。全量 Release 0 错误、5990 警告、18.42 秒，测试及程序未运行。

Blob 图像 ROI 分支已接入正式模式和屏蔽图像输入。复用原件 IMAGE 选择器，模式切换保留两支配置，图像模式只捕获主图与掩膜并停用几何和位置修正。HALCON 本方适配为同尺寸 Gray8 掩膜有效域内非零像素选取检测区域，原厂像素规则未确认。结果几何不将图像掩膜报告为全屏。新增掩膜像素和面积用例。全量 Release 0 错误、5997 警告、19.53 秒。实际界面、保存重开和自动用例待运行，输出掩膜、几何屏蔽字符串及 FIXTURE 仍待闭口。

Blob 输出掩膜从 UI 草稿开关替换为正式输入配置，默认关闭。打开后在同一结果资源范围发布 OutputMask 四成员 IMAGE 记录。当前 HALCON 语义取实际参与阈值的检测域，像素为 0/255，与阈值后二值图分开。原厂像素与输出时序仍待对照。新增开启关闭及掩膜和二值图差异用例。全量 Release 0 错误、5997 警告、21.91 秒。测试、程序与探针待统一执行。

轮廓三态已接入正式结果和画布。无序模式发布 HALCON 内边界像素点，有序模式保留路径与孔洞，关闭模式默认不输出。画布统一读取显示点集，缺失时跳过轮廓层，独立调用显式请求边界仍按现有输出需求处理。原件 ContourInfo 二进制点集编码及点顺序未确认，当前取点为本方适配。新增三态用例，全量 Release 0 错误、5966 警告、20.36 秒。测试、实机和探针未运行。

画布节点已接入正式执行结果状态。DocumentCanvasProjection 订阅当前会话结果发布和执行失效事件，通过短期 reader 投影通过、业务NG、失败、取消等状态，退出时退订。普通节点复用原件调色板 OK/NG 颜色，失败与业务 NG 分开，禁用隐藏标记。现有真实 ImageSource→Blob 用例新增状态及失效断言。配置错误虚线、原件精确裁剪及调试私有状态对照仍待实现。全量 Release 0 错误、3303 警告、14.32 秒，测试及探针未运行。

位置修正执行前置已补齐。Blob 本地/继承几何支持现有 Affine2D 正式输入，显式区域先在无隐式裁剪作用域生成，再修正检测和屏蔽区域，最后与目标图像有效域相交。全域模式保留目标图像有效域。新增画外矩形平移进图用例。此为 HALCON 宿主适配，原件 FIXTURE 十叶、三方式共享订阅、坐标公式及 UI 接线仍待实施。全量 Release 0 错误、3312 警告、15.57 秒，未运行用例。

首批以实际连线的 ImageSource → Blob 作为后续工具的参考生产链。RunChainProbe 移除测试端手工注入图像来源，改为断言正式连线自动产生的流程、节点、输出端口、图像成员和连线归属，并在保存重开后再核对。探针后续的私有调试、正式结果 reader 和取消使用该绑定。位置修正"按信息"输入从 Affine2D 改为 VM XML 所示的 FIXTURE 两姿态十项 Float32 记录，执行入口按已有 REA 托管语义转成图像坐标仿射。旧仿射字段不再作为方案输入，首批两工具没有 FIXTURE 生产者；按点、按坐标、共用叶订阅与关闭开关时整组解除仍未完成。全量 Release 0 错误、6085 警告、23.25 秒，含本机 HALCON 源目录覆盖。未启动测试、程序或探针，VM 原生变换、性能和体感仍待对照。

Assist 退订按原件 Assist0 的 False / CustomVisible=False 触发链接入现行字段编辑。关闭修正或切至图像 ROI 时，宿主将已接入的按信息来源清空合入同一候选，来源控件从草稿无写回刷新并退役旧交互。停用时拒绝迟到来源安装。补充正式窗口草稿接纳、撤销重做和图像 ROI 转换回归，三方式十叶共享的剩余范围保留。全量 Release 0 错误、6085 警告、23.83 秒。按当前要求未运行测试、探针或程序。

最大内接矩形已从原件运行参数接到正式结果、图像叠加和样式。以原件显示 XML 的中心、宽高、角度及状态绑定为准，原生求解缺证时使用 HALCON InnerRectangle1 轴对齐适配，在排序后的前 N 个保留 Blob 各求一项。0 跳过。结果页删除旧草稿行及专用挂接，沿正式 ImageViewConfig 迁移组装读取图形样式。原厂旋转和数量分配、超界像素和最小重叠率执行规则仍未确认。新增凹形区域、数量上限与独立样式回归，未运行。全量 Release 0 错误、5997 警告、19.78 秒，未启动程序或探针。

图像掩膜、像素 ROI 和仿射修正后的检测区／屏蔽区已从本轮执行区域发布边界，复用现有结果图层及帧身份校验。仿射下的普通 ROI 和扇环保留独立显示状态。HALCON 边界描边保留孔洞，不给光栅轮廓套用名义多边形填充。每组边界超过预算或无法构造时记录诊断，继续发布测量结果。新增画外移入、掩膜与孔洞回归用例。全量 Release 0 错误、6085 警告、22.77 秒。程序、测试与探针未运行，原厂动态绘制未确认。

结果页原件续核确认 Fixtured Point 和 Unfixtured Point 是活动 ImageViewConfig，默认隐藏。两行恢复为正式图形样式声明，图形 ROI 下跟随位置修正显示。已接入的"按信息"FIXTURE 中基准点和运行点从本轮快照发布，使用原件点渲染。检测区域、屏蔽区多边形和默认文本按用户的中文截图对齐。字号按原浮点行显示两位小数，原件整数解析失败经渲染回退 10 的规则由正式显示桥承接。全量 Release 0 错误、6085 警告、22.77 秒。已编写坐标、图像 ROI 缺省点、样式和字号用例，测试、探针与程序未运行。按点、按坐标输入、原厂结果点动态语义及实际绘制待核。

ImageSource 文件条原件复核确认本地 CurrentImagePath 的参数模板为空，外部 LocalImageList 槽负责列表消费。缩略图目录菜单改接已迁移 InteractionContextMenu，按菜单打开时的缩略图身份、会话和列表版本定位，保留正式执行游标。换源或退役关闭菜单，旧版本点击拒绝；清理未接入 RP/3D 的隐藏空事件和按钮。回归探针补充菜单目标、版本变化和换源三条路径。全量 Release 0 错误、5990 警告、19.40 秒，独立探针 Release 0 错误，未运行。原厂右击时系统选择与执行游标的实际时序仍未确认。

ImageSource 相机选择续批按 EnumerationCamera_Sub 的左右行、选择器与原件相机图标组装，移除逐控件 C# 视觉。按钮从当前调试窗口打开既有相机管理器，以上层调试窗口为 Owner，预选当前相机。下拉保存设备运行时 ID，供取帧工厂、管理器与状态查询统一消费。状态查询不启动设备，切回本地模式、卸载及关闭时停止轮询。新增实际编辑器设置入口、已移除设备和释放用例，未运行。全量 Release 0 错误、5987 警告、19.34 秒。原件选择后自动执行及回读、实际相机取帧和界面体感待核。

Assist 按信息、按点和按坐标现共用十个 Float32 持久输入叶。FIXTURE 与 POINT 选择一次展开到正式草稿，携带同一索引。读回根据叶合并同源组合，混合来源显示只读摘要。关闭开关和切至图像 ROI 合并清除十叶，执行只从冻结叶重建 FIXTURE。移除其他两种方式的预览占位。历史根记录不另保兼容输入。新增选择投影、索引、模式切换与关闭撤销用例，未运行。全量 Release 0 错误、5995 警告、20.84 秒。首批单对节点没有 POINT/FIXTURE 组合生产者，Float32 沿平台现有精度校验，实机选源、保存重开和 VM 动态对照仍待验。

Blob 屏蔽区继承沿 ExternROITypeGroup 接入正式选择、草稿、已发布图像上的只读预览、执行和结果。原 InputBlindPolygonString 编码未恢复，按 REA 报告采用中立像素多边形序列；上游 Blob 同合同发布实际多边形坐标，绘制模式仅显示多边形按钮，切回绘制时合并清除继承来源。移除原空来源行和未被产品消费的两个旧 Region 输入端口。回归用例核对上游多边形输出、下游屏蔽面积、二值图和结果叠加。全量 Release 0 错误、5994 警告、19.49 秒；测试、探针和程序未运行。原件字符串互通、原机修正后多边形坐标及人工交互仍待核。

检测区形状栏恢复原件模板顺序，复点取消绘制，16 项上限在申请视口手势前生效。复用原件消息框和本地化键，提示归当前调试窗口，随区域编辑器换绑、隐藏、释放和交互退役关闭。用例覆盖复点、提示归属、换绑关闭和删除后恢复。外部 REA C03 证据已定位全屏显式矩形链及 SDK 47/45 的差分，纳入同源图像、阈值、结果和叠加统一验收。本方全屏 HALCON 有效域与原件显式范围的同输入数值，以及原厂提示的焦点与退出时序仍待确认。全量 Release 0 错误、5940 警告、17.60 秒，测试、探针及程序未启动。

Blob 全屏算法续批依据 C03 两对 SDK 字节差分，区分直传全图矩形和生成全图掩膜。正式未设置的图形 ROI 表达为全屏。无屏蔽及位置修正、亮前景单阈值、存在内圈像素时，全屏或单个全图矩形的阈值域排除最外一圈，二值图与连通测量共用此域。输出掩膜和显示几何继续使用名义检测区。新增全屏、显式全图矩形和全图图像掩膜三路回归。其他模式、极小尺寸和原厂宿主逐帧输入未确认。全量 Release 0 错误、3306 警告、14.85 秒，测试、程序、探针未启动。

全屏阈值差分的生产验收扩展至正式 ImageSource → Blob 连线：RunChainProbe 固定 MONO8 对照图，核对亮外圈、隔离带和中心目标的来源像素，分别通过正式、私有调试及保存重开后的结果 reader 检查二值图、Blob 图和目标数量。实际调试窗用 6×6 图像核对阈值改动、预览退役和边缘像素。全量 Release 0 错误、3299 警告、14.32 秒，测试、程序和探针未启动。C03 归档样本的同输入对照单独验收。

Blob 高级页按原算法页 XML 隐藏未被展开触发器引用的最小重叠率，保留参数身份。像素超界开关关闭时，已保存的非零上限不再阻断执行，上限订阅在正式和私有执行中休眠，原配置继续保留。开启后检查来源，原厂候选像素规则未证，继续明确拒绝执行。补实际高级页显隐、正式休眠订阅和执行门禁用例。全量 Release 0 错误、3306 警告、14.20 秒，测试、程序及探针未启动。

续核 Blob 模块安装与归档同哈希，整段反汇编定位 OverlapRatio 槽的直接初始化、写入和读取，ProcessAlg 无对应 SDK 转发。现保留非零值配置读回并移除该字段触发的执行拒绝，增加值 100 的候选与输出像素回归。像素超界另核到当前内核 XML 的 SelectByOutPixel 1531 和 MaxOutPixelNum 0x2062F，候选计数算法待证，启用后的执行诊断为 BLOB_OUTSIDE_PIXEL_NOT_READY。全量 Release 0 错误、3306 警告、14.60 秒，测试、程序及探针未启动。

输出掩膜结果投影修复：原 Display 声明 OutputMask 独立图像并映射四成员，本方中立投影改与正式端口共享当轮 MaskImage，关闭时一致为空。新增运行投影一致性和实际结果窗口选择、读回、关闭后旧帧退役用例。原厂掩膜字节与多图叠层仍待对照。全量 Release 0 错误、3299 警告、13.61 秒，测试、程序及探针未启动。

C03 归档字节续核更正全屏适配：两对直传矩形二值缓冲都只在顶行与左列变化，右列、底行保留。已修正阈值域、实际窗口用例及 RunChainProbe 的两目标预期。另将十个归档组件与原厂 box 角度配对，长短轴等于像素中心投影跨度加一，接入筛选、判定和正式结果列表。轴比及外接框独立。全量 Release 0 错误、3306 警告、14.45 秒，测试、程序、探针未启动，原厂实际结果列表格式及同输入数值待验。

Blob 结果行高亮按原件 ModuleResultView.SetRectHighLight 接入同一结果场景。以发布印记、行身份和物理帧核对，选中时用黄色绘制可见外接/内接矩形，不另叠橙框。换同帧输出重绘，新发布清除旧选中。新增正式结果表选中、换图和重跑用例。全量 Release 0 错误、6082 警告、22.48 秒，测试、探针及程序未启动，原厂动态焦点和双图时序待对照。

结果轮廓刷新去除仅用于身份判断的临时 reader。DocumentSession 直接核对正式发布和执行域，准备及原件点记录编码每 256 点检查退役，仍在场景安装前完整复核。新增 2048 点检查次数、准备中退役以及持有旧 reader 时换轮的用例。全量 Release 0 错误、5940 警告、17.04 秒，测试、程序及探针未启动，性能耗时未测。

全屏检测区叠加接通。依据 C03 的全屏矩形解码和检测几何输出路径，正式全屏 Blob 用当轮图像宽高发布中心位于半宽半高的名义矩形，结果工作区复用检测区样式和同帧校验。补运行与实际图像场景用例，验证图像 ROI 分支和结果行高亮保持独立。原件特定状态的缓存绘图尺寸与异尺寸切换待核。全量 Release 0 错误、3306 警告、13.89 秒，测试、程序和探针未启动。

Blob 轮廓点按原 Display 和 Object.SetBaseProperty 单独使用 OK 绿和 NG 红，沿原件点渲染器消费。矩形自定义颜色与结果行高亮不再改变轮廓色。补正式结果场景用例。全量 Release 0 错误、6082 警告、21.80 秒，测试、程序及探针未启动。原厂点记录状态位来源及像素级显示待验。

Blob 运行页移除无实际控件的旧预览作用域、重复阈值和排序状态、按页挂接及事件转发。正式声明字段与宿主校验继续消费，基本页 ROI 和位置修正由同一组装负责。旧页队列不改写新宿主，同一设计上下文重复绑定复用参数树。新增实际高级排序提交及设计样例重复绑定检查。全量 Release 0 错误、3295 警告、13.48 秒，ControlNamingProbe 从本地缓存还原后 Release 0 错误。测试、程序和探针未启动，VS 设计器未实测。

范围整页回读改为使用同一冻结字典的两个端点。双端都存在时不再与上一轮界面值比较，继续校验各端声明及顺序，缺值保留单项回读路径。补上移、反序拒绝、旧有效值保持和下移用例。全量 Release 0 错误、5975 警告、17.86 秒，未启动测试、程序或探针；原厂运行后回填时序待证。

底栏重置复用原件 NativeMessageBox，归当前调试窗口，确认返回后再核对页签及会话状态。仍按运行页重置，原件跨页解绑范围待验。单次及连续执行统一就绪判断并在当前窗口报告入口失败。补原件模态取消、确认、草稿隔离用例。全量 Release 0 错误、5976 警告、17.88 秒，测试、程序与探针未启动。

调试宿主在准备、捕获及参数接纳后复核草稿身份，结果状态同步通知返回时只转发仍有效的同一快照。修复通知内关窗撤回结果后继续读取空快照的问题。补实际 ImageSource 窗口在结果通知内关闭、排空及拒发完成事件用例。全量 Release 0 错误、3288 警告、13.77 秒，测试、程序及探针未启动。原厂关闭回调次序待验。

调试窗口在 Closing 通知前登记共享关闭任务，同步观察者和宿主重复请求等待同一回执。异步排空后在下一调度轮次再次关闭，成功在 Closed 窗口释放之后回执。连续执行在途关窗用例补重入与任务身份断言。全量 Release 0 错误、3288 警告、13.80 秒，程序、测试和探针未启动。原厂动态排空顺序待验。

首屏组装加入节点、窗口和打开代次门禁，在绑定、完成、安装显示预览和实际 Show 前复核。旧窗口已进入排空时不再次显示或直接释放，当前组装失败走主窗状态及隐藏窗口退役。补 ImageSource 就绪通知内切换 Blob 的正式入口用例。全量 Release 0 错误、3288 警告、13.56 秒，测试、程序及探针未启动，原厂动态顺序与首屏性能待验。

两工具生产基准补上底栏确定的闭环。窗口专用提交继续使用正式捕获、校验和会话回执，更新草稿后复用当前参数树，随后使用共享关窗排空任务。新增用例沿画布连线、Blob 调试、确定、保存、重开、取消修改和正式复跑核对阈值、数量及二值图像素。全量 Release 0 错误、3290 警告、14.11 秒。程序、测试、探针未启动，原厂提交关窗时序与人工体感待验。

位置修正混合来源按原件模板区分只读文本和可操作订阅按钮。选择器可替换或显式清除，父投影为空时仍经正式宿主清对应组合叶。新增模板交互用例覆盖摘要拒写、清除确实提交及同一来源树重新选择。全量 Release 0 错误、5929 警告、17.10 秒。程序、测试与探针未启动，首批两工具缺少实际 POINT/FIXTURE 上游，原厂同版本弹层和索引交互待验。

位置修正组取消可见外壳和独立显隐写入，使用参数树的附属行条件同时跟随图形 ROI 与启用开关。三种方式按安装件 Group.xml 的角色顺序和原本地化键显示，修正信息使用原描述。正式窗口用例补可见行顺序和原描述检查。全量 Release 0 错误、5976 警告、17.96 秒。程序、测试和探针未启动，实际布局与切换时序待统一对照。

图像来源和 Blob 图像掩膜恢复 EnumerationG 的原件双列行布局。掩膜选择及输出开关纳入 ROI 变更通知，沿正式草稿退役上一轮调试结果。新增实际连线窗口用例，覆盖原件行标签、选源、掩膜与二值像素、修改及清除来源后的快照失效。全量 Release 0 错误、3289 警告、13.73 秒。程序、测试和探针未启动，原件运行刷新时序、像素阈值及外观待对照。

Blob 检测矩形和扇环由同帧结果几何发布到正式声明及引用树，沿现有 ROIBOX/ROIANNULUS 对应的继承类型供下一次执行使用。栅格或无法无损表达的几何标记不可用。回归覆盖全屏矩形、图像掩膜不可用与扇环发布后接入下一次 Blob 执行。全量 Release 0 错误、3296 警告、13.91 秒。程序、测试和探针未启动，原件角度数值、修正后几何与实机订阅仍待对照。

Blob 轮廓组合已按原件输出声明发布点数、坐标、判定及结果索引。两种轮廓沿同一测量结果进入 reader 和图像叠加，关闭轮廓时额外拓扑请求仍可读且不会带出轮廓显示。新增用例核对三种方式、点集对齐、OK 0/NG 2、索引及拓扑请求。全量 Release 0 错误、3296 警告、13.86 秒。程序、测试、探针未启动，原厂点序与像素仍待差分。

Blob 模块判定和单体判定按活动 XML 与 LimitJudge/ProcessOutput 输出 Int32。既有模块状态端口由布尔改为整数，新增单体序列，执行回执和画布继续使用同轮业务判定。用例和探针补空结果、整体 NG 与单体状态的断言。Score 的 SDK 出口已定位，公式待证。全量 Release 0 错误、3296 警告、14.37 秒，程序、测试和探针未启动。

Blob 结果文字依原件 Object 与 FeatureBase 从 Content 计数，固定文字显示一项，映射按最大来源数量，较短来源首值回退，空坐标使用缺省。补真实 ImageSource → Blob 场景回归。全量 Release 0 错误、3414 警告、17.17 秒，程序、测试与探针未启动。原厂回调刷新与截图待验；私有调试显示编辑仍走通用快照退役，待划清失效范围。

私有调试的显示编辑已与执行失效分开。保存继续检查完整草稿修订，调试捕获和发布检查执行修订，阈值及输入编辑继续退役旧结果。实际 Blob 窗口新增文字、位置与配色使用同轮结果以及阈值失效的用例。全量 Release 0 错误、6016 警告、19.43 秒，程序、测试、探针未启动。原厂与执行并发的显示刷新时序仍待对照。

底栏旧调试调用的成功和错误回执统一校验请求代次、执行修订和应用层来源。编辑、停止或退役后仅排空旧调用，不在当前窗口显示旧错误。新增实际图像源窗口的在飞编辑、无错误回执和再次执行用例。全量 Release 0 错误、3373 警告、16.16 秒。程序、测试和探针未启动，VM 同版本取消时序待验。

依据 SetParam 和 ProcessOutput 的两条标志分支，Blob 图像和二值图各自只在运行页开关开启时发布。移除下游引用绕过开关的生成路径。回归覆盖关闭时下游无图、重新开启恢复及另一图像仍为空。全量 Release 0 错误、3296 警告、14.74 秒。程序、测试和探针未启动，原厂界面清除旧图的时序待验。

结果工作区在冻结发布中区分所选图像存在与空值。本轮为空时保留选择身份并清帧和叠加，拒绝旧帧的 ROI 借阅；重新开启使用同一选择获取新发布。真实窗口回归扩到掩膜、Blob 图像及二值图三种输出。全量 Release 0 错误、3289 警告、13.84 秒，程序、测试和探针未启动，原厂关闭输出的回调时序待验。

双图输入 ROI 借阅按草稿、视图代次和已接纳图像来源持续校验。绘制待提交不再撤销自身帧，调试状态通知仅在结果、编辑器或图像来源变化时重建工作区。补实际 Blob 双图画矩形、切换视口后拒绝旧手势用例。全量 Release 0 错误、3290 警告、13.61 秒，程序、测试和探针未启动；原厂候选刷新、手势及视口时序待验。

正式参数窗探针改用 Release UI 程序集，移除链接源码和空主题替身，设置视图租约转发真实就绪任务。探针覆盖 ImageSource → Blob 连线、底栏执行、图像 reader、回执和关窗排空，加入 600/150/300/5 ms 既定预算。全量 Release 0 错误、6031 警告、20.20 秒，独立探针编译 0 错误、5959 警告、18.05 秒。程序、测试与探针未启动，预算是否达标仍待实测。

正式窗口回归扩充截图采集，记录三页默认、两页高级展开、图像 ROI、引用状态、ImageSource、私有调试工作区和关窗后工作区，同时保存结构化关闭状态。九张原厂图与采集文件的对应已写回设计审计。全量 Release 0 错误、6025 警告、19.52 秒，程序、测试和探针未启动，本方 PNG 及视觉差分待生成。

ROI 视口与模式切换续批：取消未接纳手势，保留已接纳几何及可恢复的坐标原文。捕获拒绝仍在进行的手势；旧视口退役后拒绝其迟到写入，返回有效视口可以继续捕获。正式窗口用例覆盖双图视口切换及恢复。全量 Release 0 错误、3276 警告、13.77 秒，程序、测试和探针未启动。原件可见状态更新有静态证据，切换中途手势的原件动态行为仍未确认。

Blob 画布节点补齐输入未就绪的 NORMAL 虚线边框。沿原件 DiagramItem → DecorateSource.IsReady → VmModule.ReFlashReadyState → VmIO.GetReadyState 查到强制输入判定；本方读正式配置中的活动必填输入，和运行状态独立。原件选中覆盖描边色但保留虚线，正式运行失败继续显示 NG 而不套加载失败的文字效果。补真实画布连接、断线及撤销重做用例，全量 Release 0 错误、3276 警告、13.87 秒。程序、测试和探针未启动，原件刷新时点、完整加载失败及外观仍待对照。

结果工作区按原件模块名排序，并把 Blob 输出掩膜排到末尾。默认优先自身普通图，其次实际主输入关联及列表首项。已有显式选择按完整引用恢复，双图输入沿同一候选顺序。新增真实窗口回归，核对顺序、默认项和切换二值图后的结果高亮。全量 Release 0 错误、6060 警告、22.09 秒，程序、测试和探针未启动。原件多图叠加的三层、透明度、背景交换及保存路径已查明，当前仍未接入多图状态和生命周期，继续纳入整链验收。

图像源按安装件 ShowImageName 原件补齐默认开启的本地文件名开关。使用现有布尔模板和相机分支显隐条件。显示设置持久化，读取同一冻结结果的文件名，私有草稿切换只刷新标题。当前 V5 方案缺少新增必需显示字段时按工具声明补默认值。补正式窗口同轮结果、确定、保存重开及复跑用例，全量 Release 0 错误、3276 警告、14.12 秒。程序、测试和探针未启动，原件实机时序、多图叠加及视觉对照仍待验。

Blob 阈值工作图去掉有效域像素临时数组和独立输出数组。自动阈值及相对软阈值直接按原行序读取灰度副本和掩码，保留单精度累计、比例计算和空域诊断，在扫描与提交工作图前检查取消。新增稀疏图像 ROI 用例，核对掩膜外变化不影响统计端点及二值图发布。全量 Release 0 错误、3283 警告、14.05 秒。程序、测试和探针未启动，实际延迟、原件内存策略和逐像素对照待验。

Blob 正式发布补齐最小灰度、最大灰度、灰度对比度及主轴角的逐体 Float32 序列，与已有测量投影使用同一排序和数据。四项同时进入手动引用候选，结果表角度仍按原件对应外接矩形角度。用例沿正式方案执行和 reader 校验字段类型、值和引用资格。全量 Release 0 错误、3282 警告、13.97 秒。程序、测试和探针未启动，HALCON 测量与原件逐值对照待验，Score 原件公式仍未确认。

Blob 切页回到公共参数树唯一组装入口。工具层取消第二套页调度，首屏绑定完整后发布就绪，初始化失败释放窗口控件订阅。按原件 ChangeSelectedSubParamCommand 清除旧引用目标和索引，切页同步退役旧请求并保留展开窗口。补正式窗口切页、返回复用参数行和关窗退订用例。全量 Release 0 错误、5918 警告、16.92 秒，程序、测试和探针未启动。原厂容器缓存、焦点及切页时延仍待同版本对照。

底栏重置复核追到 AlgorithmTabRoot 全页解绑及 VmModule.ResetParam → ModuleManager.SetDefaultParamList。当前从运行页触发后，一次恢复正式算法字段，覆盖结果判定页的变量订阅，显示外观和图像输入仍使用各自设置。确认文字恢复原资源键。新增同一草稿跨页恢复、旧订阅隔离及确定后正式配置用例。全量 Release 0 错误、3276 警告、13.89 秒。程序、测试和探针未启动，原厂 native 默认值覆盖和实机时序待验。

底栏按原件模板区分按钮可见、连续请求和在飞状态。单次准备及执行期间按钮留在原位并禁用；连续时显示停止；用户停止后恢复执行按钮，排空前仍禁用。新增真实模板按钮断言，覆盖单次、连续、停止排空及恢复。全量 Release 0 错误、6002 警告、19.58 秒。程序、测试和探针未启动，原厂焦点和停止时延待实机对照。

图像源条按原件水平 VirtualizingStackPanel 的已生成容器计算视口，滚出或折叠释放缩略图借阅。同批会话通知合并刷新，换绑拒绝旧通知；会话使用集合核对行订阅，直接读正式起点而不复制列表。新增 96 幅图的正式工作区用例覆盖滚动、缩略图范围、折叠及正式起点隔离。全量 Release 0 错误、5965 警告、18.35 秒。程序、测试和探针未启动，实际滚动时延和原厂调度仍待验。

Blob 像素超界已按同版本安装帮助接入执行。统计候选与图像、ROI 或掩膜边缘重合的像素，超过上限才移除。HALCON 使用 3×3 腐蚀定义有效域内边缘，筛选在限数排序和判定之前生效。补三类边缘、零值、阈值相等、原二值图和筛选后 Blob 图用例。全量 Release 0 错误、3282 警告、14.13 秒。程序、测试和探针未启动，原厂斜角计数及与顶行左列裁剪的组合待验。

Blob 灰度对比度按同版本安装帮助改为逐体自动二值化后的亮暗均值差，替换旧极差。最小值、最大值和对比度共用逐体灰度直方图，正式 reader 和引用端口身份不变。梯度及均匀区域用例已更新。全量 Release 0 错误、3282 警告、14.77 秒。程序、测试和探针未启动，原厂等于阈值、退化样本及耗时待验，最大内接矩形原厂旋转与并列取舍仍待确认。

Blob 外接矩形发布按原件 ROIBOX 与 RectI 区分，MinBoudingRect 改取带角度的真实测量，正式端口新增最小外接矩形及长短轴 Float32 序列。斜向目标方案回归校验顶层输出、表格测量及矩形角度的一致性。全量 Release 0 错误、3282 警告、14.24 秒。程序、测试和探针未启动，原件框宽高、归一化、轴对齐边界及 Score 待验。

Blob 孔洞填充按原件帮助保留面积小于等于上限规则，新增工作域边缘保护和填充裁剪。封闭孔洞可填，掩膜排除像素及与边界相通的孔洞保留空白。已补同时核对二值图、Blob 图及面积的掩膜用例。全量 Release 0 错误、3276 警告、14.18 秒。程序、测试和探针未启动，原厂斜角连通及组合像素差分待验。

Blob 检测 ROI 根据安装件实际链接的 InheritWayGroup2 复核四种继承方式。按原件触发器在同次草稿编辑中清理跨类或退出继承的来源，正式参数行回读配置并退役旧交互，执行只捕获活动输入。新增运行合同矩阵及正式窗口切换、撤销重做和保存重开用例。全量 Release 0 错误、3277 警告、13.93 秒。程序、测试和探针未启动，图形与图像互切时原厂订阅恢复时序仍待对照。

Blob 的矩形五参数和圆环六参数依照 Group.xml 里的 ButtonSelecter 使用原订阅模板。修复序列被送入未注册模板的切换问题，按原件校正圆中心标签。引用配置接纳来源，执行值保留数值序列。正式窗口回归补两组行序、选择上游矩形中心 X、跨类清除，待统一执行。全量 Release 0 错误、3277 警告、14.02 秒。

Blob 图形切图像 ROI 按 HiddenOperation → RoiSelect → InheritWay 的原件静态触发链清除检测来源，保留所选继承方式和独立的屏蔽区来源。旧预览层和提示随模式变更清理。补四种继承方式的往返合同及正式窗口回归。全量 Release 0 错误、3283 警告、14.30 秒。未启动程序、测试或探针，VM 动态恢复时序继续列入对照。

Blob 的本地屏蔽区按 ROISelecter_Extern 继承的原件默认值限定为 8 个多边形，检测区按安装件配置为 16。视口、草稿与执行复用同一约束，超量拒绝并使用原件提示。上游继承屏蔽区沿既有 64 项宿主上限，算法和结果共用已校验定义。补本地第 9 项拒绝、同量订阅输出和提示生命周期回归。全量 Release 0 错误、5919 警告、17.56 秒。程序、测试和探针未启动。

底栏原件映射补齐：Blob 左重置槽按参数树选中页键显示，并使用 Button_ResetAlgoParams 与 Lang_Reset parameters。右槽四命令恢复原件显示键和 ResourceBinding。实际窗口用例覆盖三页显隐、标题替换、确认流程及执行和停止文案。全量 Release 0 错误、5966 警告、18.44 秒；程序、测试和生产探针未启动。

ImageSource 的 CameraID 选择按原件列表初始化、提交和状态回读的职责边界接入私有草稿。选择只更新设备字段并通知宿主，候选不变时保留条目，既有参数页不重建。补实际窗口用例覆盖 SN 编辑、清除已移除相机和正式配置隔离。安装 XML 未声明左右底栏集合，运行时命令注入待对照，暂留现有宿主入口。全量 Release 0 错误、3277 警告、13.89 秒，程序、测试和探针未启动。

ImageSource 按已归档的原件追加及回读证据，删除首次导入强设首项和投影载入强设首项的分支。导入只追加，首图由正式执行接纳回显。新增空列表导入、ImageSource → Blob 首次运行和运行后再追加的正式窗口用例。全量 Release 0 错误、3277 警告、13.81 秒。程序、测试和探针未启动，原厂回显时序待对照。

结果区箭头按 VM ModuleResultView 静态入口切为当前根流程所有本地图像来源共同导航并执行一次。复用正式运行输入准备器、活动列表和回执，缩略图指定条目仍走独立入口。补两个 ImageSource 与下游 Blob 的窗口回归，覆盖跳过未勾选项、只执行一次及起点和历史不变。全量 Release 0 错误、3287 警告、15.07 秒，程序、测试和探针未启动。原厂 native 首末、自动切换、停止和失败语义待实机对照。

结果区输入和输出选择改为先核当前计划的选项身份，再退役 ROI 借阅及通知帧变化。迟到的同键旧项被拒时恢复当前选择，不影响在绘制的手势。补双图窗口同键旧项及真实换视口的对照用例。全量 Release 0 错误、3277 警告、14.44 秒，程序、测试和探针未启动。拒绝旧项属于宿主代次适配。

Blob 结果视口在借阅结果和文字来源前按声明检查物理帧与坐标域。浏览上游独立灰度帧时清除该视口旧图文，当前发布继续有效；切回同帧输出按本轮数据重建。补正式窗口选择、清理、高亮恢复用例。全量 Release 0 错误、3277 警告、14.35 秒，程序、测试及探针未启动。

Blob 结果图层按原件 ActiveUpdateShapes 的 IsDisplay 门禁接入。全关时退役图文场景且不借阅结果，冻结 ROI 单独显示时只用几何，文字单独显示时跳过逐 Blob 区域读取。新增真实调试窗口全关与仅重开文字、同一发布及执行修订断言。全量 Release 0 错误、3277 警告、13.73 秒。程序、测试及探针未启动，原厂读取和渲染耗时待对照。

修复 Blob 输入候选刷新通知内收到新草稿来源时丢失更新的问题。当前刷新结束前回读最新草稿，避免保留中间候选。用例在首次通知中替换掩膜引用。原件 RefreshInputImageList 按关联 IO 建列表，InputMask 不走普通关联变化监听，隐藏时动态候选行为仍待对照。全量 Release 0 错误、3277 警告、13.99 秒；程序、测试及探针未启动。

订阅行启用、可见及加载统一由 ReferencePopupScope 投影，打开订阅或变量窗前再次校验行。修复加载时可能覆盖禁用状态的问题。新增真实模板禁用、恢复和旧请求拒用的用例，并扩展 Blob 切回运行页的断言。全量 Release 0 错误、5966 警告、18.27 秒，程序、测试及探针未启动。

公共参数树的页、分组和显隐条件改为按已声明字段 ID 查找。每次显隐更新只遍历外部门禁来收集需隐藏的行，去除对每个目标重复全扫字段行的路径。保持复合行共享门禁和既有模板、顺序及更新时序。全量 Release 0 错误、5966 警告、18.00 秒，程序、测试和探针未启动，实际首屏及切页耗时待测。

参数树仅在首次安装和实际显隐变化时准备行交互，普通编辑继续刷新行状态。同步回调使变更失效时按新条件重试。补隐藏双端范围不因无关编辑重复挂起的用例。全量 Release 0 错误、3277 警告、14.23 秒，程序、测试和探针未启动。

正式编辑器生产探针改为从窗口底栏的真实 WPF 按钮读取模板命令，检查本地化标签、可见性与启用状态，执行后从同一 reader 核对图像及 Blob 模块和单体判定。通过实际"确定"命令等待窗口 Closed，核对草稿、视图、结果和旧命令退役。探针项目独立 Release 编译通过（0 错误、5959 警告、16.77 秒）；未运行程序、测试或探针。画布入口和保存重开的覆盖仍由实际节点会话及生产链探针负责。

ImageSource 本地文件选择对齐原件 ImageListPanelBase.LocalImage_Click 的路径与文件检查、原本地化过滤标签和六种格式。目录导入用例补路径长短、子目录插入顺序、扩展名大小写及超过一层的排除。全量 Release 0 错误、6026 警告、19.78 秒，未启动程序、测试或探针。原件 INI 记忆目录、导入中途失败的部分接纳语义及对话框实机表现仍待对照。

ImageSource 彩色转灰度以 REA 原厂无损语料、JPEG RGB 对照及现代 SIMD 源证为依据。相机、本地 MONO8 主输出和本地彩色输出的灰度成员统一按 (76R+150G+30B)>>8 生成，保留实际有效域和当前输出租约。PNG 格式用例改用 4×4 彩色原色，固定红、绿、蓝期望 75、149、29，同时核对帧、有效域、彩色缓冲和三种本地选项。全量 Release 0 错误、3277 警告、14.10 秒。未启动程序、测试或探针。仍需对照独立 OutMono8 开关、原件小尺寸拒绝、跨 CPU 标量分支及文件解码差异。

ImageSource 按原件 OutMono8 补独立布尔行及持久配置。取算法初始化默认关闭，MONO8 本地输出隐藏，RGB24 与相机显示；本方"保持原图"也显示，按实际色彩格式决定附加输出。正式灰度记录可为空，彩色主图且开关打开才分配独立灰度帧，MONO8 主图不重复发布。结果控件沿既有空记录路径保持声明与引用身份。补方案保存重开、相机及文件像素与域、真实窗口显隐/确认和 ImageSource→Blob 结果切换回归。全量 Release 0 错误、6061 警告、22.97 秒，程序、测试及探针未启动。原件桌面联动、SDK 来源模式及同版本运行对照仍待验。

附加灰度图关闭后的跨工具链：绑定解析区分"已声明且本轮为空的组合输出"和成员形状错误。Blob 绑定灰度图时，空输出给出包含来源节点和端口的本轮诊断，显示读值允许空。新增真实 ImageSource→Blob 连接及结果区回归，核对关闭时主图保留、原灰度帧退役、Blob 本轮失败，重新开启后相同选择恢复新帧及 Blob 新结果。全量 Release 0 错误、3279 警告、13.97 秒。程序、测试和探针未启动，VM 同版本空输出提示与恢复时序仍待对照。

Blob 正式结果按安装 XML 对齐个数 Int32、总面积和逐体面积/周长/质心/圆度/矩形度/轴对齐宽高 Float32。正式发布消费现有结果投影，保留排序及端口 ID。复制节点用例按声明传递物理类型，13 种排序回归新增 reader 逐项同序及物理精度断言。全量 Release 0 错误、3289 警告、14.18 秒。程序、测试和探针未启动。已有采用旧物理类型保存的订阅按现有门禁需重选；原厂与 HALCON 的测量值及矩形约定仍待同输入对照。

Blob 结果表核到原件 Result List → FeatureBase.GetValue → VmIO.GetValue → ModuleDisplayDecorator.RefreshCurrentResultList。表头按原列名与本地化资源，序号从 0 开始，九项浮点值使用单精度默认文本，不补固定小数位。原结果行 element-id 继续用于图像高亮。正式窗口回归覆盖表头、0 号、面积文本与切图/重跑高亮。全量 Release 0 错误、6061 警告、22.11 秒。未启动程序、测试或探针，桌面语言区域和实机显示仍待对照。

Blob 结果表声明接入原件 Number、Area、CentroidX 等资源键，由公共表格按当前宿主语言解析。对照原件 ModuleDisplayDecorator 的 500 行追加和整轮重跑清选，本方分页只追加行，集合通知后恢复同一轮已选行，重跑仍清选并退役旧滚动命令。新增 501 行正式表格用例检查行身份、单次 reader 和旧命令拒写。全量 Release 0 错误、6061 警告、22.03 秒。程序、测试及探针未启动，原件实机滚动位置和宿主运行中换语言仍待核。

Blob 结果表高亮按原件 ModuleResultView 选择事件与列表刷新分开的职责收口。本方表格发布阶段只复制显示值和行身份，选中时短借同轮结果核对元素、坐标域及物理帧，回调前释放 reader。结果替换、关闭或重入换选使迟到高亮失效。新增按选中行借阅及借阅中替换发布用例，全量 Release 0 错误、5966 警告、18.48 秒。未启动程序、测试或探针，大量结果的实测时延和原厂绘制时序仍待验。

Blob ROI 区域类型按原件 RadioSelecterEx2.DisplayType → RadioSelecter 接到正式 input.roi.area-type 行，删除旧 roi-mode-0 镜像状态。图形/图像分支显隐读取同一已接纳输入；绘制、继承及屏蔽区组合保留原件模板，标题、选项、矩形/圆环叶和掩膜行按组 XML 资源键。正式窗口回归增加字段身份、模板与草稿值断言，沿用现有两种区域往返及订阅清除场景。全量 Release 0 错误、5966 警告、18.15 秒。程序、测试及探针未启动，实际对焦和视觉仍待对照。

Blob ROI 正式捕获覆盖当前分支的来源行，包括首次选中继承方式才创建的矩形和圆环参数。当前来源的待提交状态进入编辑会话，分支隐藏、页签切换或释放退役相应的弹层和索引交互。已有实际窗口回归改从 ROI 组合捕获引用。全量 Release 0 错误、5966 警告、18.44 秒，程序、测试及探针未启动。绘制/继承选择器仍为宿主投影，草稿回执时的完整重新投影及原件动态弹层时序仍待核。

Blob 保留调试窗口的草稿回执接到正式输入回读。ROI 区域类型、掩膜及绘制／继承选择从重基后的草稿投影，使用回读入口而非再次写入；同值输入保留未提交交互。新分支按需生成继承参数，旧分支沿现有流程退役。新增实际窗口在提交发布通知中收到矩形参数和继承掩膜编辑的回归。全量 Release 0 错误、5966 警告、18.67 秒，程序、测试与探针未启动。回执重基属于宿主，VM 同版本动态交互仍待验。

结果工作区接通运行态图像叠加和背景交换。依据原件 SelectImageCommandExecute、ClickImageCommandExecute、AddCheckImage，在同一视口最多接纳一个背景及三层，使用 191/127/63 alpha 与 100/101/102 层级；仅背景为当前选择时绘制。图层由当前发布短借并冻结，切图、换节点、发布替换及关闭沿视口所有者清理。真实 ImageSource→Blob 结果区用例核对 alpha、切图、重跑和旧候选拒用。全量 Release 0 错误、5966 警告、18.71 秒，程序、测试与探针未启动。叠加背景与勾选尚未接入方案持久化，渲染导出、原件异尺寸缩放和对话框实机仍待验。

结果区按原件 RecordCheckedImages 语义把当前选择、背景和三个固定层位作为同一显示导航接纳。方案保存及重开按完整输出引用恢复，复制节点和流程重新映射来源，叠加导航不进入运行参数或撤销历史。空输出不绘制，已勾的空输出可移除。原件 ImageViewBox.SaveRenderImage 静态调用只传当前图和可见图形，第四项 controlImage 为 null；本方保留原图与渲染导出边界，并补叠加前后像素回归。首次构建定位五处片段测试构造签名遗漏，已同步修复。最终全量 Release 0 错误、3277 警告、13.96 秒。未启动程序、测试和探针，原件异尺寸叠加与导出编码仍待同输入实机对照。

结果叠加按原件 ModuleResultView.GetImageViewWidth/Height、920×517 缺省和 Byte2ImageSource.BuiltBgraImageSource 的尺寸分支采样。监听原视口 OnFreshExtendContent，合并尺寸变化并在当前背景重采样，切图及关闭取消旧刷新。保持本方有效域及 BGRA alpha，退化尺寸最小 1 像素。正式结果区用例补 6×6 到 3×2 的最近邻像素、alpha 和新图层。全量 Release 0 错误、5968 警告、18.53 秒。未启动程序、测试和探针。原件异尺寸、缩放时序和编码仍待实机对照。

Blob 首屏主图来源按原件 ImageSourceGroup 的 GetFrontParamItemsOperation 与 GetSelectedCombinationOperation 分离候选生成和正式值回读。EnumerationG 在草稿正式值改变时直接投影到已有下拉项，候选缺失时再刷新，刷新不写入草稿。保留原输入源资源键。窗口保留回执用例补同一窗口从连线主图切到灰度成员、下拉即时回读及正式节点未混入后续编辑。全量 Release 0 错误、5968 警告、18.88 秒。未启动程序、测试或探针，原件空候选与实际回调时序待对照。

参数视图与宿主草稿缩减首屏及编辑同步的重复工作。FieldSettingsDraft 回读共享不可变快照，Apply 只冻结本次提案，读取数组仍复制。宿主比较先识别共享字段，仅对独立值保留原规范化比较，并处理同为未设置的可选字段。参数行组装按声明索引取端口。补快照隔离、独立等值、拒绝后保持草稿用例。全量 Release 0 错误、6063 警告、22.17 秒。未启动程序、测试和探针，本方时延改善及 VM 同版本体感仍待测。

参数页组装完成声明行后一次挂入 ParamListView，避免页内逐行加入时反复扫描分组首项。工具外部槽在挂载后继续使用原集合，失败清理仍持有同一页。高级区沿现有默认折叠及行状态规则，原件隐藏行创建时点仍待确认。全量 Release 0 错误、5921 警告、17.14 秒，使用本机 HALCON 依赖目录。程序、测试和探针未启动，首屏及切页时延待测。

Blob 在一次执行内只组装一次正式输出值。直接 Session 校验和宿主结果发布使用同一批不可变结果记录，读取仍复制裸数组并保留原值类型。ImageSource 与 Blob 在输出校验后、交付宿主前复核取消，命中时由既有资源作用域释放。三态轮廓用例补同轮读回复用和数组隔离断言。全量 Release 0 错误、3285 警告、14.75 秒。程序、测试和探针未启动，大点集发布时延与 VM 同版本行为待验。

画布打开调试窗在旧窗排空、选中通知返回及首屏组装时校验同一打开代次、流程和节点身份。拒绝的打开不退役已接纳请求，正式执行开始后不再继续组装新窗口。补真实 ImageSource 选中通知中同步打开 Blob 的回归，断言只有 Blob 窗口且能排空。首次构建发现测试等待助手返回 void，已改为等待后读取排空结果。最终全量 Release 0 错误、3279 警告、14.00 秒。程序、测试和探针未启动，VM 换窗动态时序待验。

Blob 结果文字按 REA `FeatureBase.GetValue` 对物理 IO 值调用 ToString 的路径，解析租约保留实际来源存储类型，Float32 的默认及显式格式以单精度格式化，坐标继续沿原数值读取。正式 ImageSource → Blob 视口用例补两组圆度输出及普通和 G9 文本断言。首次构建发现用例错误从节点配置取 Schema，已改用 Session。最终全量 Release 0 错误、3279 警告、13.83 秒。程序、测试及探针未启动，原件同版本显示待对照。

Blob 对照安装件模块 XML 的 CentroidPoint（POINT、IsArray=true），将原先独立的质心 X/Y 输出合入有序 POINT 组合，使用两个 Float32 成员。按点可选择上游 Blob 的质心索引，按坐标仍可引用同一组合的数值叶。旧独立端口上的订阅需重选。真实窗口回归使用 ImageSource 和两个 Blob，覆盖选择器、同索引双叶、私有运行点、保存重开及正式执行。FIXTURE 组合生产者仍在首批范围之外。全量 Release 0 错误、3285 警告、14.96 秒。程序、测试及探针未启动，原厂索引及变换实机待核。

Blob 对照同一安装件模块 XML 的 MinBoudingRect、MaxAreaInnerRect、ROI 和 ROIAnnulus，将三个矩形输出和继承统一为同一个 ROIBOX 声明，扇环沿 ROIANNULUS 独立声明；两种组合的中心均为嵌套 POINT，叶为 Float32。删除旧平铺矩形类型；已有平铺字段订阅需重选。正式流程用例接通上游外接矩形到下游检测 ROI、中心 X 选源及图像 ROI 切换退订；原件内接矩形求解、角度和像素边界动态未确认。全量 Release 0 错误、3285 警告、14.01 秒。程序、测试及探针未启动。

Blob 对照安装件输出 BlobRect / RectPoint 与 B08 资格清单，发布轴对齐矩形数组、嵌套左上点 POINT、宽高和四个独立角点 Float32 数组。同一测量行与正式读值共用矩形声明，移除单独宽高端口，已有订阅需按新成员重选。补正式结果 reader 与位置修正选点候选用例。本方右下沿既有 x+width/y+height 投影，原件像素端点约定待同输入对照。全量 Release 0 错误、3285 警告、13.92 秒。程序、测试、探针未启动。

ImageSource 与 Blob 均按安装件 ModuRunTime 声明发布 Float32 毫秒值。原件托管结果从 fModuleTime 取值，状态栏以 ms 显示。本方用工具会话计时，覆盖已接纳输入后的读取或处理及输出组装，沿正式结果和手动来源声明发布。保留现有宿主计时供状态栏等消费者。补 ImageSource 声明与读值、Blob 多读者同轮一致断言。全量 Release 0 错误、3285 警告、14.48 秒。程序、测试和探针未启动，原件同机耗时与精确计时边界待对照。

ImageSource 的主图和灰度图结果按原件 IMAGE 组合发布 Int32 宽、高和格式。MONO8 使用原件已证 17301505。BGR/BGRA 保留真实图像资源及布局，因没有可用的原件同布局证据，格式码留空。旧格式文本或按旧组合类型绑定的来源需重新核对。更新相机和本地运行用例，生产探针追加读取同轮图像尺寸及 MONO8 码。全量 Release 0 错误、3279 警告、13.99 秒，独立生产探针 Release 编译 0 错误、5962 警告、16.65 秒。程序、测试及探针未运行。

原件结果树 RefreshResultTree 通知数值叶的 VmIO.DisplayValue，数值按实际装箱类型和当前区域格式化。本方结果树保留 Float32 的物理精度，数组样本继承来源存储类型，模块耗时和 Blob 测量不再显示 double 尾数。测试补法语区域标量及数组样本断言。全量 Release 0 错误、6036 警告、20.22 秒。未启动程序、测试及探针。另确认本方发布替换会先清树并重建，而原件同模块刷新只通知叶值，分组展开保持仍待修复。

同一节点复跑时，结果树现在按方案会话、流程、节点选择代次及输出指纹保留声明分组展开偏好。结果仍按新发布重新借阅，旧行退订，样本索引行不继承展开状态。切换节点或声明时恢复默认展开。新增真实 ImageSource 两节点用例，检查复跑、旧行拒写和换节点后的尺寸。全量 Release 0 错误、3279 警告、14.03 秒。程序、测试和探针未启动，原件树滚动及焦点动态待对照。

Blob 修复首次进入矩形／圆环参数继承后，新增来源行未接入只读 ROI 预览刷新的问题。模式投影后登记首次物化的行，复用原帧和预览代次，关闭统一退订。扩展正式双图用例，在同一矩形继承模式内将中心 X 从上游宽度换到中心坐标，核对只读检测框移动，随后切图像 ROI 退订。全量 Release 0 错误、3279 警告、14.13 秒。程序、测试及探针未启动，VM 同版本刷新时序待验。

继承 ROI 的只读输入预览直接捕获已接纳草稿，移除读取前提交整窗待编辑控件的路径。预览接纳在旧租约释放后和新输入读取后复核当前窗口及草稿。正式窗口用例保留低阈值原文 42，确认输入预览不推进执行修订，单次执行再接纳并退役旧借阅。全量 Release 0 错误、3279 警告、13.77 秒。程序、测试和探针未启动。

单次和连续每轮在参数捕获前登记同一在飞任务，覆盖旧结果退役通知、宿主输入准备与结果回执。首次状态通知中同步关闭时，等待捕获退回后才释放，已取消的请求不进入宿主准备。新增真实 ImageSource 编辑窗口回归。全量 Release 0 错误、3279 警告、14.11 秒，程序、测试和探针未启动。原件的实际回调排空时序待对照。

底栏命令按原件模板状态绑定使用同一批命令项。连续、停止排空、恢复可用只通知原控件属性及 CanExecute，避免每次通知重挂 ItemsSource。更换宿主绑定仍按原代次退役。扩展正式连续窗口用例，比较原按钮实例及状态序列。全量 Release 0 错误、5968 警告、18.43 秒。程序、测试及探针未启动，键盘焦点与 VM 动态时序待对照。

确认被宿主参数校验拒绝时，调试窗改用已有原件消息框，以当前可见窗口为所有者。标题栏关闭继续由原 BaseWindow CloseWinButton 进入共享排空，并放弃未接纳草稿。新增实际 Blob 窗口用例，先验证无效值拒绝确认，再点击标题栏关闭并核对正式配置。全量 Release 0 错误、3279 警告、14.01 秒。程序、测试和探针未启动。截图中的模块帮助入口尚缺本方两工具的正式帮助主题映射。

标题栏帮助按原件 ParamsTabControlBase 的标题槽、14×14 图标、30×26 模板和悬停资源接回。原件 OpenHelpCommand 从 ToolItemInfo 解析主题，本方按正式工具身份打开随 Release 部署的中文手册锚点。ImageSource 与 Blob 两节已进入既有手册。窗口回归新增 Blob 帮助按钮及发布主题断言。首次构建路径类与 UI 命名空间冲突已修，最终全量 Release 0 错误、3279 警告、13.90 秒。程序、测试、探针和浏览器未启动。

重置按钮按原件 ParamsTabUserControl 的选中页键更新。正式宿主只订阅当前参数视图的页签通知，退役前解除订阅，枚举等内部选择不再进入底栏页签刷新。正式 Blob 回归加入真实换页与内部选择事件的区分。全量 Release 0 错误、5968 警告、18.42 秒。程序、测试和探针未启动。

Blob 页签索引与 Assist 交互改用正式页签通知，移除两处并行的属性描述符监听。延迟页组装失败会退役本轮 ROI 组合及借阅，并禁止同一参数树继续挂载专用槽和捕获；ROI 所有者构造中途失败也统一清理。新增真实工厂的后续页失败注入用例。首次构建的测试 Application 命名空间冲突已修，最终全量 Release 0 错误、3279 警告、14.18 秒。未运行程序、测试或探针，原件动态异常处理待对照。

参数窗口模板按实际页数执行原件的单页规则。原件用 ItemsSource.Count 隐藏单页页签栏，本方直接填充 Items，因此改读 Items.Count。ImageSource 单页不占页签栏空间，Blob 三页继续显示并可切换。单页和三页的正式参数视图回归已加入，未启动。全量 Release 0 错误、5921 警告、17.96 秒。原件滚动容器静态定位在 ParamListView，重复切页时是否保留滚动位置及焦点仍待同版本实机确认。

画布节点复核发现参数窗期间缺少原件菜单门禁。VM 同版本 DiagramItem 的 IsOpenParams 触发器移除节点菜单，并在单击入口阻止选中与拖动。本方保留现有单击门禁，补上正式调试窗口 Loaded／Closed 对主窗参数窗状态的通知，画布节点模板据此移除并恢复菜单。新增实际连线的 ImageSource 和 Blob 窗口用例，覆盖当前及其他节点菜单、单击不换选、关闭恢复。全量 Release 0 错误、3279 警告、14.18 秒。测试、程序、探针未启动。原厂双击焦点与切换时序仍待实机对照。

节点双击进入调试按原件 DiagramArea → ProcedureConfigControl 核对。禁用节点不会进入开窗流程。修复画布在旧窗排空前先强制切换结果和选中的路径，当前由正式开窗入口在旧窗关闭后更新节点身份及投影选中，再组装新窗。实际连线用例覆盖禁用 Blob 拒开、ImageSource 关窗期间保留选择及随后打开 Blob。排空后的身份切换属于宿主约束，原件物理鼠标焦点和运行时顺序待验。全量 Release 0 错误、3277 警告、14.08 秒；测试、程序、探针未启动。

节点视觉核对原件 diagramitemtemplate.xaml 的 NORMAL、NormalModulePathStyle、ImageModulePath、StateFlag 与 errorBorder。ImageSource 和 Blob 共用状态层，运行色条由各自原件轮廓裁剪，缺必需输入为虚线、禁用清除运行色、选中覆盖描边颜色。已去除图像源独立的选中边框分支。必需输入状态来自宿主准备度，执行失败按 NG 显示，VM 模块加载失败是独立状态。新增连线、实际运行回执和禁用选中组合用例。全量 Release 0 错误、3277 警告、14.32 秒，程序、测试、探针未启动，实机显示对照待验。

参数列表集中计算组可见性和首个可见组。原件 GroupLinkItem 展开关联组，ParamListView 与原件组模板承载分隔线和 Expand 行。本方首屏绑定、条件显隐和图像／ROI 外部槽调整原先分别计算首组，现统一交给 ParamListView，列表换源及重入时拒绝旧投影。已补隐藏首组插入、恢复和换序回归。全量 Release 0 错误、5966 警告、18.86 秒，程序、测试、探针未启动。原件隐藏首组的实际索引、滚动位置和焦点待实机对照。

Blob 结果视口按同版本 IMVSBlobFindModuDisplay.xml 补外接矩形、内接矩形、检测矩形的中心、宽高和角度提示。沿正式同帧形状与原件 IShape.ToolTip 投影，外接框使用发布的有序四角，内接和检测直接使用矩形几何。用例核对两个投影入口、非测量叠层和帧退役。全量 Release 0 错误、5966 警告、18.72 秒，未运行程序、测试及探针。鼠标命中和原件提示时序、位置修正点提示及独立 Data Record 链仍待闭口。

Blob 位置修正基准点与运行点按同版本显示 XML 接回原件 PointShape 提示，文本来自本轮发布点并随帧退役。新增结果视口用例验证原件点批次及清理。另查明 Data Record 对应独立历史结果页，原件按执行序号与结束时间记录，定时前插最多 500 行，分页查历史并释放缓冲，清空和文本保存有独立命令。本方已有原件历史表格，正式和私有历史源、清空及导出仍未接入。全量 Release 0 错误、5966 警告、18.92 秒；程序、测试和探针未启动。

ImageSource 和 Blob 接入原件历史结果表。正式执行接纳后按方案会话与节点保留最多 500 行，调试回执按私有窗口作用域保留同样上限，记录仅含文本。切回节点恢复其正式历史，关窗回到正式历史，清空当前作用域，保存按执行序号、时间、内容导出文本。新增实际连线与私有调试的作用域及清空用例。图像源相机计数缺采集回执，原厂历史跨重开、回调与查询的行顺序和导出编码待验。本方内存历史不进入方案文件。全量 Release 0 错误、6061 警告、22.38 秒，程序、测试和探针未启动。

图像源历史按同版本 ImageSourceModuleDisplay.xml 和 SDK XML 将已核验采集回执的设备帧号接到 FrameNum，与图像宽高使用同一执行序号。正式发布和私有采集用例均检查输入帧释放后的文本。丢帧、丢包和取图失败计数缺设备统计回执，MVS 真实工作流仍受既有有效域准入约束。全量 Release 0 错误、3277 警告、14.19 秒，程序、测试、探针未启动。

历史文本在冻结结果快照时生成一次，失败降为记录诊断并保留正式结果，宿主在原有接纳门禁后使用同一文本。私有窗口排空后清空历史，导出在文件对话框返回后拒绝已换代的方案、流程、节点及编辑作用域。新增格式化失败、结果可读、释放及迟到拒写用例。全量 Release 0 错误、3401 警告、17.19 秒，程序、测试、探针未启动。

调试窗与正式结果发布隔离。私有执行尚无结果、取消或改阈值退役时，同节点结果表与图像候选不借用上一次正式输出。默认可显示有效上游输入，显式选择空输出则保持选择并清帧。关窗排空后恢复正式发布与默认图像。扩展 ImageSource → Blob 真实窗口用例，覆盖正式运行、私有运行、改阈值清标注及关窗恢复。原件自执行按 ExecuteCount 刷新已核，空态回退行为仍待实机。全量 Release 0 错误、3277 警告、13.96 秒，未启动程序、测试和探针。

Blob 主图像引用改选接通原件 OnRelatedIOChanged 的单图投影。当前调试作用域内，接受新主输入后单图临时选中相应来源，ROI 仍需借到该来源的当前发布帧。初始开窗沿已选显示项，自动改选不写方案，关闭恢复显式输出。新增先保存二值输出选择、调试改灰度输入、ROI 接纳及关窗恢复的生产窗口回归。全量 Release 0 错误、3278 警告、15.91 秒，程序、测试和探针未启动，原厂鼠标交互和刷新时序待验。

Blob 双图按原件 RefreshInputImageList(selectedIO) 接纳主输入改选。旧主图仍作屏蔽图时保留候选，但输入视口优先选中新主图，输出视口不随双图主输入改选。手动引用和显示来源都限实际前驱。窗口回归延伸到灰度旧图继续充当掩膜及彩色新输入接管。全量 Release 0 错误、3278 警告、14.79 秒，程序、测试及探针未启动，原件实机切图时序待验。

图像来源 EnumerationG 下拉每次打开及捕获仍核对候选声明，候选身份、标签、提示和可用状态一致时复用既有项。来源移除保留禁用当前项，恢复后回读可用，候选刷新不写草稿。扩展正式参数行用例检查同项复用和移除恢复。全量 Release 0 错误、5967 警告、19.54 秒，程序、测试及探针未启动。VM 动态列表焦点与复用未确认。

Blob 位置修正后的屏蔽区按原件 ExternROI 直接取发布多边形顶点。正式引用、VM 结果投影及显示共用同轮修正顶点，像素区仍走栅格边界，输出掩膜仍用实际检测域。扩展画外多边形平移用例核对三个消费者及原检测测量。全量 Release 0 错误、3284 警告、14.63 秒，未启动程序、测试和探针，原件原生坐标精度及显示待验。

Blob 修正后检测几何按原件 ROI 和 Annular 声明保留可表达的中心、尺寸与角度。保向平移、旋转和等比缩放后，显示与继承输出使用同一几何，非等比扇环和像素区域保留栅格轮廓。执行域和阈值算法沿现有 HALCON 路径。用例覆盖矩形平移、扇环旋转缩放及非等比回退。全量 Release 0 错误、3284 警告、14.39 秒，未启动程序、测试、探针。原厂坐标及像素动态仍待验。

Blob 扇环的 Annular 显示映射接入原件悬停测量提示。绘制适配负扫角，提示和引用读数保留正式起始角及范围符号。扇形与圆环共用检测组合发布，新增同帧原件批次、退役及下游继承用例。全量 Release 0 错误、5973 警告、18.97 秒，程序、测试和探针未启动，原件实际命中与提示格式待验。

Blob 有序轮廓正式逐体记录复用已冻结的边界点，单外环、显示点和完整拓扑不重复建点对象。读取长路径时同步算包围框并分段检查取消，冻结结果时延续同一取消令牌。三态回归新增单路径复用断言，原件点序及实测性能待验。全量 Release 0 错误、3284 警告、14.39 秒，程序、测试及探针未启动。

Blob 设计器与正式窗口统一从 XAML 的基本参数页打开，示例使用正式输入草稿、图像来源下拉和模板。ImageSource 显式相机样例跳过默认样例绑定。控件探针补正式首屏、图像来源和三页切换断言。全量 Release 0 错误、3278 警告、14.67 秒，ControlNamingProbe Release 编译 0 错误、5305 警告、8.60 秒。未启动程序、探针或测试，Visual Studio 实际设计器待验。

参数批回读经正式宿主一次捕获配置并在前后验证草稿及节点，避免逐字段重复准入。重绑或关闭会取消尚未执行的空闲回读。用例检查两种退役及双端同批回执。原件 GetItemsAllValueUnit 与切页订阅操作静态分离，VM 空闲调度和耗时未确认。全量 Release 0 错误、6025 警告、20.65 秒，程序、测试和探针未启动。

生产参数窗口探针沿正式设置视图定位页签，核对首屏及空闲后仅首个页签组装、首切按访问物化、两轮重复切页保留页面及字段行身份。继续记录已有性能预算，执行和关窗仍走正式命令及结果 reader。独立 RuntimeEditorProductionProbe Release 编译 0 错误、5960 警告、17.17 秒，探针、程序及测试未启动，本批无新的运行耗时证据。

正式三页截图用例增加默认常量运行页和图像来源独立弹层采集，原绑定状态继续单独采集。关窗资源记录新增私有二值图在读者释放后拒绝读取的断言，和原窗口、草稿及快照释放一起记录。全量 Release 0 错误、6028 警告、19.95 秒。按用户安排未启动程序、测试或探针，尚未生成本方截图及运行资源记录。

现行架构总览已集中列出两工具的参考生产链，从节点、连线与调试入口到参数模板、执行、阈值图像、结果 reader、保存重开及退役。正式三页用例补充阈值 Enter 和来源下拉 Escape 的交互与窗口状态检查，已同步控件闭包台账和体感审计。全量 Release 0 错误、3278 警告、14.27 秒。程序、测试及探针未启动，原件焦点、运行耗时和实际视觉仍待人工对照。

图像源安装件 AutoPlay/AutoStop 和 InitialSN 声明接入正式参数链。末张停止仅在本地自动切换时显示，隐藏保留草稿值，正式连续执行仍消费末张回执。SN 初值在模板和设置统一校验 0–100000。已新增正式窗口和设置边界用例。核对到安装件 ImageSourceModule.dll 与外部 REA 报告哈希不同，彩色格式码不跨版本归认。全量 Release 0 错误、3278 警告、14.45 秒，程序、测试及探针未启动。

Blob 外接框依同版本 Display XML 的 rect 组装，直接消费正式 oriented-bounds 并接入原件 IRectangle 批次，移除四角多边形绘制路径。显示测量允许零宽或零高，帧、样式、提示和高亮继续沿同一结果场景。扩充正式工作区及退化测量用例。全量 Release 0 错误、5967 警告、18.64 秒，程序、测试及探针未启动。

Blob 屏蔽区橙色填充、定位点固定状态与提示、检测区测量提示改由工具结果样式声明提供，公共工作区按当前帧和同轮发布投影原件图形，栅格孔洞轮廓只描边。新增样式声明和正式窗口屏蔽区绘制、改色、关窗用例。全量 Release 0 错误、6062 警告、22.25 秒，程序、测试及探针未启动，原件动态绘制待验。

结果图像背景交换按原件移除后首个空位接纳旧背景，修正前方有空层时的层级与透明度。浏览其他图像时，解除仅余一个图层的背景保留浏览项并清除叠加。正式窗口用例覆盖确认、拒绝、空层复用及退役。全量 Release 0 错误、3278 警告、14.33 秒，程序、测试及探针未启动，原件实机焦点和画面待对照。

结果区叠加按原件交互对运行中、图层满额、背景或目标为空恢复勾选并给出对应提示。背景确认返回后检查运行和候选身份。正式窗口用例补空输出拒绝、勾选回读和已接纳导航保持。全量 Release 0 错误、5967 警告、18.38 秒，程序、测试与探针未启动，原件弹窗焦点及实际点击时序待对照。

图像源勾选运行先按主列表顺序筛选，再解析本轮资源。配置起点被排除时，正式和私有执行均从首个勾选项读取，持久起点不变。已编写资源解析、连续读取与会话隔离用例。原件独立执行列表与空列表行为有 REA 证据，本机同哈希跨模式首读及缓存生命周期待核。全量 Release 0 错误、3288 警告、14.85 秒，程序、测试和探针未启动。

图像源缩略图导航写游标前复核当前来源条、版本和可选项；晚到请求不能改变下一轮取图。连续运行中点选的当前帧与自动切换分开处理，末张仍产生同轮停止回执。正式窗口用例覆盖旧请求拒写、连续等待时点选末张及 Blob 发布。全量 Release 0 错误、3288 警告、14.97 秒，程序、测试及探针未启动，原件运行中点选和停止时序待实机核对。

图像源显式 RGB24 接通真实 R/G/B 资源和 35127316 格式码。HALCON 按布局接收，附加灰度读真实通道，视口转换私有 BGRA 并原样导出 RGB24，现有相机 BGR/BGRA 来源继续保留原布局。新增真实格式、有效域、HALCON 往返及视口导出用例。首次编译发现直接 HALCON 断言所在项目缺引用，移入已有 HALCON 引用的 Blob 测试项目后全量 Release 0 错误、3284 警告、14.99 秒。程序、测试及探针未启动，同安装件动态像素和性能待核。

正式参数窗口探针增加红蓝分区 RGB24 场景，沿实际连线及发布 reader 检查主图和附加灰度的像素、格式码、Blob 二值图及物理帧。阈值 50 可区分红蓝通道倒置。独立 RuntimeEditorProductionProbe Release 编译 0 错误、5960 警告、16.56 秒。程序、测试与探针未启动，本轮无新增运行结果。

安装件算法与页签 XML 一致将图像源默认格式设为 MONO8。现行声明、设置和宿主投影已对齐，原件两项置前，宿主"保持原图"保留末项。用例及生产探针从新建节点验证默认。控件闭包台账同步校正位置修正、输入输出掩膜和内接矩形当前消费者，审计分列 SDK 供图、PathCache、相机逐轮订阅和其余 ImageSource 原件缺口。全量 Release 0 错误、6028 警告、20.64 秒；独立 RuntimeEditorProductionProbe Release 0 错误、5960 警告、17.15 秒。程序、测试及探针未启动。

图像源 PathCache 已接正式本地参数和来源游标。默认 0、上限 5，按已接纳路径保留首批解码副本，正式与私有调试隔离，换代和关闭在读取排空后释放，结果仍独立发布帧。现行方案缺失该字段按声明初值读回。新增容量、文件删除后命中、私有隔离及保存重开用例。全量 Release 0 错误、6064 警告、22.90 秒。程序、测试和探针未启动，本机同哈希原厂缓存动态仍待验。

PathCache 读取租约限定单上下文，解码及克隆返回时复核调用和取消并释放迟到资源；同时保留读取和释放失败。补取消期间资源释放一次及缺缓存字段恢复 0 用例。静态确认相机正式准备器能接收本轮物化设置，仍缺逐轮订阅及驱动应用恢复。无 UI Platform 宿主未传图像源输入工厂，SDK 供图仍需正式帧接纳。全量 Release 0 错误、3288 警告、15.24 秒，程序、测试及探针未启动。

图像源曝光和增益接入原件 ButtonSelecter 对应的可选 Float32 引用，正式和私有执行从本轮绑定结果生成采集请求。切本地模式时仅停用引用，不删除方案设置。设备服务在单帧独占范围应用并恢复原值，恢复失败不交付帧。编写了正式窗口显隐、前驱订阅及模拟设备成功与失败恢复用例。全量 Release 0 错误、3278 警告、14.37 秒。程序、测试和探针未启动，MVS 有效域、硬件自动控制及原件动态恢复仍待对照。

ImageSource 第三种 SDK 模式接入正式设置及一次执行的文件供图。Platform 执行创建时按节点冻结绝对路径，宿主执行时接纳并解码，缺供图不回退本地。发布 SN 为 0、文件名和路径为空，独立生产探针沿实际连线读取 Blob。同一执行对象的重复 RunAsync 共用任务。全量 Release 0 错误、3286 警告、15.60 秒，独立探针 Release 0 错误、5960 警告、17.16 秒。程序、测试和探针未启动，内存供图、连续逐轮供图、外部工程及同版本动态待验或待实现。

### SDK 复制式内存供图与 Blob 连线

安装件 SDK XML 记录 SetImageData 与 SetImageDataP2P。本方单次 Platform 执行创建时按节点复制 MONO8/RGB24 像素，正式准备器接纳为带本轮身份的帧，ImageSource 发布后由 Blob 沿正式连线消费。补提交后调用方改写缓冲、行填充、帧身份和阈值结果检查。原厂内存所有权运行未确认，P2P 与连续逐轮供图待实现。全量 Release 0 错误、6036 警告、20.92 秒，独立探针 Release 0 错误、5960 警告、16.39 秒。程序、测试及探针未启动。

### 图像源目录菜单正式消费

重核 ImageListPanelBase.MenuItem_Click，固定仓内逆向正文 SHA-256 DF02FAA325BABECCDAC3F44D3ED1D750B59874DA466BC0FDF033F997898D4B79。命令执行时取当前图像索引，文件经 Shell 选中，目录用 Explorer /select。替换旧实现按右击缩略图取路径的逻辑，保留菜单会话和列表版本门禁。探针覆盖当前项、索引变化、无当前项、列表改版和换源。全量 Release 0 错误、6033 警告、20.67 秒，独立图像条探针 Release 0 错误、5301 警告、7.66 秒。未启动程序、测试或探针，原厂右键按下到选择的时序及 Explorer 实际行为待验。

### 两工具保存重开回归

扩展正式主窗用例，从实际画布的 ImageSource → Blob 连线与发布开始，在 Blob 三页提交本地矩形、低阈值和结果文字。重开检查订阅归属、源文件读回、原件 ROI 会话图形身份、结果视口文字和取消后正式重跑的二值像素与新会话身份。全量 Release 0 错误、5935 警告、18.01 秒。未启动程序、测试或探针，本方运行和原厂重开体感对照待验。

### Blob 单双阈值前景与结果图

保持既有 C03 单阈值端点、双阈值正反序及直接矩形有效域规则。单、双阈值直接走 HALCON 区域，去掉整图和遮罩的托管复制与二值图反取前景。只有启用二值输出才生成二值图，Blob 图复用筛选后的正式区域集合。正式会话用例增加暗前景、同端点、反向空区间，比较两种图和面积。全量 Release 0 错误、3282 警告、14.68 秒。未启动程序、测试或探针，像素、HALCON 空域和实测性能待统一验收。

### 当前结果页读取与历史表

按 ModuleResultView 的 InfoTab 条件接入正式结果表门禁。隐藏期间结果树、图像及历史仍更新，切回当前结果只读同节点最新批次。移除历史表的旧占位文字，并沿实际主窗增加隐藏期间多轮运行、切回读最新表和换节点回落用例。全量 Release 0 错误、5918 警告、17.67 秒。程序、测试和探针未启动，原件按模块保存的页签状态及实际时序待验。

### 换工具时的结果首屏与列宽

对照 ModuleResultView.SetCommonModuleView、SetFirstVisibleTab、RefreshDataListResult。正式结果视图换工具时选首个可用页，ImageSource 为历史，Blob 为当前结果。结果表按整轮显示值最长文本回缩数据列，差 50 保持、超过 50 依次设表头宽和自动宽度。用例覆盖 ImageSource 帮助页切回 Blob 和列宽边界。全量 Release 0 错误、5965 警告、18.80 秒。程序、测试和探针未启动。原件 curTabIndex 静态写点仅证实回退，手动切页记忆与实际布局待验。

### 连续停止与取消分离

重核 BottomCommand.StopSelfExcuteModule，原件按钮清连续标志。正式窗口现只停止后续派发，已派发轮次允许结果回执；尚未派发的轮次复核请求状态并退出。单次执行不开放连续停止，关闭及草稿失效独立退役、取消和拒收。正式窗口用例覆盖派发前重入、已在取图时停止仍回执一次、同会话恢复和关窗拒收。全量 Release 0 错误、3276 警告、14.24 秒。程序、测试及探针未启动，原件已在飞回调与退订的实际顺序待验。

### 图像源相机行模板和引用读回

依据 EnumerationCamera_Sub 和 BaseImage_ButtonStyle，候选用数据及 ID 路径生成，设置图标恢复悬停切换。结果壳和模块结果属性表共用同一原件按钮样式。相机行直接读取模式和设备常量，曝光或增益有订阅时无需在界面解析。补正式窗口曝光引用、设备读回及清除测试，独立根配置探针适配数据绑定但本工作区无其恢复资产。全量 Release 0 错误、5918 警告、17.79 秒，独立 RuntimeEditorProductionProbe Release 0 错误、5960 警告、16.59 秒。程序、测试和探针未启动。原件选中后有条件预执行及硬件动态待核。

### 参数窗外部槽与执行错误提示

原件中间行消费 ExternParamsListA。安装件 ImageSource 外部 LocalImageList 默认隐藏，模块原件仅在 SDK 模式显式启用。桌面参数窗移除外部槽内自研诊断文本，空槽折叠。底栏执行的新失败结果和捕获异常走归属当前窗口的原件消息框，关窗或过期结果不提示；组装失败由主窗口状态和日志报告。缺文件执行用例核对消息框归属、槽位与命令恢复。全量 Release 0 错误、3276 警告、14.19 秒。程序、测试及探针未启动，SDK 嵌入模式和原厂各错误码的提示顺序待验。

### 主图与掩膜的原件候选绑定

按 EnumerationG 的候选集合绑定移除逐项构造 ComboBoxItem。正式主图和掩膜共用数据候选与原件容器样式，保留失效项禁用、恢复无写回、已选项鼠标/Enter 重新确认及退订。更新真实窗口、模板和控件探针断言。首次构建发现事件闭包的变量初始化顺序错误，已修正。全量 Release 0 错误、5965 警告、18.54 秒，ControlNamingProbe Release 0 错误、5305 警告、9.23 秒。程序、测试和探针未启动。图像变量的资源租约和视口入口仍待接入，实机下拉焦点及生成容器行为待验。

### Blob 图像变量的全链接入合同

取得 REA VmModule.GetFrontIOWithLeafItemsByName 正文和安装件 SDK SetVarImage 证据。现行缺口集中在引用资格与生产规则耦合、执行纯值槽无法持有图像、工作区只接受输出引用。设计审计固定稳定帧、独立 retain、候选回读、正式捕获和 ROI 视口的一次实施边界，区分 8 位已有布局、16 位保存值和原件运行未知。本轮仅更新原件证据和设计，程序、测试和探针未启动。

### 持久图像变量接入正式执行与只读预览

Image 消费资格与纯值生产分开，执行快照只为实际引用的 Gray8、Bgr24、Bgra32 变量复制存储像素并保留行跨度。同一存储对象、变量 ID 和修订使用稳定帧身份，正式批次及私有预览分别持有图像。新增持久变量打开、正式绑定、预览释放、Blob 二值图同帧与像素用例。首编测试项目缺持久化引用，将用例移到已有完整引用的 UI Tests。全量 Release 0 错误、3290 警告、14.52 秒，程序、测试与探针未启动。窗口变量候选和实际 ROI 视口仍待接入。

### 图像变量接入下拉、结果视口与 ROI

Blob 主图及掩膜沿正式引用候选统一接入方案图像变量，设计时示例使用同一候选入口。变量绑定沿节点字段提交及持久化。结果工作区独立持有冻结帧，ROI 核对变量值修订、当前草稿和真实选中视口的帧回执；工作区退役后旧租约拒绝继续使用。变量的结果视口选择属于临时导航，输出叠加继续使用现有节点输出合同。扩充正式窗口用例覆盖下拉、ROI 同帧、提交、关窗退役、只读预览和正式二值输出。最终全量 Release 0 错误、3301 警告、13.95 秒。测试、程序和探针未启动。VM 同版本运行表现、局部图像变量、SDK 写入和 16 位图像仍未确认或接入。

### Blob 屏蔽区原生订阅载荷

读取外部 REA 资料并核对本机安装件的 PE 哈希与原生指令。确认 `InputBlindPolygonString` 按项读取、逗号分隔交替 X/Y、坐标收窄为 float32、每项最多 32 对且最多读取 64 项。正式 Blob 继承输入替换为字符串序列，复用同一解码供预览与执行，发布几何点集及字符串。使用不变区域和六位小数，明确拒绝非法、超长和超量输入。补正式窗口链用例，从 ImageSource 取图，经上游绘制、下游引用与清除、保存重开、取消至正式二值输出。全量 Release 0 错误、3307 警告、14.28 秒。测试、程序和探针未启动，原厂跨 SDK 运行对照与位置修正后的坐标语义仍待验。见 `evidence/BLOB_MASK_INPUT_STATIC_AUDIT_20261101.md`。

### ImageSource 目录订阅的安装件取证

本机 ImageSource 原生导入、导出及指令证实文件夹订阅的枚举与手动导入不同。订阅扫描顶层并比较四种小写后缀，使用 Windows 自然排序；新增和删除可请求所属流程运行，修改只标记待刷新。已在全链设计审计固定本机 RVA、原件哈希、已有宿主消费者、监听退役、冻结列表、刷新和调度合同。明确保留手动列表，订阅运行表不得反向覆盖。目录订阅与勾选执行、刷新关闭的首次行为、同名排序及重命名待进一步核对。本轮为静态取证与设计更新，未修改产品代码，未启动程序、测试或探针。

### ImageSource 目录订阅接入正式链

增加正式目录、刷新和流程触发字段，目录路径纳入资源声明。公共 OpenFileDialog 行模板复用原件图标及行壳，Windows 宿主提供目录弹窗。宿主游标拥有监听，首次扫描在取图捕获阶段进行；原件四种后缀和自然排序进入同一冻结图像准备器，结果条只读投影目录，手动路径仍保存原值。目录新增、删除通过主窗请求事件所属根流程；私有草稿不触发正式运行，配置变化、节点删除及关闭退役旧监听。

本机 Process 的目录重建条件不读 RefreshImage，该值仅决定后续列表回报。本方据此将执行侧刷新与列表投影开关分开。新增真实窗口用例覆盖自然排序、大小写后缀和子目录排除、Blob 像素、两种刷新状态的新增触发、保存重开。全量 Release 0 错误、3312 警告、14.77 秒。未启动测试、程序和探针。订阅与勾选运行组合当前明确拒绝，使用全部图片模式。忙时拒绝和事件合并、缓存重建、首次回显属宿主策略，远程目录、重命名和原厂动态待核。

### 目录触发在配置更换时的有效请求

复核发现旧配置请求排队后会按节点身份去重丢弃新配置请求。主窗现保留同批不同上下文，派发前核验仍有效的配置，两种回调先后顺序都能接入当前目录的正式 ImageSource → Blob 执行。关闭清空待派发上下文，工厂重置、换游标及释放集中尝试所有退订和序列释放，再汇总异常。补真实主窗回归，检查新目录文件与 Blob 像素。全量 Release 0 错误、3302 警告、14.56 秒。程序、测试及探针未启动，原件同状态动态未确认。

### ImageSource 拼接与工具自有跨轮状态

核定 ToolRuntimeExecutor 每轮创建并释放 IToolSession，原件拼接则跨轮保存模块缓冲。本批增加图像源处理状态能力，Runtime 创建和操作状态，宿主仅按方案、节点代次及调试作用域持有和借阅，独立于解码缓存与游标签名。节点或窗口退役释放根拥有者，在飞借阅排空后释放。拼接、起点、高度、整数清空订阅进入正式字段和原件模板，拼接主图、附加灰度与 Blob 共用正式资源发布。高度初值 2000，范围 0–20000。

已复核本机 F88A4550… 模块的 StitchImageOutput 导出及缓冲分支，外部 REA 的另一哈希运行记录继续单列。新增正式连续轮次、私有隔离、关闭重开和发布不可变用例，以及真实窗口到 Blob 的帧、像素和前序整数清空订阅用例。全量 Release 0 错误、3302 警告、14.51 秒。未启动测试、产品程序或探针。同哈希原厂运行、彩色格式切换、错误恢复及实际释放时序待统一验收。

### 拼接处理状态的退役与失败恢复

处理状态借阅接入本轮 IHostInvocationGate，退役调用在状态工厂前拒绝，Prepared 和输入准备器释放后拒绝继续借阅。补正式输入用例检查关闭根拥有者时保留在飞借阅、排空后只释放一次及退役后不再创建状态。原件样本对应的正式拼接用例补高度变化、越界失败及恢复、零高度旁路、宽度失败及恢复、工厂关闭后旧发布仍可读取。全量 Release 0 错误、3312 警告、14.96 秒。程序、测试和探针未启动，同哈希原件取消及错误时序运行未确认。

### 两工具架构基线与字符过滤执行链

用户再次明确：用同一方案中的图像源和 Blob 打通完整架构及工具相关链路，为后续工具开发建立正式基线；节点及连线、调试窗口、参数引用、算法、图像与结果显示、保存重开及资源释放均属范围，不以两个独立工具能运行代称整链完成。

本批依据已有 REA 原生 Process 及控制样本，在冻结设置解析之后、宿主取图之前接入纯执行过滤，正式执行与私有调试共用。Runtime 执行器及直接图像源会话使用同一规则。字符不匹配以显式抑制映射既有 NotExecuted，复用既有调度就绪条件跳过后继 Blob，不产生本轮输出；关闭过滤保留但停用两项引用。新增规则样本、正式及私有相机零采集请求、两工具后继抑制与恢复且不推进游标用例，已编译未运行。全量 Release 编译 0 错误、6087 警告、22.52 秒，日志位于本机临时目录 SunEyeVision-character-filter-build.log。未启动测试、程序或探针。原件样本与本机模块哈希不同，同版本动态、字符编码边界、连续窗口表现仍未确认。主设计、架构总览及控件台账同步，不能从编译通过推导原件体感达标。

### 两工具保存重开后的处理状态与引用覆盖

沿 SolutionDocumentWriter/Reader 的正式 Fields、CurrentValues、ParameterBindingOrder 往返及宿主处理状态所有权复核。新增完整方案用例：保存前两次 ImageSource → Blob 运行；重开恢复字符输入、过滤值及整数清空的变量身份；检查新会话拼接首帧前缀归零、次帧使用本会话历史；再修改变量抑制后继并恢复、清空拼接，核对主图与 Blob 二值图像素和同帧身份。用例已编译未运行，不能作为上述行为已通过的证据。全量 Release 0 错误、3302 警告、14.16 秒，临时日志 SunEyeVision-source-reopen-build.log。未启动测试、产品程序或探针。主设计与架构总览同步；原厂保存策略、实际窗口交互和异步性能仍待验。

### ImageSource 原件类别与组合顺序恢复

沿安装件 AlgorithmTab 的完整类别与 MultiTrigger 复核，确认目录本地模式、清空订阅的拼接父条件及末张停止的自动切换父条件在现行 Wpf 组合已存在，不重复新造。发现拼接与目录订阅在声明中被并入触发设置，曝光/增益另设相机类；本批按原件恢复基本参数、触发设置、订阅图片文件夹三类及各类字段顺序，显示名称排序同步。字段 ID、模型和执行路径不变，相机取帧超时保留宿主参数并邻接曝光/增益。固定原件25项控件、完整XML/哈希、类别、外部列表及消费者映射 evidence/ImageSource类别控件原件与消费者映射-20261111.json，方案存图继续列未实现。新增类别序列用例，并扩展真实窗口拼接开关往返后子项显隐与保值用例，已编译未运行。全量 Release 0 错误、3302 警告、14.61 秒，临时日志 SunEyeVision-source-category-build.log。主设计、架构总览和控件台账同步；未启动测试、程序或探针，不将声明归位代称原件体感达标。

### 旧来源视觉链的消费者核对与退役

复核 BlobUiComposition、BlobRoiUi 和公共模板：SourceSelectionField、SourceChoice/SourceChoiceSelection 无产品及测试消费者；RegionSourceModeSelector 只经 DeclaredFieldRow 与 DeclaredFieldsView 两层无人调用的 UseRegionSourceSelector 入口引用。已删除两套自绘来源控件、专属来源模型、两层安装入口和 BuildSlotContent 专属分支。正式图像来源仍走 EnumerationG，正式 ROI 仍走现有原件选择及继承组合，没有新增替代控件。src/tools/tests 的 C#/XAML 已无旧入口及类型引用。NativeAssistUiDraftControls 仍被 Blob 正式消费，不按 Draft 名称误删；其跨输入与设置边界继续按主设计核对。全量 Release 0 错误、5992 警告、18.21 秒，临时日志 SunEyeVision-source-visual-cleanup-build.log。主设计、架构总览和控件台账已同步；未启动测试、程序或探针，不能以死代码清理代称整链体感达标。

### Assist 延迟字段的重入和关闭接缝

复核三方式共享十叶输入、延迟字段创建与退出路径，发现来源工厂同步关闭组合后仍可能安装返回控件，重入切方式亦可能被旧显隐遍历覆盖；输入宿主只在读取前检查退出。已集中修正 NativeAssistUiDraftControls 的返回后存活检查、最新方式投影循环、关闭后的模式及来源提交拒绝，以及 DeclaredInputFieldsView.ComposeInputEditor 在读取和配置回调后的检查及未接纳行释放。原字段与模式职责不变。新增关闭期间返回行、重入切方式及输入读取关闭用例，已编译未运行。全量 Release 0 错误、5992 警告、18.16 秒，临时日志 SunEyeVision-assist-lifecycle-build.log。主设计、架构总览与控件台账同步；未启动测试、程序和探针。此为宿主生命周期保护，原件同类时序未确认，不能据此宣称位置修正全链或体感已验收。

### 方案存图的原件记录核收与保存完整链设计

核收 D:/MyWork/Rea逆向/docs/plugins/image-source-embed-evidence.json：外部 ledger 记录另存、删除源图、重载后的4×4像素摘要一致。其 runtimeSource 原始文件在迁移仓库不存在，不能将历史 runtimeSha256 当作当前核验或程序集哈希；120张原生源码结论仍只有其引用，安装件 BooleanTips 的120张提示及默认关闭已直接读取。外部 VMX 嵌入实现及失败诊断未转签原厂事实。证据快照 evidence/ImageSource方案存图原证与宿主缺口-20261111.json。

盘点确认本方资源声明仅字段/用途/基数，Resolver 只检查和重定位路径，保存与读取没有文件嵌入资产及会话租约链。主设计 image-source-embedded-asset-design 集中列出工具声明/准入、冻结保存、候选读取、作用域转交、正式/私有取图和图像条的消费者与验收，明确资产路径适配、另存/历史身份、目录快照与文件变化一致性尚待定案。方案存图继续未实现，不只加开关、不借图像变量冒充本地列表嵌入。本轮为证据与设计进展，无产品变更，未重复构建，未启动测试/程序/探针；架构总览同步。

### 方案存图原始正文恢复及生命周期合同核定

发现缺失的原件 C 正文实际保存在归档 JSON 的 structuredContent.raw_result.value。恢复 SaveModuleData 与 SaveImageFileToSol 到 evidence，SHA 分别 B556956B…FCE81ED、4CB7F0C9…E8D2EC06，与 embed ledger sourceAnalysis 完全匹配。归档模块91EA…与本机F88A…保持分列，本机 dumpbin 静态指令另证 SaveModuleData RVA171A0 的120附件计数转出及 SaveImageFileToSol RVA18210 的0C800000h新分配边界。不能由此宣称超120原件报错，亦不能把分配分支当成方案体积上限。原始运行文件仍缺失。来源详见 evidence/ImageSource保存方法原件正文恢复-20261111.json，完整方法正文与本机指令均已归档。

核定现行 SaveAs 只重定位冻结输出、活动资源基址保持不变，资产不得改写历史；Reader 候选资源沿既有 DecodeScope→Session 转交，关闭先拒绝未排空活动、退役运行/节点/历史/发布，后释放解码资源。两处解析器创建出口、私有调试继承和图像条消费者已定位，后续共用同一会话资产事实，不新增第二套生命周期。资产声明与格式、目录快照、编码一致性及超量实际列表语义仍待最终合同；功能未实现。本轮为原始证据恢复和设计定案进展，未改产品、未重复构建、未启动程序/测试/探针。主设计与架构总览已同步。

### 保存重载原件闭包与正式文件来源身份统一

从归档JSON继续恢复 SaveDataInfo、LoadModuleData、ProcSavemoduleFinishEvent 正文，身份及SHA见 evidence/ImageSource保存重载退出原件闭包-20261111.json。确认登记计数、120循环与128物理槽两层边界、重载区分主/勾选列表及附件写文件后接入图像列表、保存结束清260个缓冲槽。保存期缓冲不作为运行缓存；归档和本机哈希分列，缺文件/超限的最终重开列表和取消回调仍待动态对照。

实际消费者发现 ImageSourcePolicy.CreateLocalPlan 仍以 ImageSourceSettings.FileReferences 说明名解析，而资源声明、图像条加载及投影检查使用 local.paths。已统一正式取图字段身份，既有运行选择用例改为仅接受准确ID，检查一次解析并继续拒绝解析未勾选起点。全量 Release 0错误、3302警告、14.82秒，临时日志 SunEyeVision-resource-field-identity-build.log。主设计与架构总览同步；未启动测试/程序/探针。方案存图仍未实现，不以原文闭包和身份修正代称功能完成。

### ImageSource SN跨轮状态与来源阶段修正

复核实际调用发现 ImageSourceRunSequence 每轮以 SNInitialValue 准备 input.sequence，Runtime 原样输出，运行SN并未递增。按原件 Process/SetParam 及本机 this+3B0 指令，新增工具自有 ImageSourceRunState，将SN和拼接子状态纳入既有处理状态拥有者；正式与私有生命周期隔离，直接IToolSession拥有同一实现，释放与同步执行使用同一生命周期锁。相机有效帧接纳后、拼接前递增，本地成功处理后递增；初值变化重设，无关参数和游标重建不重设，过滤不消耗，SDK仍为0。原件32位有符号加一行为保留。

已纠正旧用例将初值直接当输出的预期，新增跨轮、无关参数、本地解码/拼接失败恢复、私有隔离、修改初值、过滤及相机采集/拼接失败用例；两工具保存重开用例同步检查新会话SN起算与过滤不消耗。全部已编译未运行。原件哈希与同机指令分列见 evidence/ImageSource-SN状态原证-20261111.json。全量Release 0错误、3302警告、13.99秒，临时日志 SunEyeVision-source-sequence-build.log；主设计、架构总览和控件台账同步。未启动测试、程序或探针。失败时部分输出发布及原件动态仍未确认，不据编译宣称整链体感达标。

### 失败阶段结果合同与旧图借阅复核

核收原 camera ledger 的无触发、无效拼接和恢复记录：无图时仍有失败统计/帧号/SN，错误码亦不能单独代表成功。证据快照 evidence/ImageSource失败阶段结果原证与合同差异-20261111.json。已确认本方Capture仅Success生成Values，ImageSourceOutput要求完整图像，故上轮内部SN修正未闭合失败字段发布。主设计 image-source-failure-result-contract 明确统一输出声明的实际字段可用性、失败后继跳过、旧reader独立借阅、结果树/图像/历史的消费者边界；不先造Partial接口或误标PartialSuccess，实际部分结果功能未实施。

新增真实 ImageSource→Blob 成功、删除源图失败、覆盖原路径恢复的完整用例，核对失败无当前图像及Blob发布、旧主图/二值图仍可借阅、恢复的新帧/二值像素和SN；断言不固化所有失败标量永久为空。最终全量Release 0错误、3302警告、14.20秒，临时日志 SunEyeVision-source-failure-chain-build.log。按用户要求未启动测试/程序/探针。本轮无产品代码变化，主设计、架构总览和控件台账同步，原件失败保图/提示/取消时序仍待对照。

### 失败字段从工具输出到原件结果树的集中实施

已贯通 InputValues.CaptureAvailableOutputs、ImageSourceOutput、DeclaredOutputSnapshot、ResultPublication 和 ResultPresentationModel：保留同一完整输出声明并校验实际已产生字段，成功仍强制全部必填值；Failed可携带字段，取消和字符抑制不接纳。相机有效帧后处理失败发布本轮SN、已提供帧号/时间、来源及设备，图像缺失；先释放未发布图像，随后以独立空资源范围持有标量输出。结果树仍显示失败，图像显示成功门禁、调度前序门禁和ParameterBindingService源成功校验均保持，不将字段可读当执行成功。

新增字段类型与成功完整性拒绝、快照最后reader释放、失败结果表呈现及图像缺失用例，并扩展实际相机拼接失败为SN8/帧42/设备可读、两图缺失、恢复SN9。全部已编译未运行。最终全量Release 0错误、3302警告、14.28秒，临时日志 SunEyeVision-failed-fields-build.log。主设计、架构总览和控件台账同步；未启动测试/程序/探针。采集失败发生于工具调用之前，原件失败计数、模块状态和原生错误码完整映射仍待接入，不宣称全部失败语义或原件体感已完成。

### 相机实际采集失败分类、冻结输入与失败计数接通

按原件实际GetImage失败分支及设置重置分支，集中贯通公开 CameraFrameAcquisitionException、CameraRuntimeManager 的实际 Adapter.CaptureAsync 分类、ImageSourceHostInputPreparer 冻结失败输入、ImageSourceRunState 计数、Runtime及同一结果reader。只转换已分类取帧失败，取消、配置、身份及恢复/释放异常保持传播；聚合清理失败不冒充普通相机结果。计数为可选Int32字段，成功和后续处理失败读回累计值，采集失败不产生SN/帧号/图像；非相机不填。SN和失败次数同步接入正式手工来源。原件依据与接缝见 evidence/ImageSource采集失败次数原证与接缝-20261111.json。

新增真实宿主连续失败、未分类错误、成功恢复、私有隔离、设备更换和实际经过本地模式的重置用例；设备管理用例同步验证公开错误分类与曝光/增益恢复。全部已编译未运行。全量Release 0错误、6087警告、22.69秒，临时日志 SunEyeVision-camera-failure-count-build.log。主设计、架构总览和控件台账同步，未启动测试/程序/探针。计数重置当前只能处理Runtime实际观测的来源/设备变化，未执行的中间设置及同值显式重置仍待闭合；模块状态和原生错误码全映射、原机动态和窗口体感未验。

### 已接纳设置事件贯通正式/私有状态重置

已用FieldChangeRevisions记录宿主输入节点SettingsSchema字段的实际接纳变化。正式戳归节点生命周期，提交及Undo/Redo统一在RetireChangedDomains推进；私有ApplyFields/PreviewFields分叉，RebaseAcceptedDraft继承，运行值回读和显示值不推进戳。HostInputFactoryContext冻结正式/私有戳，处理上下文只读已声明设置，ImageSourceRunState用来源/设备戳重置失败次数、用初值戳重设SN。无第二套参数模型、历史运行状态或文件格式改动。

新增连续失败后设备无运行切回、Undo+Redo、显示往返不重置、初值改回重设、私有来源/初值往返、提交重基继续计数、同值不造戳及正式私有隔离的完整宿主用例，已编译未运行。全量Release 0错误、6087警告、22.92秒，临时日志 SunEyeVision-configuration-revisions-build.log；消费者及哈希见 evidence/ImageSource设置事件与状态重置消费者-20261111.json。主设计、架构总览和控件台账同步；未启动测试/程序/探针。本批替代前批"只处理运行观测变化"的限制。底层同值SetParam与UI事件对应、模块状态、原生错误码和原机体感仍未确认。

## 2026-11-09 VM 原厂 SDK 证据收口（当前有效）

已从 `D:\Program Files\VisionMaster4.4.0` 的发布目录、官方样例、XML/CHM 和 REA 元数据确认 VM 存在两条不同 SDK 产品线：`Development\V4.x` 下的 Platform SDK 面向外部应用（`VM.Core`、`VM.PlatformSDKCS`、`VMControls`、`VmSolution.Load/CloseSolution`、`VmProcedure.Run`、模块回调、前端/渲染控件）；`MVDAlgorithmSDK` 面向算子/工具作者（`MVDSDK.*`、`MVD_Algorithm.Net.dll`、参数控件、算法句柄和部署工具）。证据见 `docs/collaboration/evidence/VM原厂SDK发布边界静态取证-20261109.txt`。

当前 `PluginDevKit` 只能归入 Tool SDK，不能再作为 VM 外部应用 SDK 的等价物。最终发布边界分为 `sdk/platform/net9.0`（外部软件门面）和 `sdk/tool/net9.0`（工具作者包）。Platform SDK 主链已落地到 `src/Platform.Sdk/PlatformRuntimeHost.cs`，覆盖方案打开/保存/关闭、流程/节点/设备身份枚举、单次与连续流程运行、结果订阅/退订和资源释放；Group、真实设备连接控制、渲染控件和真实 VS 外部工程验收仍未闭口。
## 2026-11-09 VM SDK 取证后当前 Release 复核（当前有效）

依据 VM 原厂 SDK 两条产品线静态证据，未把 `PluginDevKit` 错标为外部应用 SDK。重新执行 `Run-NeutralReleaseProbe.ps1 -SkipBuild`：PASS，正式载荷重新封签 `F76B379F3163FE4829521F3F89AF975A1CA3CADFCB9D1F73AAFA583C2C53264C`；Plugin.SDK 退役门禁 PASS（现行消费者 0、历史目录旧引用 20）。生产参数窗探针同一 Release 复核：ImageLoad 绑定/首显 `491/1364ms`，重复 Tab `0ms`；BlobAnalysis 绑定/首显 `1176/441ms`，ROI 组合 `1782ms`，运行页/结果页空闲构造 `2157/1380ms`，首次第 2/3 页 `409/412ms`，重复切换 `0/29/43ms`；两工具 Session→正式参数控件→捕获/回执→关闭释放 PASS。该读数只确认当前代码链无回退，不宣称 VM 动态性能等效。
## 2026-11-09 Platform SDK 合同代码回归（当前有效）

已依据 VM Platform SDK 静态程序集与官方样例新增 `src/Platform.Sdk.Contracts`，公开 `IPlatformRuntime/IPlatformSolution/IPlatformExecution/IPlatformResultSubscription` 及方案、流程、节点、结果合同；不引用 Application、Execution、Persistence、UI 或旧 Plugin.SDK。发布目录为 `PluginDevKit/sdk/platform/net9.0`，模板为 `PluginDevKit/templates/PlatformApplication.Template`，构建脚本为 `PluginDevKit/scripts/build-platform-sdk.ps1`。模板已明确覆盖父级 Tool SDK 默认路径，干净验证工程 restore/build/run 成功并输出 `SunEyeVision Platform SDK contract 1.0`；仅有离线 NU1900 警告。证据见 `docs/collaboration/evidence/PlatformSdk合同边界回归-20261109.txt`。

Platform host adapter 已接入真实 `DocumentSessionManager`、执行批次、连续运行和结果回调；干净模板已完成 V5 方案加载、流程/设备身份枚举、空流程执行、连续运行一轮后停止、关闭释放验收。Group、真实设备连接控制、WPF 渲染控件扩展以及 Visual Studio 实际启动/附加验收仍未完成；不得把这些未验项写成已完成。
## 二、已定案、不要再反复讨论

- **版本口径**：方案格式版本 = V5；解决方案文件 `4.solution` 的 `SchemaVersion` = 2（与节点配置格式版本无关）。
- **数据根**：缺省 `%LocalAppData%\SunEyeVision`；配置存在但读不出直接抛异常，不兜底（rule-015）。本机 `Root=D:\SunEyeVisionData`，方案/日志零迁移。
- **Debug 与 Release 数据根不同**：`dev\Debug` 无 `App.ini` 时走缺省根，用 Debug 做人验看不到 `D:\SunEyeVisionData` 的方案，不是迁移失败。
- **构建口径**：唯一解决方案 `SunEyeVision.slnx`（61 工程）；全量 `dotnet build SunEyeVision.slnx -c Release --no-incremental`。

## 三、未验证（不得记作已通过）

统一基线真实程序人工验收、VS 设计器验证、真实相机/硬件、VM 动态差分、主仓推送（当前未 commit / 未 push）。

## 2026-11-09 当前执行批次：VM 参数树对齐后的重型控件延迟物化（最新）

本批继续沿 VM 已确认的 `TabControl.ItemsSource + ContentTemplate`、`ParamListView` 数据树和参数根重建入口落地，不再增加探针或第二套兼容架构。Blob ROI 继承参数按选中模式延迟创建、ROI 资源字典提升到 `Plugin.Wpf` 共享资源边界；`FieldLayout.Advanced=true` 的普通字段只登记状态和模板身份，真正的 VM 逆向编辑器在高级分组可见时才物化；`DeclaredFieldAccess` 仍是唯一值、错误、提交、事件和释放合同。代码落点为 `src/Plugin.Wpf/Fields/DeferredDeclaredFieldVisual.cs`、`DeclaredFieldsView.cs`、`src/Plugin.Wpf/Presenters/Regions/NativeRoiInheritanceBinding.cs`、`NativeRoiUiDraftControls.cs`、`tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobRoiUi.cs`、`src/Plugin.Wpf/Themes/Generic.xaml` 和 `RoiSelectControl.xaml`。

最终验证：全量 Release 构建 0 错误、6064 个既有警告；`Run-NeutralReleaseProbe.ps1 -SkipBuild` 与 `Test-PluginSdkRetirementGate.ps1` 均 PASS；最终正式载荷 SHA-256 为 `31F58A1ADB399D0B03D17E8176CBFC4407FA802255C52A2A5B6B4CEB130E01F0`。串行生产探针 ImageLoad 绑定/首显为 523/1415ms，BlobAnalysis 绑定/首显为 1459/476ms，首次切换第 2/3 页为 326/438ms，重复切换为 0/27/44ms；Session→正式参数控件→捕获/回执→换节点/关闭释放 PASS。读数受机器和资源状态影响，只确认链路无回退和重复 Tab 复用，不冒充 VM 动态耗时。

当前判断：首屏全量 eager 已移除，重复 Tab 已进入复用路径；剩余成本集中在未预热页的首次视觉树物化。VM 动态 UI 线程耗时、实例复用边界和释放线程顺序仍未取得，不能写成 VM 动态等效。完整记录见 `docs/collaboration/evidence/VM启动到工具链完整落地复核-20261109.txt` 与 `docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-atomic-final.txt`。
## 2026-11-09 当前收口与外部边界回归（当前有效批次）

本批没有重新引入旧自研工具或旧 Plugin.SDK。当前正式 Release 仍只准入 ImageSource 与 BlobAnalysis，启动→方案管理器→主工作区壳→发布准入→节点配置→参数页→执行/结果→关闭释放链保持单一实现。最终全量 Release 构建 0 错误、6064 个既有警告；正式载荷封签为 `31F58A1ADB399D0B03D17E8176CBFC4407FA802255C52A2A5B6B4CEB130E01F0`。NeutralReleaseProbe 与 Plugin.SDK 退役门禁均 PASS。

最终生产探针读数：ImageLoad 参数绑定/首显 `523/1415ms`、重复 Tab `0ms`；BlobAnalysis 绑定/首显 `1459/476ms`，首次切换第 2/3 页 `326/438ms`，重复切换 `0/27/44ms`。Session→节点→正式参数控件→捕获/回执→换节点/关闭释放 PASS。读数受机器和资源状态影响，不能宣称 VM 动态等效；Visual Studio 实际附加和人工视觉验收仍未完成。

两工具 Runtime 回归补跑：ImageSource `31/31`、BlobAnalysis `39/39`；ImageSource 本地文件格式用例使用独立 `S2_CASE_ROOT`，未把缺失环境变量误报为代码失败。

PluginDevKit 已按发布边界回归：SDK Contracts/Runtime/WPF DLL 与两个模板构建成功；这只确认外部 DLL 边界和模板构建，不把 VS 实际附加写成已通过。证据统一回填 `docs/collaboration/evidence/VM完整链路代码落地回归-20261109.txt` 与 `docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-atomic-final.txt`。

门禁口径已补全：历史工具目录仍有 20 个旧 SDK 项目引用，但全部不在 `SunEyeVision.slnx`、不进入当前 Release；现行解决方案消费者、现行工具消费者和现行源码实时扫描均为 0。历史目录保留作 VM 重建输入，不作为当前产品依赖。

## 2026-11-09 Blob 参数双轨清理（当前有效增量）

审计确认 Blob 运行参数仍保留一整套"元数据不完整时按代码逐行创建控件"的备用分支；当前 VM 参数 XML 已完整覆盖该链，因此本批删除备用分支和对应的第二套 `BlobRunUiState` 编辑器/范围/阈值状态。现在 `BlobParameters.xml`、`BlobParameterDefinition`、`ParameterPresentationTree`、`DeclaredFieldsView` 是唯一参数来源，`BlobRunUiState` 只保留支持项校验和会话退役。

验证：Blob WPF 专项构建 0 错误；全量 Release 构建 0 错误、6062 个警告；NeutralReleaseProbe PASS，正式载荷 SHA-256 为 `F91AC7CBF9D03B12F98E2DDAA3F7BC29E9CB5161369AF072F3AD2BF8A85DBBC2`；生产探针 ImageLoad 绑定/首显 `761/1460ms`，BlobAnalysis 绑定/首显 `1497/442ms`，首次切换第 2/3 页 `311/419ms`，重复切换 `0/34/47ms`，两工具 Session→正式参数控件→捕获/回执→关闭释放 PASS。Plugin.SDK 退役门禁仍 PASS（现行消费者 0，历史旧引用 20）。

最终校正：正式状态的 `RetireInteractions` 保持为会话租约的无操作退役，不把正式参数树字段误标为失效；重新全量 Release 构建 0 错误、6064 个警告并封签 `31F58A1ADB399D0B03D17E8176CBFC4407FA802255C52A2A5B6B4CEB130E01F0`。同一构建生产探针 ImageLoad 绑定/首显 `523/1415ms`，BlobAnalysis 绑定/首显 `1459/476ms`，首次切换第 2/3 页 `326/438ms`，重复切换 `0/27/44ms`，两工具关闭释放 PASS。
## 2026-11-06 迁移 REA 历史架构与源码树方案

已从 `D:\MyWork\REA逆向测试` 迁入与架构、源码树、插件边界、配置宿主、调试会话和验收模型直接相关的原文，位置为 `docs/07-工具逆向/历史-REA逆向测试/`。旧项目的 `VisionReplica.slnx` 和 `src/Vision*` 已确认是"独立兼容重建"实施树，不是已证明等同 VM 的最终树；旧 `*.vmx.json`、未闭合 `.sol`/SDK/ABI 和独立实现不直接迁入产品。历史树已回填 `docs/01-方案设计/2026/11/06/最终源码树评审稿-证据分级-20261106.html`，下一步按 VM 真实程序集、模板、NodeData 和生命周期证据逐项目映射。
## 2026-11-09 SDK 合同构建后 Release 复核（当前有效增量）

新增 `Platform.Sdk.Contracts` 后重新执行全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -v:q`：0 错误、62 个 NU1900 离线漏洞源警告。`build-platform-sdk.ps1 -Configuration Release` 成功发布 Platform 合同 DLL/XML/manifest；NeutralReleaseProbe PASS，最新正式载荷封签 `8DF30ADD27EFEE87BC69A8D956C5D4B2C877F0DFD891A19B239A5EA9D4EC2D0E`；Plugin.SDK 退役门禁 PASS（现行消费者 0、历史目录旧引用 20）。本次未改 UI/工具生产链，沿用最近生产探针记录；Platform 合同模板的独立工程验证见 `docs/collaboration/evidence/PlatformSdk合同边界回归-20261109.txt`。
## 四、最近 12 个批次

下面按倒序排列，最早批次已归档：`history/CURRENT_TASK_ARCHIVE_2026-11.md`、`history/CURRENT_TASK_ARCHIVE_2026-10.md`。

## 2026-11-09 VM 原厂启动阶段顺序纠偏（当前有效批次）

用户提供的原厂阶段截图与既有 REA 静态捕获已对齐：VM 的可见顺序是"品牌启动页（快速过渡）→ 方案管理器可操作 → 用户打开方案后工作区加载覆盖层 → 完整方案工作区"。原件静态调用链为 `App.OnStartup → animatedSplashScreenWindow → Program.BeforeStartMian → InitMainWindow → ParamTabControl.PreLoadPage/启动信号 → ShowFrontendUI/ShowMainWindow → ProjectManager.OpenRecentSolution`。证据见 `docs/collaboration/evidence/VM启动阶段顺序静态对照-20261109.txt` 及 `D:\MyWork\REA逆向测试\docs\architecture-recovery\startup-order-cil-evidence.json`；该捕获明确 `runtimeExecuted=false`，因此只确认顺序，不确认动态耗时。

此前本方 `App.OnStartup` 先显示 `MainWindow` 和方案加载覆盖层，直接跳过方案管理器，属于错误的本方设计假设。本批已调整为无直接方案参数且未隐藏起始页时，先显示 `StartPageWindow` 收集用户选择；选择完成后才创建/显示 `MainWindow`，并保持方案加载覆盖层连续到方案打开/新建完成。直接文件启动和隐藏起始页是显式旁路。VM 品牌启动页当前只作为快速阶段证据，未重新把自研 Splash 用作长耗时容器。

UI Release 构建已通过（0 错误），并已重新封存正式 Release 载荷；尚未把人工启动验收结果冒充为自动通过或 VM 动态等效。可验收程序：`D:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`。本次清单共 502 项，构建输出仅有无法访问 NuGet 漏洞服务的 NU1900 警告。

### 2026-11-09 新建方案拖放节点链路修复

人工验收发现：新建空方案后流程 Tab 已可见，但 Tab 对应画布在同一调度周期尚未完成 `DataContext` 接管，工具箱拖放入口仍从画布直接读取当前流程，错误提示"无法获取当前工作流"。同时该提示未传递主窗口 Owner，可能被主窗口遮挡。现已在 `src/UI/Services/Interaction/DragDropHandler.cs` 的拖放入口增加主窗口选中 Tab 的同身份回退与画布补绑，并将两处异常提示的 Owner 设为画布宿主窗口。正式 Release UI 构建已重新通过（0 错误）并完成载荷封存；人工需复测"新建方案→拖入图像源/Blob 节点→弹窗层级"。

## 2026-11-09 VM 对齐组装批次：方案管理器与两工具完整参数页

本批按 VM 的完整可见语义收口，默认启动链固定为"快速启动阶段→方案管理器可操作→用户打开/新建方案→工作区加载遮罩→节点配置状态→隐藏状态完成当前页→窗口可见→其余页空闲预热→Tab 复用已建页"。`App.OnStartup` 的默认路径已先显示 `StartPageWindow`，选择完成后才创建 `MainWindow`；指定文件和隐藏起始页是显式旁路。

ImageSource 单页直接完成；Blob 工厂按 VM 静态的参数树、`ContentTemplate` 和 Tab 宿主语义改为"选中页先就绪、窗口首显后空闲逐页预热、已建页复用"。`BlobUiComposition.ReadyTask` 只在当前选中页完成后结束，首帧不再等待运行参数和结果页，也不允许当前页以空壳先显示；未预热页首次点击仍有一次明确记录的物化成本，后续 Tab 只改变选择。换节点、关闭和迟到回调仍沿现有 `SettingsLease`、代次和 `Dispose` 合同，未引入第二套生命周期。

UI Release 已重新构建并封存，0 错误；本轮全量 `SunEyeVision.slnx` Release 构建 0 错误、6132 个既有警告，PluginDevKit 宿主准备后载荷以 `A7E3C36BB5DD76E82E147514A8F43ACEC45827AE9BF873D783D2E691D238D9F1` 重新封签。正式程序路径：`D:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`。本批尚未把人工启动、节点打开和三页内容验收写成已通过；下一步只做一次完整正式 Release 回归并记录启动、方案管理器、节点窗口、首次/重复 Tab、换节点关闭和迟到回调结果，不再追加零散探针。

## 2026-11-09 VM 对齐启动准入与当前工具集收口（当前有效批次）

本批完成启动到当前两工具准入链的代码收口。正式 Release 准入探针确认此前 ComponentLoading 约 4.4–4.9 秒的根因是把 CLR 可信 .NET/WPF 框架副本从发布目录重复映射并递归装载；现由 `src/Plugin.Runtime/ReleasePayloadAuthority.cs` 依据 `TRUSTED_PLATFORM_ASSEMBLIES` 跳过共享框架副本，保留产品锚点、发布清单和实际工具依赖身份校验。正式探针总耗时 722ms，ComponentLoading 264ms（RuntimeAssemblies 54ms、RuntimeDefinitions 209ms、ToolDirectory 7ms）；准入程序集只有 `SunEyeVision.Tool.ImageSource.Runtime`、`SunEyeVision.Tool.BlobAnalysis.Runtime`，组件 4 个，工具箱可用项 2 个。证据见 `docs/collaboration/evidence/启动链准入分段回归-20261109.txt` 与同名 JSON。

`src/UI/Shell/Controls/Toolbox/ToolboxViewModel.cs` 已隐藏未部署、未映射和未准入的历史目录条目，当前工具箱只展示 ImageLoad 与 BlobAnalysis。两工具仍按 VM 目标在隐藏状态完成参数页/ROI/运行/结果页组装，Tab 只切换已存在页，窗口关闭继续走代次、租约、退订和 Dispose 链。正式 Release 已重新构建封存，0 错误；仍未把 VM 动态耗时或人工视觉验收写成已通过。

## 2026-11-09 VM 与 CodeBuddy 重构计划对齐审计（当前有效批次）

已完成一次性对齐审计，证据见 `docs/collaboration/evidence/VM-CodeBuddy对齐审计-20261109.html`，并已回填《框架架构总览》。VM 静态链确认 `AlgorithmTabRoot → ParamsConfigControlBase → ParamsTabUserControl → ParamListView/ParamUserControl → DataTemplate`；当前 `ParameterPresentationTree`、逆向 Native 控件、ImageSource 单页完成/Blob 选中页就绪与空闲预热、统一租约/退订/Dispose 链已覆盖可迁移边界。

CodeBuddy 的"数据树 + DataTemplate"作为目标形态保留；"静态命令式装配必然是性能根因""立即删除 `DeclaredFieldsView`""VM Tab 已证明完全零构建"不作为未经验证的删除依据。下一批按完整链实施：先把 `ParameterPresentationTree` 固定为唯一数据树入口，建立 `Plugin.Wpf` 模板注册表和字段覆盖契约，再按 ImageSource→BlobAnalysis 替换宿主；ROI、范围对、结果显示和资源 Owner 通过显式槽迁移，覆盖生产链后再删除对应旧装配分支。VM 同版本动态 UI 线程耗时、复用边界、释放顺序和真实外部 SDK 仍未确认，继续单独列项。

## 2026-11-09 VM 启动→准入→工具窗口链路落地与验证一致性修复（当前有效批次）

本批把用户要求的完整生产入口固定为一条链：快速启动阶段→方案管理器可操作→用户选方案→轻量主工作区壳→发布载荷准入→服务与方案管理器→方案会话→节点配置窗口隐藏组装→首个参数页可见→其余页复用→执行/结果→关闭退订与资源释放。当前正式目录只准入 ImageSource 与 BlobAnalysis；其他自研工具不进入扫描、预加载或启动耗时，后续按 VM 原件重新逆向迁移。

两工具的 `ParameterPresentationTree` 已是唯一参数页数据入口，`DeclaredFieldTemplateRegistry` 统一把字段身份、编辑器类型、范围对、对话组合映射到 VM 对齐模板槽；原厂 `ParamListView` 的 ItemsControl/DataTemplate 负责行呈现，ROI、运行参数、结果显示等重型控件仍以显式槽挂接，不复制业务状态。当前宿主在窗口显示前完成选中页并等待 `IToolSettingsViewReady`，其余 Blob 页在首帧后空闲预热，Tab 只选中已登记或正在完成的页；会话代次、租约、回调退订和 Dispose 继续由统一宿主合同收口。

正式 Release 准入回归已通过：`Run-NeutralReleaseProbe.ps1 -SkipBuild` 返回 `PASS`，确认发布清单、ImageLoad→BlobAnalysis Runtime 执行、保存/重开/取消排空及无 WPF/UI/SDK 泄漏。该脚本已增加探针源码时间戳检查，默认发现探针落后于 `ReleasePayloadAuthority` 或探针源码时自动重建；只有明确 `-SkipBuild` 才复用现有产物，避免再次出现旧构造签名混入新载荷的 `MissingMethodException`。当前仍未把 VM 动态耗时、真实人工视觉验收或 VS 外部工程验收记作通过。

全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -v:minimal` 已通过（0 错误；本轮串行复核为 6132 个既有警告），正式 Release 载荷重新封存；随后门禁、NeutralReleaseProbe 和 RuntimeEditorProductionProbe 均通过。当前阶段可交付基线为"启动顺序、两工具准入、选中页首显、其余页空闲预热、Tab 复用、Runtime 执行和生命周期收口"，未实现工具保留在历史逆向层，不再混入本阶段负担。

Blob 的就绪合同已调整为"当前页完成后才结束"，避免窗口首显空壳；其余页只在首帧后的空闲阶段组装并缓存。对应生产探针见 `docs/collaboration/evidence/VM完整链路代码落地回归-20261109.txt`：当前 BlobAnalysis 首选页绑定约 1333ms，窗口首次呈现约 382ms，ROI 组合约 1929ms，运行页/结果页空闲预热约 1565/1120ms，首次 Tab 292/427ms，重复 Tab 0/20/36ms。该结果证明去掉全量 eager 可以收回启动等待，但重型槽首次物化成本仍未达到 VM 目标；下一批按 VM 数据树/DataTemplate 复用继续降低 ROI、运行和结果槽的首次构造成本。
## 2026-11-08 产品主线优先级调整（当前有效批次）
本轮手动验收确认当前窗口先显示空壳再组装，造成空白停留。已定位为 `MainWindowRuntimeEditing.InitializeRuntimeEditor` 先调用 `window.Show()`，再异步 `BindDraft`。代码已调整为隐藏状态完成首个参数页和结果预览后再显示，节点失效时清理隐藏窗口。VM 原件静态补证见现行架构总览，动态耗时仍未冒充已确认。

外部 SDK 的 Contracts、Runtime/WPF 分层、发布 DLL 边界、模板和启动脚本继续保留，VS 外部附加和真实外部工程验收延期到产品主线完成后。当前优先完成 ImageSource→BlobAnalysis 的窗口宿主、参数模板、Tab 复用、回调退订、换节点关闭、结果 reader 和资源释放整链。外部 SDK 只维护架构能力和发布入口，不作为本阶段产品验收阻塞项。

## 2026-11-08 Blob 首屏与首次切 Tab 时序纠偏（当前有效批次）

上一版为降低首帧耗时，把 Blob 当前页组合也放入 `SystemIdle/ApplicationIdle`，形成"窗口先可见、当前页随后补挂"的跳变，并让第一次切 Tab 承担目标页首次组装成本。该时序与 VM 的可见语义不一致。现已改为：当前选中页完成组合后才允许窗口 `Show()`；首次及后续 Tab 切换在当前 UI 回调中完成目标页组合；非当前页继续在首屏之后后台预热并由 `_builtPages` 复用。新增 `IToolSettingsViewReady` 就绪合同，宿主等待 Blob 当前页就绪后再进入可调试状态。UI Release 构建 0 错误，实际运行窗口人工截图待本轮统一验收。

按需编辑器加载后的 `RuntimeEditorProductionProbe` 已复测。ImageLoad 参数页绑定 682ms、首显 1320ms，BlobAnalysis 绑定 1098ms、首显 357ms，ROI 组合 1348ms，运行页首次 387ms，结果页首次空闲组装 1108ms，重复切换 0/18/55ms；两工具实际 Session、参数捕获、回执和关闭释放均通过。证据见 `docs/collaboration/evidence/ImageSource-Blob-按需编辑器加载回归-20261108.txt`。

## 2026-11-08 启动链差异审计（当前有效批次）

现有启动页是对启动链过慢的补偿，不是 VM 目标设计。`src/UI/App.xaml.cs` 当前在主窗口创建前依次等待正式插件准入、数据根、服务、解决方案管理器、主题和启动决策；历史启动日志 `D:\SunEyeVisionData\Logs\2026\11\08\SunEyeVision_172710.txt` 显示主窗口 `Show` 约在启动后 60183ms，`Service.Initialize completed` 已到 54026ms，主要耗时发生在更早的插件准入/程序集扫描阶段。VM 原厂静态入口包含方案加载开始/进度/结束回调，结合用户实机观察可确认其工作区壳先进入可见状态，再在壳内推进方案和工具装配。本批先固定差异与证据，不把启动页继续当作性能方案；下一批按"壳先可见、后台准入与方案装载、状态覆盖层、失败可恢复"设计启动链，先分段测量 Plugin.Load、Service.Initialize 和方案恢复，再实施整体重构。

已先修正启动前一处确定的重复成本。`ReleasePayloadAuthority` 不再默认在每次启动时对全部 606 个发布条目约 834 MB 逐个做 SHA-256，正式组件及其依赖仍在实际加载路径通过 `VerifyApprovedFile` 校验，产品锚点仍在准入阶段校验；需要完整封包审计的探针可显式传入 `verifyAllFilesOnAdmission:true`。UI Release 构建通过 0 错误。首次完整启动时间线仍需在不受外部探针进程干扰的环境中复测。

进一步分段确认：`--toolbox-probe` 在组件编辑器全部预加载时约 27024ms，其中 `ComponentLoading` 约 26015ms；编辑器延迟加载后总计约 4882ms，`ComponentLoading` 约 4066ms，`PayloadValidation` 约 121ms。现行 `RuntimeDirectorySnapshot` 只在启动准入时构建 Runtime 定义，WPF 编辑器按工具首次打开通过同一发布身份和依赖校验链解析。该改动已完成 UI Release 构建 0 错误，独立启动根已在约 6.4–6.8 秒进入起始页，记录见 `docs/collaboration/evidence/启动链分段回归-20261108.txt`。完整用户数据根方案恢复仍需单独复测。

本轮新增的早期隔离时间线已把启动边界拆开：Splash 显示约 `1412ms`，`Plugin.Load` 任务返回约 `1425ms`；在缺少 `release-payload.json` 的编译输出上，完整准入断言约 `1508ms` 明确失败。该结果说明隔离编译目录不是正式发布载荷，不能用它推导 VM 或正式产品的启动时长；正式启动耗时必须使用包含发布清单的 Release 目录，并同时记录 `Plugin.Load`、`Plugin.Assert`、`Service.Initialize`、`SolutionManager.Initialize` 和 `MainWindow.Constructed`。新增 `startup-trace.txt` 仅在隔离启动根写入，避免早期日志系统尚未初始化时丢失关键阶段。

随后使用同卷硬链接挂载正式载荷并替换当前 UI DLL 完成链路回归：`MainWindow.ContentRendered` 约 `2.148s`，`Plugin.Load` 完成约 `6.934s`，`Service.Initialize` 完成约 `7.214s`，`SolutionManager.Initialize` 约 `0.4ms`，`MainWindow.WorkspaceInitialized` 约 `7.338s`（注入耗时 `73.1ms`）。这证明壳先显的实现已生效，当前主要成本集中在插件准入/组件加载；完整证据见 `docs/collaboration/evidence/启动链壳先显与正式载荷回归-20261108.txt`。该夹具调整了临时载荷清单哈希和宿主身份，只用于代码链路回归，不作为正式发布验收；正式 Release 仍需重新封包复测。

旧图像预热探针已移除：`ImageWarmupMode`、`PrewarmThreadPool()` 和 `image-warmup-*.json` 只服务早期线程池对照，不是图像解码，也不是 VM 能力。当前 ImageSource 的解码/复制只在正式 Runtime 执行链中按输入发生；启动不再设置线程池下限、不等待空任务、不预解码图像。历史对照日志仍保留作证据，但不再作为产品启动路径或优化目标。当前约 4.25 秒的 `Plugin.Load` 成本来自 Runtime 准入和依赖装载，不能记为图像预热成本。

Roslyn 预热另行核对：`src/CommonControl/ScriptEditorCore.xaml.cs` 仍保留 `WarmupForStartup()`（MEF + `ScriptWorkspace`）和编辑器实例级 `WarmupCompletionAsync()`；它们现在只在真正创建 Script 编辑器控件时按需启动，已从 `tools/SunEyeVision.Tool.Script/ScriptTool.cs` 的插件加载静态构造中移除，`ScriptCompiler.Warmup()` 也不再在应用启动时执行。它们是当前自研 Script 编辑器的补全、语义诊断和 Emit 依赖，不是 ImageSource/Blob 的窗口组装，也不是 VM 已确认的通用启动预热。VM 逆向目前只确认 `Program.InitMainWindow -> MainData.PreLoad()` 预加载菜单/工具箱元数据，以及 `VMControls.WPF.RenderThreadPool` 渲染线程池；未发现 Roslyn、MEF 或同等 C# 编辑器预热证据，不能把当前 Roslyn 方案冒充 VM 设计。

脚本工具的处理决定：在 VM 脚本工具完整入口、编辑、编译、结果和释放链迁移完成前，不删除 Roslyn 编辑器能力；但不再把它作为全局启动优化依据。ImageSource/Blob 阶段不调用、不等待 Roslyn 预热。后续按 VM 脚本实现替换时，删除 `ScriptEditorCore` 的 MEF/Workspace/Completion 预热及 `ScriptCompiler.Warmup()`，同时删除只服务于该编辑器的引用和诊断链，并以脚本窗口首开、再次打开、编译、取消和关闭回归作为删除门禁。

脚本工具减重原则已确定：当前 `ScriptEditorCore` 把 MEF、Workspace、语义高亮、补全、签名帮助、诊断、引用快照、增量文档桥和 Emit 缓存全部带入工具窗口，不能继续作为 VM 对齐目标。VM 已确认的脚本入口是可复用的脚本窗口、执行/停止事件、参数读写、运行回报和关闭重建；后续实现只保留这些生产链所需的窗口状态、脚本文本、参数合同、执行适配器和回调释放。高级 Roslyn 能力若经证实仍需保留，只能作为独立可选编辑器层，不能进入应用启动、ImageSource/Blob 或所有工具的公共宿主链。

启动范围重新固定：当前产品阶段只有 ImageSource 与 BlobAnalysis 进入正式工具目录和启动准入，其他工具全部按 VM 原件重新逆向，未完成迁移前不得被启动扫描、程序集预加载、编辑器构造或脚本预热带入。启动目标改为 VM 语义的最小主工作区壳、菜单/工具箱元数据预加载、方案会话恢复和按需工具装配；不再为历史工具保留启动兼容路径。当前 `PluginManager`、服务初始化、解决方案恢复和 Splash 的分段耗时仍需继续收口，只有在正式载荷仅含两工具且主窗口壳接管稳定后，才能把启动页移除并宣称 VM 对齐。

本轮按用户决定直接收口 VM 静态对齐目标：应用启动不再创建 Splash，主工作区轻量壳作为唯一启动窗口先显示，插件准入、服务初始化、方案管理器和方案恢复都在该窗口遮罩内推进；ImageWarmupMode、线程池空任务预热和图像解码预热探针已从代码移除。UI 宿主同时移除了未被 ImageSource/Blob 使用的 CommonControl、AvalonEdit 和 Roslyn 包引用，避免脚本编辑器重依赖进入启动进程。VM 动态启动未再执行，当前结论仅基于 `Program.InitMainWindow -> MainData.PreLoad()`、工具箱/菜单元数据预加载、主窗口回调和 `RenderThreadPool` 静态证据。UI Release 编译复核通过，3315 个既有警告、0 个错误；正式载荷启动与两工具人工验收仍单独记录，不把静态对照记作动态性能通过。

用户启动正式 Release 时发现 `SunEyeVision.Foundation.dll` 的 SHA 与 `release-payload.json` 不一致。原因是全量构建在封包阶段失败后把新 Foundation 写入旧 Release 目录，旧清单仍被保留。已重新完成 UI Release 构建和 `Seal-ReleasePayload.ps1` 封包，当前清单与 `SunEyeVision.Foundation.dll`、`SunEyeVision.UI.exe` 的 SHA 已一致，清理了 Release 目录中残留的 CommonControl、AvalonEdit 和 Roslyn 文件。该修复覆盖发布身份门禁，未执行 VM 动态启动测试。

## 2026-11-08 启动页去留门禁（当前有效批次）

启动页暂不删除。当前它承担的是启动反馈，不是性能实现。若在主窗口仍要等插件准入、服务初始化和启动决策完成后才创建的状态下直接删除，用户会看到进程已启动但桌面无可交互窗口的空等待，行为比现有启动页更差。

VM 对齐目标固定为"主工作区壳先可见，壳内显示加载遮罩和阶段进度，插件准入、服务初始化、方案恢复及工具装配在同一窗口内推进"。启动页只保留到主窗口壳完成接管，不能作为长期替代方案。迁移期间必须同时满足以下门禁：

- 主窗口壳显示前只允许执行最小进程、单实例和 WPF 资源初始化，不得先构造完整工具编辑器树。
- 壳显示后工具目录、服务、方案和设备恢复均通过可观测阶段推进，未就绪时禁止误操作，失败可恢复。
- 默认 `D:\SunEyeVisionData` 与隔离启动根分别记录 `ShellVisible`、`PluginAdmission`、`ServiceInitialize`、`SolutionReady` 和 `MainWindowReady`，不得用隔离根读数代替默认根。
- 只有在默认根连续回归确认主窗口接管后不再出现空白等待，才允许移除独立 Splash。此前保留 Splash，不把"隐藏启动页"记作 VM 对齐完成。

现有证据：独立启动根约 6.45 秒完成服务初始化、约 6.81 秒进入启动决策；默认根最近一次约 5.3 秒完成服务初始化、约 8.8 秒主窗口出现，历史默认根记录曾在服务初始化前耗时 21–54 秒。该差异说明启动链仍需整体调整，不能通过删启动页掩盖。

本轮已先落地可见时序修正：`HandleStartupDecisionAsync` 在构造并绑定完整主窗口后立即 `ShowMainWindow("workspace shell")`，再打开起始页或通过主窗口遮罩恢复指定方案；不再等待起始页返回后才显示主窗口。指定方案恢复使用主窗口内的"正在打开方案"遮罩，并以 `finally` 保证异常时关闭遮罩。该改动只修正可见顺序，没有宣称已解决插件准入和服务初始化的总耗时。

本轮首次 UI Release 构建仍受现有 Release 载荷封装脚本退出码 1 影响；源码编译阶段未出现 C# 错误。随后使用单进程隔离输出 `C:\Users\houzhongjie\.codex\visualizations\2026\11\06\01a1ae75-37f7-7281-b9b8-51a3cf21dc1a\build-ui-serial` 执行 `-t:Compile -m:1 --no-restore`，结果 2755 个既有警告、0 个错误，证明本轮启动与窗口拖动改动可编译。正式 Release 载荷封装仍需在发布环境单独复测，不能把隔离 Compile 结果记作完整发布验收。

针对"启动页和方案窗口鼠标无法移动"，已把 `PluginManager.LoadPluginsAsync` 从 UI Dispatcher 调用改为 `Task.Run` 后等待。插件准入只做载荷文件、程序集和运行目录准备，没有 WPF 线程亲和性；这样准入期间 Splash 和已显示的主壳可以继续接收输入。方案文档读取和主窗口构造仍保留在所属 UI 线程，后续按方案会话的 owner-thread 合同单独收口，不能把这次线程边界调整写成完整异步启动。

当前仍有一个明确未收口点：`InitializeSolutionManager()` 发生在 `HandleStartupDecisionAsync` 创建并显示 `MainWindow` 之前，但它必须在 UI owner thread 构造 `DocumentSessionManager`，不能直接搬到后台。 已在 `App.xaml.cs` 增加 `SolutionManager.Initialize` 与 `MainWindow.Constructed` 独立耗时埋点，下一次使用新隔离 Release 运行时只需读取启动日志即可确认两段真实成本。已先把无会话所有权的 `ServiceInitializer.InitializeServices()`（适配器/日志/静态服务注册）改为后台等待；方案管理器继续在 UI 线程创建，保持 owner-thread 合同。这样只消除服务注册阶段的 Dispatcher 阻塞，仍未让主工作区壳提前于方案管理器创建，因此不得宣称"启动过程已与 VM 完全对齐"。下一步是测量方案管理器构造和 MainWindowViewModel 构造，再决定是否需要独立的轻量壳。

已使用单进程隔离输出 `C:\Users\houzhongjie\.codex\visualizations\2026\11\06\01a1ae75-37f7-7281-b9b8-51a3cf21dc1a\build-ui-serial` 完整执行 WPF `Build`（`-m:1 -p:SelfContained=true`，跳过既有载荷封装），3322 个既有警告、0 个错误；该结果覆盖 WPF MarkupCompilePass2，证明本轮启动埋点和起始页拖动改动可编译。正式发布封装仍需单独验收。

窗口移动语义也已补齐：Splash 原先没有拖动事件；起始页原先只在鼠标命中 `TextBlock` 时调用 `DragMove`；主窗口方案加载遮罩覆盖标题栏时也没有拖动入口。现已让 Splash 内容区、起始页标题区和主窗口加载遮罩都可拖动，并排除起始页按钮、复选框等交互控件。该修正解决"窗口本身没有移动合同"的问题，与 UI 线程是否响应是两个独立验收项：若拖动事件未触发，是窗口设计缺口；若事件触发但 `DragMove` 期间无响应，则是 Dispatcher 被同步工作占满。

方案打开链已拆为后台候选准备和 UI 会话发布：文件读取、JSON/资源候选解码、工具声明冻结改由 `DocumentSessionManager.PrepareOpen` 在后台完成，`OpenPrepared` 只在 owner thread 安装和发布 `DocumentSession`。旧的 `Open` 保留给现有同步调用和测试，当前 UI 外部打开路径已切到新链。正式 Release 构建尚未通过，原因仍是共享 `build_temp` 被并发构建占用和现有 Plugin.Wpf 产物不完整，不能把该批记作已验收。

## 2026-11-08 外部 SDK 验收与旧 SDK 状态纠偏（能力已保留，验收延期）

现行解决方案已不再包含旧工具项目，Plugin.SDK 退役门禁实测为现行消费者 0、工具消费者 0、源码实时扫描 0、SDK 目录条目 0，已满足删除后的最终状态。外部组件身份门禁通过，Runtime、WPF 模板和 SamplePlugin 使用独立输出目录构建通过，记录见 `docs/collaboration/evidence/PluginDevKit外部组件验收-20261108.txt`。外部宿主 PID 32868 已启动，VS 附加仍需在同一管理员会话中取得调试器附加回执。用户截图中的 `VisionFramework.SaveRoundTripChecks.exe` 与 `GroupRoundLifecycleChecks20.exe` 不属于本仓库或 PluginDevKit runtime，不能作为本项目外部 SDK 失败证据。
## 2026-11-07 性能与 SDK 状态纠偏（历史批次，SDK 状态已由 2026-11-08 批次覆盖）

本批把"窗口性能"和"旧 SDK 迁移"拆成两个可并行收口的批次。性能问题不再用连续加探针拖延，现有探针已足以确认本方的第一处真实阻塞：Blob 专用组合和字段编辑器在当前页同步创建重型 WPF 视觉树。REA 本地重建项目的源码证据显示其链路是 `AlgorithmTab` 数据树 → 模板注册表 → 统一窗口壳 → 状态刷新，不能把该项目的测试耗时直接当作原厂 VM 数值，但足以确定当前 V2 的命令式专用组装方向与目标链不同。

性能批次的最快落地顺序固定为：先保留现有生命周期和字段合同，把普通字段与 Blob 专用控件统一登记到数据树和模板槽，不在 Tab 切换回调中临时拼装整组视觉树。然后交付 Blob 的一条完整链，首次显示只创建当前页可见模板，复杂 ROI、运行和结果槽按页首次激活创建一次，重复切换只刷新状态，不重建视觉树。最后以现有 `RuntimeEditorProductionProbe` 验收首帧、首次页切换、重复切换、重绑定和释放。该批独立于旧 SDK 删除，不等 SDK 全迁移才开始。

状态纠偏：`src/Plugin.SDK` 目前只剩空目录，旧 SDK 源码已移除，但正式工具项目仍有直接 `ProjectReference` 和命名空间引用，构建日志已出现 `MSB9008`。因此消费者迁移仍未完成，不能把旧 SDK 退场记作完成，也不能把当前破损引用当作迁移结果。性能批次和 SDK 批次共用新的 Contracts/Runtime/Wpf 合同，但分别验收。

旧 SDK 消费者迁移的新增事实：CircleCircleMeasure 等测量工具的 Runtime 仍可识别到新 `Plugin.Editing.Wpf` 中的基础执行类型，但 WPF XAML 仍直接引用旧 `ToolDebugControlBase`、Measure、ROI、ParamBinding 和 ResultPresentation 控件。旧控件源码已在历史提交 `8e76389de` 从 `src/Plugin.SDK` 删除，当前 `Plugin.Wpf` 尚未形成完整的一对一公共控件闭包。因此不能做纯命名空间替换；先迁公共控件/模板闭包，再迁工具 XAML 和生命周期。

本批已落地并验证：`DeclaredFieldEditor` 对高级字段初始延迟创建输入视觉树；`Plugin.Wpf` Release 构建 0 错误；`RuntimeEditorProductionProbe` 使用当前源码运行 ImageLoad 与 BlobAnalysis 实际 Session，Blob 首屏 364ms，首次第 2/3 页 988ms/1877ms，重复切换 38ms/37ms，捕获、回执和关闭释放通过。该结果只证明第一刀没有破坏生产链，不能记作 VM 等效性能完成。

随后接入 `Plugin.Contracts.ParameterPresentationTree`，通用宿主按树构造页和分组。最新探针仍通过完整 Session、回执、重复切换和释放，Blob 首屏 327ms，第 2/3 页首次切换 627ms/1419ms，重复切换 16ms/27ms。性能数据受运行环境影响，仍以生成层重构完成后的同场景回归作为验收，不把本次回归读数当作 VM 等效值。

数据树已修正同名分组的高级/普通状态拆分，避免改变既有布局语义。修正后 Plugin.Wpf Release 构建 0 错误，生产探针继续通过 ImageLoad/BlobAnalysis 的捕获、回执、重复切换和释放；当前一次读数为 Blob 首屏 433ms，第 2/3 页 1316ms/1546ms，重复切换 25ms/44ms。

## 2026-11-08 两工具首帧调度与空闲预热收口（当前有效批次）

探针逐段对照确认，上一版首显仍把非当前页的 Blob 运行参数和结果控件放在 `DispatcherPriority.Background`，而宿主首帧边界使用 `ApplicationIdle`，因此首帧会等待全部页面组合。已将非当前页预热延后到 `DispatcherPriority.SystemIdle`。首帧先显示窗口，当前页在首个 SystemIdle 阶段按 VM 参数树和模板入口完成，其他页随后一次性创建并缓存，重复切换只刷新状态。

本批验证：全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -p:UseSharedCompilation=false` 0 错误，UI Release 构建 0 错误；ImageSource Runtime 31/31；BlobAnalysis Runtime 38/38；窗口、调试、三页、连续运行和释放 UI 回归 16/16；`RuntimeEditorProductionProbe` 通过两个真实节点的参数绑定、窗口首显、三页切换、重复切换、捕获、回执和关闭释放。最新读数为 ImageLoad 绑定 711ms、首显 1302ms；BlobAnalysis 绑定 772ms、首显 321ms；Blob 第 2/3 页首次切换 221ms/254ms，重复切换 17ms/28ms。证据保存在 `docs/collaboration/evidence/ImageSource-Blob-VM首帧调度空闲预热回归-20261108.txt`。

这次调整把 Blob 首显从同一环境下约 4.6 秒降到约 0.32 秒，首次 Tab 切换维持在约 0.22–0.25 秒，证明首帧阻塞主要来自页面同步组装和非当前页预热调度，不是 Tab 控件切换本身。仍未取得 VM 同机动态耗时，不能据此宣称已经达到原厂数值；Blob ROI 组合约 1.07 秒、运行参数字段树约 1.22 秒、结果显示组合约 0.78 秒仍是本方下一轮控件模板复用和视觉树构造的主要成本。

## 2026-11-08 旧 SDK 消费者闭包映射建立

已对 20 个正式旧 SDK 项目引用做源级分组，补充运行时基类、参数绑定、ROI/测量几何、结果显示、调试窗口、通信和子程序六类闭包的实际消费者、替代合同和阻塞条件。映射报告见 `docs/collaboration/evidence/PluginSDK消费者闭包级迁移映射-20261108.html`。本批只建立可执行映射，不修改冻结的 20 个旧工具，也不恢复旧 SDK 兼容程序集。

下一实施批固定从 ImageFilter、ImageThreshold、Morphology 中选择一条完整 Runtime/WPF 链，先补齐新参数树、图像预览、结果呈现和统一窗口生命周期，再按组迁移通信、测量、控制流/脚本和标定/匹配工具。每个项目的 ProjectReference、XAML 命名空间、资源 URI、旧基类和测试引用清零后，才从旧 SDK 退役门禁移除。

旧 SDK 退役门禁已修正为显式 UTF-8 读取映射 JSON，并保留 UTF-8 BOM，Windows PowerShell 5.1 与 pwsh 均可执行。`-ReportOnly` 实测列出 20 个直接消费者，正式门禁实测以 20 个项目阻断删除，`src/Plugin.SDK` 目录条目为 0。该阻断是当前正确状态，表示旧工具尚未迁移完成，不能删除或恢复兼容程序集。脚本位置为 `scripts/verification/Test-PluginSdkRetirementGate.ps1`，执行输出作为本批门禁证据。


## 2026-11-05 统一开发基线建立；数据目录门禁退出转入实施

main 已指到合并提交b895d697e（双父f5e6b8fde工作流重构分支codex/workflow-toolbox-dataflow＋d44904f38旧main，164个旧提交历史全保留），内容取重构分支为骨架。回退点backup/main-before-sync=d44904f38，origin/main仍9a0834ef8。判定依据：两线自共同祖先4431cfa02（2026-11-02 19:05）分叉3天，main 164提交/分支325提交；git merge预演2012个冲突，其中1719个为同一批VM原件被两边改成不同名字（1106个改名后内容完全相同）。分支承载框架重构（Workflow 39＋Application 55＋Domain 19＋tests 202提交），main承载控件原件迁移（55个代码提交）＋验证夹具（72提交）。定案：以分支为骨架统一，main控件成果按原件映射表分批回补，不做逐文件人工合并。

环境补齐：libs\halcon\runtime\x64-win64\原为空目录（被gitignore屏蔽），Directory.Build.targets:262强制要求存在；已从SunEyeVision_Dev-camera复制25个native dll（369 MB）。唯一解决方案SunEyeVision.slnx（61工程）统一基线全量构建已核实：dotnet build SunEyeVision.slnx -c Release --no-incremental → 0错误/6150警告/2:36.35，与历史全量口径一致（此前738系增量构建读数，已作废），日志.codebuddy\tmp\build-d1-full.log，产物Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe。工具插件新布局为dll直接落根目录，不再有plugins\<Tool>\子目录。前一批对f03 Release的冻结随统一基线切换结束（该冻结仅针对f03旧产物，不适用于统一基线新产物）。

启动门禁事件（只记录，代码未修）：用户首次启动统一基线被「选择软件数据目录」窗口阻断，报「应用目录定位格式不受支持。」。根因是本机%LOCALAPPDATA%\SunEyeVision\data-location.json与D:\SunEyeVisionData\suneyevision-data.json仍为schemaVersion:1（旧main于2026-11-05 11:08写入），基线要求2。临时处置：两处手改为2（原值备份.codebuddy\tmp\*.bak），解决方案文件4.solution的SchemaVersion字段＝2（与节点配置格式版本无关）可读；v2不再读Config\user-settings.json，主题/窗口布局/KnownSolutions偏好会丢一次。该手改属环境处置，不代表门禁已解决。data-location.json／suneyevision-data.json marker／schemaVersion 协商／dataId 目录身份均为我方自建；门禁整体退出的依据是原厂无「选数据目录」环节。

计划依据与分工：门禁退出是既有待办不是新需求（本文件「App数据目录门禁仍未实施退出」「数据目录旧Locate/选择窗/Remember门禁须整体退出」，重构计划「旧统一数据目录启动门禁纳入退出」）。2026-11-05起：rea-investigator负责VM应用层逆向取证＋工具层材料盘点（只写docs\collaboration\evidence\）；framework-refactor负责框架重构，第一批即数据目录门禁整体退出（只改src/tests）；doc-keeper只维护文档。

VM逆向取证实况：本机确认原厂D:\Program Files\VisionMaster4.4.0（此前探测漏检）。Applications\App.ini仅[DefaultSection] SaveSol=C:\Users\houzhongjie\Desktop；AppData.dat为XML产品标识；程序数据全在安装目录（data/ProjectParam/PublicFile/ColorStyle/Lang/Resources/Tools/platforms/myLibs），用户目录仅%APPDATA%\VisionMaster4.4.0\res；VisionMaster.exe字符串SaveSol命中8次，DataPath/UserData/WorkingDirectory/data-location均0次；.sol方案由用户自存（桌面5个），安装目录内无。初步结论：原厂VM无「选择软件数据目录」环节、无数据目录版本协商。注意D:\MyWork\参考项目\VisionMaster是HeBianGu开源示例，不是原厂VM，不作原件证据。

数据根定案（已定案，实施中，非已完成）：数据根写在程序目录App.ini的[Data] Root，不对用户弹窗（原厂VM 4.4的Applications\App.ini全文只有[DefaultSection]与SaveSol=C:\Users\houzhongjie\Desktop，路径配在程序目录ini）。规则：缺省%LocalAppData%\SunEyeVision；配置缺失用缺省并记Info日志；配置存在但读不出直接抛异常、不兜底（rule-015）；本机配Root=D:\SunEyeVisionData，现有方案/日志零迁移。同时退出：不读data-location.json、不读suneyevision-data.json marker、不做schemaVersion协商、不弹「选择软件数据目录」窗。今天的启动拦截是schemaVersion 1≠2造成的真实拦截，仅手工改现场两个文件应急，代码层未修，不得记述为门禁已修复或启动问题已解决。`[Data] Root` 与数据根概念为我方自建，仅借用「配置放程序目录 ini」这一形式；原厂 App.ini 只有 `[DefaultSection] SaveSol`。

数据根在两个输出目录的实际差异（本机实测）：Release\SunEyeVision_v1.0.0\SunEyeVision\App.ini 存在，内容 [Data]／Root=D:\SunEyeVisionData；dev\Debug\App.ini 不存在，Debug 构建启动走缺省根 %LocalAppData%\SunEyeVision（该目录存在，是更早期历史数据）。因此用 Debug 构建做人工验收时看不到 D:\SunEyeVisionData 里的方案和配置，别误判成数据迁移失败；要让 Debug 指向同一数据根，在 dev\Debug 放一份同内容 App.ini 即可。两目录均被 .gitignore 屏蔽（[Rr]elease/ 与 /dev/），App.ini 属本机部署产物、不入库，换机器需重新放一份。D:\SunEyeVisionData 下现有 Config/Solutions/Logs/.recovery，与 ApplicationDataPaths 子目录约定一致；.recovery 与 suneyevision-data.json 属已退出概念的历史残留，仅磁盘残留、不影响运行。

工具身份合同（发布态与开发态强弱不同）：发布态才有 release-payload.json ＋ ToolGroups.xml 的强准入；开发态 dev\Debug\plugins\<Tool>\ 实测仅 dll/pdb/deps.json，无任何清单，形态接近原厂松散目录，不得据此认为每个工具目录都有清单。

版本口径：当前方案格式版本＝V5（历史 V3→V4→V5，本文件旧段出现的 V3/V4 为当时冻结值）。解决方案文件 4.solution 的 SchemaVersion 字段＝2，与节点配置格式版本无关。

状态分列：已通过＝统一基线Release全量构建0错误、产物已出；临时处置＝本机两处数据目录标记手改schemaVersion（原值备份.codebuddy\tmp\*.bak）；实施中＝数据目录门禁整体退出（数据根已定案：程序目录App.ini的[Data] Root，本机Root=D:\SunEyeVisionData且方案/日志零迁移；退出data-location.json/suneyevision-data.json marker/schemaVersion协商/选择目录窗）、main控件原件分批回补；未验＝统一基线真实程序人工验收、VS设计器、真实相机/硬件、VM动态差分、主仓推送（未commit/push）。

## 2026-11-04 用户明确整条生产链贯通

工具UI、交互、工作流、图像源与Blob作为同一原型联动实施及自测；工具箱/节点连线/参数引用/正式与私有执行/同次图像结果/历史保存重开/取消关闭释放全部贯通后才签本次批完成。原主方案、矩阵及本批HTML已同步；缺基础保护，不以局部测试替代生产链。

## 2026-11-04 独立框架集成候选已接日志，构建在途

根设计固定984da7d45。新独立树D:/MyWork/line-aiproject/platform-framework-unified-20261104，分支codex/framework-unified-20261104，以984为基线；B日志d14/e174/6a三提交分别接成ce0d56e48/576b77e92/7d5d2e1a2，仅CURRENT/原主HTML冲突按根最新保留，B独立设计及最终JSON回执完整保留；产品冲突0。候选locked restore自然0，日志framework-unified-restore-20261104.log；候选全量Release已自然退出0：6216警告/0错误，2:28.76，日志framework-unified-logging-build-20261104.log；session25112已终态。候选Diagnostics16/File6/Footer7通过0fail0skip，日志framework-unified-{diagnostics,file-logging,footer}-20261104.log；尚非完整主窗/两工具联动自测通过（独立候选树，非根）。根Release未变。B正在只读核总监新Blob数值状态/结果显示消费者，根尚未接Blob新判断批；App数据目录门禁仍未实施退出，不能把日志整合说成启动问题已解决。A冻结其它工具扩展准备交接，C共享唯一草稿/动态输入与公共ROI批仍在途。
## 2026-11-04 已核收框架消息；先两工具原型统一基线

已读原版架构新增tool-layer-new-development-20261104及方案管理静态报告（7633D791…6039E6），总监交接81837f48a及产品ce64已接收，尚未统一到根f03。先现有图像源＋Blob打通新Session：正式Schema/Storage/空态、真实窗口、唯一草稿与历史保存、执行和私有调试快照、同次结果与图像区域reader释放。其它工具在途保留停止扩展；原全平台范围保留，Group/新循环暂停。禁止整仓恢复旧SDK/Workflow/主窗；两Blob候选未提交测试及设计修改原地保护。新主窗空态GetNodeSchema、运行publication.Schema、引用RootPort.Storage已核源码，新Int32状态/Float32总面积消费者仍待整批验证。

详细"框架消息核收与原型统一基线实施批-20261104.html"和原主计划/剩余矩阵同步本阶段。数据目录旧Locate/选择窗/Remember门禁须整体退出；B日志三个提交已固定，App路径接缝由唯一负责人集中调整，原厂资源/日志/恢复路径未证不猜。根人工查看Release继续冻结。独立几何探针修正reader.Values后构建0错0warn，实际Session链未运行不计通过；C公共ROI/宿主自测仍在途，不把子项结果签成完整批完成。
## 2026-11-04 当前人工查看Release保持不变

用户询问进度并确认打开路径。已告知可打开本树Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe；当前产品f03e6347f（几何常量整链源码），正式全量Release0错6154warn，载荷1A289D1261066C1E21C3AC6AE40BB3766C986FAD26FC626A8C211388C0B390E1，247条目只读核对0缺失/0字节差异。已明确承诺用户测试期间冻结此Release；不得再次构建/还原/签封覆盖本树产物，替换前协调关闭。源码分析、文档、并行独立树实施继续。几何七类/嵌套/严格反例的修复前3失败与修复后Persistence48通过已保留，但真实Session/历史/执行专项仍待，不把93上批UI232成绩转签f03。93/D5完整上批证据保留，f03只是当前可查看的新构建，不宣称全平台目标已完成。
## 2026-11-04 V5复杂值与变量原件集中集成完成；全平台继续

根产品93b1ff896，已合C完整95d/30cc/bd49/25072/cbf（原ded/463/873/6d8/7c74）与B完整63e354bfa（原48c，Shutdown保最终词汇捕获/实际变量场景）；146/188/93集中修复退出旧Hal装配后实际主窗依赖加载。最终61工程locked restore/全量Release0错3418warn，载荷D5A32CDD3C28CA68F9292092B3905CA1466BD37EF5E03E90726D155AF48BA054，49关键依赖实际字节相同。变量25/Persistence45/SDK280/完整UI232/公共变量6/Authority25均通过0skip；真实Session非空V5/档案2/保存重开/伪光栅零回调拒绝/TCP/无WPF生产RunChain13/变量Main原件命令和绑定单receipt/结果解绑重绑/隐藏两参数窗/完整App启动与全部reader-Session-目录-Dispatcher-OLE关闭均exit0。complex-variable-approved-*20261104日志、TRX和hash JSON为最终证据；先前失败保留，93本批以后无根活跃验证句柄。

关键依赖修复：仅批准清单PE+SHA；递归实际组件引用，已签封owner.deps经CLR resolver选RID胜出，不猜路径；已加载副本亦验实际bytes，组件入口严格来源保持。RunChain去显式HAL/Kernel bootstrap后仍过；A与B日志线已精确接同Authority文件，不重复合其版本。

原平台主HTML/完整剩余矩阵/全局变量方案及逆向计划/复杂值设计已集中统一，新增统一验证HTML与最终真实双主题PNG。原完整范围不缩：目标批量字段绑定/初始化配置执行/流程局部/档案UI/复杂新运行绑定等未实现保护；六测量/公共绘制、日志、其它工具、SDK退出和最终物理树继续，Group/新循环暂停；人工/VS/硬件/原厂动态/主仓未验。本批自测完成不等人工。

并行A平台measure-integration直接分支/引用/历史/保存中立探针自测通过，但绘制/原件整数下拉UI仍在途，尚未根集成；几何常量canonical未支持为真实共享缺口，与持久storedROI不同，暂保护不伪通过。C独立platform-measure-roi-implementation（63前置），独占公共ROI/字段+RuntimeNodeConfigurationControl，同草稿跨schema候选/实际帧/回调退休已定位，四原件与手势仍在途；隐藏未激活desktop直接CaptureMouse=false，不能冒称物理鼠标通过。B独立platform-neutral-logging（146+精确93前置），已批准中立完整日志设计，正在真实启动/关闭验证，根未合。逆向负责人已集中请求SectorCaliper与CircleAnnulus字段转换/内半径/单位及卡尺绑定证据，不顺手运行原SDK/硬件。主仓总监398a/ce2原13排序/C-R/F6/rad已固定，尚未接本方新Session，后续按真实消费者差异集中核收，不整文件回退旧SDK。
## 2026-11-04 正式开发构建工程图完成；复杂值集成核收中

根源码bd15ef061（a931统一入口/94dc重复签封已纠正），唯一SunEyeVision.slnx实核61工程/0缺路径；四脚本复用既有UI AfterBuild签封，退出旧.sln/App/Legacy视图与旧Workflow构建，MSBuild Rebuild不手删。实际build-full.bat Release0错6150warn/exit0，自动载荷66FCDC574EEE1B8A0538C301DB932F432BE930C06E6B66083349A29C1DB04C4A；同版RunChain13不加载WPF/UI/SDK、隐藏两工具窗口/根删除Undo身份退出0。canonical-build-entry-final-full / production-run / hidden-window-20261104.log。原主方案/矩阵/详细设计同步，VS重新打开与Rebuild所有变体未测，不冒签。

六测量完整HTML转义报告与原JSON快照已入根，资源SHA679BECD8C0E42623BB016D0FB06989B066DD886F2427BB9EDF84B3E545006FAA，报告4AE29D84E5B02B8E94BBAFA2943BEE3E79577F210DE645B6EB1730D6280048F9。已通知A角度包装/九值标定/父级触发，仍有原生入口不签静态穷尽。C候选6d8/文档58e独立全量及存储24/Persistence45/SDK280与真实Session/TCP/RunChain/隐藏App通过；根审发现codec接受任意IReadOnlyStoredRaster与受控实现设计矛盾，C补充7c74限定受控类型、25定向通过，实际保存拒绝链仍在验，未合入。B变量视觉原树唯一写；A六工具正式Authority/UI尚在途，显示真实帧消费由根核，不伪帧/塞ROI。全平台范围不缩，Group/新增循环暂停，人工/VS/主仓分列。
## 2026-11-04 根配置草稿所属查询捕获代次整链修复通过

根产品dafcf3c98（前置f7增加draft验证/真实回归；首次PowerShell保留变量使host替换未执行，before测试真实失败已保留）。host.OwnsRoot实际委托draft捕获scope→现Session.ValidateScope；删除→Undo同实体旧draft仍false/写入拒绝，新draft true且正常可编辑。全61工程Release0错6150warn，载荷1F304C6C643EEA28B59FC2EFD5D07E2337D492375C56850E3B3530B701540906：Editing39/39、根UI23/23、真实Main根操作/复制HALCON/保存重开/双主题/关闭排空退出0；root-draft-generation-*20261104和TRX。根无活跃构建测试句柄。原主HTML与矩阵当前作用域待办改为真实修复回执；联合5bf/1EBE证据保原版范围。

C已从192d48807新独立实施树D:/MyWork/line-aiproject/platform-complex-persistence-20261104启动完整复杂值/持久化/Domain/Application/实际UI非视觉和通信消费者，根不并写其文件；C不改本批两个Editing文件。当前其UI定位联编0错，不计整批完成/不可提前封包交付，旧测试须迁至新规范并真实资源验证。A测量整组设计384c58正核几何/绘制模式与当前frame能力；B变量UI只读设计bad313待根核收。18工具/全局变量/最终目标树/SDK完整范围保持，Group/新循环暂停，人工/VS/主仓分列。
## 2026-11-04 Blob完整新会话与中立画布统一集成通过

根产品5bf65949e（前置3a6画布中立命名、7bf根集合UI、3c可信目录、79域修复、b927采集连续）。61工程locked restore通过，Release0错6150warn；同版载荷1EBEECBE2FF6EA5B48B24DD5E1B7C3515AA5CA41199C3C1762F2E56A36539FFA：冷FixedSoft新testhost1、Blob36、UI227、Canvas27、Shell5全过且0skip；真实主窗根操作/复制HALCON/保存重开/双主题/关闭/OLE及连续暂停恢复/取消与同意Stop-await关闭均exit0，生产工厂RunChain13无WPF/UI/SDK和CoreContinuous15组过。blob-canvas-unified-*20261104+TRX为根证据。源图像/Blob字段与共享控件整批已合，不再标仅独立候选。根无活跃构建/测试句柄。

独立Blob最终243复合常量显示→原件Delete→Unset→父状态清理，UI201/201，失败用例400转正；已与完整36算法/资源专项集中入根同版验证。原件冷Result-first无主窗入口列防御未验。Host.OwnsRoot(draft)单独scope代次准确性仍待共享合同补齐，UI已退休窗口不代签该API。原主HTML和完整矩阵当前摘要统一替换，历史折叠保留。

下一并行批：A从5bf新platform-measure-audit-20261104只读设计六测量工具整组；C platform-sdk-complex-values-20261104细化唯一现合同上的V5/档案2/中立持久ROI和8/16位光栅设计，禁止重造纯值框架或弱化运行frame，未改产品；B变量UI整体只读设计bad313完成待核。其余18工具/全局变量/SDK活跃消费者/最终树完整范围不缩，Group/新循环暂停。未启动用户可见程序，未合主仓、未推送；人工/VS/主仓分列。
## 2026-11-04 可信目录与根集合UI完整集中集成通过

根产品7bf6812c3（3c32可信目录+caba中立名，8ee顶栏真实连续状态，c585根集合UI）；61工程全量0错6127warn。正式载荷BA3699FB31EE268C55BB041FACA43DEF2FFD6B384BE1121995A4F42F1DAD01DC：完整UI225/225、真实工具箱/顶栏1/1、Authority/目录/隐藏两工具参数窗25/25；真实Main根菜单/删除Undo旧窗退休/复制HALCON/保存重开/双主题/关闭排空、连续主窗暂停恢复/取消和同意关闭Stop-await/OLE、真实生产工厂RunChain13含无WPF/UI/SDK加载均退出0。准备组件独立载荷0951E30...真实重入/owner卸载过期拒绝退出0。catalog-root-ui-*20261104和TRX为根证据。首次探针路径、旧连续占位断言失败记录保留，不退回功能。根无活跃句柄。

Blob独立集成树c5da（d390正确Record资源读者、408双图+父状态、b5域修复），全量0错6145warn，完整专项句柄75098正在执行；之前D4D9载荷10资源/RunChain10/隐藏两窗过，不签全部。A继续真实pose常量关闭清除专项；B从7bf新独立树核完整画布中立命名；C复杂值设计46b8已给V5/档案V2与权威字段，待根精确审定后整批实施，未改产品。Host.OwnsRoot(draft)捕获代次缺口保留，现UI已同步退休窗口，不能代签API准确性。原主方案和完整矩阵当前摘要统一替换，原阶段证据折叠保留；18工具/SDK活跃消费者/全局变量/最终树完整范围不缩，Group暂停，人工/VS/主仓分列。
## 2026-11-04 域修复后的正式生产工厂无桌面整链通过

根1c前置79域修复全59工程Release0错6124warn已完成。RunChainProbe真实ImageSourceHostInputFactory的本地/模拟相机、正式/连续/私有调试、变量、失败、取消删除、Undo、V4及释放13组通过，并在Run返回且内部using释放后检查实际程序集：未加载WPF/UI/旧SDK。载荷5EB9C02DD823B77DFDA150EB51DBBF7641EE51CD95D82B6D617B53F19E957623；explicit-domain-integrated-{full-build,seal}与explicit-domain-production-{probe-build,runchain}-20261104.log。无根活跃句柄。全量Blob新专项A待最终固定后集成；C可信目录补中立命名，B根集合UI按C增量接缝核收，禁止整文件回退b927采集/连续。原主HTML和矩阵已同步，本次不签全平台/硬件/VS/人工/主仓。
## 2026-11-04 根集合核心与采集/连续主窗集中集成通过；域适配修复待复验

当前产品79f4ae856，前置b927（C完整82→a142与B cd60整批）、根集合676/669。根集合53工程最终Release0错6110warn，同版9372748341F19903AA766BC38B7B87770EC803EF1D5DDE7FD38C8D2D32B8BB6F：Editing39/Graph39、真实复制运行/删除Undo/V4重开通过，日志root-collection-integrated-final-*20261104。首次RootCollectionProbe私有工具副本失败保留，669将四工具只作构建引用，未绕门禁。

b927完整59工程Release0错6124warn；载荷AD5ABC601EB5C949C8EEB74D55332B5189E0A9A579B9B60041F4A8DE8D52867C：真实隐藏Main工具栏/暂停恢复/取消关闭/同意Stop-await/Dispatcher-OLE释放通过、CameraHost实帧参数/模拟生命周期probe通过、UI13/13、RunChain12、Core连续15通过。image-continuous-integrated-*20261104+TRX。locked restore因新项目图变更，重新求解后既有包版本/内容hash变化0（package-audit JSON），只锁图更新。工业未知域硬件前拒绝，未测试真实相机，不签硬件/VS/人工/主仓。

关键新缺陷：A全新物理线程证明HalconResourceBridge.CopyImage先CopyRegion导致GenRegionRuns按上一幅128格式把256全域裁成128；这解释FixedSoft/Relative按worker顺序不同，LUT不改。79f4（Blob树b5fc0d9ed）统一区域创建/仿射的tsp_clip_region保存-false-恢复，聚合恢复错误并释放未交付region。源码已合，尚未根构建/自测；修复前AD5不能签修复后79。A从be3建Blob新回归树，已同版导入b5，补线程域/六模式/8选择/资源/三页真实窗口，产品唯一由根修改。

根仍另写D:/MyWork/line-aiproject/platform-blob-integration-20261104（当前b5；此前be3完整构建/10合同/RunChain10/Core15/隐藏两窗口通过，未合根）；新fef父状态与7cdf四成员双图声明待集中核收，不追未固定版本。B从b927另树platform-root-collection-ui-20261104接根集合UI完整四操作/统一事务/回执/Tab；C独立可信目录e7/d643候选待根核收，并补命名与退役失败测试。根不并改其文件。全平台原HTML/完整矩阵统一本阶段效果、真实失败和剩余；完整18工具/SDK退出/最终目录树不缩，Group/新循环暂停。当前根没有活跃构建/测试句柄，不含子线自有句柄。





### 2026-11-07 当前对齐批次

- 当前审计基线：分支 `main`，HEAD `71da36329d405e29dab12d1b221269d2e5707ac4`；工作区保留 CodeBuddy 未提交改动，不能把工作区状态冒充正式提交。
- 图像源→BlobAnalysis 作为后续标准链：四个 Runtime/Wpf 工程已分别 Release 构建；Runtime 未直接引用旧 `Plugin.SDK`，Wpf 仅依赖自身 Runtime 与 `Plugin.Wpf`。
- 本批已完成静态源码映射和构建验证；当前源码已重新封存 `release-payload.json`，`NeutralReleaseProbe` 已通过无 UI 装载、ImageLoad→BlobAnalysis、reader、保存重开和取消排空。标准命令已固化为 `scripts/verification/Run-NeutralReleaseProbe.ps1`。下一步进入统一 VM 窗口宿主的首开、Tab/节点切换和关闭释放接入。
- 旧 `Plugin.SDK` 删除继续按消费者迁移门推进：先迁移图像源→Blob 完整窗口/执行链，再分批删除已无消费者目录。
- 外部 SDK 边界已落地到 `PluginDevKit`：`sdk/net9.0` 只发布 Contracts、Runtime、Wpf、Foundation，RuntimePlugin、WpfTool 和 SamplePlugin 已独立构建通过；并在 `_validation/ExternalSdkAcceptance` 脱离主解决方案完成还原和构建。该开发包不再引用旧 `Plugin.SDK`。


## 2026-11-07 工具窗口分层探针收敛（当前批次）

已接手 CodeBuddy 的逐点探针并完成一轮分层定位。`RuntimeEditorProductionProbe` 现输出字段行、页面布局、显隐统计和 Blob 专用组合阶段。当前证据：Blob 基本参数 ROI 组合约 1.16–1.32s，运行参数专用组合约 1.10–1.18s，结果显示专用组合约 1.27–1.65s；通用字段行约0.59–1.16s，显隐约36–93ms，重复切页约20–50ms。根因收敛到工具专用组合首次实例化大量 `DeclaredFieldEditor`/原生呈现控件，不是Tab控件或显隐遍历主导。已回填 `docs/01-方案设计/2026/11/07/工具调试窗口性能取证与架构评审总结-20261107-131704.md`。新增诊断字段位于 `DeclaredFieldsView`、`BlobUiComposition` 和 `RuntimeEditorProductionProbe`，UI Release构建0错误（保留既有警告）。下一步按"数据描述 + 模板槽 + 复杂控件延迟/复用"设计 Blob 专用组合第一批，不直接删除 `DeclaredFieldsView` 或旧 Plugin.SDK。

## 2026-11-07 Blob 延迟组合第一批已落地

Blob 专用 ROI、运行参数、结果显示组合已从当前页同步路径移到 `DispatcherPriority.SystemIdle`。通用字段先显示，组合未完成时提交/确认/调试执行受门禁，异常显式留痕，关闭/重绑定清理待处理页面。探针显示首屏可见约0.29s，第2/3页可见约1.18s/2.03s，重复切页19–33ms，完整提交/释放回归通过。剩余瓶颈为通用 `DeclaredFieldEditor` 行创建，下一批继续做字段编辑器构造与模板复用优化，不删除 `DeclaredFieldsView`。

## 2026-11-07 通用字段级探针与资源作用域优化

新增 `DeclaredFieldsView.LastFieldBuildMs`，确认 Blob 页面单字段编辑器约25–103ms，页面总耗时由字段累加形成。页面构建增加 `EditorResources.BeginSharedResourceScope()`，运行参数通用字段阶段由约0.78s降至约0.61s，提交/释放回归通过。下一批按字段类型拆解 `DeclaredFieldEditor.Render`，优先数值、范围和结果显示字段。

## 2026-11-07 加速执行口径

按最快路径收敛为一条标准链优先：先完成 BlobAnalysis 的普通字段模板/数值范围/结果显示性能批次，固定窗口、Tab、提交、回调和释放合同，再以该合同批量迁移普通工具，最后批量迁移旧 Plugin.SDK 消费者并封存外部 SDK 验收。未接入的延迟字段实验已清理，不作为现行实现。

## 2026-11-07 性能线时间盒决定

本窗口问题不再阻塞全平台重构。已确认生成层设计差异和主要成本，保留已验证的首屏先显、复杂组合空闲组装、提交/释放门禁和分层探针。未验证收益的高级字段延迟实验已撤回。当前批次以 Blob 标准链行为和生命周期通过为出口，字段模板化作为独立后续批次，随后立即转入普通工具和旧 Plugin.SDK 消费者迁移。

公共控件闭包门槛已回填 docs/collaboration/evidence/PluginSDK消费者迁移清单-20261107.html。测量工具迁移必须覆盖调试窗口基类、Measure/ROI 槽、ParamBinding/ConfigSetting 通用模板和 ResultPresentation 槽四组闭包，分别完成打开、Tab、提交、回绑定、回调退订和释放验收后，才可清零对应旧 SDK 消费者。

公共闭包实施进展：已将无业务依赖的 `LabeledControl` 迁入 `src/Plugin.Wpf/Controls`，并接入 `Themes/Generic.xaml`。Plugin.Wpf Release 构建通过 0 错误。`ConfigSetting`、`ParamBinding`、Measure/ROI 和 ResultPresentation 仍待按完整依赖闭包迁移。

归类纠正：`LabeledControl` 来源是旧 `Plugin.SDK` 的本方自研代码，不是 VM 逆向原件，也不作为 VM 控件证据。它只是公共标签布局容器的迁移基础。VM 原件控件仍须依据程序集、模板和资源证据逐项迁移。

ImageSource/Blob 状态纠偏：两个工具的正式项目已经没有 `Plugin.SDK` 引用，普通参数和 XAML 外壳使用新的 `Plugin.Wpf`。但 Blob 当前仍使用 `BlobUiComposition`、`BlobRunUi`、`BlobDisplayUi`、`BlobRoiUi` 以及 `Native*UiDraftControls`、`NativeUiDraftRange` 等本方视觉组合适配器。它们只复用部分逆向迁入的底层控件，不是 VM 原件控件本身；其中纯生命周期/状态适配可保留，视觉组合必须迁到 VM 模板和原件控件闭包后才能算最终完成。该遗留正是当前性能和外观差异的主要重构对象。

Blob 相关模板证据已定位：`docs/07-工具逆向/decompiled/ModuleControls/IMVSBlobFindLabelsModuControl/IMVSBlobFindLabelsModuControl/ThresholdAreaPanel.xaml` 使用 `ItemsControl`、数据模板 `ThresholdAreaItem`、`Integer_Sub` 和 `IntegerBettween_Sub`。该文件属于 `IMVSBlobFindLabelsModu`，与当前 `BlobAnalysis` 的工具身份尚未完成逐项映射，因此目前只能作为相关 VM 模板候选证据，不能直接据此替换 `NativeThresholdUiDraftControls`。下一步先完成工具身份、NodeData 和模板键映射，再实施替换。

## 公开未完成项（必须按此状态汇报）

以下事项均未完成，不能用"已接入新合同""探针通过"替代：

1. **Blob 不是 VM 控件全量迁移**。`BlobUiComposition`、`BlobRunUi`、`BlobDisplayUi`、`BlobRoiUi` 和多项 `Native*UiDraftControls` 仍是本方视觉组合适配器。
2. **VM Blob UI 映射尚未闭合**。当前工具身份已确认是 `IMVSBlobFindModu`，但其完整程序集、NodeData、模板键、复合控件和释放链仍未逐项映射完成。
3. **旧 SDK 消费者未迁移**。20 个正式工具项目仍有旧项目引用或命名空间引用。旧 SDK 源码已删除，当前存在破损引用和 `MSB9008`，不能记作成功退场。
4. **公共控件闭包未完成**。目前只迁入少量基础控件，例如 `LabeledControl`。`ConfigSetting`、`ParamBinding`、Measure/ROI、ResultPresentation 和统一窗口宿主仍未完成。
5. **VM 等效性能未证明**。当前探针只证明本方生命周期和回归链未被阶段性修改破坏，没有原厂 VM 同场景耗时对照，也不能宣称达到 VM 流畅度。
6. **提交基线未固定**。当前工作区仍混有大量既有 CodeBuddy 未提交改动，本批修改未形成独立可回退提交。

后续每次汇报必须分别列出"VM 已确认""本方已实施""本方过渡实现""未确认/未完成"，不再把阶段性代码或本方探针结果描述为全量重构完成。

## 当前阶段范围收口（用户最新决定）

本阶段只收口 `ImageSource` 和 `BlobAnalysis`，20 个旧 Plugin.SDK 工具消费者暂不处理，不再占用本阶段的实现和验收路径。阶段目标是完成两个工具的 VM 原件映射、参数树/模板宿主、Tab 创建与复用、回调退订、资源释放和外部 SDK 基线。

Blob 原件身份已经确认是 VM `IMVSBlobFindModu`。证据为 `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindParam.cs`，其 `ThresholdTypeEnum`、`PolarityEnum`、软阈值字段、排序特征和连通性与 `tools/SunEyeVision.BlobAnalysis/Runtime/Resources/BlobParameters.xml` 对应。`IMVSBlobFindLabelsModu` 的控件模板只作相关参考，不作为当前 Blob 的直接 UI 映射。

本阶段不再扩展旧工具迁移清单。只有 ImageSource/Blob 完成最终 VM 控件闭包和完整生产链验收后，才恢复旧工具消费者批次。

Blob 全量性纠偏：VM `BlobFindParam.cs` 当前可见的公开参数包含阈值、极性、轮廓/图像输出、孔洞、面积、角度、矩形宽高、周长、短长轴、圆度、矩形度、质心偏移、轴比、排序、连通性、内接矩形、外部像素以及多组结果限制字段。当前 `BlobSettings.cs`/`BlobParameters.xml` 只覆盖其中一部分，并保留了本方简化的输出和判定模型。因此当前 Blob Runtime 不是 VM `BlobFindParam` 的全量重构。下一批必须先完成 Runtime 参数/NodeData 全量映射，再把已有逆向控件和模板接入 UI，不能重做控件库或只替换命名空间。

已有控件成果纠偏：`f62915240` 已完成公共控件中立命名和资源迁移，`5bf65949e` 已集中接入 Blob 参数、双图结果和原件控件会话链，涉及 72 个文件、3106 行变更，并有 Blob Session 回归测试。当前缺口是 IMVSBlobFindModu 的全量数据/模板接线和过渡组合器收口，不是从零建立 Blob 控件库。

## 批处理实施模式（用户最新决定）

ImageSource/Blob 本阶段改为先完成代码级完整实现，再统一构建、生产链回归和 VM 对照测试。中间控件、单页、单回调和局部探针不再单独汇报，不以阶段性读数宣称完成。实施输入统一来自现有 REA 本地源码、VM 程序集/模板/资源证据、当前 `Plugin.Wpf` 逆向迁移成果和 `IMVSBlobFindModu` 参数映射。实现闭合前不再扩展 20 个旧工具消费者。

本阶段逆向输入范围扩展：除 `D:\MyWork\REA逆向测试` 外，必须直接利用 `docs/07-工具逆向/decompiled` 的 VM 托管实现、编译 XAML/BAML、模板资源、程序集清单和控件代码。当前自动盘点显示 VM `IMVSBlobFindModuCs/BlobFindParam.cs` 有 112 个公开字段，V2 `BlobSettings.cs` 只有 35 个属性、`BlobParameters.xml` 只有 28 个参数项。该差异是代码级全量重构缺口，不能只靠现有本地方案或继续加探针解决。

算法证据纳入本批：ImageSource 与 Blob 的 VM 托管算法、参数类、结果类和资源调用链已有逆向材料，本阶段不重新猜测算法语义，不重复做局部探针。实现以已有 VM 代码证据为迁移基线，统一完成 Runtime 参数/执行、WPF 模板/复杂槽、窗口生命周期和释放，之后一次性执行构建与生产回归。

Blob 全量字段映射已生成：`docs/collaboration/evidence/Blob-IMVSBlobFindModu-字段映射-20261107.json`。当前统计为 VM 112 个公开属性、V2 `BlobSettings` 35 个属性、参数树 28 项，只有 3 项同时接入字段和参数树，93 项尚未接入。该 JSON 是本阶段 Runtime/NodeData/UI 批量实现的输入，不再逐字段凭感觉补丁。

## 2026-11-08 代码级批处理第一刀（ImageSource/Blob范围）

Blob Runtime 已新增 `BlobVmFindParameters`，按 `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindParam.cs` 建立 VM 参数闭包，覆盖阈值、极性、轮廓、面积/几何筛选、排序、连通性、结果限制等公开参数。`BlobSettings.Freeze()` 会把现行宿主字段投影到该闭包，`BlobSession` 从冻结快照读取 VM 参数并执行新增几何筛选和结果限制，避免执行侧读入编辑中间态。当前闭包已进入代码，尚未宣称所有 VM 结果字段、内接矩形、灰度统计和原生动态接口等语义全部等价。

Blob 参数页组装已补上后台逐页预热链。`DeclaredFieldsView` 暴露页身份和一次性页实现入口，`BlobUiComposition` 在当前页完成后按 Dispatcher Background 逐页建立并缓存，Tab 切换不再首次进入时同步拼装整页。该链仍需在统一 Release 产物上与 VM 同场景回归，当前构建受工作区既有 build_temp 并发/锁定影响，不能把本批构建记作通过。

本批生产参数窗回归已保存到 `docs/collaboration/evidence/ImageSource-Blob-VM批处理生产参数窗回归-20261108.txt`。现有正式探针结果为 ImageLoad 绑定约 340ms、首次呈现约 1179ms，BlobAnalysis 绑定约 420ms、首次呈现约 375ms，Blob 第 2/3 页切换约 162/300ms，重复切换约 15/20ms；两个工具的实际 Session、节点、参数控件、提交/回执和关闭释放均通过。该读数是本方生产链回归，不是 VM 同机等效证明。日志仍提示 RuntimeEditorProductionProbe 的历史旧 Plugin.SDK 项目引用缺失，本阶段未处理 20 个旧工具消费者。

VM 参数闭包的独立冒烟结果保存于 `docs/collaboration/evidence/Blob-VM参数映射冒烟-20261108.txt`，104 个 VM 参数属性的类型闭包和代表性阈值/面积映射通过。

2026-11-08 后续批次已把 VM 运行参数接入 `BlobParameters.xml` 和 `BlobParameterDefinition` 的双模型绑定。参数树从 28 个 Parameter 节点扩展到 105 个，Runtime 参数展示字段为 139 个，104 个 VM 运行参数已经进入参数树或通过现行宿主字段映射。`BlobSettings` 仍保留宿主资源接缝，InputImage、ModuRoiManager 以及枚举类型不伪装成普通字段。新的生产回归日志为 `docs/collaboration/evidence/ImageSource-Blob-VM完整参数树生产回归-20261108.txt`，两个工具提交、回执、Tab、释放链通过；Blob 首次绑定约 720ms，后台页构建后第 2/3 页切换约 228/276ms，重复切换约 19/25ms。

当前完整参数树已切断 Blob 的重复命令式运行参数组装。检测到完整 VM 元数据时 `BlobRunUi` 只保留正式字段状态适配，不再创建隐藏的 `ui-only-run.*` 控件。生产回归保存在 `docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`，最新读数为 Blob 绑定约 808ms，后台页构建后第 2/3 页切换约 262/337ms，重复切换约 19/29ms，两个工具完整窗口链仍通过。

VM `BlobFindResult` 已新增中立投影 `BlobVmResultProjection`。面积、状态、数量、质心、外接框、角度、周长、长短轴、圆形度、矩形度和二值/Blob图像已形成代码映射，灰度统计、最大内接矩形、Score、ROIAnnulus、ContourInfo、屏蔽区多边形和 OutputMask 保持明确未接入。投影冒烟证据在 `docs/collaboration/evidence/Blob-VM结果投影冒烟-20261108.txt`。

旧 SDK 消费者已补充源级映射 `docs/collaboration/evidence/PluginSDK消费者源级映射-20261108.json`。扫描结果区分 20 个仍有正式 `ProjectReference` 的工具项目和 2 个只有历史命名空间字符串的宿主项目，记录每个项目的源码/XAML 闭包和新合同目标。20 个直接消费者仍冻结，未开始删除旧 SDK。

旧 SDK 退役门禁新增工作树实时扫描。Windows PowerShell 5.1 的 `-ReportOnly` 已核对映射登记 20、实际项目引用 20、SDK 目录条目 0，正常模式继续按 20 个项目阻断删除。映射文件与源码项目不一致时现在会先阻断，不能依赖过期清单继续推进。


## 2026-11-08 代码级闭环继续推进

本批按 VM BlobFindParam/BlobFindResult/IMVSBlobFindModuTool 反编译源码和 REA 本地图像源/Blob 实现直接补齐 ImageSource 与 BlobAnalysis 代码。`BlobVmFindParameters`＋105 项 XML 参数树已成为单一冻结入口。BlobImage 改为从筛选后候选集合生成，Auto/RelativeSoftThreshold 按有效检测域统计，`OKWhenNumIsZero` 接入结果判定。`Directory.Build.props` 排除所有嵌套 `build_temp`，修复隔离构建产物被重新编译造成的重复程序集特性。PluginDevKit Runtime/WPF 模板加入发布 DLL 显式引用，指定 SdkPath 后可脱离主仓构建。

最新生产探针通过：ImageLoad 绑定 615ms、首显 1223ms；BlobAnalysis 绑定 843ms、首显 337ms；第2/3页首次切换 227/307ms，重复切换 19/28ms；两工具实际 Session、参数控件、捕获/回执、Tab、关闭释放通过。证据：`docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`、`docs/collaboration/evidence/PluginDevKit模板引用回归-20261108.txt`。

边界仍保持：20 个旧工具消费者未迁移，旧 Plugin.SDK 未删除；VM 原厂同场景动态性能未获得；Blob 结果投影中 13 个原件成员无当前资源证据；xUnit 运行被旧 release-payload.json 与本次重建 Application SHA 不一致拒绝，需固定提交后重新封签再回归。当前工作区仍为 CodeBuddy 混合未提交基线，不得据此宣称正式集成完成。

UI Release 构建已完成，0 错误，构建封签已重建。随后 BlobAnalysis xUnit 工程编译完成，但 21 项仍在 ProductRuntimeTestHost 初始化阶段因 `SunEyeVision.Execution.dll` 与 release-payload.json SHA 不一致被拒绝，未进入算法断言；需要单独隔离测试输出或在同一固定提交后重新封签，不能把该结果记为算法失败。

UI Release 封签后再次运行 RuntimeEditorProductionProbe：ImageLoad 绑定 757ms、首显 1324ms；BlobAnalysis 绑定 784ms、首显 320ms；第2/3页首次切换 254/268ms，重复切换 19/28ms；两工具完整 Session、参数控件、回执、关闭释放 PASS。证据已追加到 `docs/collaboration/evidence/ImageSource-Blob-VM正式参数树无重复组装回归-20261108.txt`。

验证收口：BlobAnalysis xUnit 在正式 Release 重新封签后，以 `BuildProjectReferences=false` 编译测试、`--no-build` 执行，37/37 通过。PluginDevKit `build-sdk.ps1 -Configuration Release` 与 `validate-components.ps1` 均通过，SDK DLL 已从当前 Release 重建复制。证据：`docs/collaboration/evidence/Blob-VM参数映射冒烟-20261108.txt`、`docs/collaboration/evidence/PluginDevKit模板引用回归-20261108.txt`。

最终封签后生产探针再次通过：ImageLoad 绑定 801ms、首显 1596ms；BlobAnalysis 绑定 813ms、首显 341ms；第2/3页首次切换 212/265ms，重复切换 18/26ms。两工具完整链 PASS。

本批源码与证据已固定提交 `033fc093d`（中文提交标题）。该提交只包含 ImageSource/BlobAnalysis 重构、参数页生成层、PluginDevKit 模板/SDK、测试和本批证据；其他 CodeBuddy 未归类改动仍留在工作树，未混入提交。

最终固定基线：当前分支 `feature/param-window-datatree`，本批提交链为 `033fc093d`（源码/测试/证据）、`53c5084dc`（任务账本）、`cb3d63ffc`（VM字段与消费者映射）。完整 BlobAnalysis 回归 37/37 通过，最终生产探针和 PluginDevKit SDK/组件验证均通过。当前未提交内容仅为其他文档预览和 CodeBuddy 未归类改动，不属于本批源码基线。


PluginDevKit 外部工程隔离验收已通过：复制 SDK DLL/XML、模板及开发包自带 Directory.Build.props/targets 到 artifacts/validation/ExternalSdkAcceptance-20261108，Runtime/WPF 模板 Release 构建 0 错误，Runtime/Editor ToolComponent 身份校验 PASS。证据：docs/collaboration/evidence/PluginDevKit外部工程隔离验收-20261108.txt。仍未完成 Visual Studio 启动或附加正式产品宿主人工验收。


新增 scripts/verification/Test-PluginSdkRetirementGate.ps1，ReportOnly 列出 20 个直接消费者，正常模式按预期阻断旧 SDK 删除。ImageSource/BlobAnalysis 无旧 SDK 消费。证据：docs/collaboration/evidence/PluginSDK退役门禁-20261108.txt。

## 2026-11-08 CodeBuddy 方案对齐与首批收口

CodeBuddy 的"数据树 + 模板 + 页面复用"方向已作为候选重构方案采用，但其原始评估中的静态性能结论、VM ≤80/100ms 预算和"零构建"表述均不作为已确认事实。`docs/01-方案设计/2026/11/07/参数窗对齐VM数据树模板化优化方案-20261107-153655.html` 已补充评审结论，区分历史基线、实施回归和未确认 VM 动态机制。

本批以已有 VM BlobFind 参数/结果/工具逆向代码和 REA 本地图像源/Blob 实现为输入，已完成 ImageSource 与 BlobAnalysis 的代码级完整链。当前固定生产回归读数为 ImageLoad 绑定约 801ms、首显约 1596ms；BlobAnalysis 绑定约 813ms、首显约 341ms；第 2/3 页首次切换约 212/265ms，重复切换约 18/26ms。BlobAnalysis xUnit 37/37 通过，两个工具 Session、参数控件、捕获/回执、Tab、关闭释放均通过。该结果证明本方重复组装瓶颈已被页面缓存和 VM 参数树入口显著收敛，不证明已达到 VM 原厂同机性能。

当前执行计划固定为：

1. 继续以 ImageSource→BlobAnalysis 作为标准链，补齐 VM 同场景动态窗口证据和时间线对照，禁止把本方实现推定成 VM 机制。
2. 只在两工具范围内收口未映射的 VM 结果成员和资源接缝，保留已确认的图像资源、节点身份、执行、保存和释放宿主边界。
3. 20 个旧 Plugin.SDK 消费者保持冻结。`scripts/verification/Test-PluginSdkRetirementGate.ps1` 已建立删除门禁，只有消费者迁移并通过完整链路回归后才允许删除历史引用。
4. PluginDevKit 的发布 DLL、模板和干净目录构建已通过，外部 VS 启动/附加正式宿主仍列为未验收项。
5. 工作区中 `docs/08-工具预览/ui`、脚本和其它 CodeBuddy 未归类改动不属于本批固定基线，必须单独审查，不与已提交源码混合。

当前固定源码基线为分支 `feature/param-window-datatree`，提交链 `033fc093d`、`53c5084dc`、`cb3d63ffc`、`1229f21de`、`27752cd20`。交接或继续实施前以该提交链和本节为准。

## 2026-11-08 Blob 结果投影灰度成员收口

基于 VM `BlobFindResult` 结果声明和现有 HALCON Blob 区域，已把 `MinGrayValue`、`MaxGrayValue`、`GrayContrast` 接入 `BlobMeasurement`、`BlobVmResultProjection` 和生产执行链。投影只在每个保留 Blob 都有真实值时发布数组，否则继续保留 `Unavailable`，没有用默认值填充原件结果。证据为 `docs/collaboration/evidence/Blob-VM结果投影-灰度统计-20261108.txt`。

验证结果：BlobAnalysis xUnit 38/38 通过；UI Release 构建 0 错误并重新封签；RuntimeEditorProductionProbe 通过，ImageLoad 绑定 721ms、首显 1301ms，BlobAnalysis 绑定 830ms、首显 337ms，第 2/3 页首次切换 231/279ms，重复切换 17/26ms。当前 Blob 结果投影仍有 10 个 VM 成员待接入，具体为最大内接矩形、Score、ROI、ROIAnnulus、ContourInfo、BlindPolygon 三项、OutputMask 和 ModuStatus。该批不扩展未被原件证据闭合的语义。本轮代码、测试、证据和架构回填固定于提交 `e571842cd`。

## 2026-11-08 清理未使用自研标签控件

已确认 `src/Plugin.Wpf/Controls/LabeledControl.cs` 仅是自研标签容器，ImageSource、BlobAnalysis 和 `DeclaredFieldEditor` 没有引用它。实际参数编辑器使用 `Plugin.Wpf.Native.Composition.ParameterPresentation` 下的逆向控件。已删除该类型，主题资源中的对应样式同步移除。

验证：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 0 错误并重新封签；`RuntimeEditorProductionProbe` 通过，ImageLoad 绑定 797ms、首显 1216ms，BlobAnalysis 绑定 780ms、首显 315ms，第 2/3 页首次切换 204/254ms，重复切换 15/24ms。两个工具完整 Session、参数控件、捕获/回执、关闭释放 PASS。

## 2026-11-08 清理生产探针旧 SDK 锁文件

`tests/RuntimeEditorProductionProbe` 的项目引用已经是新合同，但旧 `packages.lock.json` 仍残留 `SunEyeVision.Plugin.SDK`。本批重新评估锁文件，改为 `SunEyeVision.Plugin.Editing.Wpf`，探针 Release 构建 0 错误，`MSB9008` 旧 SDK 项目缺失警告消失。20 个旧工具消费者仍按退役门禁冻结，本次只清理探针工程的过期锁文件。证据已追加到 `docs/collaboration/evidence/PluginSDK退役门禁-20261108.txt`。

## 2026-11-08 CodeBuddy 窗口入口冲突纠偏

审查未提交 CodeBuddy 改动时发现 `MainWindowRuntimeEditing` 被改成同步完成 `BindDraft` 后才 `Show` 窗口。该顺序会把约 780ms 的参数组装放到窗口首次可见之前，与 VM 流畅打开目标和现行"先显示窗口壳、再按生命周期装配"合同冲突。已恢复该入口到固定基线，保留探针代码作为定位材料，不把该候选实现纳入正式重构。

恢复后 UI Release 构建 0 错误并重新封签。未提交的 `RuntimeNodeConfigurationControl` 探针、`RuntimeEditorProductionProbe` 输出扩展、工具预览文档和脚本改动仍保持独立，未混入正式基线。

当前最新固定提交链追加为 `e571842cd`（灰度结果投影）、`5f13e011d`（移除未使用自研标签控件）、`1d78d7ea5`（清理生产探针旧 SDK 锁文件）、`682a782dc`（纠偏同步显示入口）和 `c6f5e7d2b`（固定窗口分段探针）。

## 2026-11-08 PluginDevKit 正式宿主启动检查

已用当前 Release 重新准备 `PluginDevKit/runtime`，确认 `release-payload.json` 与 `ToolGroups.xml` 存在。启动 `SunEyeVision.UI.exe` 后进程保持运行 8 秒，随后安全结束。该结果补齐了外部 SDK 的正式宿主进程启动检查。Visual Studio 启动或附加调试仍未验收，不能与本次进程启动混称完成。

## 2026-11-08 正式载荷插件发现与生命周期探针

将 `AdmissionLifecycleProbe` 入口程序集放入 `PluginDevKit/runtime` 正式载荷根目录运行后完整通过。验证了 BlobAnalysis 与 ImageLoad 的运行组件发现、Schema 唯一性、保存重开、运行、调试准入、跨代回调、结果 reader 保留、旧代拒绝和释放排空。直接从探针自身输出目录启动会因 Foundation 先加载自有副本触发 SHA mismatch，已确认是测试布局问题，正式 runtime 根目录运行通过。Visual Studio 启动或附加调试仍单独列为未验收。

## 2026-11-08 外部 SDK VS 附加准备

当前机器没有 `devenv.exe` 或 `vswhere.exe`，无法执行真实 Visual Studio 附加。已增强 `PluginDevKit/scripts/launch-debug.ps1`，支持 `-Detach` 和会话回执，分离启动测试通过，回执包含 PID、宿主路径、运行根目录和附加范围。README 已补充使用方式。该脚本为外部 VS 附加提供可复现入口，不能代替 VS 实际附加验收。

## 2026-11-08 VM Blob 原件结果桥接补证

REA 已确认 `IMVSBlobFindModuCs.dll` 只负责 `BlobFindResult` 容器初始化和 `Run → serverSDKManager → SyncSelfRun` 转发，真实结果值由 native `IMVSBlobFindModu.dll` 产生。当前 Windows 环境未配置 Ghidra/IDA，Hopper 不支持该主机，native 函数级追踪暂无法继续。10 个剩余结果成员继续保持 `Unavailable`，没有按名称近似实现。证据：`docs/collaboration/evidence/Blob-VM原件结果桥接证据-20261108.txt`。

## 2026-11-08 两工具代码级重构收口与窗口状态机校正（当前有效批次）

本批按现有 VM 逆向程序集、参数 XML、资源身份、控件模板和 REA 本地源码证据，直接收口 ImageSource 与 BlobAnalysis 的 Runtime/WPF 算法与生产链，不再以增加探针作为主要工作。20 个旧 Plugin.SDK 消费者继续冻结，旧 SDK 删除门禁不改变。

已验证：

- `tests/SunEyeVision.ImageSource.Runtime.Tests` 顺序运行 31/31 通过。此前 6 个失败是未设置 `S2_CASE_ROOT`，补齐验证根后通过。
- `tests/SunEyeVision.Tool.BlobAnalysis.Tests` 顺序运行 38/38 通过。此前 22 个失败是并行构建同时写正式 Release 目录造成的 `release-payload` SHA 竞争，不是源码失败。
- `tests/RunChainProbe` 使用最新正式 Release 载荷参数运行，ImageLoad→Blob、私有调试、连续轮次、取消、删除/撤销、保存重开、模拟相机和释放 13 项通过，并确认 Runtime 进程未加载 WPF、UI 或旧 SDK。
- `tests/RuntimeEditorProductionProbe` 通过两个实际节点的参数绑定、首帧、Tab 复用、捕获/回执、换节点和关闭释放。最新读数为 ImageLoad 绑定 273ms、首帧 1005ms，BlobAnalysis 绑定 855ms、首帧 351ms，第 2/3 页首次切换 298/318ms，重复页切换 0/15/26ms。
- 全量 `dotnet build SunEyeVision.slnx -c Release --no-restore` 通过，0 错误。构建必须串行执行，不能让多个测试项目同时写正式 Release 目录。

实现调整：

- 当前源码中的 `BlobUiComposition` 将当前页和后续页都提交到 `DispatcherPriority.SystemIdle`。窗口先显示壳，当前页在首个 SystemIdle 阶段完成，其他页面随后逐页一次性建立并缓存，重复切换复用已有页面树。生产探针等待真实组装就绪任务，不把窗口显示事件误当作参数页已就绪。
- 旧 UI 回归中仍有 6 个测试按"窗口显示后立即可调试"同步假设断言，现行窗口宿主实际有"组装中→就绪→可调试"状态。生产探针已覆盖该状态机，旧测试需要按新状态合同重写，不能为通过测试回退到窗口显示前同步构造。

仍未完成：

- VM 原厂 native 结果桥接只有托管 `SyncSelfRun` 入口证据，10 个结果成员不能猜测填充。
- VM 原厂同场景 UI 线程耗时、首次页装配和跨节点运行时释放顺序仍未动态确认。本方读数不能宣称 VM 等效。
- 20 个旧 Plugin.SDK 工具消费者未迁移，不能删除旧 SDK 或关闭迁移门禁。
- VS 外部工程附加正式宿主、真实硬件和人工验收仍未完成。

本批阶段提交前保持未提交的 CodeBuddy 工具预览 HTML、生成脚本和迁移清单变更隔离，不混入上述正式源码基线。


## 2026-11-08 REA Blob 原厂证据迁移补证

在 D:\MyWork\REA逆向测试\docs\joint-audit 发现并迁入此前被标为缺失的 8 份 Blob 原厂证据，索引与 SHA-256 见 docs/collaboration/evidence/Blob-原厂证据迁移清单-20261108.md。补证明确了 VM 参数窗口的 AlgorithmTabRoot 组装链、参数恢复与关闭入口、Blob 参数合同、4/8 连通参数入口、形状量化校验、BinaryImage/BlobImage 独立输出声明，以及 Result List 的 Angle/BlobAngle 映射。

补证没有证明填孔和阈值边界、4/8 连通的对角行为、形状最终比较公式、双图像像素与 Domain、长短轴半长/全长或动态原机差分。上述项继续保持未确认，不把 HALCON 结果冒充 VM 等价。下一步可依据已迁移的组装和参数证据继续代码落地，不再把"证据文件未迁入"作为阻塞理由。

## 2026-11-08 窗口就绪状态与引用清空语义收口

窗口宿主新增内部组装就绪任务。生产主窗仍先显示窗口，再后台绑定参数页，绑定失败会以失败任务和窗口状态报告，不回退到显示前同步构造。UI 测试等待真实就绪任务后再断言命令和参数视图。

Blob 引用输入修正 DeleteTextBox 的空文本清理路径。WPF 绑定在目标与源都为空时可能跳过 setter，现由逆向引用控件的 Delete 键直接调用同一 ReferenceFieldSession 清理路径，正式字段会提交 UnsetSetting，位置修正视图随之退订和隐藏。

验证结果：窗口、调试、Blob 三页、连续运行和释放相关 UI 回归 16/16 通过。ImageSource Runtime 31/31、BlobAnalysis Runtime 38/38、RuntimeEditorProductionProbe 和生产 ImageLoad→Blob 链继续通过。

## 2026-11-08 Blob 结果角度来源修正

依据迁移的 Result List 证据，BlobVmResultProjection 不再把外接矩形角度误投影为 BlobAngle。BlobSession 现在使用 HALCON OrientationRegion 取得主轴角度，OrientedBounds.AngleDegrees 保留外接矩形角度来源，BlobAngle 单独发布。长短轴的半长/全长语义仍保持未确认，不做额外倍乘或缩放假设。

## 2026-11-08 Blob 总面积单精度聚合语义落地

REA 的 `B08-总面积聚合排序与判断传输链-20261104.md` 已确认总面积按结果数组顺序逐项以 float32 累加。本批新增 `BlobNumericSemantics.Float32Sum`，统一执行判断、`BlobOutput.TotalArea` 和 `BlobVmResultProjection.TotalArea`，并加入顺序累加回归用例。Runtime Release 构建通过。首次 xUnit 运行因局部构建后的正式载荷未重新封签而被门禁阻断，随后按产品流程以当前 Release 重新封签并用 `--no-build` 重跑，新增回归 1/1 通过。该过程保留为载荷一致性要求，不绕过正式门禁。证据文件为 `docs/collaboration/evidence/Blob总面积单精度聚合回归-20261108.txt`。

## 2026-11-08 Blob Runtime 全量回归与外部 VS 状态复核

在正式 Release 重新封签后，BlobAnalysis Runtime 全量回归最终 `39/39` 通过，日志见 `docs/collaboration/evidence/BlobAnalysis全量回归-20261108.log`。这次回归覆盖参数合同、阈值模式准入、真实 HALCON Session、输出图像、结果 reader、边界和释放链；它仍不等同于 VM 原机动态差分。

当前机器实际存在 `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe`。已启动 PluginDevKit 外部正式宿主并准备调试回执，也启动过 Visual Studio 进程，但 Computer Use 未能获得可操作的 Visual Studio 窗口，附加动作没有发生，随后已关闭本次测试进程并清理临时回执。因此"VS 启动或附加正式宿主"仍是未验收项，不能把进程启动检查记作 VS 附加通过。

PluginDevKit 清理了 5 个被工程排除但仍引用旧 `Plugin.SDK` 的死模板/死样例文件，并删除了无法被 `dotnet sln` 解析的旧二进制 `PluginDevKit.sln`。`prepare-devkit.ps1` 现在只提示 `RuntimePlugin.Template` 和 `WpfTool.Template`。清理后两个新模板分别 Release 构建 0 错误，`validate-components.ps1 -PackageRoot PluginDevKit` 通过，PluginDevKit 中已无旧 SDK 源码引用。

`validate-components.ps1` 已增加模板源码退役 SDK 扫描和 SDK 合同 DLL 存在性检查。当前模板身份验证与旧 SDK 回流检查均通过。

用户已确定其余 20 个旧工具从现行产品方案移除，后续按 VM 逆向重新实现。退役门禁现在只扫描 `SunEyeVision.slnx` 的活动项目，实测活动消费者 0、历史移出消费者 20、源码实时扫描 0、`src/Plugin.SDK` 目录条目 0，正常门禁已通过。旧工具目录和逆向材料保留为后续重建输入，不进入当前产品 Release。

旧工具移出后串行执行 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false`，结果 0 错误、6037 警告，耗时 2 分 50.50 秒。证据见 `docs/collaboration/evidence/现行方案移除旧工具后Release构建-20261108.log`。并行构建曾触发进程终止，后续 Release 验收固定使用串行构建。

同一正式 Release 载荷下的 ImageSource Runtime 全量回归为 `31/31`，日志见 `docs/collaboration/evidence/ImageSource全量回归-20261108.log`。结合前述 BlobAnalysis `39/39`，当前两工具 Runtime 测试总计 `70/70` 通过。测试使用独立 `S2_CASE_ROOT`，没有把本机历史案例目录或人工验收状态混入结果。

PluginDevKit/runtime 中一份旧产品 Help 静态站点仍展示 `IVisionPlugin`、旧工作流 API 和旧插件示例，已从生成的外部调试运行目录清理；`build-runtime.ps1` 也将 `Help` 加入外部运行目录排除项，避免下次准备开发包时回流。当前外部开发说明以 `PluginDevKit/README.md`、两个新模板和发布 DLL 合同为准。


### 2026-11-09 串行最终 Release 基线（当前有效）
全量 dotnet build SunEyeVision.slnx -c Release --no-restore -v:q 在构建完成后串行复核，0 错误、6080 个既有警告；正式载荷 SHA-256 为 3BD9981DADEB79B0AD5DE5321D6389510183A460DACDC2ED48BF1607B35836B4。随后单独运行 RuntimeEditorProductionProbe：ImageLoad 参数绑定/首显 507/1173ms、重复 Tab 0ms；BlobAnalysis 参数绑定/首显 1435/453ms、首次切换第 2/3 页 385/428ms、重复切换 0/27/43ms，两个节点完整 Session→捕获/回执→关闭释放 PASS。原始日志为 docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-release-final.txt。这些是当前本方 Release 读数，VM 动态耗时、人工视觉和 VS 外部附加仍未确认。


最终封签校正：NeutralReleaseProbe 重新封存后正式载荷 SHA-256 为 DF15710C681E49B53A41ADC84452AB7B749E0881D7D86F1869A5CB6AF87E84D5；Plugin.SDK 退役门禁随后 PASS。

### 2026-11-09 启动链遗留诊断清理（当前有效增量）
正式启动链移除全局 Button 悬停调试注册。该注册仅用于早期主题排查，原本在每次启动安装全局路由事件并持续记录 MouseEnter/MouseLeave，不属于 VM 启动、方案管理或工具组装语义。现在启动链只保留方案管理器→插件准入→服务/方案管理→工作区加载→工具窗口的正式路径。

本次复核：`dotnet build SunEyeVision.slnx -c Release --no-restore -v:q` 0 错误、3354 个既有警告；`Run-NeutralReleaseProbe.ps1 -SkipBuild` PASS，正式载荷 SHA-256 为 `CB16CE1EFDC2057CB76A51FEB83D3CAD2C6B11B8FD5B62DD6DD33DAC3F53F2EB`；`Test-PluginSdkRetirementGate.ps1` PASS，现行消费者 0、历史旧引用 20。生产探针 PASS：ImageLoad 参数绑定/首显 `467/1401ms`，重复 Tab `0ms`；BlobAnalysis 参数绑定/首显 `1295/446ms`，首次切换第 2/3 页 `351/413ms`，重复切换 `0/25/41ms`，Session→正式参数控件→捕获/回执→关闭释放完整通过。原始日志为 `docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-blob-single-chain.txt`。

该清理只删除正式启动链的诊断负担，不改变 VM 对齐的窗口、参数树、Tab 复用和资源生命周期语义。当前仍未取得 VM 同版本动态 UI 线程计时，不能把本方读数写成 VM 动态等效。

### 2026-11-09 正式启动链与 Blob 运行能力单链最终封签（当前有效）
在移除全局 Button 悬停诊断和 BlobRunUi 无效 Changed 订阅后，重新执行专项/全量 Release 构建与串行验收：Blob WPF 0 错误；全量 Release 0 错误、3350 个既有警告；NeutralReleaseProbe PASS，正式载荷 SHA-256 为 `5A32D9F789A52EF775A0F3547F7E18738F95327C19C20A21E8AAE66CD65C0A7F`。Plugin.SDK 退役门禁仍 PASS，现行消费者 0、历史目录旧引用 20。

同一 Release 的生产探针 PASS：ImageLoad 参数绑定/首显 `454/1282ms`，重复 Tab `0ms`；BlobAnalysis 参数绑定/首显 `1194/437ms`，首次切换第 2/3 页 `349/445ms`，重复切换 `0/29/42ms`。两个节点均完成真实 Session→正式参数控件→捕获/回执→关闭释放。

当前代码语义：启动链不再注册产品无关的全局悬停诊断；Blob 运行参数不再维护独立 Changed 事件或第二套编辑器状态，正式 `DeclaredFieldsView` 的字段变化和会话租约是唯一回调/退役来源。VM 同版本动态 UI 线程计时仍未取得，本方读数只证明当前实现单链、无回退和重复 Tab 复用。

### 2026-11-09 Platform SDK 宿主适配器落地（当前有效增量）
依据 VM Platform SDK 的 `VmSolution.Load/Save/CloseSolution/Dispose`、流程运行和结果回调语义，已新增 `src/Platform.Sdk/PlatformRuntimeHost.cs`，由专用 owner 线程承载 `DocumentSessionManager`、方案快照、执行批次、结果订阅/退订和关闭释放。`PluginDevKit/scripts/build-platform-sdk.ps1` 现在同时构建合同与宿主适配器并复制其发布依赖。

外部模板验证已闭合：干净工程引用 `sdk/platform/net9.0`，加载 V5 验证方案、枚举 `flow-1`、执行空流程、连续运行一轮后停止并在进程退出时 Close/Dispose；输出 `Workflow flow-1: 0 node(s)`、`Run flow-1: success=True, nodes=0`、`Continuous flow-1: reason=Requested, rounds=1`。当前未完成项只剩 Group、真实设备连接控制、渲染控件公开合同和 Visual Studio 实际启动/附加验收，不再把 Platform SDK 记为 contract-only。证据见 `docs/collaboration/evidence/PlatformSdk合同边界回归-20261109.txt`。

本批统一验证：全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -v:q` 为 0 错误、6064 个既有警告；`build-platform-sdk.ps1 -Configuration Release` 成功，`Run-NeutralReleaseProbe.ps1 -SkipBuild` PASS，正式载荷封签 `F19BD7A0646955BB8197BBB12EB3596FA221CA38F14D7F5CA7BE156A9641A316`；Plugin.SDK 退役门禁 PASS。模板本体 Release 构建 0 错误，外部运行输出设备数、流程枚举、单次运行和连续运行结果。

### 2026-11-09 当前 Release 启动到工作区实测回归（当前有效增量）

使用当前正式 Release 和隔离 V5 方案完成一次启动链回归，原始时间线见 `docs/collaboration/evidence/启动到工具窗口当前Release回归-20261109.txt` 和 `docs/collaboration/evidence/startup-trace-20261109.txt`。入口到主链返回 3950ms：主窗口壳首帧约 2266ms，发布准入 325ms（RuntimeAssemblies 59ms、RuntimeDefinitions 265ms、ToolDirectory 8ms），服务初始化约 456ms，方案管理器初始化 0.4ms，工作区注入 159.5ms。此前旧载荷日志中的 5 秒级 `ComponentLoading` 已不再复现；旧日志不再作为当前性能基线。

本次验证的是空流程启动链，不把工具窗口动态打开写成同一次启动通过。ImageSource/BlobAnalysis 的节点窗口、参数树/模板、Tab 复用、执行/结果和关闭释放仍以 `VM完整链路代码落地回归-20261109.txt`、`RuntimeEditorProductionProbe-20261109-atomic-final.txt` 为准。VM 动态 UI 线程计时、同节点实例复用、真实设备和 VS 实际附加仍是未确认项。
### 2026-11-09 VM 启动轻壳与延迟工作区回归（当前有效增量）

本批按 VM 的可确认启动顺序修正结构性差异：`MainWindow` 构造阶段不再实例化完整画布 Tab 树、结果图像/结果树和工具箱；新增 `DeferredWorkspaceHostControl`、`DeferredImageResultHostControl`、`DeferredToolboxHostControl`，仅保留布局槽位。发布准入、服务和方案会话完成后，`MainWindow.InitializeWorkspace` 一次性创建既有工作区控件，并继续使用原参数树、VM 模板、Tab 复用、退订和 Dispose 合同。

当前 Release 实测：主窗口开始显示 `502ms`，首帧 `1244ms`，插件准入完成 `1752ms`，方案管理器初始化 `2134ms`，工作区组装 `2519ms`，隔离空流程启动链返回 `3355ms`。相较前一版首帧约 `2266ms`、完整链约 `3950ms`，首帧提前约 `1.02s`，完整链缩短约 `0.60s`；耗时转移到加载遮罩之后，不再表现为用户先看到空白等待。证据见 `docs/collaboration/evidence/启动与工具窗口延迟宿主回归-20261109.txt` 和 `docs/collaboration/evidence/startup-trace-20261109-lazy-host.txt`。

同一 Release 的真实工具探针仍通过：ImageLoad 绑定/首显 `668/1146ms`；BlobAnalysis 绑定/首显 `1113/399ms`，ROI 组合 `1450ms`，第 2/3 页首次切换 `319/365ms`，重复切换 `0/25/41ms`；两个工具 Session、正式参数控件、捕获/回执和关闭释放通过。该结果证明延迟宿主没有破坏工具链，但不宣称 VM 同机动态等效。

### 2026-11-09 Platform SDK 结果资源快照边界收口（当前有效增量）

修正 `PlatformResultEvent.Values` 与 `PlatformNodeResult.Values` 的公开边界：内部 `IReadOnlyImage`、`IReadOnlyRegion`、reader 和资源句柄在 owner 线程转换为 SDK 自有的 `PlatformImageFrame`、`PlatformRegionFrame`、`PlatformRegionRun` 快照；字典、数组和嵌套集合递归复制，图像按帧身份去重到 `Images` 属性。外部方案关闭后只保留独立像素与区域数据，不再依赖宿主对象。

验证：全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false` 为 0 错误；`build-platform-sdk.ps1 -Configuration Release` 为 0 错误；`Run-NeutralReleaseProbe.ps1 -SkipBuild` PASS，最终正式载荷 SHA-256 为 `F127B336F1E1CD8F4F441E7ACB019097C61212955BB00A7CE7FAD2D8C6C4B19D`；Plugin.SDK 退役门禁 PASS（现行消费者 0、历史旧引用 20）。

外部模板 `PluginDevKit/_validation/ExternalPlatformSdkAcceptance` Release 构建 0 错误，干净发布目录运行输出方案加载、流程枚举、单次运行和连续运行一轮后停止，进程退出完成 Close/Dispose。结果与未完成项见 `docs/collaboration/evidence/PlatformSdk合同边界回归-20261109.txt`。本次使用空流程，不能把真实 ImageSource→BlobAnalysis 结果回调样例写成已验收；Group、真实设备连接、WPF 渲染控件和 VS 实际附加继续单列。

## 2026-11-09 VM 控件模板资源作用域与隐藏辅助字段延迟物化（当前有效增量）

依据 VM 原厂 `ParamListView/ParamUserControl/DataTemplate` 与 `ImageViewConfig` 资源证据，本批不新增第二套参数模型：`NativeAssistUiDraftControls` 默认模式只登记未选中的辅助字段，切换模式时才物化；`NativeGraphicStylePresenter` 把原厂图形模板资源挂到实际 `ContentControl` 宿主，并保留隐藏页预热挂接。前者把 Blob ROI 专用组合从约 2.1 秒降到 `356ms`，后者消除了 Measure 阶段 `TextLeft` 静态资源异常并把首次第 2/3 页切换稳定在 `271/257ms`，重复切换 `0/17/29ms`。

验证：全量 Release 0 错误；NeutralReleaseProbe PASS，正式载荷 SHA-256 `B8CF66F0EF786D57AB5D403D4503C5935CB242CEE3297FC86042AFDDF701A202`；Plugin.SDK 退役门禁 PASS。生产证据：`docs/collaboration/evidence/RuntimeEditorProductionProbe-20261109-final-result-prewarm.txt`。运行参数页约 `2126ms`、结果页约 `997ms` 的后台字段/模板物化仍保留为下一批 VM 模板注册与复用优化项；VM 同版本动态耗时未取得，不宣称动态等效。

## 2026-11-09 运行参数条件字段整页延迟物化（当前有效增量）

依据 VM 参数 XML 的 `visibleWhen` 和高级分组声明，`DeclaredFieldsView` 对两类字段统一保留状态、行身份和访问合同，仅在当前条件可见时物化编辑器。该调整覆盖运行参数整页，Release 全量构建 0 错误，正式载荷 SHA-256 为 `0EB2902A77706C82628FF528C5573D42DA19AA1541549742A05E2FF629544FB2`。本批没有新增自动生产探针，人工验收由用户直接启动 Release 程序完成。

**2026-11-13 ROI 提交阻断进一步收口**：确认 BlobResultDisplay 字号正式默认值为10，ROI调试窗口提交时出现的 100.00 来自已物化但隐藏的旧结果显示页控件；其残留校验状态不应否决当前活动页的正式草稿。ToolEditorCommit 的活动验证现仅让可见控件阻断提交，正式字段状态仍由编辑草稿/参数校验负责；Blob三页回归保持5/5。ROI回归已越过 PrepareInteraction、ROI绘制和阈值40草稿提交，继续在测试入口的通讯租约排空处阻断（直接 SolutionManager.Open 的夹具生命周期），该失败未归因于ROI产品链，需单独补齐测试入口释放或改用真实方案切换入口后再验保存重开。

**2026-11-13 ROI 保存重开回归闭口**：独立 ROI 回归已补齐与生产方案切换相同的通讯租约排空前置（DisposeCommunicationRuntimeAsync），不改变产品提交语义；Confirmed_blob_roi_threshold_and_display_reopen_on_the_same_image_source_connection **1/1 通过**，确认 ROI 绘制、阈值40提交、结果显示文本、保存、重开、输入来源连接和 ROI 再次准备均贯通。此前的租约失败是测试入口漏掉生产切换前置，不是权限或算法阻断。

**2026-11-13 变量图像 ROI 阻断已解除**：根因是私有变量图像借帧后，租约有效性错误依赖正式结果发布身份；在双图切换/编辑器重绑定后，该身份会被清理，导致 ROI 会话 PrepareInteraction 判定帧失效。现对临时变量预览使用独立私有视口和当前帧有效性合同，释放仍由租约负责，不改正式结果选择。验证：变量图像源→ROI→正式执行回归 1/1；变量 ROI、正式 ROI 保存重开、Blob 三页回归合计 7/7；UI.Tests Release 构建 0 错误。当前阻断不再是权限或通讯租约问题，剩余总目标仍需按既有矩阵继续完成人工验收与全量主线封签。

**2026-11-13 上游失败结果退役修复**：全量 UI 回归进一步暴露 ImageSource 失败后下游 Blob 仍保留旧成功 publication 的真实生产缺口。DocumentSession.AcceptRunNodeResult 现以同一流程连接图递归退役失败节点的下游结果 reader，源节点失败结果仍保留用于诊断；旧 reader 在结果安装后释放，恢复运行可重新发布新代次。验证：Source_failure_retires_the_blob_input_but_preserves_borrowed_frames_and_recovers_with_a_new_frame 1/1；ImageSource Runtime 在设置 S2_CASE_ROOT=E:\\SunEyeVision_V2\\Release\\_validation 后 45/45；全量 Release 构建 0 错误并已重新封签。Blob Runtime 当前 127/129：1 项测试发现阶段缺少正式工具程序集探测，1 项屏蔽区像素期望差异待按 VM 原证继续核对，未计作通过。

**2026-11-13 当前链路复核**：重新封签后，ImageSource Runtime 在 S2_CASE_ROOT 验证根下 **45/45**，Persistence **56/56**，UI 关键链路（变量图像 ROI、上游失败退役与恢复、ROI 保存重开、Blob 三页）**8/8** 通过；全量 UI 仍有历史/环境与若干结果工作区测试失败，不能把局部 8/8 扩大为整个 VM 生态已闭口。
**2026-11-13 结果工作区作用域判定修正**：按 VM 证据，打开调试窗口且仍使用正式 publication 时，编辑作用域变化本身不应重建图像/图形/来源对象；只有变量图像来源或来源值集合实际变化才跨作用域重建。`DisplayUnchanged` 已按该边界调整。回归：`Opening_the_debug_window_keeps_the_presented_result_workspace_untouched`、变量图像 ROI、ROI 保存重开 **3/3** 通过。仍有"非默认 Blob 输出在调试窗口打开时应清除图像值"等结果策略用例待继续对照，未宣称结果工作区全闭口。

**2026-11-13 结果选择阻断仍在**：已确认编辑器把 Blob 输入草稿切换到 `output.gray-image`，但 `Blob_primary_image_edit_selects_the_new_roi_frame_without_replacing_the_saved_output_choice` 仍在结果工作区刷新后读到旧 binary 输出，期望与实际分别为 `output.gray-image` / `field:8ca100...00000006`。当前不再继续叠加猜测性补丁；已保留输入来源、正式显示选择和私有调试帧三者分离的现行合同，下一步需按 VM 原件补齐"输入变更触发当前显示帧、且不写回已保存显示选择"的确切事件顺序。该项未通过，不计入完整链路闭口。
**2026-11-14 VM 结果图形第二轮托管逆向补证**：按用户要求继续自行取证，不把现有 VM 截图或导出名称当作完整依据。REA 对本机 `VMControls.WPF.dll` 与 `VMControls.RenderInterface.dll` 完成托管成员/CIL 检查。原厂 `RenderControlBase` 明确维护 `shapeBuffer`、`shapeList`、临时图形存储，执行后经 `UpdateShapesWithCallBackData`、`DrawCurModuleUIResult` 分发结果图形；`IRectangle` 只包含中心、宽高、角度和倾斜角，`IPoint`、`IOutlinePoint`、`IText` 分别承担点、轮廓和文本。`IMVSBlobFindModuCs.BlobFindResult` 同时提供 `CentroidPoint`、`BlobRect`、`ContourInfo`、`BlobStatus`、`BlobNum`，据此确认质心应作为独立点图形消费同一结果对象，不能把中心标记臆造为矩形属性。证据已追加到 `docs/collaboration/evidence/VM原厂本地图像与ROI刷新逆向取证-20261114.json` 和现行 HTML 清单。尝试对原生 `IMVSBlobFindModu.dll` 做函数/字符串深度检索时，REA 返回 provider unavailable，诊断为 Windows 未配置 Ghidra/IDA/Kuna，故原生参数布局、调用顺序和运行时值保持待证，未据此新增设计或代码。当前仍不运行测试，继续以本地图像 → Blob 完整链和最终人工验收为闭口条件。
**2026-11-14 Blob 结果显示输出需求链静态闭环核对**：沿当前代码入口核对 `BlobResultDisplay.Descriptor.RequiredOutputs`、`ExecutionNodeSnapshot.RequiredDisplayOutputs`、`BlobSession.ExecuteAsync`、`BlobResultProjection/BlobOutput` 和 `ResultWorkspacePresenter`。轮廓显示声明会形成 `OutputValueRequest`，Blob 执行按 `boundary/contour/contour-lossy` 请求边界，边界、轮廓点、质心、矩形和结果表由同一批 BlobMeasurement 冻结到同一结果快照，再由同一 publication 借值绘制。该静态链与 VM 的结果对象分发证据一致，未发现需要新增旁路计算的差异。实际图像下数量、颜色、下拉切换和保存重开仍属于人工验收项，未运行测试。
**2026-11-14 本地图像方案持久化静态链核对**：核对 `SolutionDocumentReader.ReadNode`、`DocumentSession.CapturePersistenceDeclarations/Save/SaveAs`、`DocumentSessionManager.Persistence` 和 `DocumentSession.FromPersistenceCandidate`。当前链路会恢复和保存 Blob 参数、显示字段、参数绑定顺序、ImageSource 路径与运行选择、节点布局/连线、嵌入资源及声明指纹，并在保存前接纳编辑器、保存后建立正式基线。实际保存重开后的运行、ROI 重投影、取消关闭和资源释放仍待正式 Release 人工验收，未运行测试。
**2026-11-14 本地图像资源生命周期静态链核对**：核对 `ImageSourceHostInputFactory.Dispose`、`ImageSourceRunSequence.Release`、`LocalImagePathCache.Dispose`、`ImageSourceInvocationInputs.Prepared.DisposeAsync` 和 `ImageSourceSession.DisposeAsync`。游标、文件夹 watcher、缓存副本、处理状态、输入租约及成功/失败输出资源均有明确所有权转移或释放路径，取消和异常路径也有临时资源清理。多张文件夹连续运行、取消与迟到回调仍待正式 Release 人工验收，未运行测试。
**2026-11-14 VM 本地图像列表控件第三轮补证**：REA 对 `ImageSourceModuleControl.LocalImageListView` 重新核对成员与 CIL，确认 `Root`、`list`、`cbx`、`CurrentImagePath`、`OnRootChanged` 和 `cbxMode_SelectionChanged` 是参数树/列表会话接缝，控件自身不持有算法执行状态。该证据与本方 `ImageSourceStripSession`、`ImageSourceProjectionBinding`、`ImageSourceHostInputFactory` 的职责边界一致，未发现应把执行游标移入 UI 控件的依据。模式切换时序仍待正式 UI 人工验收，未运行测试。
**2026-11-14 正式 Release 人工验收顺序固化**：已在 `docs/09-现行文档/ImageSource-Blob完整生态链对照清单-20261114.html` 增加一条连续验收顺序，覆盖方案打开、单图运行、结果下拉切换、ROI 绘制与运行重投影、多图/连续运行、保存重开、文件夹订阅取消关闭和资源排空。该顺序只作为统一人工验收入口，不把未执行步骤记作通过，本轮不运行测试。
**2026-11-14 用户验收状态回执**：用户选择“已完成，继续修复失败项”，但当前回执未包含失败步骤、现象、截图或日志。已检查工作区、Release 进程和临时截图，未发现新的 SunEyeVision 失败证据。继续修复前需要具体失败链，否则不能依据猜测修改。当前正式 Release 入口和完整验收顺序仍保持不变。
**2026-11-14 ROI 运行后形状丢失根因修复**：按用户最新实机问题继续沿完整链定位。`BlobLocalRoiOwner.RefreshFrame` 在运行换帧或参数控件短暂卸载时会退役 ROI 会话；现有 `PreviewLoaded/PreviewVisibilityChanged` 只重建只读预览，未重新借用可编辑 ROI 帧，导致控件恢复后编辑器保持 `Bind(null)` 空态。依据 VM `SetRoiVisible`/`UpdateROI` 的“视图恢复后重投影”语义，已在 `BlobLocalRoiOwner.Preview` 增加一次 Dispatcher Loaded 优先级的有界 `QueueFrameRefresh`，控件重新 Loaded/Visible 时调用现有 `RefreshFrame` 重建会话并从已写入参数恢复形状；卸载和 Dispose 会取消排队操作。未增加显示刷新→ROI 的跨路径通知，未改变阻断提示和自动切图语义。待 UI Release 构建后由用户复验“绘制→运行→形状保持可交互”。
**2026-11-14 全入口闭链批次最新构建**：沿“调试执行完成 → 结果快照保留租约 → 图像结果/Blob 结果层 → 可编辑 ROI 借帧 → 调试窗口关闭 → 正式结果替换/节点切换/方案关闭释放”完整链路复查后，全量 `SunEyeVision.slnx` Release 构建通过，0 个错误。最新 Build payload `5E79580B07EFF9D729DA314715BC2BE2297E8A11B3EBF80E0769CF09F72763B4`，正式程序生成时间 `2026-11-14 13:00:53`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`。本批没有运行自动化测试，构建只证明源码和正式载荷一致；ROI 可编辑、检测区域结果显示、调试窗口关闭后的结果保留、保存重开和资源释放仍由用户统一人工验收。`anti-flicker-wip.patch` 仅保留为历史参考，不属于正式载荷。
**2026-11-14 执行后 ROI 编辑层强制换代修正（正式 Release 已构建，待人工复验）**：用户在上一版 Release 实机确认，绘制前橙色控制点正常，点击执行后只剩蓝色检测结果框，编辑层再次不可命中。该现象排除绘制入口和结果算法，实锤为执行完成后的视口回执换代没有落实到 ROI 编辑会话。原有 `Refresh()` 只在 `Frame.IsCurrent=false` 时退役，不能表达“视口已重新绑定但物理图像可能相同”的原厂 `UpdateROI()` 语义。新增 `NativeRoiFrameSource.FrameReplaced/RebindCurrentFrame` 专用通知，主窗口结果/执行完成通知改走强制换代，Blob owner 按“退役旧 attachment → 释放旧会话 → 当前 Loaded/Visible 条件下重新借帧 → 重建编辑会话并恢复参数形状”执行；普通参数刷新仍保留原路径。第一次编译检查发现强制路径在 `_refreshing=true` 内调用获取会被守卫跳过，已修正为退役结束后再获取。正式全量 Release 构建成功，0 错误、6026 个存量警告，Build payload `C5E75AC14B3AFF13FDF58FC42AACDBB5865C025BC4A53818561806F66E19EBAF`。程序 `SunEyeVision.UI.exe` SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `2B91EC372E9A74534D6B98F89C66ED38BBA5C74913A96C5B93530C3A3AC90121`，Blob WPF DLL SHA-256 `32F9EC7822F1737EA9E9C87271FF7761807811EEF5BC3EC2AFC4BF091443D172`。按用户要求未运行自动化测试，待人工复验“绘制→执行→橙色控制点仍在并可命中”。

**2026-11-14 结果绑定后恢复活动 ROI 编辑组（正式 Release 已构建，待人工复验）**：上一版实机仍显示执行后仅有结果投影，进一步定位到 `NativeResultViewport.BindImage` 会在执行换图时清空 editable attachment；Blob owner 只恢复参数和 session，没有恢复执行前的活动编辑组，因此新帧不会再次调用 `BeginRoiEditing`。本批在结果工作区 `Present` 成功并进入空闲调度后触发 ROI 换代，并在 Blob owner 换代前保存 `_active` 到 `_pendingActivate`，重借当前 `Frame/Epoch/Coordinates` 后由 `CompletePendingActivation` 重新创建可命中的编辑 attachment。结果层和交互层继续分离，ROI 形状仍从参数图像坐标按当前视口回执投影。正式全量 Release 构建成功，0 错误、6004 个存量警告。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `91B2B8BA71801542053812D0F7F23BDD4AF7835BAF8B0070D3C710280FDF7E1C`，Blob WPF DLL SHA-256 `637F637D8E1330C8ADE1DE5A7E3262C190EDD4D4984405A8578974EE70288FB8`。Build payload 需从本次构建输出最终封装信息补录。未运行自动化测试，待人工复验“绘制→执行→橙色控制点仍在且可命中”。

**2026-11-14 ROI 选中态恢复（正式 Release 已构建，待人工复验）**：用户确认执行后可编辑图像仍在，消失的是 ROI 选中态和橙色控制点。根因是换帧重建时新 `NativeRoiSession` 的 `CurROIData` 从空开始，活动编辑组恢复不足以恢复原厂选中对象。现已在 Blob owner 中按稳定 ROI ID 保存各组当前选中项，创建新 session 并绑定参数后先恢复选中项，再执行活动组 `BeginRoiEditing` 和当前视口坐标投影。定向编译通过，正式全量 Release 构建成功，0 错误、5896 个存量警告。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `91B2B8BA71801542053812D0F7F23BDD4AF7835BAF8B0070D3C710280FDF7E1C`，Blob WPF DLL SHA-256 `19A7311716D1EAF382566E0CC0AA265A939931873E0DA5FB8BBB3E3579D13BBA`。按用户要求未运行自动化测试，待人工复验“绘制→执行→原 ROI 仍选中、橙色控制点仍在、点击可编辑”。

**2026-11-14 调试窗口关闭/重开 ROI 生命周期补齐（正式 Release 已构建，待人工复验）**：用户指出可编辑图像仍在，但关闭调试窗口再打开后 ROI 生命周期不完整。原厂证据要求窗口重开重新执行 `SetRoiVisible`/`UpdateROI`，恢复当前输入图上的 ROI 选中与编辑状态。现已在 Blob owner 首次重新借帧时恢复稳定 ROI ID；若当前草稿没有跨窗口可用的选中 ID，则为本组首个有效 ROI 建立初始选中态并恢复活动编辑组，再创建 attachment 和当前坐标投影。正式全量 Release 构建成功，0 错误、仅 1 个 log4net 安全警告，Build payload `26BB7EE4E7E38D9E46563064F681547F31582B150FDD93E7612A0EC294E08E5C`。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `91B2B8BA71801542053812D0F7F23BDD4AF7835BAF8B0070D3C710280FDF7E1C`，Blob WPF DLL SHA-256 `9BC3FA02BED276393B9CD674BAD276E00953DF050EE49EB843F35D926379AEE1`。未运行自动化测试，待人工复验“打开窗口→确认 ROI 可编辑→关闭窗口→重新打开→ROI 仍显示且可命中编辑”。

**2026-11-14 节点样式与选择状态完整对照启动（正式 Release 已构建）**：用户对照 VM 截图指出当前节点图标、主体和选中边框仍有差异。已确认当前画布节点由 `CanvasNodeProjection.IsSelected`、`RunState`、`IsEnabled`、`NodeDisplayType`、状态色块和图标资源共同决定，不能只改一个图标。上一批已补调试窗口关闭后的节点选中投影恢复，当前正式全量 Release 构建成功，0 错误、5896 个存量警告。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `8BD9F86B499B04934D5273F44E1818FC8F2B4885651454949EEF4CC7BC874116`，Blob WPF DLL SHA-256 `AD3CDE9C0FAC0EE69F99FBDAF33A0F5F15D4CAF6CBF4F206FF250B968F4B3CFD`。待按 VM 证据逐状态核对普通/选中/悬停/执行/成功/失败/禁用/子程序/节点重建后的图标、色块、主体、边框、阴影、文字和端口，不把静态截图差异直接归因于单一颜色。未运行自动化测试。

**2026-11-14 回退跨窗口 ROI 选中缓存（回归修正，待统一构建）**：用户实机确认新增选中缓存改变了既有状态，重新打开后可能选中错误 ROI，节点选择/样式也出现回退。复核确认跨窗口 `ConditionalWeakTable<IToolSettingsDraft,...>` 与首个 ROI 自动选中不符合 VM 的状态边界，已撤回；当前只在同一 Blob owner 的视口换代期间保存稳定 ROI ID，关闭重开不再强行复用旧 owner 选中对象。关闭后的节点画布选中强制重写也已撤回，避免覆盖原有多选/上游节点选择。后续以参数几何快照、当前编辑会话、结果快照、画布选择四种状态分离为准，统一构建后再验收。

**2026-11-14 回归修正后的正式 Release 基线**：已撤回跨窗口选中缓存、首个 ROI 自动选中和关闭时强制覆盖节点选择，保留同一 owner 内的执行换帧恢复。正式全量 Release 构建成功，0 错误、628 个本次构建输出警告。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `E96FE7D41DECB4EEA6A2F3E4C9760D0321C89C483B11F3FF5DDA4164B6D7039A`，Blob WPF DLL SHA-256 `0701687CBD6785A6BA05D137B47FD2D3DE509F3A1E1FAB7C50745F3A3D717792`。该基线用于统一人工验收，不把先前受跨窗口缓存影响的结果继续沿用。

**2026-11-14 未执行编辑保持最近结果 + 重开 ROI 编辑层（正式 Release 已构建）**：收口要求确认：修改 ROI 或 Blob 参数但不执行时，Blob 必须保持上一次成功执行的结果图形、文本、结果表和节点执行状态；关闭/重开调试窗口时，无论是否执行过，当前参数中的 ROI 都必须重新挂接编辑层并可命中。已接入窗口关闭提交前的结果快照保留回调，并在重开时从当前参数有效 ROI 创建 session、选中对象和编辑 attachment。正式全量 Release 构建成功，0 错误、5896 个存量警告。程序 SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，UI DLL SHA-256 `B7DE7F13ACDB80C3CC8798133FD5459229EAD16831E2BDABBFBC5FFA9A990B5C`，Blob WPF DLL SHA-256 `FEEE640E09D4F5DD62416FDFFD145A7B5CFCC112157E4C47416A1F413D645F2A`。未运行自动化测试，待统一人工验收。

**2026-11-14 快照更新机制边界修正**：用户确认执行快照、参数快照和结果快照的更新机制存在耦合。当前统一合同为：参数草稿快照随编辑更新，执行快照仅在调试/工作流成功执行后替换，结果发布快照引用最近成功执行快照，ROI 编辑会话快照只随当前视口回执重建，节点执行状态按稳定节点 ID 关联。参数提交导致同一节点实例重建时保留执行/结果快照，真正切换节点时才清理。`SetSelectedNodeIdentity` 已按稳定节点 ID 保留当前已完成结果，避免未执行参数或 ROI 修改清空上一次图形、文本、表格和节点状态。待正式 Release 构建和统一人工验收。

**2026-11-14 VM 全链逆向分析基线（当前有效批次）**：按用户要求暂停继续叠加局部补丁，先完成 ImageSource→Blob→结果 publication→结果图形层→ROI 编辑层→参数草稿→执行快照→调试窗口开关→节点实例重绑→保存/退役的完整对照。原厂证据已沿 `ModuleResultView.OnModuleCallBackArrived`、`SetRoiVisible`、`OnRelatedIOChanged`、`UpdateModuleViewResultShow`、`RenderControlBase.UpdateShapesWithCallBackData`、`AlgorithmTabRoot.UpdateROI` 和 `ROISelecter.UpdateRoi` 固定。确认原厂参数修改未执行时通过 `GetValue(uint.MaxValue)` 保持最近结果，窗口关闭只隐藏 ROI 编辑层，结果图形/文本/表格不清空；执行回调末尾才对同一 ROI 对象重投影。当前本方实锤差异是 `RefreshFormalNodeSelection` 无条件 `ReleaseActiveResultView(); ClearResultDisplay()`，而 `ClearResultDisplay` 又清理调试快照保留租约，导致稳定节点实例重绑、参数接纳或关窗路径清掉上一次结果；另有节点实例与结果工作区陈旧视图拒绝、ROI 几何与选中/编辑会话分离问题。完整分析写入 `docs/09-现行文档/VM-ImageSource-Blob全链快照与窗口生命周期逆向分析-20261114.html`。本条只记录逆向结论和设计边界，尚未按该结论修改源码，也未重新构建；此前 Release 构建不能视为覆盖本轮分析。

**2026-11-14 结果快照与节点重绑边界修复并正式构建**：按 VM 全链结论修改 `MainWindowRuntimeEditing.RefreshFormalNodeSelection`。同一流程/节点稳定身份的参数草稿接纳、节点实例替换只释放旧结果视图实例并保留最近完整调试结果租约，不再调用会清理保留快照的 `ClearResultDisplay`；真实节点/流程切换仍走原清理路径。Blob ROI owner 当前已有“借当前输入帧、恢复既有 ROI、自动恢复编辑 attachment”的重开补链，编辑 ROI 与结果投影继续分开。执行 `dotnet build-server shutdown` 后完成正式全量 Release：0 错误、366 个存量警告，Build payload `88C293FBF17C4B6380DEDE086BE000C9869FFB6A77D653AE429BCCB29C596DEE`。正式载荷 `SunEyeVision.UI.exe` 生成时间 `2026-11-14 15:54:28`，SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`；`SunEyeVision.UI.dll` SHA-256 `C64458980065DAE29E4B8DAC33FE24594997E75AA300C476A5A57CBBA35E303A`；`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `FEEE640E09D4F5DD62416FDFFD145A7B5CFCC112157E4C47416A1F413D645F2A`。按用户要求未运行自动化测试，待统一人工验收“执行后结果层与 ROI 编辑层共存、未执行修改保留上一轮结果、关闭/重开 ROI 可命中、节点切换和方案关闭释放”。

**2026-11-14 节点运行状态保持修正并正式构建**：用户确认不执行参数/ROI 修改后节点状态仍发生变化。根因在 `DocumentCanvasProjection.OnChanged` 对 `ExecutionInvalidatedNodes` 立即重新读取已被执行域退役的结果，并在 `Synchronize` 按 `NodeDefinition` 对象引用重建投影，导致稳定节点实例替换后 `RunState` 变为 `None`。现改为按稳定 `workflowId + nodeId` 重用 `CanvasNodeProjection`，新增 `Rebind(NodeDefinition)`，保留上一次运行状态；参数编辑的执行失效通知不再清空画布状态，下一次真实运行结果仍通过 `RuntimeResultAvailable` 更新状态。全量 Release 构建通过，0 错误、368 个存量警告，Build payload `5D338CDF862EAE7251CA6E850A864821E2888B250F805D60ABFEAF3BD9F368EB`。ROI 交互本轮未改动，继续按用户当前反馈作为已基本正常，待统一人工验收节点状态保持与 ROI 命中编辑。

**2026-11-14 ROI 参数页切换生命周期修正（正式载荷待重建）**：用户确认从基本参数切到运行参数时结果投影仍在，但 ROI 交互层被清空。根因是 `BlobLocalRoiOwner.PreviewUnloaded/PreviewVisibilityChanged` 把基本参数控件的页签卸载/隐藏当成 ROI 帧退役，且 `FrameCurrent` 强制要求控件可见，违背 VM 参数页切换只改变视图可见性、不销毁 `ModuleResultView` ROI 对象的语义。已移除页签卸载/隐藏时的 `RetireFrame`，并将帧当前性改为由 owner、代次、物理帧和租约决定，重新显示时复用原会话和既有 ROI。Blob WPF 候选输出构建通过，0 错误、5269 个存量警告，输出位于 `Release/_candidate/TabRoiLifecycle/app`。正式 Release 重建第一次因 `Release/SunEyeVision.Tool.BlobAnalysis.Wpf.dll` 被外部进程占用失败，当前没有 SunEyeVision 进程但仍存在文件锁；已通知用户关闭 CodeBuddy/IDE 或残留调试进程后再完成正式构建。当前正式 Release 仍是上一载荷，不能用于验收本轮切 Tab 修复。

**2026-11-14 ROI 参数页切换修复正式 Release 完成**：用户关闭占用进程后，正式全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 成功，0 错误、5634 个存量警告。Build payload `AF4C504A6CEB5DA78EF0F2BB460D2CB5B60B59ADA225708F349C1C45F789D826`。正式载荷程序生成时间 `2026-11-14 19:46:38`；`SunEyeVision.UI.exe` SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`；`SunEyeVision.UI.dll` SHA-256 `330BFE8A1DCB890C0E5D1B803D29D10D22A70E6636BE2468D28163036603BAFA`；`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `90CAAE5EE93C5FD64AF937286F81A9068E01F0E16BC8D975A082246C2DA561C0`。本轮切 Tab ROI 生命周期修复已进入正式载荷，自动化测试仍按用户要求未运行，待人工验收基本参数↔运行参数切换后 ROI 仍可见、可命中、可编辑，以及关闭/重开和执行后重投影。

**2026-11-14 Blob 右上角图像下拉原厂顺序/名称修正并正式构建**：对照用户两张截图和 VM 结果视图证据，确认当前差异来自 `CaptureNativeImageSources` 按全局节点序号排序且直接使用输出声明名。已改为当前选中模块自身图像输出优先、上游来源随后；ImageSource 显示名映射为“输出图像/灰度图像数据”，Blob 图像项映射为“显示Blob图像/显示二值化图像”，端口身份和结果绑定保持不变。正式全量 Release 构建通过，0 错误、60 个存量警告，Build payload `C8E23C86FE24FB28F4C43865F9952FE44846722BE5703CC368FBB9BF09EEF8A0`。正式载荷生成时间 `2026-11-14 19:51:40`；`SunEyeVision.UI.exe` SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`；`SunEyeVision.UI.dll` SHA-256 `BB9F8A5DABAC375C5DF52445EFCA8E69176B379B7278625ACD4ED426959E0E4B`；`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `90CAAE5EE93C5FD64AF937286F81A9068E01F0E16BC8D975A082246C2DA561C0`。按用户要求未运行自动化测试，待人工验收 Blob 选中时下拉顺序、标题和默认选中来源。

**2026-11-14 未执行编辑不改写结果快照**：用户复验发现执行后修改 ROI/参数再切换节点，结果图形和文本仍跟随未执行草稿变化。根因是 `CaptureCurrentResultDisplay` 在保留上次执行快照时仍用当前编辑草稿读取结果显示描述，旧快照的几何与文本因此被新草稿重排。已按 VM 语义拆开两条来源：当前有效调试快照可使用已接纳草稿，保留的上次执行快照一律使用正式节点配置，直到下一次执行完成替换。此前节点切换清理保留快照的路径也已移除，关闭窗口、切换节点和返回 Blob 都继续持有正确快照租约。全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 通过，0 个错误、366 个存量警告。正式载荷生成时间 `2026-11-14 20:06:31`；`SunEyeVision.UI.exe` SHA-256 `D909095125AD3ACF3F5E36DD1D7FD62C74A94FAA3C4135276C302C9696533AC7`，`SunEyeVision.UI.dll` SHA-256 `591D7FF49798ED4B14C6AA1EF9CF953F536C07D1EFA8612B46CA1257C4BEA261`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `90CAAE5EE93C5FD64AF937286F81A9068E01F0E16BC8D975A082246C2DA561C0`，载荷清单哈希 `C82FF5ABAA75BF99F01A0299767717A1420B43A4CD9CDF51C080CC3515693DA`。未运行自动化测试，待人工按“执行一次→修改 ROI/参数但不执行→切换图像源→切回 Blob”验收结果层保持上次执行状态。

**2026-11-14 人工验收通过**：用户确认当前测试正常。已覆盖执行后修改 ROI/参数但不执行、切换图像源节点、切回 Blob、结果图形与文本保持上次执行状态，以及调试窗口重新打开后 ROI 编辑层恢复并可交互。上述结论对应本条 Release 载荷，未扩展为保存重开、连续运行或其他未验证链路。

**2026-11-14 工作流 Tab 连续执行停止入口修正**：用户反馈从工作流 Tab 点击“连续”后无法停止。复核发现 Tab 停止回调额外要求 `_formalRunningRootIds` 已包含该流程，而该集合是运行状态投影，在轮次结算和 UI 通知之间可能短暂滞后，导致停止点击直接返回 `false`，没有调用连续会话的 `Stop()`。已改为以活动的 `DocumentContinuousRun` 作为停止所有权，只有确实没有连续会话时才拒绝，单次正式运行不会被该入口误停。该入口由所有工作流 Tab 共用，因此其他连续运行入口也一并覆盖。全量 Release 构建通过，0 个错误、366 个存量警告；`SunEyeVision.UI.dll` SHA-256 `D420B33C14264828BF50FB17C4E3E4B7B4A1B5334AFB22C200D30C494F7370FA`，生成时间 `2026-11-14 20:22:04`。未运行自动化测试，待人工验证“工作流 Tab 点击连续→再次点击停止→状态回到已停止”，并补验多流程连续运行停止。

**2026-11-14 ROI/屏蔽区命中与形状按钮合同差异复核**：用户补充并确认原厂行为：已有检测 ROI 或屏蔽多边形可直接命中、选择和编辑，是否选中对应形状按钮只影响下一次新绘制的形状类型；命中已有区域后，对应形状按钮自动高亮。未执行时交互层保持蓝色轮廓和控制点，执行后橙色填充属于结果显示层。本方当前 `BlobLocalRoiOwner.Project` 将 `Enabled` 同时用于命中和当前绘制组权限，只启用当前按钮组，导致未选中按钮对应的已有区域无法命中；检测区和屏蔽区还各自持有 `NativeRoiSession`，结果视口一次只挂一个 `NativeRoiAttachment`，与原厂单一结果视图共享 ROI 集合的结构不同，跨组命中、选中传播和按钮高亮因此存在合同缺口。当前先完成共享选中/命中/按钮同步与新建权限的完整设计和原厂对照，再集中实施，不做颜色或单点命中补丁。

**2026-11-14 ROI/屏蔽区共享命中链实施**：按 VM `ROISelecterHolder` 与 `ImageViewBox` 证据，将本方两组 ROI 的命中权限与新建权限拆开。结果视口挂载的任一 ROI Session 现在包含检测区和屏蔽区的全部本地可见对象，已有对象可直接命中；命中后 `NativeRoiSession.Select` 按实际对象组切换当前编辑组，并由 owner 将稳定选中对象同步到另一组 Session，使对应参数栈和形状按钮高亮。新建仍由当前绘制组的 `canDraw`/`canSetCoverage` 门控，点击形状按钮只决定下一次新建形状，不再控制已有 ROI 的命中。提交/替换/删除提案按真实目标组写回，撤销/重做按当前选中组回退，避免屏蔽区命中后写入检测区。UI Release 编译通过，0 个错误；随后全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 通过，0 个错误、374 个存量警告。正式载荷生成时间 `2026-11-14 23:41:43`；`SunEyeVision.UI.dll` SHA-256 `D420B33C14264828BF50FB17C4E3E4B7B4A1B5334AFB22C200D30C494F7370FA`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `BF8A8D5C5B680385BB1ACF74091FBC9CB44D43044C5431164805E025F1D8FA8B`；UI 载荷封签 `55D5775E65830D5635611C7702801A7C5BB57CF6E575DAE9F494350914D469F8`。未运行自动化测试，待人工验收检测 ROI 与屏蔽多边形可自由命中、命中后按钮高亮、按钮只影响新建类型、拖拽提交和跨组写回。

**2026-11-14 共享命中链最终载荷更新**：补充选择同步重建期间的刷新门禁，避免双 Session 重绑时互相覆盖选中对象。UI Release 增量构建通过，0 个错误、315 个存量警告；当前 Release 目录产物 SHA-256 为 `SunEyeVision.UI.dll=A2DDC82CC1FDF779EF013059731360AD97CB8649119A71C5EDF2F9446A950E83`、`SunEyeVision.Tool.BlobAnalysis.Wpf.dll=763ADA37404D9B1B7BBA371EA8680A574627E25F1FE06B15076E74A96C5E6819`，生成时间 `2026-11-14 23:43:45`。全量 Release 已在同一共享命中改动上通过，未运行自动化测试。人工验收仍以当前目录载荷为准。

**2026-11-14 屏蔽交互填充与调试态图像源导航修正**：屏蔽多边形交互层现在依据 ROI 对象自身的 `IsAvoid` 使用橙色半透明填充，检测 ROI 继续保持蓝色空心；该样式不再依赖执行结果层。另修正命中屏蔽多边形后继续新建 ROI 的状态恢复，点击新的形状按钮会先把当前绘制组切回对应组，再进入新建流程。调试窗口打开时图像源缩略图不再被 `IsDebugWindowOpen` 硬阻断，允许切换当前显示游标，且不会启动正式工作流或提交调试草稿。全量 Release 构建通过，0 个错误、412 个存量警告；当前产物生成时间 `2026-11-14 23:56:12`；`SunEyeVision.UI.dll` SHA-256 `C4EFB1048D578290D655F578906BFDA68424E7FD488EF73318D0AFF9D7B37C37`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `B50D2C8222A5E8A77C161CB8DE3EBF979D2607D7B51D0ACD967A9BF1EBA19B14`，`SunEyeVision.Plugin.Wpf.dll` SHA-256 `0CA4D8B4ADEDA01BD696F58E0FF30CC19364B9B4F58D25DE6CB8D072087B315F`。未运行自动化测试，待人工验收屏蔽区执行前填充、选中屏蔽区后继续绘制检测 ROI，以及调试窗口打开时切换图像源缩略图。

**2026-11-15 调试态缩略图执行语义按原厂证据纠正**：原厂 `ImageListPanelBase.lst_SelectionChanged`（仓内 `ImageListPanelBase.cs.pending:1316-1358`）在缩略图选中变化后写入 `CurrentImagePath`，通过 `VmProcedure.Run(null, bIsWait:false)` 立即执行，没有调试窗口门禁。此前将调试窗口打开时的切换改成“只更新游标、不执行”属于错误实现，现已撤回。当前实现允许调试窗口打开时切换图像源缩略图，并通过 `RunFormalWorkflowsAsync(..., allowDebugWindow:true)` 走正式执行链，保留调试窗口。全量 Release 构建通过，0 个错误、5675 个存量警告；产物时间 `2026-11-15 00:00:29`；`SunEyeVision.UI.dll` SHA-256 `6517CBE775EA6869ADE259E952B78C94A63D964FEDDD7262E09A6D42BA8E531D`，`SunEyeVision.Tool.BlobAnalysis.Wpf.dll` SHA-256 `B50D2C8222A5E8A77C161CB8DE3EBF979D2607D7B51D0ACD967A9BF1EBA19B14`，`SunEyeVision.Plugin.Wpf.dll` SHA-256 `7AB797B82BFD7393449767845C81D889ED8112BD54CFCA46D8AD9FE48E722023`。未运行自动化测试，待人工验证“调试窗口打开→切换缩略图→立即执行→调试窗口仍在”。










**2026-11-15 自动切换结果层清空修复（无探针 Release）**：用户截图确认此前版本仍会出现“图像保留、Blob 结果图形/文本/当前结果表清空”。沿 `ImageInputSource.AutoSwitchEnabled → ImageSourceProjectionBinding.AcceptEdit → DocumentSession.Changed → MainWindowDocumentHistory.RefreshChangedDocumentViews` 复核，根因是策略提交的文档变更通知先于显示留存租约接纳，观察者提前执行 `RefreshFormalPublishedResult/NotifyResultDisplayChanged`，使正式结果发布退役；图像像素随后被保留，所以出现上下两层不一致。已将策略提交窗口识别为 `IsAcceptingEdit`，在接纳窗口跳过结果发布退役和结果显示刷新；接纳完成后由 `LastAcceptedPreservedDisplay` 一次性保留当前结果发布。自动切换策略不再以配置起始索引判断显示留存，只有显式选图才改变显示游标。探针代码已清理。全量 Release 构建成功，0 个错误、1 个警告；Build payload SHA-256 `39EB44B0DE7488BCE730D7523C00697A3CC8B65D4CAD091F10501AC3AAD70304`，`SunEyeVision.UI.dll` `FCA7B78EF2C9F4040660346179F495BF66BE9FA69D3DE5EBB3C6CB92794D89AE`，`SunEyeVision.Tool.ImageSource.Runtime.dll` `B65D767274BB0397CCF3A66BB30F28851F24F3EAE90D80F2D5CF96F6D2CA6DDD`，`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `73F0C7980E24BD57F3549D947251935FADF46C1646C6FC32829676FF0F1C55E3`。ImageSource.Runtime 定向测试 39/45 通过，6 条 `Missing validation root` 为既有测试夹具初始化失败，与本次 UI 提交链无关；未将其计作本修复通过。人工验收：用该 Release 执行 ImageSource→Blob 一次，确认图像、Blob 图形、文本和当前结果表均存在；只切换“自动切换”开关，四者均保持；再执行一次确认策略仍生效；显式切换缩略图时才允许结果按新图刷新。




**2026-11-15 性能差异定性与设计对齐方案 V2（待 Codex 审阅）**：用户明确"要从设计上对齐 VM，执行耗时最优、又快又稳定"。实测重新定性：本方流程 300ms 中**算法仅 ~40ms（13%），框架开销 ~260ms（87%）**；VM 相反（算法 7.05ms/96%，框架 0.27ms/4%）。据此结论：**追平 VM 的主战场是消除每轮框架开销（模块常驻模型），不是优化算法常数**。原厂稳定机制已实证：`Program.cs:128-133` 启动期 `BeforeStartPart`（后台最高优先级线程）执行 `VmSolution.StartNewInstance()` 创建/接管算法 Server 实例——算法引擎启动即就绪、与 UI 解耦，点方案只加载方案，执行只发请求；本方 HALCON 延迟装载（`ReleasePayloadAuthority.cs:245-268`）使一次性成本隐式落在首次 `new HImage`（`ImageSourceSession.cs:195-199`），且受 OS 文件缓存状态影响（本机可用内存 2.9GB/15.8GB、Standby 2.5GB），故"时快时慢"（1107-3134ms vs 4ms）是**设计把确定性成本变成随机成本**，非环境问题。对齐路线 L1 预热（消首次随机）→ L2 执行链常驻化（引擎池化/会话复用/绑定物化缓存/快照按需，目标框架 260→80-120ms）→ L3 Blob 按需（38→≤15ms）→ L4 渲染差量；综合预期 300→约 100-130ms 且首次≈后续，仍约为 VM 的 15 倍（进程内 HALCON + 托管边界 + 快照隔离），完全追平需算法迁独立进程（单独立项）。方案文档 `docs/01-方案设计/2026/11/15/性能差异对比与优化总方案-20261115-173000.html`（V2，含 6 个待 Codex 审阅开放问题：快照按需端口消费矩阵、会话复用可行性、绑定缓存失效键、batch-capture 70ms 内部构成、预热挂点冲突、与 Codex 解码定性对齐）。**实施第一步必须先加执行链细粒度探针**定位 batch-run 内 160ms 分布，不得凭猜动常驻化。工作区另有 Codex 未提交改动（图像源缓存修复 `ImageSourceRunSequence.cs` +51 行 + 各层探针埋点）与 CodeBuddy 临时 `[DecodeSplit]` 探针待清理。

**2026-11-15 首次执行耗时临时探针（仅诊断，未构建/未运行）**：按用户要求不修改执行语义，新增环境变量 `SUNEYE_EXEC_TIMING_PROBE=1` 控制的分段埋点，覆盖正式运行批次捕获/批次执行/结果发布、绑定与设置物化、宿主输入准备、工具会话创建与执行、ImageSource 读取解码/图像帧捕获/输出、Blob HALCON 作用域初始化/图像复制/阈值分割。探针只输出 `[ExecTimingProbe]`，不参与状态、资源或结果决策；本批未构建、未运行，待用户下次实机复现后读取日志并删除临时探针。

**2026-11-15 图像源首次读取/解码/缓存生命周期修复（本批）**：按原厂证据 NC01/NC03/NC04 对照本方探针日志，确认首轮 3134ms 全部集中在 `new HImage(address)` 的本地解码；会话创建和 Blob/HALCON 初始化只占毫秒级。原厂的本地路径缓存记录挂在模块/节点生命周期，命中后复用已保存图像描述，不因游标、AutoPlay 或 `ClearImage` 直接释放像素缓存；`PathCache=0` 是参数默认值，不能把当前图像记录变成每轮临时对象。本方此前仅在 `PathCache>0` 时创建运行序列缓存，默认方案因此每次都重新进入解码入口，且没有文件变更指纹。已集中修复：所有本地图像源都在节点游标生命周期创建至少一个当前图像缓存；`PathCache` 仅增加保留记录数，缓存按路径+文件长度/最后写入时间校验，变更时淘汰旧资源；容量淘汰和节点/方案/配置退役仍由 `ImageSourceRunSequence.Release` 统一释放，游标选择、自动切换和 `ClearImage` 不清缓存。未改可见参数默认值，也未把预览缩略图缓存与运行图像缓存混用。下一步：正式 Release 构建；用户验收首轮/后续执行、自动切换、文件修改后重读及节点/方案关闭释放。

**2026-11-15 图像源缓存修复正式 Release 构建完成**：全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 成功，0 个错误，5656 个存量警告（含既有 `log4net NU1902`）。载荷封签 `A9F116B64656E4DDC59F998453A12AB7753FD791DA498561EFD01D1149D53D64`；关键文件：`SunEyeVision.Tool.ImageSource.Runtime.dll` `1B6A6B1357B63730625BD66258D389B6995FBF853A16417660AF1B622AE541EF`、`SunEyeVision.Infrastructure.ImageSources.dll` `32BFD02D602B0CD50CFF76F6E4B9059477A8FCA3E31D0A4F906AF9D80B5B58DF`、`SunEyeVision.UI.dll` `C132CF42667F0C8DE294BA4545FC2E320B865BF8F63B3AE083F2208F8331301D`、`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。本批未做实机运行，下一步用该 Release 验收首轮和后续执行的工具耗时、自动切换不清缓存、修改文件后重新解码，以及节点/方案关闭后资源释放。

**2026-11-15 公共执行链首次运行探针正式构建**：依据多工具首轮耗时差异，公共链新增环境变量开关 `SUNEYE_EXEC_TIMING_PROBE=1` 下的插件程序集解析/进程内首次使用标记，继续串联已有的会话创建、输入准备、工具执行、快照捕获和批次级发布耗时。全量 Release 构建成功，0 个错误，5749 个存量警告。载荷封签 `06502F5ABE1182B12678E54C576CD6FA69D5698992A43C83B38D5E77D64D88AE`；`SunEyeVision.Plugin.Runtime.dll` `0213D18BE7A34F283D6730B9302B2DB587F887448978C5579AB5ED0F2782DF6B`、`SunEyeVision.UI.dll` `B329FF3A909AF57EC0B5D82BCF197B7EDA51A255D4C35F2049551192F05679B6`、`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。下一次启动需设置该环境变量，首次执行至少覆盖图像源和 Blob，再执行同一工具两次，日志将用于区分插件首次使用、会话、输入准备、HALCON/工具执行及结果捕获耗时。









**2026-11-15 耗时查看窗口独立生命周期修复**：用户复现关闭“耗时查看”窗口后方案也关闭。查看器原先作为 `Application.Current.MainWindow` 的 owned window 创建，存在错误的宿主生命周期耦合。已解除 Owner，改为独立居中、显示任务栏的只读窗口；关闭只清空查看器 DataContext 和 ItemsSource，不参与主窗口/方案关闭链。全量 Release 构建成功，0 个错误、366 个存量警告。载荷封签 `0C740CA7C68F01E5B07619BCF1410F81C33BA36CDBCA11C6BF38E6D8BB318B99`；`SunEyeVision.UI.dll` `680A21BBDF210608CD4D6C045E860A18F8CD4F0E3AD4088864EE813B67ED8CAD`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。待用户人工确认关闭耗时窗口后方案仍保持打开。

**2026-11-15 公共执行链常驻化 Release 批次（当前有效）**：依据 VM 的服务端/ModuleProxy 常驻、启动预加载和执行请求复用证据，完成本方公共链第一阶段重构。`DocumentSession` 现在按方案会话缓存完整 `ExecutionPlan`（流程结构、节点身份、控制边和依赖图），命中时只校验当前工具声明，不再重复捕获 GraphState 和依赖图；计划只在节点配置/身份、连线、流程根或运行目录失效时退役。每轮仍重新捕获当前参数值、变量、宿主输入、图像租约和结果，避免把运行期状态冻结进计划。`WorkflowExecutionEngine` 改由方案会话持有并跨 `DocumentRunBatch` 复用，批次结束只清理运行态。节点快照缓存键增加当前配置对象身份，修复运行期当前值更新后旧快照被错误接纳的问题。执行定向测试 `SunEyeVision.Execution.Tests` 16/16 通过；图像源运行时套件未计入通过，之前的 `--no-build` 运行命中了旧产品封签并报 `SunEyeVision.Application.dll` SHA mismatch，非断言失败。全量 Release 构建 0 错误、5697 个存量警告；最终 `release-payload.json` SHA-256 `88F77081C1C8DD91AADD3CE7CE6447EFFFBE7C0EE9505D67F51361F79B0A921F`；`SunEyeVision.Application.dll` `17324F12C96F17BC6C0CEC2E71BAD901772A2B73AC13539631DDDDB96DAE01B9`；`SunEyeVision.Execution.dll` `00EA4187E919D64E37A6910B411C20B8D6ECA0D94419CC55A53D991BC01D7102`；`SunEyeVision.UI.dll` `CD5B0D8FEC02AC2BADD9B3F434D8553672A30CDE77567B3684CDC852F2FB0018`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。待人工验收首次/后续执行、连续运行、取消、参数修改后执行、节点/方案切换和关闭释放；重点观察日志中的 `execution-plan-build-start/complete` 只出现于计划失效或首次建立，后续运行出现 `execution-plan-hit`，并确认结果数值、图形、文本、历史和资源所有权不变。

**2026-11-15 常驻执行计划人工复测结果**：最新日志 `C:\Users\Administrator\AppData\Local\SunEyeVision\Logs\2026\11\15\SunEyeVision_203730.txt` 已确认方案预热批次出现 `execution-plan-build-start/complete`，首次正式执行及之后 3 次均出现 `execution-plan-hit`。计划命中段约 4–6ms，但正式首轮 `workflow-snapshots=37ms`，说明当前仍会按当前配置实例重建每轮 `ExecutionWorkflowSnapshot`，本批完成的是图结构/依赖图复用，不是完整执行快照复用。首轮正式执行从 batch-capture 到 result-publish 约 183ms（44ms 捕获、ImageSource 18ms、Blob 78ms、workflow-engine 161ms、结果发布 9ms）；后续约 65ms、60ms、52ms。与上一版同类日志首轮约174ms相比，首轮没有确认改善，差异仍主要在 `engine-build-result=149ms`、Blob 首轮 78ms 和 ImageSource 首轮解码 11ms；计划命中已证明，但不能宣称首次耗时问题已解决。后续优化边界应继续拆分每轮结果构建/显示来源捕获和工具首轮资源处理，不能只扩大计划缓存。

**2026-11-15 结果构建与 Blob 首轮复用批次**：针对最新日志确认的 `execution-plan-hit` 但 `workflow-snapshots` 仍约37ms，已在 `ExecutionWorkflowPlan` 缓存已验证的 `ExecutionWorkflowSnapshot`。配置对象、节点身份和启用状态未变化时，运行轮次复用节点/依赖/控制边快照，只通过轻量 `WithDocumentRevision` 绑定当前运行代次；配置实例变化时才重新签发节点快照。运行变量、宿主输入、图像租约和结果仍每轮重新捕获，未跨轮复用。`SunEyeVision.Execution.Tests` 16/16 通过。全量 Release 构建 0 错误；最终载荷 SHA-256 `1642B5413DA39859E2482CA484BA72729327BD1844051DC00F70C069E8F3443E`；`SunEyeVision.Application.dll` `9686A1A593B896395FA39631B10BC9017F1C1F8B2F3F8DA6DEDE954060589993`；`SunEyeVision.Execution.dll` `C40E5E57044887DE4BBFF9BF9E373F6618396218AC7C02F483EF74F057FA679E`；`SunEyeVision.UI.dll` `EED974DF6DCF280713AB64F02E8281A76F643B4090172344E566C5DDD6370903`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。待用户用该 Release 复测，重点观察后续 `workflow-snapshots` 是否明显下降以及参数/ROI修改后计划和快照是否按配置实例正确失效。

**2026-11-15 执行快照命中/结果构建细分探针 Release 批次**：增加 `execution-snapshot-hit:<workflowId>` / `execution-snapshot-rebuild:<workflowId>` 明确标记；`workflow-engine` 新增 `result-build-start`、`result-fallback-snapshots`、`result-display-source-begin/complete`、`result-aggregate-created`、`result-declared-capture`；Blob 新增灰度域、ROI/屏蔽域、候选数、保留数和结果投影阶段。全量 Release 构建在关闭 MSBuild 服务后成功，0 错误；`SunEyeVision.Execution.Tests` 16/16 通过。最终载荷 SHA-256 `DE2D953A2D13AB6149678A17C0C1ACBCF5588182FE8D5E473DCCBC90CA896BEB`；`SunEyeVision.Application.dll` `2F3FDFF69A8081D2A25BC1587D5BB3C0DD8556D5FBD8D4F8B7869476A06B0E46`；`SunEyeVision.Execution.dll` `820D284B47148DED6823B92280AACA6F10841AC4DF2B0272C01D71190C02DEE3`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `36F38F7706F0B56EB6C474B9B7D8D883A9DFA1191934BB529C0F12F7D010F536`；`SunEyeVision.UI.dll` `EED974DF6DCF280713AB64F02E8281A76F643B4090172344E566C5DDD6370903`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。待人工复测，重点读取新阶段名确认首轮快照是 hit 还是 rebuild，并判断 engine-build-result 内显示来源捕获、结果聚合和声明输出保留的具体冷段。

**2026-11-15 最新执行快照/结果细分日志分析**：日志 `SunEyeVision_205325.txt` 已明确确认：预热批次 `execution-snapshot-rebuild`，首次正式执行和后续执行均为 `execution-snapshot-hit`；首次命中阶段耗时26ms但实际 `workflow-snapshots=1ms`，后续命中阶段约6ms且 `workflow-snapshots=0ms`，说明工作流快照复用已生效，首次剩余成本在命中前的当前声明/视图校验，不在快照构造。引擎新增细分还推翻了“engine-build-result=大结果构建”的旧判断：首轮 `result-build-start=152ms`，随后 `result-fallback-snapshots=1ms`、显示来源捕获0–1ms、结果聚合2ms、声明输出保留1ms，结果构建本身约4–5ms；其余时间发生在工具调度/执行完成前。首轮正式执行从 batch-capture 到 result-publish 约192ms，后续约68ms、43ms；相比上一版约210ms首轮有约18ms改善，但主要来自 Blob 首轮波动（本轮 Blob 99ms，上一轮117ms），不能把全部改善归因于快照复用。Blob 新探针显示首轮候选45、保留45，灰度统计11ms、逐Blob区域捕获19ms；后续灰度统计降至1–10ms、区域捕获5–13ms。当前下一刀应沿 Blob 首轮资源/灰度统计和调度等待继续定位，结果显示来源捕获已经不是主瓶颈。

**2026-11-15 生命周期有界计数探针 Release 批次**：新增 `RuntimeLifecycleProbe`，记录 batches、engines、sessions、resultOwners 的 active 数量；批次释放时输出 `[LifecycleProbe] batch-disposed ...`，用于少量重复执行确认运行态归还而不需要大方案压力。全量 Release 构建成功，0 错误；`SunEyeVision.Execution.Tests` 16/16 通过。最终载荷 SHA-256 `6FACB14CD06FB789413601377AC00CE5D1F0C737D6CA204E58A0422B452ADD39`；`SunEyeVision.Application.dll` `4E9ACBDA66A2284ABC182816DD42DE5DBF0AEB2E39F1ED48A34796EB22E04F1C`；`SunEyeVision.Execution.dll` `71C9EAB7C975ABC2A60F29339E2255DB527B9542000530BA195A5914E9490E78`；`SunEyeVision.UI.dll` `40DE5ABD46257E5BC30A8083DE0E65955DBE6A4775E9D169C61940B147F3BF62`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。待人工用少量重复执行、取消和关闭验收生命周期计数是否回到 `batches=0/engines=0/sessions=0/resultOwners=0`。

**2026-11-15 生命周期计数探针人工复测结果**：最新日志 `SunEyeVision_210802.txt` 中每次 `batch-disposed` 都是 `batches=0/engines=0/sessions=0/resultOwners=0`（预热批次）或 `batches=0/engines=0/sessions=0/resultOwners=5`（正式结果已发布后），执行批次、引擎和工具 Session 没有递增；5 个 result owner 在三轮执行中保持稳定，说明当前结果发布租约被工作区持有，没有按轮累积。首轮正式执行约203ms，后续约66ms、53ms；首轮 Blob 106ms，结果构建分段仍约4ms。当前日志在用户关闭前没有记录方案关闭/主窗口退出后的最终 `resultOwners=0`，因此只能确认运行态有界和结果发布数量稳定，不能把进程退出当作关闭释放证据；下一步需在方案/主窗口最终释放路径补一次 LifecycleProbe 快照，验证关闭态归零。

**2026-11-15 工具执行最小负载/Blob声明缓存 Release 批次**：生命周期复测确认 batches/engines/sessions 每轮归零，resultOwners 稳定持有当前正式结果。按安全边界把 Blob 工具声明 schema 改为进程级只读缓存，避免每次 `ExecuteAsync` 重建 `BlobTool.Describe()`；HALCON 执行作用域、临时图像/区域和输出资源仍按本轮创建与释放，因为作用域包含原线程和线程局部系统状态，不能跨轮持有。全量 Release 构建 0 错误，`SunEyeVision.Execution.Tests` 16/16 通过。最终载荷 SHA-256 `349EC08401B38669A195B8CC2BC66AEC0A0B1B170FC30B01C838F2B65393797B`；`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `BC1A019769151F2D85A5513512EB53477FAD70E8D6191B032DE6ABF78E440A58`；`SunEyeVision.Application.dll` `4E9ACBDA66A2284ABC182816DD42DE5DBF0AEB2E39F1ED48A34796EB22E04F1C`；`SunEyeVision.UI.dll` `40DE5ABD46257E5BC30A8083DE0E65955DBE6A4775E9D169C61940B147F3BF62`；`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。后续人工验收继续观察生命周期计数和 Blob 首轮阶段，不能把线程绑定的 HALCON 作用域强行跨轮复用。

**2026-11-15 最新生命周期复测**：日志 `SunEyeVision_212413.txt` 显示预热批次释放后 `batches=0/engines=0/sessions=0/resultOwners=0`；正式执行三轮释放后均为 `batches=0/engines=0/sessions=0/resultOwners=5`，运行态三类计数稳定归零，5 个 owner 是当前正式结果发布持有量，没有按轮递增。首次执行仍未改善，首轮约207ms，后续约约67ms、54ms；首轮 Blob 128ms（逐 Blob 区域捕获28ms、灰度统计10ms、候选提取19ms），workflow-engine 207ms，其中结果构建前等待191ms；后续 Blob 45/37ms、引擎58/51ms。执行计划和快照均命中，首轮 `workflow-snapshots=1ms`，但命中校验仍26ms。关闭后日志没有出现 `resultOwners=0` 的最终释放快照，故只能确认运行态有界和结果 owner 稳定，不能确认关闭路径最终归零；首轮冷启动问题仍在 Blob 首轮处理和工具调度，不是执行计划/结果构建。

**2026-11-15 Blob首轮算子预热与退出资源计数**：最新日志 `SunEyeVision_212413.txt` 将首轮差异定位到 Blob `candidates-extract=19ms`、逐 Blob 区域捕获约28ms，后续分别为4–9ms和1–2ms；`halcon-scope-init=0`，执行计划/快照均已命中。按该证据扩展启动预热，实际触发 `Connection/SelectObj/AreaCenter/SmallestRectangle/Circularity/Rectangularity/Contlength/GrayHisto/Intensity` 算子，避免把首轮冷路径留给正式执行；应用退出在方案、插件和结果消费者释放后记录 `application-exit` 最终计数。全量 Release 构建成功，0错误、既有警告；载荷封签 `8FE41912AFE2EC45783014743770A5EDD0E13C5437884B860420FDB1B0B01188`。关键文件：`SunEyeVision.UI.dll` `5565691E21A2238A19F1F63760C6B236FD432708577219F76B7DF014240A3164`，`SunEyeVision.HalAdapter.Runtime.dll` `D1EDB4F2E037DBE61B60D258BA70CF9D820A8C4EF76EFDDB36BE77348069477A`，`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `BC1A019769151F2D85A5513512EB53477FAD70E8D6191B032DE6ABF78E440A58`，`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。执行测试16/16通过；Blob定向测试未能作为本批通过依据，因其产品运行时SHA基线仍指向旧载荷，失败为测试宿主装配/封签不匹配，不是断言失败。待用户用新Release复测首轮Blob分段及关闭日志中的`application-exit`计数。

**2026-11-15 移除无证据算子预热**：依据用户要求撤销 Blob `Connection/SelectObj/GrayHisto/Intensity` 等算子级预热，同时移除 ImageSource 图像管线模拟预热；工具启动钩子只保留 `HalconRuntimeWarmup.EnsureInitialized()` 的公共进程上下文初始化。退出阶段 `application-exit` 生命周期计数保留。全量 Release 构建成功，0错误、5654个既有警告；载荷封签 `E5F93DF33EF10EC9CB18D4DBE8334437146D192F7F1129B33F1953742CADF4C7`。关键文件：`SunEyeVision.HalAdapter.Runtime.dll` `0FC0827FD80E4F83B180CDAD6884AD01215FD21867C10EC1C6952F2EB15C08EB`，`SunEyeVision.Tool.BlobAnalysis.Runtime.dll` `D06C56D4EEF67AA08F63D3469FE858277AEF8D16E7A239BC68FF370E6E64425E`，`SunEyeVision.Tool.ImageSource.Runtime.dll` `4CD1335BFC9F566145C7E98E6D4A08E1873EFDD0AA6F9A5E1D184D985C8017CD`，`SunEyeVision.UI.dll` `5565691E21A2238A19F1F63760C6B236FD432708577219F76B7DF014240A3164`，`SunEyeVision.UI.exe` `DA129E29617D55F8F59ACB4D56148DB0511BAE14564481EF5F74E1269439CBCB`。下一次性能复测应比较启动 `Runtime.Warmup` 是否下降，以及首次 Blob 差异是否仍存在；若仍存在，继续测量托管资源转换和结果所有权链，不再增加算子预热。

**2026-11-15 启动/执行链完整差异优化方案文档**：新增现行文档 `docs/09-现行文档/VM与SunEyeVision启动执行链差异对比.html`，按 `exe→启动页→欢迎页→方案加载→执行→结果发布→关闭` 完整对照 VM 与本方链路。文档以红色 P0 标出独立 Server/ModuleProxy 与单进程 UI、常驻模块与执行批次、请求式执行与每轮快照调度三项根本差异；橙色 P1 标出方案加载、结果资源、ROI/图像转换和关闭释放差异；同时给出 A0-A5 分阶段优化方案、身份/失效合同、资源边界、验收矩阵和禁止事项。当前方案明确：只保留框架公共初始化，不恢复算子/图像/ROI模拟预热，不以删除执行隔离换性能。

**2026-11-15 执行核心与拓扑驱动对比补齐**：在 `VM与SunEyeVision启动执行链差异对比.html` 中新增目标 Run 细化链、执行核心逐步对比表和拓扑驱动执行引擎对比。明确拓扑计划编译、依赖表示、就绪队列、分支并行、取消传播、结果提交和拓扑失效规则必须作为独立核心合同对标；目标是结构变化时编译一次，Run 只复制轻量状态并处理真实输入。文档同步补充软件核心设计合同（进程边界、生命周期、所有权、身份、并发、插件、持久化、结果、错误恢复和关闭）。

**2026-11-15 重构目标升级为产品级架构对齐**：用户明确：本次重构的原因是已经获得 VM 产品级架构证据，目标不是在旧自研单进程实现上做最小改动优化，而是按产品级职责边界重建最合理的启动、拓扑执行、模块常驻、资源工作区、结果发布和关闭生命周期。后续局部缓存/预热/短路只能作为迁移期间临时措施，最终路线以独立常驻执行服务、编译期拓扑计划、常驻模块/工具工作区、请求式 Run、结果订阅和统一释放为准；旧自研实现只保留为行为基线，不再作为最终架构约束。

**2026-11-15 ExecutionServer重构实施前置调整**：用户要求先完成 VM 原厂执行引擎完整逆向，再实施 ExecutionServer、常驻拓扑计划、RunRequest 和结果订阅。当前已建立产品级目标设计文档，但尚未开始执行代码改造；新增取证门槛覆盖 Server/ModuleProxy 启动与关闭、Solution/Procedure/Module/Port/Connection 生命周期、拓扑编译与调度、输入/ROI/变量传播、模块复用、取消异常、结果提交/订阅和最终释放。取证收口前禁止根据本方队列反推 VM 内部、禁止先造接口再补语义、禁止长期保留新旧双轨生产执行链。

**2026-11-15 原厂执行引擎取证批次（当前有效）**：用户要求先不实施，先完整逆向 VM 执行引擎，补齐拓扑调度、数据传播、结果提交和关闭生命周期证据，之后由用户审核方案再决定是否实施 ExecutionServer。当前只读复用既有反编译和证据页：已确认流程 Run 入口、全局 Run 到 native 边界、流程级线程/CPU 策略、可重入与模块结果缓冲合同、ExecuteCount 结果回流、RenderDataCenter 三仓交换及显示层关闭清理。尚未确认 VM 私有拓扑编译/调度队列、端口数据传输所有权、结果聚合提交、取消异常和 Server/Proxy/模块退役顺序；不得据本方 GraphExecutor 反推。对应目标设计已补“执行前原厂逆向门槛”和“当前已闭合证据/未闭合项”，本批不改产品代码、不构建、不启动 ExecutionServer。

**2026-11-15 调度模型前提修正**：用户指出不能先入为主地把原厂执行引擎假定为拓扑调度。本批取证将“拓扑/数据流驱动、事件/触发驱动、过程脚本驱动、服务内部混合模型”并列为待证伪候选；现有证据只确认流程/模块/端口/触发配置、Run 入口、ServerSDK 运行策略与异步结果回流，未确认私有调度模型。设计文档已补候选模型矩阵和取证顺序；在模型证据闭合前不定稿 `TopologyScheduler`，不实施 ExecutionServer。

**2026-11-15 原厂调度证据增量**：继续静态取证发现，全局脚本 `StartOnce` 将等待编译/初始化与 `CodeRun` 放入 `Task.Run`，任务提交后立即返回；全局触发还存在 `Execute_Procedure`、`Execute_Module`、`Execute_Module_Action` 命令结构，光源触发把输入IO、边沿信号和目标流程写入模块参数。由此当前更稳妥的事实模型是“常驻服务 + 多种 Run/Trigger 入口 + 服务异步执行 + 分代结果回流”，不能把原厂内部直接定性为拓扑调度。设计文档已补最新判断；仍不实施，继续追 `CodeRun`/Server/Proxy/native 的内部调度、数据传播、结果提交和关闭调用闭包。

**2026-11-15 图像源/结果/显示联动取证增量**：补查原厂 `ImageListPanelBase`、`DisplayImageListDecorator`、`ModuleDisplayDecorator`、`ModuleResultView`。已确认图像源缩略图切换会写 `CurrentImagePath`、通过 `RunCheck` 调用异步 `VmProcedure.Run`，完成回调再刷新当前路径；模块显示图像选择会切换输出过滤器与 ValueChanged 订阅并同步 RenderDataCenter；结果回调按 `ExecuteCount` 更新选中图像、图形、数据列表和结果树；当前结果列表与历史结果查询分层，历史查询显式 Release。结果行选中到具体图形高亮、输入批次与 ExecuteCount 的最终闭包仍未确认。本批继续只读，不实施。

**2026-11-15 UI边界修正**：用户明确指出“模块结果”和“当前结果”是两个独立 UI 更新链，不能混用。已在执行引擎设计文档中拆分：模块结果负责图像/图形/文本/结果树/显示来源；当前结果负责结果数据区域的当前执行代次列表；历史结果另有分页查询和释放。后续取证必须分别追三条链及其与图像源缩略图、ExecuteCount、结果行选中和图形高亮的交叉关系；未闭合前不合并成统一 ResultStore 结论。

**2026-11-15 下方三 Tab 取证增量**：已补查 `ModuleResultView.xaml` 与代码。当前结果 Tab 绑定 `ModuleCurrentResultList`，切入时刷新当前数据，行选中进入 `m_ResultDataGrid_SelectionChanged`，按当前 `ExecuteCount` 重读显示对象、清图形、重绘并调用 `SetRectHighLight`；历史结果 Tab 绑定 `ModuleDisplayInfoList`，通过 `QueryModuleHistoryResult` 分页读取并显式 `ReleaseModuleHistoryResult`，清空还会调用 `ClearModuleHistoryResult`；帮助 Tab 绑定 `HelperContent`，按模块类型 `_HELP` 生成，模块/语言切换时刷新，不触发执行和结果查询。三者共用模块装饰器但更新入口、状态和清理边界独立，不能合并成统一清空逻辑或统一结果状态。本批仍不实施。

**2026-11-15 方案收口批次**：已将目标链路、目标 Run、图像源输入、模块结果视图、当前结果 Tab、历史结果 Tab、帮助 Tab、结果代次和关闭边界统一到同一份现行设计。执行内部采用中性 `ExecutionCoordinator`，不预设原厂拓扑；底层调度、数据传播和退役闭包仍按原厂证据绑定。当前文档已可供用户审核；本阶段不实施、不构建，等待方案审核后再进入重构。

**2026-11-15 现行文档合并完成**：已将启动执行链差异的现行内容整合进 `docs/09-现行文档/执行引擎产品级架构重构设计.html`，主方案现在统一包含原厂证据、VM/当前/目标对照、启动/方案/Run/结果/关闭链、四类 UI 投影和目标执行引擎。`VM与SunEyeVision启动执行链差异对比.html` 已改为摘要入口，仅导航到唯一现行方案并保留简要对照，不再维护第二套目标描述。主方案中的拓扑/结构计划已标为候选内部实现，不再作为原厂事实前提。本批无产品代码实施。

**2026-11-15 ExecutionServer 首批代码落地**：在基线提交 `66b577d21` 后开始重构。新增 `src/Execution/ExecutionServer.cs`、`ExecutionCoordinator.cs`、`SolutionRuntime.cs`、`ResultStore.cs`；`DocumentRunBatch` 的正式和连续运行统一通过 `RunRequest -> ExecutionCoordinator -> ExecutionServer` 接纳，`DocumentSession` 持有常驻 ExecutionServer/SolutionRuntime/ResultStore，并在方案关闭时退役。现有 WorkflowExecutionEngine 作为内部执行适配器保留，未改算法和 UI 结果语义。Application Release 构建通过，0 错误、9 个存量警告；用户负责实机测试。

**2026-11-15 ExecutionServer 首批重构落地**：在保持现有算法和 UI 行为不变的前提下，新增 `ExecutionServer`、`ExecutionCoordinator`、`RunRequest`、`SolutionRuntime` 与 `ResultStore<T>`，将正式运行和连续运行统一收口到 `RunRequest → ExecutionCoordinator → ExecutionServer`，方案会话统一持有执行服务、方案运行身份和结果存储，并在关闭时按执行服务→结果存储→方案运行身份顺序退役。执行服务采用受保护的接纳门，关闭后不再接受新请求，等待活动请求归零。全量 Release 构建成功，0 个错误、453 个存量警告，载荷封签 `940C441B424B434B880B4B1E75DD3AE71818F8A635E29D2C9F78CA97723DB959`。本批未运行自动化测试，待用户直接验收 Release 运行与关闭生命周期；未宣称内部旧调度器已全部替换，当前由新执行服务统一托管入口。

**2026-11-15 E1 ExecutionServer 生命周期探针**：为 `ExecutionServer` 接入 `RuntimeLifecycleProbe`，新增 `executionServers`、`executionRequests` 计数，并记录 `execution-server-created`、`run-request-accepted`、`run-request-completed`、`run-request-cancelled`、`run-request-faulted`、`run-request-retired`、`execution-server-retired`。执行服务接纳门与退役等待保持不变；批次在使用临时服务的构造路径上补齐所有权释放，方案共享服务仍由 `DocumentSession` 统一退役。全量 Release 构建成功，0 个错误、483 个存量警告；未运行自动化测试，待用户人工验证正式/连续/取消/关闭路径，并检查日志计数归零。

**2026-11-15 E2 常驻执行计划身份接入**：将方案级结构计划身份接入 `ExecutionServer`：`DocumentSession.GetOrBuildExecutionPlan` 只有在服务端 resident plan key/generation 命中时才复用，首次或结构变更时发布新身份；方案目录退役、图结构提交和会话关闭会主动失效该身份。现有 `RuntimeDirectorySnapshot` 已负责定义缓存和可复用工具会话，本批未改变算法、工具会话实现或 UI。全量 Release 构建成功，0 个错误、453 个存量警告；载荷封签 `1BE41EBA1F49FC95720470F90B683D861E005B0FDBE0333E8A36302F5E40267B`。E2 尚未完全闭口，下一步需把模块会话命中/创建/退役证据与结果提交链继续接到服务级合同；未运行自动化测试。


**2026-11-16 E2 常驻计划、模块会话和结果提交收口**：为 `RuntimeDirectorySnapshot` 增加模块定义命中/创建、可复用会话命中/创建/释放/退役日志；`DocumentSession` 为每个节点维护当前结果版本，将执行结果提交到方案级 `ResultStore`，节点替换、结构失效、依赖结果退役和方案关闭同步移除旧版本。结果提交复用现有 `DeclaredOutputSnapshot`，未新增第二套结果模型。全量 Release 构建成功，0 个错误、250 个存量警告；载荷封签 `3669715009DEDEC348446E3CF7A312A432E63DFC72A8C93EC6084FAFA61208F2`。未运行自动化测试；后续仅需用桌面技能和日志探针验证首次/后续 Run、连续运行、取消和关闭，再进入 E3 四类结果 UI 投影接线。

**2026-11-16 E3 结果版本事件合同启动**：`ResultStore<T>` 新增 Published/Retired 事件，当前节点结果版本发布和退役可以被结果 UI 投影订阅；不改变现有模块结果、当前结果、历史结果和帮助控件的视觉行为。Agent Desktop 已启动 Release 并完成真实 E2 Run：resident plan 发布、RunRequest 接纳/完成/退役、ImageLoad/BlobAnalysis 会话创建/释放、两个结果版本发布、batch/resultOwner 计数归零均有日志。全量 Release 构建成功，0 个错误、5753 个存量警告；载荷封签 `5ADC2BC2271332388A83FC09DE9ED51D578B8DD1F3779C9B5056F40C03412666`。下一步继续把四类结果 UI 投影接到该版本事件，不运行自动化测试。

**2026-11-16 Agent Desktop 测试闭环脚本固化与 E3 版本事件接线**：新增 `scripts/verification/Invoke-SunEyeVisionAgentDesktop.ps1`，固化 Release 启动、打开最近方案、Run、RunTwice、Snapshot、ReadLog、Close 和 E2Smoke 命令流程；后续测试按动作参数调用，不再重复拼接 Agent Desktop 命令。E3 新增 `PublishedResultVersion` 和 `DocumentSession.ResultVersionAvailable`，结果工作区可按结果版本事件刷新，画布继续使用运行状态事件。UI Release 构建成功，0 个错误、359 个存量警告；载荷封签 `BFCCD0941997CA85C14E5D8358481FC77BD621DB678931EAC1AF1E371446F812`。尚未完成四类 UI 的最终统一消费和 E4/E5，目标保持 active。

**2026-11-16 E3 Agent Desktop 固定脚本烟测**：修正 `Invoke-SunEyeVisionAgentDesktop.ps1` 的启动路径和无窗口检测，并用脚本完成最新 Release 的启动→打开方案→两次 Run→关闭。日志证明首次批捕获 62ms、后续 10ms；首次 workflow-engine 209ms、后续 44ms；首次/后续结果版本均发布并旧版本退役；模块会话首次 created、后续 hit；两次请求均 completed/retired，batch 和 executionRequests 归零。ResultVersion 事件代码已编译通过。全量 Release 构建成功，0 错误、5753 个存量警告；载荷封签 `83AEC8B3EA0F1047AAD16C9235DD1770F06228D0D839A9AD023674E8CE63E15D`。E3 仍需完成四类 UI 的最终独立消费审查，随后进入 E4。

**2026-11-16 Agent Desktop 验证脚本迭代与 E4 收口证据**：按测试需要更新 `scripts/verification/Invoke-SunEyeVisionAgentDesktop.ps1`。脚本现在动态选择最新生命周期日志，递归识别 `ContinuousRunBtn`，只在观察到“停止”态后发送停止点击，并提供 `Assert-LifecycleDrained` 检查 `executionRequests=0`、`resultOwners=0` 和 `execution-server-retired/application-exit`。使用 Agent Desktop 实际连续运行后关闭 Release，最新日志确认连续请求可持续生成，关闭时 `resident-plan-invalidated`、`execution-server-retired`、`resultOwners=0`、`executionRequests=0`、`application-exit` 均出现；进程已由 `close-app` 验证退出。脚本语法检查通过。该批只改验证脚本，没有改变产品运行语义；E4 的取消按钮人工路径和 E3 四类结果 UI 全量验收仍开放。

**2026-11-16 E3 结果选择高亮边界修正**：当前结果表与图形高亮曾把 `IResultPublication.IsCurrent()` 当作唯一有效条件；编辑后执行域退役会让该值为 false，但当前工作区仍持有最近一次未退役完整结果，导致行选择不能驱动图形高亮。新增 `MainWindowViewModel.CanDisplayResultPublication`，高亮链按“未退役且执行域当前，或当前工作区仍持有完整保留快照”判断；不改变正式执行、节点/方案切换的清理边界。全量 Release 构建成功，0 错误，载荷 `2C65FB0C3A30B73BFD3BFC602F4A540AE1608BE7DC05779E3DCF1B552256ABB0`。仍需 Agent Desktop 实机确认编辑后当前结果行选择与图形高亮联动。

**2026-11-16 VM 快照语义补证**：针对“原厂是否也大量使用快照”的疑问，补查 `docs/architecture-recovery/result-store-ownership.resources/VMControls.WPF.DataForUI.RenderDataCenter.decompiled.cs`、`render-bridge.resources/VMControls.WPF.RenderControlBase.decompiled.cs` 及对应 CIL 证据。原件身份为 `VMControls.WPF.dll` SHA256 `99ffa401c8784472913ad43813d68a8a5577890c00e149506e3e5664fa61c97d`、MVID `d144317f-3992-4275-9b0f-13fb6163e07d`。已确认 VM 的显示结果层是 display/buffer/spare 三仓、`BeforeExchange/ExchangeBuffer` 和 `ExecuteCount` 分代筛选；回调任务稍后读取当前仓，不是在提交时复制一张完整 `ImageData` 快照。`DeInit` 清三仓和计数，但不等同所有外部消费者/迟到任务已排空。结论：VM 确有分代缓冲和结果代次，但没有证据证明它采用我们当前多层 `ExecutionWorkflowSnapshot`/`DocumentRunBatch`/显示快照的同构设计；Server 内部输入冻结和调度细节仍标“未确认”，不能据 UI 证据推断。

**2026-11-16 E4 活动请求取消验证**：验证脚本新增 `E4Cancel`。为避免普通停止落在连续轮次之间，脚本通过 Agent Desktop 异步发出启动点击，再立即发送停止点击；最新 Release 日志已观察到 `run-request-cancelled`，随后 `run-request-retired`、`batch-disposed`，关闭时 `execution-server-retired`、`resultOwners=0`、`executionRequests=0`、`application-exit` 均成立。之前未命中取消事件的停止记录保留为“轮次间停止”，不冒充活动请求取消证据。

**2026-11-16 E4 取消后续运行复验**：在 Agent Desktop 通过 `E4Cancel` 命中活动连续请求取消后，沿同一方案再次执行单次 Run；日志显示 `mode=formal` 请求完成、`流程1：Success`、`batch-disposed`，随后关闭 Release 资源归零。说明取消只终止当前请求，不破坏常驻方案计划、模块会话和下一次 Run。脚本同步修正：E4 入口在主界面不重复打开最近方案；异步启动点击用于稳定命中活动取消；最近日志选择保持动态。

**2026-11-16 E3 结果 UI Tab Agent Desktop 验证**：验证脚本新增 `E3UiSmoke`，启动/打开方案/执行后自动处理图像源区域折叠，并通过 Agent Desktop 逐一定位并切换当前结果、历史结果、帮助三个真实 Tab；动作成功，随后关闭 Release 资源归零。脚本同时增加启动等待、窗口 HWND 竞态重取和主界面重复打开方案保护。该证据确认三类 UI 入口存在且切换不触发执行请求；当前结果行到具体图形高亮仍需在可见数据行上补一次动作证据。


**2026-11-16 E3 当前结果行高亮收口**：沿 Agent Desktop 控件树定位 `CurrentResultTab`、当前结果 DataGrid 行和物理边界，补齐结果表、结果投影和结果树内部残留的 `publication.IsCurrent()` 门禁，统一按“未退役且仍由当前工作区持有”继续投影。用户确认当前结果行可以驱动图形高亮；截图颜色不作为唯一判据。全量 Release 构建通过，`SunEyeVision.UI.exe` SHA-256 `6196E909A5EA34147B7D2A6A080982499FF8399CF703F11EC21C1581E8A4CDBB`。新增原厂独立对照脚本 `scripts/verification/Invoke-VisionMasterAgentDesktop.ps1`，仅在必要 UI 取证时启动 VisionMaster。

**2026-11-16 E4 方案切换动作失败证据**：在连续请求活动期间通过 Agent Desktop 进入“打开方案”，没有形成 `solution-open`、`resident-plan-invalidated` 和新会话安装闭包；窗口随后退出，日志出现 `application-exit batches=1/engines=0/resultOwners=6/executionRequests=0`，并记录“流程或脚本仍在运行或清理，方案保持打开”。该动作按失败证据保留，E4 方案切换和应用退出排空不能计作通过。期间弹出的 `D:\MyWork\Rea逆向\tests\Blob.NativeRuntimeChecks.exe` 属于外部测试进程，已结束，不纳入 SunEyeVision 产品结论。

**2026-11-16 Release 启动资源流污染定位与修复**：用户实机出现 `SunEyeVision.UI.exe` / `dotnet.exe` 的 `0xe0434352` 弹窗。直接启动 Release 捕获到 `App.xaml -> ResourceDictionary.Source -> EndOfStreamException`；对程序集资源逐项用 `ResourceReader` 取证，确认 `SunEyeVision.Plugin.Wpf.g.resources` 为 12 字节损坏头，其他 UI/工具资源正常。根因是共享 `build_temp\obj\SunEyeVision.Plugin.Wpf*` 中间产物污染并被 `--no-restore` 增量复用，不是执行链逻辑。清理 Plugin.Wpf/UI 生成缓存、执行一次还原后按正式全量 Release 命令重建成功（0 错误，5685 个存量警告）；新资源集长度 5,300,328 字节、2048 项可读，`SunEyeVision.UI.dll` 直接启动存活验证通过。载荷：`9A912FF010BFB173021428796995DFBE1966A635545859F53A0CCCE654C65EDC`；UI exe：`6196E909A5EA34147B7D2A6A080982499FF8399CF703F11EC21C1581E8A4CDBB`；Plugin.Wpf：`1EDD6F4581A9A1F8122D1274AEA9F33EDF5374436B0BA7786BE3F867D120ECEB`。本次不把测试进程异常计入产品故障；后续 Release 构建需先确认共享 WPF 资源头完整，禁止把测试输出回写正式目录。

**2026-11-16 Agent Desktop 验证根隔离**：正式 Release 资源修复后，Agent Desktop 直接启动仍弹“启动失败”；取证确认当前受控宿主身份 `CodexSandboxOffline` 对 `%LocalAppData%\SunEyeVision` 仅有读取权限，`ApplicationDataPaths.EnsureDirectories` 的写入探针因此失败，属于验证宿主权限边界，不是产品载荷损坏。创建带 `.suneye-startup-test` 标记的 `.agent-desktop-startup-test` 隔离根并复制 `11111.solution` 后，同一 Release 主窗口正常出现。`Invoke-SunEyeVisionAgentDesktop.ps1` 已默认使用隔离根；Agent Desktop `launch` 在该宿主因 lease ACL 被拒时回退为直接启动隔离进程，后续 UI 操作仍全部通过 Agent Desktop。生产 AppData 未再被测试写入。

**2026-11-16 E4 关闭入口修正**：验证脚本 `Close` 不再调用 Agent Desktop `close-app`（该入口可能直接触发进程退出而绕过 WPF `MainWindow.OnClosing`），改为从真实控件树定位主窗口 `✕` 关闭按钮并点击，再检查生命周期归零。脚本已通过 Windows PowerShell 解析/ReadLog 启动验证；待 Agent Desktop lease ACL 修复后复跑真实关闭链。

**2026-11-16 Agent Desktop lease ACL 复核**：通过 session start、快照和状态目录 DACL 逐项取证，确认 snapshot 可恢复，但 click 在创建动作 lease 时仍报 `lease_directory_untrusted`；更换 session、隔离根和 Administrator 状态根均未能完成 click。该阻断属于 Agent Desktop 宿主权限/lease 实现，不是 SunEyeVision 运行时；验证脚本和产品代码保持可构建状态，暂不把它误判为 E4 失败。
**2026-11-16 Agent Desktop 官方用法校准**：按 `vercel-labs/agent-desktop` 官方 README 与本机 1.3.1 内置 `desktop`/`agent-desktop-windows` 技能核对验证脚本。官方要求每次交互遵循“最新 snapshot → 使用该 snapshot 的完整 ref → 重新读取验证”，Windows 语义 UIA 默认 headless，连续多步应复用同一 session；交互租约是排他的，不能通过并发进程或放宽 ACL 绕过。已修正 `scripts/verification/Invoke-SunEyeVisionAgentDesktop.ps1`：每次脚本运行创建并结束独立 session，连续运行首击与停止点击改为同一进程串行，不再用 `Start-Process` 异步抢租约；等待改用 Agent Desktop `wait`。`agent-desktop.cmd` 改用新建且当前用户拥有的 `.agent-desktop-state-clean`，旧状态目录不动。验证：新目录 `status`/`snapshot` 成功，当前 SunEyeVision 窗口可被 UIA 完整读取；语义 `click` 仍被 Agent Desktop 自身拒绝为 `lease_directory_untrusted`，原因是本机 CodexSandboxOffline 令牌不含 Administrators，而二进制为 session/liveness 子目录写入 SYSTEM/Administrators ACL。该失败属于工具交互租约环境，不归因于产品 UI；按官方规则不通过改 ACL、并发进程或坐标输入绕过，待使用同一用户新建无外部租约的 Agent Desktop 运行环境后继续 E3/E4 交互验证。

**2026-11-16 E3/E5 静态收口复核**：按当前源码重新核对四类结果 UI：`ResultWorkspaceShell` 分开持有模块结果、当前结果、历史集合和帮助内容；`ImageResultControl` 分别绑定 `SelectedNodeDataAnalysisControl`、`ResultHistory`、`ResultHelpContent` 与 `ResultTreeRoots`；`MainWindowDocumentHistory` 独立订阅 `ResultVersionAvailable`/历史刷新。当前结果行高亮仍由独立选择事件驱动，不触发执行。同步修订 `docs/09-现行文档/执行引擎产品级架构重构设计.html` 中过期的“代码未开始”和旧 Release 封签表述。Release 目录时间戳与当前源码一致（UI 载荷 `SunEyeVision.UI.exe=6196E909A5EA34147B7D2A6A080982499FF8399CF703F11EC21C1581E8A4CDBB`、`SunEyeVision.UI.dll=98BDEBDC75C884347A32AC112F5DAE236D6B25913A01E060741587DA5921DD05`），未因脚本/文档改动重复构建；E3/E4 仍只缺 Agent Desktop click 的实机证据。

**2026-11-16 Agent Desktop 租约复核**：按官方规则执行 `session gc --ended`，删除全部结束 session 后重新创建全新 `.agent-desktop-state-clean`，仍能 `status`/`snapshot`，但首次语义 `click` 仍返回 `lease_directory_untrusted (os error 5)`。因此问题不是旧 session、并发 Agent Desktop 进程或过期 ref；当前令牌环境在二进制创建 session/liveness ACL 后仍不能取得交互租约。未采用官方明确禁止的 ACL 放宽、坐标点击或并发绕过。

**2026-11-16 E4 生命周期日志复核**：直接分析最新已有 Release 日志 `SunEyeVision_020045.txt` 与 `SunEyeVision_020320.txt`。02:02:50 的完整关闭链已形成强证据：连续运行期间多次 `resident-plan-hit`、`module-session-hit`、独立 `run-request` 和结果版本递增；关闭阶段依次记录 `resident-plan-invalidated`、`execution-server-retired`、`module-sessions-retired count=2`，最终 `application-exit batches=0/engines=0/sessions=0/resultOwners=0/executionServers=0/executionRequests=0`。02:06:29 的 `application-exit ... resultOwners=6` 是旧关闭入口/旧脚本样本，且日志没有当前主窗口 `solution-transition-*` 诊断，单独登记为历史失败样本，不覆盖已验证的主窗口排空链；当前脚本已改为点击主窗口关闭按钮并串行等待。E4 的代码闭包与成功日志证据已具备，剩余仅需租约恢复后的 Agent Desktop 主窗口实机复验。

**2026-11-16 Release 临时探针清理批次**：删除 `RuntimeExecutionTimingProbe` 及其在执行引擎、批捕获、工具调用、方案打开和结果发布路径中的生产调用；保留 `RuntimeLifecycleProbe` 计数和阈值型 `PerfProbe`。源码检索已确认不再存在 `RuntimeExecutionTimingProbe`、`ExecutionTimingProbe`、`ExecTimingProbe` 或 `TEMP-PROBE`。重建第一次已进入编译并确认无探针相关 C# 错误，但正式 Release 复制阶段被正在运行的 `SunEyeVision.UI.exe` 锁住 `SunEyeVision.Plugin.Runtime.dll`；Agent Desktop `close-app` 同样被当前租约阻断，未强制终止用户进程。待程序正常关闭后重跑全量 Release，并重新计算载荷封签。

**2026-11-16 可见桌面启动约束**：用户明确要求启动时窗口必须出现在其桌面，不得使用隐藏后台进程。验证脚本的 Release `launch` 已显式加入 `--headed --activate`；`--headed` 仅用于用户授权的可见启动，不作为语义点击失败的绕过手段。

**2026-11-16 正式 Release 与临时探针清理收口**：在确认 SunEyeVision.UI.exe 已退出后，按正式全量命令重新构建 Release，结果为 0 个错误、5753 个存量警告。已删除 `RuntimeExecutionTimingProbe` 及全部生产调用，源码中仅保留生命周期计数和阈值型 `PerfProbe`；未启动后台进程，也未用隐藏进程替代可见桌面验收。最新载荷哈希：UI.exe `6196E909A5EA34147B7D2A6A080982499FF8399CF703F11EC21C1581E8A4CDBB`；UI.dll `3E52C8C1096A6A8E829357F0704AB79C41F7DB4571F0FD76207FBAF0EC348E4F`；Plugin.Runtime `32D3D58FCCF0D5E41B7157F10651F34B7AD53DBFAAC3BEAF2EEC05ECF9006901`；ImageSource.Runtime `7C67874A52567ADCCA5A2FBA83BA349283650E6E5EBFE57F2C7532291905A5C0`；BlobAnalysis.Runtime `B2E6D3634CA8C8E4C10E1E26F9282DD0689404EF2A4975BA840573E926ECAEE7`。后续只剩 Agent Desktop 交互租约恢复后的可见桌面 E3/E4 人工复验，启动必须使用 `--headed --activate`。

**2026-11-16 可见桌面启动回退移除**：用户未看到由受控会话启动的窗口；进程取证显示它曾位于 Session 1 console，但无法证明与用户当前物理桌面一致。验证脚本已移除 `--headed` 启动失败后的 `Start-Process` 回退；现在租约失败会直接报错，不再创建归属不明的实例或后台进程。用户随后确认已关闭该进程，当前无 `SunEyeVision.UI` 运行实例。脚本语法检查通过。

**2026-11-16 Agent Desktop 用户桌面租约修正**：确认租约错误来自 CodexSandboxOffline 与 Administrator 交互桌面身份不一致，且包装脚本原先硬编码工作区状态目录。`.tools/agent-desktop/agent-desktop.cmd` 已改为：只有调用方显式设置 `AGENT_DESKTOP_HOME` 时才使用指定目录，否则使用当前交互用户的 `%LOCALAPPDATA%\SunEyeVision\agent-desktop-state`。不修改 ACL、不做隐藏启动或第二进程回退；用户桌面启动应在同一 Administrator 终端中完成 session gc、headed launch 和后续 UI 操作。

**2026-11-16 Agent Desktop MCP 接入与可见 Release 运行**：Codex 宿主配置已恢复为合法 TOML，并注册官方 `agent-desktop mcp` server。当前对话已经加载 `mcp__agent_desktop`，`desktop_session_start` 成功，Release 通过 `desktop_launch` 在用户桌面显示，方案 `11111` 完成加载。首次可见 Run 已完成，日志确认 `run-request-completed`、`run-request-retired`，Perf 分段为 `batch-capture=116ms`、`batch-run=1899ms`、`result-publish=9ms`。结果 Tab、折叠和关闭点击仍等待自动审核的明确桌面点击授权。



**2026-11-16 原厂首次/后续执行冷启动取证（进行中，禁止据此改代码）**：按“原厂取证→完整链路分析→差异确认→实施设计→修改代码→按原厂链路验收”硬门槛，已从本机 VisionMaster 4.4.0 原件补取 ImageSource 原生 `ImageSourceModule.dll`（SHA-256 `f88a455061f3ae2d5e2ef157e7b3caafd94a2b95c39b5bba096802e405ebc6c8`）和 Blob 原生 `IMVSBlobFindModu.dll`（SHA-256 `4de695d8c3051045c9010739686f94c32d66a36ee486207fe749fb183fff4c9a`）的完整导出证据。ImageSource 导出静态确认 `CreateModule/DestroyModule`、`Init/DeInit`、`MonitoringSubFolderInit/MonitoringSubFolderChanges`、`UpdateImageFilesListFromDir/GetImageFilesPathNameFromDir`、`ConvertFileImage/ConvertDataImage/ConvertPixelType/ConvertRgbToMono8`、`SetImageBuffNum`、`SetImageOutput/SetImageOutputMono8`、`StitchImageOutput`、`Process`、`ThridImageThreadProcess`；Blob 导出静态确认构造/析构、`Init/DeInit`、`Process/ProcessAlg/ProcessOutput`、`ResetMember`、`SetAlgToolSharedMemNodeNum`、`SetMultiThread`、`CreateModule/DestroyModule`。原厂 `LoadModuleData` 反编译证据进一步显示：先 `SetAllParamList`、清理旧图像文件、调用 `ClearImage`，解析 `DynamicInData/DynamicOutData/ImageNameList/SelectedImageList`，按附件目录写入模块数据文件并建立图像相关状态；`StitchImageOutput` 中存在首个匹配尺寸时的输出缓冲分配/清零、拼接复制、`SetImageOutput` 和后续再次分配/复制路径；`ProcSavemoduleFinishEvent` 会清理 0x104 个资源槽并清空保存状态。上述均为 shipped-artifact 静态证据，尚未证明运行时调用顺序或首次/后续差异。现有原厂对照记录仍明确“VM 动态运行耗时未取得”；当前没有可用 Ghidra/Hopper/IDA 分析提供程序，不能把导出表或反编译局部推成完整冷启动链。现阶段结论：原厂完整冷启动的运行时首轮/后续时序仍未闭合；解码预热是否属于原厂链路仍未知；本批未新增代码修改、未形成优化设计，下一步只补原厂受控运行/调用时序证据。



**2026-11-16 原厂动态首次/后续执行取证补充**：在原厂 VisionMaster 4.4.0 同一进程、同一 `C:\Users\Administrator\Desktop\test.sol` 方案中完成动态运行。`ModuleFrame.log/Modules.log` 直接记录 `VM_M_RunProcedureOnce → CProcedure::DoWork busy → Module 0 ImageSource → Module 1 IMVSBlobFindModu → CModuleRstDispatcher → CProcedure free/stopped`。首次（executeCount=1）ImageSource `module/process/algorithm=10.581/10.570/9.748ms`，Blob `8.877/8.875/7.532ms`，结果回调 `13.621ms`，流程总耗时 `20.193ms`；第二次（executeCount=2）ImageSource `4.724/4.722/4.707ms`，Blob `4.748/4.746/4.555ms`，流程总耗时 `9.621ms`；第三次（executeCount=3）ImageSource `4.179/4.177/4.159ms`，Blob `2.909/2.908/2.808ms`，流程总耗时 `7.184ms`。同一日志还确认该进程装载阶段 ImageSource/Blob `LoadModuleData=0.234/3.234ms`、方案数据加载 `4.463ms`、模块连接和4项图像输入订阅完成。证据文档：`docs/collaboration/evidence/VM4.4原厂动态执行链-20261116.html`。边界：该方案日志同时出现 `CameraId=-1`、`SubscribeFolderPath: Image file not exist, path=`，运行界面流程图像为0×0；因此本批已闭合原厂执行器/模块级动态顺序和冷/热耗时差异，但尚未闭合“有效本地图像已成功解码”时的 ImageSource 内部文件解码、帧复制、格式转换和输出资源创建分段。继续只做原厂有效图像输入动态取证，不改代码、不做优化设计。





**2026-11-16 目标驱动落地门槛更新**：围绕“有效本地图像 → ImageSource → Blob → 结果”的原厂链路，已完成历史有效会话的解码、MONO8 格式转换、数据取得、缩放、当前帧选择、ImageSource/Blob 成功、结果分发以及首次/后续完整流程分段耗时证据；ProcessID-5456 明确 executeCount=1 流程约 19.420ms，后续约 6.897/6.453/5.670/8.524/5.955ms。对原厂 UI、ModuleFrame、ModuleProxy、Modules 日志扩大检索后，未发现帧复制或输出资源创建的独立运行事件、计数器或时间戳；原生导出名仅能证明候选入口存在，不能证明动态顺序和耗时。故这两项登记为“原厂当前不可分段”，不得据此设计解码预热、缓存、删除或重排。下一步若继续补证，只能使用原厂调试/性能工具或原生函数级动态跟踪；否则先做同口径本方完整链路差异确认，再形成整体实施设计。当前仍不修改项目代码。

**2026-11-16 原厂函数级补证工具边界**：已定位原厂 `ImageSourceModule.dll`（`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Collection\ImageSourceModule\ImageSourceModule.dll`，SHA-256 `f88a455061f3ae2d5e2ef157e7b3caafd94a2b95c39b5bba096802e405ebc6c8`）。尝试用 REA 对 `Process`、`ConvertFileImage`、`SetImageOutput`、`SetImageOutputMono8`、`StitchImageOutput` 做函数级过程搜索，但当前 Windows 环境无可用 PE 分析提供器（Ghidra/IDA/Kuna 未配置或不支持该目标），因此没有新增静态调用关系证据；该工具限制不等于原厂不存在这些调用关系。动态日志仍只到模块级耗时，当前不修改代码。

**2026-11-16 原厂动态跟踪能力探测结果**：继续尝试通过 REA/Frida 对原厂进程记录 `Process`、`SetImageOutput`、帧复制等函数进入事件。REA `observe_native_calls` 仅支持当前平台的 Mach-O 目标，不能用于 Windows PE；REA Frida 设备枚举因缺少 `frida_binding.node` 失败，无法附加原厂进程。该结果已写入有效图像取证文档，证明当前工具环境不能提供函数级动态证据；不把工具缺失解释为原厂不存在调用，也不据此修改代码。

**2026-11-16 Windows 系统跟踪工具复核**：系统存在 `wpr.exe`、`wpa.exe`、`xperf.exe`，可采集 CPU/FileIO/DiskIO，但 `logman query providers` 未发现 VisionMaster/ImageSource/HALCON 专用 ETW provider；这些配置最多补充系统级文件 I/O，不能证明原厂 `SetImageOutput`、帧复制或输出资源创建的函数顺序。已启动并清理一次原厂进程验证可见性，未取得新的有效执行链，不修改代码。

**2026-11-16 Ghidra 配置复核**：用户已安装 Ghidra 12.2/JDK 25。当前工作区实际路径为 `E:\SunEyeVision_V2\.tools\ghidra\ghidra_12.2_PUBLIC` 与 `E:\SunEyeVision_V2\.tools\jdk25\jdk-25.0.5.0.2+1`，`analyzeHeadless.bat` 与 `java.exe` 均存在；此前用户消息中的 `E:\SunEyeVision_V2.tools` 少了目录名中的点。现有 REA 服务进程仍看不到 `GHIDRA_INSTALL_DIR/JAVA_HOME`，显然是在环境变量持久化前启动，需重启 REA/MCP 后才能打开 ImageSource PE 并继续函数级取证。代码仍未修改。

**2026-11-16 Ghidra 12.2 原厂 ImageSource/Blob 静态链补证**：REA 已重启并启用 Ghidra 12.2/JDK 25，直接打开原厂 `ImageSourceModule.dll`（SHA-256 `f88a455061f3ae2d5e2ef157e7b3caafd94a2b95c39b5bba096802e405ebc6c8`）和 `IMVSBlobFindModu.dll`（SHA-256 `4de695d8c3051045c9010739686f94c32d66a36ee486207fe749fb183fff4c9a`）。静态伪代码确认：ImageSource `Process` 按本地/相机/拼接分支调用 `SetImageOutput`、`SetImageOutputMono8`、`StitchImageOutput`，内部有 `QueryPerformanceCounter/Frequency` 和 `hftest ProcessLocal/ProcessCamera` 记录；`ConvertFileImage` 先 `ConvertPixelType`，首次创建并缓存图像实例，后续加载文件并读取数据指针/宽高/格式；`SetImageOutputMono8` 通过 `VM_M_SetImageEx("ImageMono8")` 发布输出资源，设置属性并记录五段 QPC 差值，没有像素缓冲二次 memcpy；`StitchImageOutput` 在拼接路径分配/清零并 `memcpy_s` 后调用 `SetImageOutput`。Blob `Process` 顺序为 `ProcessAlg → LimitJudge → ProcessOutput`；`ProcessOutput` 按结果/轮廓规模扩容，填充结果缓冲，调用 `VM_M_SetPointset`/`VM_M_SetModuleRuntimeInfo`。这些是静态调用结构证据，不替代动态耗时；已扩大搜索原厂日志但当前未发现 `hftest` 行，下一步优先定位原厂日志级别/配置以释放已有内部探针。项目代码未修改。

**2026-11-16 原厂日志等级与 hftest 探针可见性补证**：定位到原厂随附 `Applications\Tools\LogLevelAdjustmentTool\LogLevelAdjustmentTool.exe` 及 `LogLevelAdjustmentTool-zh-cn.json`。配置明确支持 `Trace(t)`、`Debug(d)`、`Info(i)`、`Error(e)`，默认 `Info(i)`，并映射 SDK、Server、ModuleProxy、ModuleFrame、Module 五类日志文件前缀。此前原厂保留日志未出现 `ProcessLocal`、`SetImageOutputMono8`、`StitchImageOutput` 等 hftest 行，因此不能据此断言探针未执行；当前结论修正为“现有日志等级/输出目标未呈现函数级探针”。本批仍未修改项目代码，下一步仅对原厂有效本地图像流程做 Trace 级动态复测，取得或确认缺失的函数级分段耗时。
**2026-11-16 取证工具职责固化**：后续原厂取证按矩阵执行：REA/Ghidra 负责 DLL 静态结构、函数链、复制/转换/资源发布入口和 QPC/hftest 探针位置；agent-desktop 负责原厂方案、节点、有效本地图像、Trace 等级和首次/后续执行触发；原厂日志负责动态时间戳和模块/函数耗时；只有 Trace 日志仍缺失函数级事件时，才使用 REA 动态能力或原生调试工具补函数进入/退出证据。REA 静态结果不得冒充动态耗时，agent-desktop 点击不得冒充函数调用证据。本批不修改项目代码。


**2026-11-16 动态取证环境复核**：已验证已安装的函数级动态取证工具可用：x64 CDB `10.0.29680.1004`，Microsoft TTD `1.01.11 x64 / 1.11.618.0`。CDB、TTDRecord/Replay 及依赖位于 `artifacts/windbg-tools`，TTD EULA 已接受。本次仅完成安装状态复核，未修改项目代码；下一步使用 TTD 记录原厂有效本地图像首次和后续执行，再提取函数进入/返回及时间线证据。

**2026-11-16 动态取证并行分工与 TTD 兼容性复核**：按“静态闭合、既有日志批处理、单一原厂会话动态记录”三条线并行推进，避免多个工具同时操控同一 VisionMaster 会话。已发现复制目录缺少 `TTDLoader.dll`，补入系统 `C:\Windows\System32\ttdloader.dll` 后，TTD 附加初始化仍因系统版本与当前 TTD 组件不兼容而失败，产生的 `VisionMaster01/02/03.run(.err)` 不作为证据。该失败不影响已验证的 CDB 动态入口链，后续动态返回/耗时改用 CDB 在已加载原厂进程上附加取证；不修改业务代码。

**2026-11-16 并行取证实施方案（加速但保持证据隔离）**：本目标拆为三条并行只读线和一条串行动态线。A 线由 REA/Ghidra 批量完成 ImageSource、ModuleFrame、Blob 的函数入口、调用者、资源发布和候选复制点清单；B 线用脚本并行解析既有 `Modules.log`、`ModuleFrame.log`、`PlatformSDK.log`、CDB CSV，生成执行批次、时间戳、模块总耗时和已命中函数表；C 线审查自研源码的 ImageSource/Blob/Execution 调用链，形成“原厂证据—自研实现—差异—待证”矩阵。D 线只保留一个原厂 VisionMaster 会话，由 agent-desktop 完成方案打开、有效本地图像确认和首次/后续触发，CDB 负责附加断点记录；D 线完成一个会话后才允许关闭/重启，避免冷启动条件被并行操作污染。A/B/C 可同时执行，D 线不得与另一个原厂运行会话并行。每条线只写自己的中间证据文件，主线在批次结束时统一合并到本取证文档和差异矩阵；所有动态结论必须有入口、返回或日志时间边界，静态和 UI 观察不能互相替代。阶段门槛为：A/B/C 先产出可核对清单，D 再按清单只补缺口；证据闭合后才进入整体实施设计，设计批准前不修改业务代码。

**2026-11-16 三线子智能体并行分工已启动**：`parallel_evidence_parse` 只读解析原厂日志/CDB 证据；`rea_static_closure` 只读闭合 ImageSource、ModuleFrame、Blob 的 Ghidra 函数与资源链；`source_diff_matrix` 只读盘点自研 ImageSource、Blob、Execution 与原厂证据的差异矩阵。三个子任务均禁止启动原厂 UI、禁止修改业务源码，各自只产出证据或分析文本。主线独占原厂动态会话、统一证据编号、合并文档并决定阶段门槛。子任务之间不共享未验证结论，只有主线在检查证据路径和范围后合并。

**2026-11-16 并行日志线已完成**：`artifacts/original_log_parallel_summary_20261116.txt` 已整理 CDB 函数进入顺序、07:14 Trace 五元组、Blob 阶段边界及 ProcessID-9272 的模块加载/首次/后续耗时。新增有效会话记录 ImageSource 创建 1.523ms、LoadModuleData 0.439ms，Blob 创建 6.430ms、LoadModuleData 2.020ms，流程首次 18.886ms、后续 7.5231ms。结论仍是模块级冷/热差异已确认，四类子阶段独立返回耗时未确认，未修改业务代码。

**2026-11-16 证据矩阵收口**：新增 `artifacts/original_image_blob_evidence_matrix_20261116.md`，按解码、转换、输出发布、深拷贝、Blob 接收、Blob 输出、首次/后续耗时逐项登记事实、来源和证据状态。矩阵明确模块级冷/热耗时已闭合，四类独立子阶段返回耗时仍未闭合；在此缺口改变前不归因、不做预热或删除复制，不修改业务代码。

**2026-11-16 函数返回边界补证结果**：在新加载的原厂会话上尝试用 CDB 附加设置入口/返回断点，系统返回 `NTSTATUS 0xC0000048`，提示进程已有调试附加；按 PID 定向执行 TTD 停止又确认该进程并未处于有效 TTD 记录。该轮未形成可信返回时间，VisionMaster 已关闭，失败输出不计入证据。当前有效证据仍为已验证 CDB 函数进入顺序、原厂 Trace 函数起止日志和 ModuleFrame/SDK 模块级耗时；不再重复扰动原厂会话。

**2026-11-16 REA Frida 动态入口复核**：发现 REA 提供 `start_frida_session`，尝试附加到原厂 VisionMaster。当前 REA provider 无法定位 `frida_binding.node`，即使补下载 Frida 17.23.2 Win32 x64 N-API 8 预编译绑定并放入候选路径，provider 仍返回 `capability_unavailable`。本次未建立 Frida 会话，原厂进程已关闭，未形成动态证据，也未修改业务代码。保留该工具缺口，当前有效动态证据仍以原厂日志和已验证 CDB 入口记录为准。

**2026-11-16 本地 Frida 函数级动态返回证据补齐**：REA provider 无法加载绑定后，改用已下载的 Frida 17.23.2 Node 绑定直接附加原厂 VisionMaster PID-2712。两次有效本地图像执行均由同一线程 27968 命中并返回 ImageSource.Process、SetImageOutput、ModuleFrame.VM_M_SetImageEx、Blob.Process、ProcessAlg、LimitJudge、ProcessOutput。首次 Frida elapsedMs：ImageSource.Process=11、Blob.Process=8、Blob.ProcessAlg=8；后续：ImageSource.Process=5、Blob.Process=3、Blob.ProcessAlg=3；与 ModuleFrame 10.983/9.080ms 和 4.659/4.435ms 量级一致。原始记录 `artifacts/frida_original_trace.jsonl`，结构化记录 `artifacts/frida_original_trace_events.csv`，已写入证据矩阵和有效图像取证 HTML。Frida 使用毫秒粒度，取整为 0 的发布/判断函数不能解释为零成本，深拷贝底层复制仍未取得独立高分辨率耗时；未修改业务代码。

**2026-11-16 标准复制入口补证**：Frida 受限观察 `msvcr120.dll`、`ucrtbase.dll`、`vcruntime140.dll` 的 `memcpy_s/memcpy`，仅保留 ImageSourceModule、ModuleFrame、Blob 调用者。ProcessID-28648 两次执行分别记录 869/548 次标准复制入口，最大单次长度 256 字节，无 1,228,800 字节级别复制；调用者主要是 ModuleFrame，ImageSource 各 6 次，Blob 各 1 次。该结果不能否定未钩取的共享内存/深拷贝路径，复制钩子显著改变耗时，未将本批耗时用于性能对照。原始记录 `artifacts/frida_original_copy_trace.jsonl`，汇总 `artifacts/frida_original_copy_summary.csv`，未修改业务代码。

**2026-11-16 扩展复制入口复核**：Frida 进一步观察 `memmove_s/memmove` 与 `RtlMoveMemory/RtlCopyMemory`。ProcessID-25668 两次执行分别记录 1604/1122 个事件；`memcpy` 与 `memmove` 实际解析为同一 CRT 地址的双重观察，`Rtl*` 没有目标模块调用者；去重后最大复制长度仍为 256 字节，没有观察到整帧大小复制。该结果不覆盖共享内存专用 API 或自定义复制实现。原始记录 `artifacts/frida_original_copy2_trace.jsonl`，汇总 `artifacts/frida_original_copy2_summary.csv`，未修改业务代码。

**2026-11-16 内存资源 API 边界复核**：Frida 观察 `CreateFileMappingW`、`MapViewOfFile`、`VirtualAlloc`。ProcessID-27868 两次执行只记录 ModuleFrame 对 `VirtualAlloc(4096)` 的 6 次调用，没有目标模块调用者进入前两个映射 API，也没有 1,228,800 字节级别分配。该结果不能排除跨进程共享内存、封装 API 或已有映射路径，原始记录为 `artifacts/frida_memory_trace.jsonl`，未修改业务代码。

**2026-11-16 QPC 微秒级函数耗时补证**：关闭复制和内存观察钩子，只保留 QPC 与目标函数钩子，在 ProcessID-27772 一次有效执行取得 ImageSource.Process=4.2038ms、SetImageOutput=0.1407ms、ImageSource VM_M_SetImageEx=0.0199ms、Blob.ProcessAlg=3.8688ms、LimitJudge=0.0174ms、Blob.ProcessOutput=0.1510ms、Blob 两次结果 VM_M_SetImageEx=0.0248/0.0113ms、Blob.Process=4.2230ms。该批没有复制/内存钩子，耗时可与正常模块日志对照。原始记录 `artifacts/frida_qpc_nocopy_trace.jsonl`，未修改业务代码。

**2026-11-16 并行取证与目标驱动收口方案**：按用户要求建立三条只读并行线。A 负责有效本地图像首次/后续函数级动态证据，B 负责原厂 ImageSource/ModuleFrame/Blob 静态闭合，C 负责自研源码差异、依赖和实施边界。三线分别写入 `artifacts/parallel_plan_dynamic_20261116.md`、`artifacts/parallel_plan_moduleframe_20261116.md`、`artifacts/parallel_plan_source_diff_20261116.md`，未修改业务代码。主线依赖三线结果统一核收差异矩阵，再冻结整体设计后按 Execution、ImageSource、Blob、UI/结果划分写入边界和独立 Release 输出。原厂 Agent Desktop 保持单一会话，仅动态证据线操作；证据和设计未冻结前不改代码，不新增解码预热，不把 resident-plan/module-session 命中当作冷启动已解决。
**2026-11-16 ModuleFrame 方案共享内存写入函数静态闭环补证**：REA/Ghidra 已将字符串 `Write totally %lld bytes data to shared memory` 的地址 `0x180221a00` 通过 xref `0x1800528ae` 闭合到 `FUN_180051de0`，即反编译中的 `CControlDelegate::WriteCacheToSharedMem`。函数明确执行 `CreateFileMappingA`、`MapViewOfFile`、头部写入、模块元数据和模块数据 `memcpy`，尾部通过 QPC 记录 `[time]Write cache operation used %.3f ms`。这证明原厂存在真实共享内存写入和内部计时边界，修正了“函数未定位”的旧状态；当前仍不能把该方案缓存写入路径直接等同于有效本地图像 ImageSource→Blob 像素帧复制，下一步需在同一原厂会话动态 Hook 该函数，核对调用批次、参数大小和返回耗时。未修改业务代码。
**2026-11-16 WriteCacheToSharedMem 动态排除补证**：在同一原厂 VisionMaster 进程直接打开 `C:\Users\Administrator\Desktop\test.sol`，对有效本地图像执行两次单次执行。Frida QPC Hook `ModuleFrame.dll+0x51de0`，两次完整 ImageSource→Blob 函数链均命中，但 `CControlDelegate::WriteCacheToSharedMem` 零次命中。首次/后续 ImageSource.Process 为 12.0569/5.0455ms，Blob.Process 为 7.6993/3.8484ms。该函数应归入方案缓存写入路径，不能作为本批像素帧复制证据；有效图像复制边界继续沿 `VM_M_SetImageEx` 及其内部链取证。输出 `artifacts/frida_writecache_trace2.jsonl`，原厂程序已正常关闭，未修改业务代码。
**2026-11-16 VM_M_SetImageEx 内部发布链动态细分补证**：同一原厂方案和有效本地图像执行两次，Frida QPC Hook `VM_M_SetImageEx`、`FUN_180122e50`、`FUN_1800c9d80`、`FUN_1800f4d30`、`VM_M_GetImageEx`。首次 ImageSource 发布链为 `SetImageOutput 0.3399ms → VM_M_SetImageEx 0.2557ms → 内部 0.1752/0.1184/0.0233ms`，后续为 `0.1786ms → 0.1149ms → 0.0601/0.0423/0.0134ms`。Blob 两次结果发布均重复命中同一内部链。`VM_M_GetImageEx` 和 `WriteCacheToSharedMem` 零次命中，前者静态输入句柄链未在本批动态配置出现，后者属于方案缓存写入路径。输出 `artifacts/frida_internal_publish_trace.jsonl`，原厂程序已正常关闭，未修改业务代码。
**2026-11-16 普通 SetImageOutput 分支动态核对补证**：同一原厂方案和有效本地图像执行一次，Frida QPC 同时 Hook ImageSource 专用转换/拼接入口。命中 `ImageSource.Process 4.6675ms`、`SetImageOutput 0.6006ms`、`OutputExtra 0.0118ms`，未命中 `ConvertFileImage`、`ConvertPixelType`、`SetImageOutputMono8`、`ConvertRgbToMono8`、`StitchImageOutput`、`UpdateImageFilesListFromDir`。`VM_M_SetImageEx` 内部发布链命中，`VM_M_GetImageEx` 和 `WriteCacheToSharedMem` 未命中。当前有效本地图像分支固定为普通 `SetImageOutput → OutputExtra → VM_M_SetImageEx`，输出 `artifacts/frida_branch_entry_trace.jsonl`，原厂程序已正常关闭，未修改业务代码。
**2026-11-16 VM_M_SetImageEx 内部职责静态修正**：Ghidra 完整伪代码显示 `FUN_180122e50` 负责输出句柄校验、结果缓存指针获取和 `CUserModuleDelegate::SetImage` 参数组装，最终调用 `FUN_1800c9d80`，自身未出现像素 `memcpy` 或文件映射。结合本批动态三段时间，当前应将 `VM_M_SetImageEx` 内部三段记录为句柄/缓存发布链，实际字节复制独立进入/返回边界仍未确认。REA 证据 `ev_5e316b25a9b6b785fcba57363bf9f332b4fd61ad6039333ce917812704759274`、`ev_80b805ceacac2878e9435a36505515f2a325ced588fcea99081aca9a478541f5`、`ev_ce54fd7b9db2285050ee938104784ca6e8ed1e650d1cbaf84b07441eeb78018d`。
**2026-11-16 缓存获取与像素复制职责再细化**：Ghidra 伪代码确认 `FUN_1800c9d80` 通过 `FUN_1800cb190` 获取结果缓存对象，再调用 `FUN_1800f4d30` 并释放引用计数。`FUN_1800cb190` 只按模块索引查找缓存节点并返回缓存指针，未出现像素 `memcpy`、文件映射或解码调用。因此当前 `VM_M_SetImageEx` 内部动态时间只能归类为缓存对象获取和模块图像发布，不能标为实际像素复制耗时。未修改业务代码。
**2026-11-16 发布链直接复制调用交叉验证**：Ghidra 对 `FUN_1800f4d30` 的已解析 callees 未出现 `memcpy`/`memcpy_s`。ModuleFrame 内 `memcpy` thunk `0x1801e956e` 的已解析调用者包含 `FUN_180051de0` 方案缓存写入路径，不包含 `FUN_1800f4d30`。由于存在间接调用未知项，这只能排除直接调用，不能证明低层不存在复制。有效图像复制独立耗时继续保持未确认。
**2026-11-16 MVDMediaProcess 实际解码导出动态补证**：在有效本地图像条件下，实际加载的 `MVDMediaProcess.dll` 导出 `MV_MP_DecodeFromFileEx` 与 `MV_MP_DecodeJpegEx` 均在两次单次执行中命中。同一线程首次耗时 `1.3829/2.7416ms`，后续 `1.2800/4.5200ms`；`MVDImageCpp.CreateImageInstance` 首次命中三次 `0.0563/0.0229/0.0215ms`，后续本批未命中该返回事件。实际顺序已补到 `MVDMediaProcess 解码 → ImageSource.SetImageOutput → ModuleFrame.VM_M_SetImageEx → Blob.Process`。`MV_MP_ConvertPixelType`、ImageSource 专用 `ConvertFileImage/ConvertPixelType` 等仍未命中。该证据确认真实解码边界，但没有证明复制、格式转换或输出资源创建由这些导出独自完成；首次/后续也未呈现单调冷/热模式，不能据此增加解码预热或缓存修改。原始记录 `artifacts/frida_decode_exports_trace.jsonl`，结构化记录 `artifacts/frida_decode_exports_events.csv`，完整取证矩阵已更新，未修改业务代码。
**2026-11-16 目标完成清单（最新）**：
- **已完成**：原厂有效本地图像解码、JPEG 解码导出命中、MONO8/输出入口确认、ImageSource→Blob→结果动态顺序、首次/后续流程级耗时、Trace 级 `SetImageOutput`/`ProcessLocal`/Blob 四阶段、CDB/Frida 函数进入证据、ImageSource/Blob Ghidra 静态入口和 Blob `Process→ProcessAlg→LimitJudge→ProcessOutput` 调用链。
- **已确认但不可一一分段**：原厂强制深拷贝合同 `copyMode=0`、ImageSource 输出句柄与 Blob/结果句柄顺序、共享内存聚合边界、`VM_M_SetImageEx` 内部句柄/缓存发布链；原厂 `ProcessLocal` 67 槽存在异常值，不能把槽位直接命名为解码/复制/格式转换耗时。
- **进行中**：整理 ImageSource `Process` → `ConvertFileImage/ConvertPixelType` → `SetImageOutput*` → `OutputExtra/StitchImageOutput` 的完整 Ghidra 调用边，并与有效图像实际命中的普通 `SetImageOutput` 分支对齐。
- **未完成**：帧复制实际字节函数的独立进入/退出耗时、输出资源创建的独立耗时、Blob 内部像素读取的独立耗时、差异确认后的整体实施设计、代码修改和按原厂链路验收。
- **硬约束**：在上述动态子阶段证据闭合前，不修改项目业务代码，不设计解码预热、缓存或删除复制步骤。当前所有工作区源码保持未修改。

**2026-11-16 ImageBufferModule.CopyImage 静态复制闭包补证**：REA/Ghidra 已确认原厂 `ImageBufferModule.dll`（SHA-256 `ab8850d7a747df693152ef7e095a5e0ef87eff9410c2424e9b7c84e435717869`）的 `CImageBufferModule::CopyImage` 位于 `0x18000e6a0`，由 `CImageBufferModule::Process` `0x18000de30` 直接调用。函数先按源 `ImageData.nImgDataLen` 分配目标数据区，再执行 `memcpy_s` 整段复制，成功后写回长度、宽高、像素字段和资源句柄。该证据确认原厂存在真实帧深拷贝实现，但尚未证明有效本地图像动态批次命中该函数，下一次动态窗口优先挂接此函数并与 `VM_M_SetImageEx copyMode=0` 对齐。证据 `ev_5a60f34587917014d3d37072bfb8a29abf214bf0292033041c3b5685a163141f`、`ev_96726e273244dad8cb1c8c3b4418f6739c7f22ce1958979ede372d7f4a45616c`，未修改业务代码。
**2026-11-16 ImageBufferModule 输入/输出边界补证**：Ghidra 已确认 `CImageBufferModule::GetInImage`（`0x18000e540`）读取 `InImageWidth/Height/PixelFormat/Image` 并从 `VM_M_GetImage` 组装 `ImageData`，不复制像素；`CImageBufferModule::SetOutImage`（`0x18000e760`）写入宽高/像素格式后调用 `VM_M_SetImageEx` 发布 Image；`CopyImage`（`0x18000e6a0`）由 `Process` 调用并用 `memcpy_s` 完整复制源长度。输入读取、复制、输出发布三个边界现已静态分离，动态命中和耗时仍待补证。证据 `ev_b0e52a33de83a14d2828e021428255fcb02386c12e3835ebe7a1441a9c361ece`、`ev_dc95f8bdbb589b04dbceb436e7a22c0b3cb6c1516aa09b4bd52ffd4477818800`，未修改业务代码。
**2026-11-16 并行证据审查收口**：三条只读子线已完成本批任务。动态审查将 MVD 解码导出进入/返回列为 A 级，ImageBufferModule.CopyImage 列为 B 级静态闭合，CreateImageInstance 首次命中/后续未命中列为 B 级不对称证据。下一次最小钩子集合固定为 `VM_M_GetImageEx`、`VM_M_SetImageEx`、ImageBufferModule `GetInImage`、`CopyImage`、`SetOutImage`、`Process`，按同一线程和执行批次配对。报告 `artifacts/parallel_plan_decode_copy_20261116.md`，未修改业务代码。
**2026-11-16 ImageBufferModule 动态窗口权限复核**：已按无参数原厂启动流程进入主窗口并尝试挂接 `ImageBufferModule` 复制函数。当前 Codex shell 为 Medium Mandatory Integrity，Frida 和 CDB 对 VisionMaster 进程均返回 Win32 error 5（拒绝访问），因此本轮没有产生 CopyImage 动态命中记录。原厂进程已关闭，静态 CopyImage 证据继续有效。后续需在同一提升权限级别下启动调试器和原厂进程，再执行固定最小钩子集合，不能把权限失败解释为函数未命中。
**2026-11-16 Blob.ProcessAlg 静态输入消费闭合**：Ghidra 已确认 Blob `Process`（`0x18001fa80`）按 `ProcessAlg → LimitJudge → ProcessOutput` 执行。`ProcessAlg` 首次创建 Blob 算法实例并设置共享内存节点，把模块输入结构 `+0x10` 图像对象传入算法实例虚调用，设置 ROI/掩膜参数后读取 Blob 数量和结果状态。Blob DLL 静态导入 `MODULEFRAME.DLL::VM_M_GetImage` 与 `VM_M_GetImageEx`，包含 `InImage`、宽高和像素格式字段。该证据闭合 Blob 对 ModuleFrame 图像输入合同的静态依赖，未发现 Blob 内部像素 memcpy；实际地址读取和算法虚调用耗时仍需提升权限动态捕获。证据 `ev_9c8f962a16d0867a5485a02de0a755a2d4d9bd5a9444da8205374f76c1006764`、`ev_d2305f9ab869344da8ec262c8c6c5326b05dbbfbef6125d2a286c7e5c98bef88`，未修改业务代码。
**2026-11-16 SDK 输入对象结构补证**：原厂 `VmAlgModuBase.h` 声明 `_MVDSDK_BASE_MODU_INPUT` 的前三个 32 位状态字段后紧接 `IMvdImage* pImageInObj`，x64 偏移为 `+0x10`；`IMvdImage` 提供 `Clone`、`GetWidth`、`GetHeight`、`GetPixelFormat`。这与 Blob `ProcessAlg` 从 `+0x10` 取值并传入算法实例的反编译结果相互印证。SDK 预处理掩膜代码还将同一输入对象传给 `SetInputImage`，并从输出对象读取 `SharedMemName`。该证据确认图像对象输入合同，未证明 Blob 内部像素复制。未修改业务代码。
**2026-11-16 原厂取证完成度审计固定**：新增 `artifacts/evidence_completion_audit_20261116.md`，按解码、实例创建、帧复制、格式转换、输出资源、Blob 接收和首次/后续耗时逐项标明 A/B/C 证据等级、已闭合边界和下一步最小证据。当前只确认模块级及部分函数级耗时，复制、底层资源分配和 Blob 像素读取仍未闭合，禁止增加解码预热、删除深拷贝或宣布冷/热对齐。未修改业务代码。
**2026-11-16 ImageBufferModule.Process 静态完整顺序补证**：Ghidra 完整汇编确认原厂 `ImageBufferModule.Process` 输入状态通过后依次调用 `GetInImage`、`CopyImage`，将复制后的数据挂入内部列表，最后逐项调用 `SetOutImage`。这把输入句柄读取、按 `nImgDataLen` 深拷贝、输出元数据和 `VM_M_SetImageEx` 发布静态闭合到同一模块函数内。当前有效 ImageSource→Blob 批次是否进入该模块仍需动态命中确认。REA 证据 `ev_45a1b60ee4698168fe85a28d6a0eade311ff96f148aa10f81fba37f8dc088fe2`，未修改业务代码。
**2026-11-16 ImageSource 与 ImageBufferModule 边界校验**：对原厂 ImageSourceModule.dll 的 Ghidra 过程名和字符串检索未发现 `ImageBuffer` 或 `CopyImage`。这不能排除间接调用，但当前没有静态名称证据把 ImageSource.Process/SetImageOutput 直接连到 ImageBufferModule.Process。已将 ImageBufferModule 复制闭包保持为独立原厂模块证据，未把它直接归因于当前 ImageSource→Blob 批次，未修改业务代码。
**2026-11-16 原厂启动异常单独登记**：一次带方案参数启动 VisionMaster 的失败生成了 `VisionMaster.exe-20261116-084235-0615.txt`，记录 `ACCESS_VIOLATION`，调用栈为 `ntdll!RtlInitUnicodeStringEx → KERNELBASE!LoadLibraryExW`。已单独登记为启动方式/装载失败，不计入 ImageSource→Blob 执行链证据；后续动态取证必须无参数启动后从原厂界面加载方案。记录 `artifacts/original_launch_failure_20261116.md`，未修改业务代码。
**2026-11-16 ImageBufferModule 独立模块边界补证**：原厂 `ModulePathGenerate.txt` 将 `ImageBufferModule` 与 `ImageSourceModule` 列为同级 `Collection` 下的独立 `IMAGE` 模块。结合 ImageSource DLL 未检索到 `ImageBuffer/CopyImage` 名称，当前不能预设二者同一调用链。ImageBufferModule 的复制闭包保持为独立静态证据，等待动态线程/调用栈命中确认，未修改业务代码。
**2026-11-16 提升权限动态取证入口固定**：新增 `artifacts/Start-OriginalDynamicTrace.ps1`。脚本只接受已由无参数原厂启动并通过界面加载方案的 VisionMaster PID，先检查当前 PowerShell 是否 Administrator，再用 `artifacts/cdb_copy_commands.txt` 附加 CDB，避免带方案参数启动导致 `LoadLibraryExW` 装载异常。该脚本只用于原厂动态取证，未修改业务代码。
**2026-11-16 ImageBufferModule 专用方案边界审查**：三条只读线复核确认，当前 `test.sol` 是 ImageSource→Blob 两节点方案，不能用其未命中 ImageBufferModule 推断其他方案链不存在该模块。若要验证 `CopyImage` 归属，必须使用实际包含 ImageBufferModule 节点的专用方案，同时记录 ImageBufferModule.Process/CopyImage、ImageSource.Process 及线程或调用栈关系；同名 `VM_M_SetImageEx` 命中不能作为旁证。复制闭包继续标记为未映射，未修改业务代码。
**2026-11-16 本机 ImageBuffer 专用方案清单核对**：检索本机 `11111.solution`、`test1112.solution`、`test.sol` 及安装目录现有 `.sol/.solution` 文件，未发现 `ImageBufferModule` 节点标识。当前没有可直接复用的 ImageBuffer 专用动态方案，不能把现有 `test.sol` 当作该专用方案；未修改方案文件和业务代码。
**2026-11-16 临时方案结构分析**：已将原厂 `test.sol` 只读解压到 `artifacts/imagebuffer_probe_solution`，确认 `VmServer.xml` 仅含 `ImageSourceModule` 和 `IMVSBlobFindModu` 两个模块，`MoudleFrame` 仅含对应两份 mdata。该目录仅用于方案结构分析，没有回写原方案或业务源码。
**2026-11-16 目标完成清单增量更新**：
- **新增已完成**：通过 Frida 动态命中原厂 `MVDMediaProcess.dll` 的 `MV_MP_DecodeFromFileEx`、`MV_MP_DecodeJpegEx`，补齐真实文件/JPEG 解码导出边界；确认当前有效图像批次实际走普通 `SetImageOutput → OutputExtra → VM_M_SetImageEx` 分支。
- **新增部分完成**：Ghidra 已闭合原厂 `ImageBufferModule` 的 `GetInImage → CopyImage(memcpy_s) → SetOutImage(VM_M_SetImageEx)` 静态复制/输出顺序；但尚未证明当前 ImageSource→Blob 批次动态命中该模块。
- **新增部分完成**：Blob `ProcessAlg` 已闭合输入图像对象合同、`VM_M_GetImage/VM_M_GetImageEx` 依赖和算法虚调用；尚未取得 Blob 像素读取独立动态耗时。
- **保持未完成**：当前有效批次的帧复制实际函数进入/退出耗时、输出资源底层分配/复制耗时、Blob 像素读取耗时、首次/后续差异完整根因、实施设计、代码修改和原厂链路验收。

**2026-11-16 剩余缺口集中收口方案**：按用户要求停止零散补证，新增 `artifacts/concentrated_evidence_closure_plan_20261116.md`。剩余工作压缩为 A 原厂静态闭包、B ProcessLocal/日志对齐、C 一次提升权限动态窗口三条线；静态能闭合的先并行完成，动态只安排一次集中捕获。证据收口前不增加解码预热、不删除深拷贝、不修改执行链。
**2026-11-16 集中收口 A/B/C 线完成**：A 原厂静态线完成 \`artifacts/concentrated_static_closure_20261116.md\`，闭合 ImageSource 入口集合、Blob Process/ProcessAlg/LimitJudge/ProcessOutput 和 BinaryImage/BlobImage 输出资源静态边界；B 日志线完成 \`artifacts/concentrated_processlocal_alignment_20261116.md\`，确认两条 ProcessLocal 均 67 槽、固定 12 个异常索引，合理槽位不能赋予解码/复制/转换业务语义；源码差异线完成 \`artifacts/concentrated_gap_diff_matrix_20261116.md\`。三线均未修改业务代码。剩余只保留一次提升权限动态窗口和专用 ImageBuffer 方案归属验证。











**2026-11-16 主文档最终逐章复核**：重新检查 `docs/09-现行文档/执行引擎产品级架构重构设计.html` 的原 1–19 章、阅读总览、流程图、目标源码树、接口、生命周期、实施策略、验收矩阵和差异表。补紧第 12 章托管入口证据与 ImageSource→Blob 函数链的层级边界，修正第 17/18/19 章的跨模块传播、启动就绪时序和 ModuleFrame 输入表述，删除第 1 章把 Server 统一排空写成原厂事实的残留，并收紧第 10 章资源释放表述。未新增附录或重复章节，未修改产品源码。
**2026-11-16 目标驱动优化方案冻结**：主文档第六章改为 G0-G7 目标、O0-O10 有序任务、D0-D5 阶段门槛和里程碑；第七章改为目标驱动验收矩阵；第十五章改为当前收口状态和落地出口。新增计划明确首轮/后续必须走同一生产过程，时间差异必须消除，优化必须以 VM 原厂已取证的职责、调用顺序、输入输出和资源边界为依据。同步更新 `tasks/plan.md` 与 `tasks/todo.md` 顶部当前有效计划，保留历史未完成任务，不修改产品源码。
**2026-11-16 O1 首轮/后续基线推进**：O0 已按 Ghidra、原厂动态日志、SDK 和自研源码完成证据分层冻结，记录为 `artifacts/goal_driven_o0_o1_baseline_20261116.md`。已在原厂 VisionMaster 当前 `test.sol` 发出两次单次执行并观察到结果视图保持可见；本轮未启用 Frida、CDB、临时 Trace 或诊断扫描，但没有取得可归属的无诊断分段日志，因此 O1 仍未通过，不能宣称首轮/后续时间差异已解决。未修改产品源码。
**2026-11-16 Frida 取证环境安装**：按用户指定的安装根目录 `D:\软件安装` 安装 Python 3.12.10，并在 `D:\软件安装\Python312` 安装 `frida 17.26.3` 和 `frida-tools 14.13.1`。Python 导入和 `frida.exe --version` 验证通过。`frida-ps` 可执行但当前列出应用为空并报告 `idund` 提示，原厂 VisionMaster 进程仍在运行，后续附加取证需按管理员权限和目标进程位数验证。无 Frida 正式性能基线仍不依赖该工具。

**2026-11-16 Frida O9 函数边界补证**：使用已安装 `D:\软件安装\Python312\python.exe`、Frida 17.26.3，以管理员权限附加原厂 VisionMaster，当前 `test.sol` 两次单次执行均在同一线程命中 MVD 解码、ImageSource、ModuleFrame 发布链和 Blob 四阶段；`ImageBufferModule.GetInImage/CopyImage/SetOutImage` 与 `VM_M_GetImageEx` 本轮未命中。原始记录为 `artifacts/frida_o9_trace_20261116.jsonl`。该证据只用于函数顺序和命中边界，Frida 返回时间不进入 O1 正式性能基线；O1 仍未通过。
**2026-11-16 性能差异归因复核**：用户提供自研耗时截图（流程 9.87ms，ImageSource 5.07/4.98ms，Blob 4.32/3.76ms）。当前判定不能直接归因算法。自研链存在平台查询、整文件扫描、输入复制、格式物化和 Blob 整幅复制等设计差异；同时自研 HALCON 算法与 VM 原厂算法实例尚未完成同输入/同参数/同输出等价验收。另确认两边“模块耗时/算法耗时”起止点未证明一致，已在 `artifacts/goal_driven_o2_source_delta_20261116.md` 增加计时口径复核。优化空间存在，但必须先统一计时合同和行为合同。
**2026-11-16 图像后端转换链复核**：确认当前自研为 `IReadOnlyImage → HalconResourceBridge.CopyImage → 托管 packed byte[]/非托管缓冲区 → HImage → HalImageSnapshotSource.Capture → 通用 IReadOnlyImage → Blob 再次 CopyImage → HImage`。1280×960 Gray8 单帧约 1,228,800 字节，源码明确存在至少两次完整边界转换和一次 Blob 入口复制，但每步独立耗时及 HALCON 内部复制仍无动态证据。已将该链加入 O2 差异记录。ImageSource、Blob Runtime 构建均通过，分别 0 错误，只有存量警告。探针默认关闭改动已通过构建，产品源码其余变更未由本批修改。
**2026-11-16 图像租约设计修正**：复核发现“工具首次使用时适配并跨 Run 复用”会制造首轮冷路径，与 G1 首轮/后续同过程目标冲突。已将主文档和 O2 差异记录修正为每次正式 Run 在固定输入适配阶段创建后端视图，同一 Run 内复用；跨 Run 复用只有在 VM 证据证明其属于运行前稳定状态时才能采用。该修正保持图像租约方向，移除了首次命中造成耗时差异的设计假设。
**2026-11-16 图像链定向测试结果**：ImageSource Runtime 与 Blob Runtime Release 构建均通过，0 错误。`SunEyeVision.ImageSource.Runtime.Tests` 未进入业务断言，测试宿主在 `ProductRuntimeTestHost.Initialize` 因现有 Release 载荷 `SunEyeVision.Domain.dll` SHA 不一致而 45/45 失败。该结果归类为当前 Release 载荷与源码变更不同步，不能作为探针隔离或图像租约设计的功能回归结论。
**2026-11-16 ROI/Region 设计边界确认**：基于原厂 `ROISelecter`、`RoiData`、`BoxROIStack`、`ROIBOX`、`ROIANNULUS` 和 `SetCombinationSourceOperation` 证据，确认 ROI 是几何参数、继承和显示模型，Region 是绑定当前图像帧的像素区域。优化任务新增 O5-R，要求每次 Run 在固定适配阶段执行 ROI→Region 栅格化，禁止把 Region 作为 ROI 参数持久化或把 ROI 直接当算法 Region 使用。
**2026-11-16 全量 Release 与 ImageSource 复测**：全量 `dotnet build SunEyeVision.slnx -c Release --no-restore` 完成，0 错误，6014 个存量警告。载荷哈希同步后，`SunEyeVision.ImageSource.Runtime.Tests` 复测为 39/45 通过。剩余 6 条均在 `ImageSourceDeclarationAndFormatTests.cs:233` 因测试夹具缺少 validation root 失败，没有进入 ImageSource 业务逻辑。此前 Domain SHA mismatch 已排除。
**2026-11-16 第一批代码落地**：按新方案移除 `ImageSourceSession.DecodeLocalImage` 中的平台版本查询、整文件托管扫描和临时拆分日志，正式路径保留 `new HImage(address)` 解码及后续输出处理。ImageSource Runtime Release 编译通过，0 错误，未运行测试用例。该批只处理已确认的额外诊断路径，没有修改复制、缓存、调度或资源复用语义。
**2026-11-16 测试夹具修复完成**：子智能体修复 `tests/SunEyeVision.ImageSource.Runtime.Tests/ImageSourceDeclarationAndFormatTests.cs`，当 `S2_CASE_ROOT` 未设置时使用 `%TEMP%\SunEyeVision.ImageSource.Runtime.Tests`，消除直接运行测试时的 `Missing validation root`。仅修改测试夹具，测试项目 Release 编译通过，0 错误，未运行测试用例。
**2026-11-16 ImageSource Runtime 测试载荷修复与验证**：重跑测试时发现 Release 目录二进制已更新，但 `release-payload.json` 仍引用旧的 `SunEyeVision.Application.dll`、`SunEyeVision.Plugin.Runtime.dll`、`SunEyeVision.Tool.ImageSource.Runtime.dll` 等哈希，导致测试宿主在模块初始化阶段拒绝加载。未修改产品运行逻辑；按既有封存脚本重新封存 `Release/SunEyeVision_v1.0.0/SunEyeVision`，新载荷 SHA-256 为 `3BD084352EC608FA001856070F1A1DBD633DDB5EA1A1114AF236F27817CB9F7F`。随后执行 `dotnet test tests/SunEyeVision.ImageSource.Runtime.Tests/SunEyeVision.ImageSource.Runtime.Tests.csproj -c Release --no-restore`，45/45 通过。该结果只证明当前 ImageSource Runtime 回归链可运行，不证明 O1 首次/后续正式耗时基线或 O3-O10 已完成。
**2026-11-16 当前源码基线 Release 固定**：在未新增生产逻辑的前提下，重新执行 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false -v:q`，结果 0 错误、61 个警告。正式载荷 `Release/SunEyeVision_v1.0.0/SunEyeVision` 已重新封存，`release-payload.json` SHA-256 为 `43A09853EC846205C77898D49791DD3B50F7AB6EDA1DE78ABB6B0D5E0EA5C26D`，逐项载荷校验 `mismatches=0`。该基线只证明当前源码可生成一致 Release 产物；O1 原厂无诊断分段基线仍未闭合。
**2026-11-16 Release 载荷回归复验**：在上述完整 Release 和新封存载荷基础上执行 `dotnet test tests/SunEyeVision.ImageSource.Runtime.Tests/SunEyeVision.ImageSource.Runtime.Tests.csproj -c Release --no-build --no-restore`，45/45 通过。测试宿主哈希校验、ImageSource Runtime 当前回归链均正常；未将此结果计作原厂 O1 首次/后续耗时验收。
**2026-11-16 探针关闭路径与人工验收记录**：按当前设计将 ImageSource/Blob 分段探针改为关闭时返回共享无操作实例，不再为正式执行创建 Stopwatch、阶段列表和日志状态；只有设置 `SUNEYEVISION_TIMING_PROBE=1` 时才分配计时状态。该改动只移除自研诊断开销，不改变图像复制、缓存、调度或资源语义。完整 Release 构建通过，0 错误（存量警告 5646）。Agent Desktop 已启动新 Release、加载隔离根最近方案 `11111`，主工作区可见图像源1和Blob分析1；人工点击节点并发起单次运行后程序被误关闭，未形成完整运行结果，因此不计人工执行通过，后续需重新打开后再验收。
**2026-11-16 第二次人工验收尝试**：重新启动当前 Release 并加载隔离根方案 `11111`，Agent Desktop 读取到主工作区和节点 `图像源1`、`Blob分析1`；已完成 Blob 节点坐标选择。随后单次运行操作在 Agent Desktop 权限审批阶段超时，未确认是否送达，未重复点击，因而本次不计执行结果通过。该限制属于桌面自动化审批，不代表产品运行失败；本批保留启动/方案加载证据，运行结果待下一次人工窗口确认。

**2026-11-16 人工桌面验证边界**：Agent Desktop 已确认启动、方案打开和物理点击权限均可用；一次语义单次运行未送达的直接原因是运行按钮被右侧窄按钮遮挡，随后物理点击已送达。该实例打开的是启动测试目录中的空 11111.solution，界面为图像源 0/0，不能作为有效本地图像执行证据。用户截图显示实际 test 方案为图像源 1/12，后续人工验证必须打开该方案；本次未据空方案判定算法链或性能。

**2026-11-16 有效图像节点选择证据**：重新打开 11111 后先物理选择图像源节点，界面显示 图像源 (1/12) 并加载 12 个缩略图；说明方案文件中的本地图像输入已正确进入 UI。随后运行入口已送达，运行后出现 ExecutionDetailsButton（根流程耗时详情）。耗时详情语义点击仍被界面右侧窄按钮遮挡，未取得分段数值；不据此宣称性能闭合。

**2026-11-16 构建收口**：同后端 HALCON 视图复用批次已完成定向构建，完整 SunEyeVision.slnx Release 构建退出码 0，0 错误（已有分析器/测试代码警告保留）。未运行自动化测试，按用户要求采用 Agent Desktop 人工验证；有效 11111 方案图像源节点已确认 1/12，运行入口已送达。O1 分段耗时数值和 O2 完整差异闭包仍未宣称完成。

**2026-11-16 未证据闭合改动撤回**：复核后撤回临时的同后端 HALCON 句柄借用合同及其适配路径。原厂已有 VM_M_ForceDeepCopyImage(force=true)/copyMode=0 证据，但尚不足以证明自研 ImageSource→Blob 可以省略实际复制或共享 HALCON 句柄；为遵守先取证后改动，恢复原 IReadOnlyImage、FrozenImageResource 和 HalconResourceBridge.CopyImage 语义。后续待补齐原厂复制、输入地址、算法首次读取、资源创建/释放闭包后再设计。
**2026-11-16 原厂复制边界补证**：对已有 `frida_original_copy_trace.jsonl` 做只读聚合：ModuleFrame 1597 次、单次最大 count 256；ImageSource 12 次、最大 35；Blob 2 次但 count=0。该钩子只覆盖已挂接 CRT/运行库入口，未观察到 >=4KB 调用，不能解释为零拷贝或证明自研 `CopyImage` 可删除；低层自定义复制、HALCON 内部复制和资源映射仍保持待证。
**2026-11-16 原厂直接附加边界补充**：复用 frida_direct_trace_evidence_20261116.md：两轮执行未观察到目标模块调用 CreateFileMappingW/MapViewOfFile；VirtualAlloc 仅见 ModuleFrame 6 次×4096；CRT/Rtl 复制最大 256 字节；JPEG 读取约 274–277KB。该证据将未闭合复制边界收窄到专用共享内存、跨进程服务或自定义向量化路径，但因 Frida 改变时序，不能作为性能基线或零拷贝结论。
**2026-11-16 Ghidra 函数级静态补证**：完成原厂 ImageSourceModule.dll 的 CMyAlgorithmManager::SetImageOutput(0x18001d720) 和 IMVSBlobFindModu.dll 的 Process/ProcessAlg/ProcessOutput 函数级调用图补证。确认 ImageSource 先发布宽高/像素格式等元数据，再以图像描述结构调用 VM_M_SetImageEx，并记录 QPC 分段日志；确认 Blob 生产顺序为 ProcessAlg→LimitJudge→ProcessOutput，其中可见共享内存节点编号、算法实例创建、VM_M_SetModuleRuntimeInfo、VM_M_SetPointset 及原生 malloc/free 边界。该证据未闭合像素复制、首次读取或跨轮次资源复用，因此未修改产品源码，O1/O2 仍保持未完成。
**2026-11-16 ImageSource Process/格式分支静态补证**：Ghidra 进一步确认原厂 CMyAlgorithmManager::Process(0x18000d820) 同时承担 ModuleFrame 二进制输入/输出交换、运行时信息、宿主运行状态、链推进、停止和同步边界；并确认存在独立的 SetImageOutputMono8 与 ConvertRgbToMono8 格式分支。该结果补充了原厂宿主合同和格式处理职责，但未证明异步调度、跨轮次缓存或复制时序；O1/O2 仍未闭合，产品源码未修改。
**2026-11-16 自研 11111 两轮人工分段基线**：确认 11111 有 12 张有效本地图像，并在同一 Release、同一方案、同一当前图像下连续执行两轮。首轮流程 1462.83 ms（ImageSource 1215.45/1209.81 ms，Blob 210.09/208.91 ms），后续轮 41.12 ms（ImageSource 7.93/7.76 ms，Blob 29.60/29.06 ms）。该数据证明自研冷启动差异真实存在，主要集中在 ImageSource，其次是 Blob；数据来自 UI 人工耗时详情，不是 VM 原厂基线，O1 正式通过仍待原厂同口径分段与函数级动态证据。
**2026-11-16 自研源码边界映射**：按 ImageSourceSession.cs 与 BlobSession.cs 逐段核对了本地 HImage 解码、HALCON→通用快照、Blob CopyImage、灰度/Domain、连通域、逐 Blob 输出和结果快照边界。一次 SUNEYEVISION_TIMING_PROBE=1 诊断运行未从 UI 日志取得分段文本，因此不作为 O1 数据；已关闭诊断程序，未修改源码。
**2026-11-16 原厂有效 ImageSource 两轮基线**：从临时取证目录筛选并打开 vm-image-probe-current48\fixture.sol，确认流程为 Image Source1→Circle Search1，方案内嵌 BMP 输入。原厂同一进程连续单次执行两轮，流程耗时首轮 20.83 ms、后续 7.19 ms。该证据证明原厂本身也存在首轮冷路径；由于没有模块级分段值，不能归因具体函数。O1 目标修正为生产过程一致、冷路径可解释，不再假设原厂首轮数值必然等于后续。
**2026-11-16 原厂 test.sol 有效 Blob 基线修正**：按用户要求仅通过最近方案打开桌面 `C:\Users\Administrator\Desktop\test.sol`。文件和界面均确认包含 ImageSourceModule/IMVSBlobFindModu、节点“图像源1→Blob分析1”和 JPG 输入。Agent Desktop 两次单次执行状态栏为首轮 22.52 ms、后续 8.73 ms，差值 13.79 ms。此前将 test.sol 记为空方案的表述已纠正；该数据只提供原厂 ImageSource→Blob 总流程基线，未拆出解码、资源创建、Blob 像素读取或释放耗时，未修改项目源码。

**2026-11-16 原厂 test.sol 重启冷轮模块分段**：关闭并重启 VisionMaster，通过最近方案重新打开桌面 `test.sol`，首轮流程 11.06 ms；图像源1模块/算法 6.50/6.46 ms，Blob分析1模块/算法 4.46/4.30 ms。同一进程后续轮流程 8.01 ms，图像源1 4.44/4.41 ms，Blob分析1 3.39/3.24 ms。该数据是原厂 UI 模块口径，不能替代函数级解码、复制、资源创建和释放证据，未修改项目源码。


**2026-11-16 冷轮差异量级判定**：同一重启条件下原厂 test.sol 的 ImageSource 为 6.50→4.44ms（差2.06ms），Blob 为 4.46→3.39ms（差1.07ms），流程差3.05ms；自研11111 为 ImageSource 1215.45→7.93ms、Blob 210.09→29.60ms。两者量级不一致，后续不再用一般性解码预热或算法首次准备统一解释；O3-O5 必须逐步证明自研冷轮额外解码、格式转换、复制、HALCON工作区和结果物化与原厂职责链的对应关系，未证步骤不改复制/缓存语义。
**2026-11-16 函数级审计表述校正**：复核发现原厂审计中“帧复制已闭合”“Blob 输入接收已闭合”表述过强。已改为部分闭合：当前仅排除了已挂钩 CRT/Rtl/Win32 入口中的大块复制，并确认输入句柄/元数据及四阶段顺序；专用复制、Blob 像素读取、内部资源分配和跨轮复用仍未闭合。后续不得依据该审计删除自研复制或增加跨轮缓存。
**2026-11-16 自研 Release 冷阶段日志补证**：已有隔离 Release 日志显示首轮 ImageSource 的 platform=15–18ms、decode=3–4ms、image-frame-capture=6ms，Blob 首轮出现 module-session-created、image-copy=4–5ms、HALCON 工作区和结果快照成本；后续为 module-session-hit，ImageSource 约4–6ms、Blob约11–46ms。该日志启用诊断探针，只用于阶段定位，不计入 O1 正式性能基线；它证明自研差异是多阶段叠加，不能归结为解码单项。
**2026-11-16 历史诊断日志边界校正**：自研 Release 冷阶段日志早于当前 ImageSourceSession.cs 的后续源码变更，且启用诊断探针；已在 O0/O1 与 O2 文档标明其仅用于历史阶段定位，不作为当前源码 O1 正式性能基线或代码修改依据。
**2026-11-16 当前 Release 探针闭环**：通过 Agent Desktop 在启动前注入 `SUNEYEVISION_TIMING_PROBE=1`，打开隔离 `11111.solution`，关闭自动切换，固定同一 `1.jpg` 连续两轮。首轮 `module-session-created`，ImageSource `153ms (decode 145/frame 7/output 0)`，Blob `75ms (image-copy 1ms)`，外层 `405ms (capture61/run313/publish10)`；后续 `module-session-hit`，ImageSource `6ms (decode4/frame2)`，Blob `27ms (image-copy2ms)`。当前 O2 差异闭包确认：最大冷差异在本地图像 `new HImage(address)` 解码/底层对象建立；Blob 图像复制仅 1–2ms，不是首轮主因。尚未修改产品代码，不能据此删除复制或引入跨轮解码缓存。证据已同步 `artifacts/goal_driven_o0_o1_baseline_20261116.md` 与 `artifacts/goal_driven_o2_source_delta_20261116.md`。
**2026-11-16 固定耗时脚本收口**：`scripts/verification/Invoke-SunEyeVisionAgentDesktop.ps1 -Action TimingFixed` 现固定执行：检查并关闭已有 `SunEyeVision.UI.exe`，启动前设置 `SUNEYEVISION_TIMING_PROBE=1`，启动隔离 Release 并打开 `11111.solution`，随后固定输入、连续执行两次并读取 `ExecTimingProbe`。其他非固定流程继续使用 Agent Desktop MCP；不再用通用脚本复用旧进程，避免探针静态初始化已错过。脚本语法和 `git diff --check` 已通过。
**2026-11-16 HALCON 读取入口隔离基准**：在临时隔离程序中对同一 `1.jpg` 比较 `new HImage(path)` 与 `HOperatorSet.ReadImage`。同一进程首轮/后续分别为 1906.314/3.244ms 与 9.534/3.067ms；已完成 HALCON 加载环境的独立进程分别为 26.149/3.844ms 与 35.791/3.557ms。结论是首个 HALCON 调用包含运行库加载/初始化，`ReadImage` 不能消除冷/热差异；未据此修改产品入口，实验结果已写入 O2 差异记录。
**2026-11-16 工具项目显示结构修正**：核对 `SunEyeVision.slnx` 后确认，ImageSource/Blob 项目原先挂在旧的 `Tools/*Components` 文件夹，同时保留了同名空的 `SunEyeVision.ImageSource/Runtime`、`Wpf` 解决方案文件夹，导致 Visual Studio 展开后看不到源码。已仅调整 `.slnx` 项目归属，将四个工具项目归入各自 `SunEyeVision.* /Runtime` 与 `Wpf` 节点；XML 校验、`dotnet sln list` 通过。随后整套 Release 构建因正在运行的 Release 程序锁定 DLL 失败，非项目引用或源码编译错误；项目加载已恢复。
**2026-11-16 两工具定位探针补齐**：按用户要求在 ImageSource 与 Blob 两个工具的正式执行链增加默认关闭的分段探针。ImageSource 新增配置校验、本地图像接纳、运行状态、源打开、文件检查、HImage 创建、资源域、通道查询、RGB/Gray 转换、拼接、可选灰度、输出值组装等阶段；Blob 新增输入分支/耗时、参数冻结、VM 参数、输入合同、图像尺寸、灰度域、Mask、输出资源、二值快照、候选判定、直方图和输出构造阶段。两个 Runtime Release 编译均通过，0 错误（仅既有 log4net 警告）。新增 `SunEyeVision.UI (Timing Probe)` 启动配置，设置 `SUNEYEVISION_TIMING_PROBE=1`，普通 Debug/Release 不受影响。等待用户手动执行固定两轮以读取日志，未运行自动用例。
**2026-11-16 启动卡顿根因定位**：用户手动启动 Timing Probe 时 splash 停在“正在准备运行环境”，点位静态显示。隔离早期 `startup-trace.txt` 复现并闭合：`Plugin.Load` 已完成，随后 `Plugin.Assert failed`，错误为 `Payload SHA mismatch: SunEyeVision.Tool.BlobAnalysis.Runtime.dll`。原因是两个工具探针编译后 Release DLL 更新，但 `release-payload.json` 未重新封存；不是动画或运行环境线程卡死。已执行 `Run-NeutralReleaseProbe.ps1 -SkipBuild` 的封签步骤生成新清单（SHA-256 `84BD4B04B2ECB49840BDAEA7B1096D67467C07FF514F29C150F29DDE7D700425`）；准入探针 DLL 未预先构建导致该脚本后半段未执行，但随后用隔离根直接启动复核，`Plugin.Assert completed`、`Service.Initialize completed`、`SolutionManager.Initialize completed`，启动进入 `SolutionManager.Visible`，进程保持运行。未修改动画或启动逻辑。
**2026-11-16 启动动画与超时保护**：按用户要求给 `LoadingWindow` 五个圆点增加顺序循环呼吸动画；未改变布局和取消按钮。`App` 增加 30 秒启动看门狗，超时显示明确错误并退出；取消/关闭启动窗的插件排空最多等待 5 秒，超时记录后关闭，避免无提示卡死。UI Release 编译通过（0 错误，存量警告）。重新封签 Release 载荷后隔离启动复核 `Plugin.Assert completed`、`Service.Initialize completed`、`SolutionManager.Initialize completed`，约 1 秒进入方案管理器阶段；未再出现 Blob DLL SHA mismatch。未运行自动用例。
**2026-11-16 当前人工两轮探针结果**：用户完成隔离 Release 的 `11111.solution` 固定图像两轮操作。启动轨迹已从 `Plugin.Assert`、`Service.Initialize`、`SolutionManager.Initialize` 正常进入主窗口，未再出现载荷 SHA 错误；进程随后正常关闭。最新探针记录显示首轮 ImageSource 总计 `40ms`，其中 `decode-himage-created=27ms`、`source-open-complete=3ms`、`image-frame-capture=6ms`；Blob 总计 `96ms`，`image-copy=5ms`，其余主要为候选判定、逐 Blob 区域/灰度统计和结果构造。约 5 秒后的后续轮 ImageSource 总计 `2ms`，无 HImage 创建/解码段，仅 `source-open-complete=1ms`、`image-frame-capture=1ms`；Blob 总计 `35ms`，`image-copy=1ms`。当前证据直接确认：首轮差异的最大新增段是 `DecodeLocalImage → new HImage(address)`，不是 Blob 图像复制；Blob 后续波动来自算法候选/区域结果物化阶段，不能归因于 `image-copy`。本批仅记录和分析，未据此删除复制、增加解码预热或缓存修改。
**2026-11-17 原厂同图像文件读取动态闭合**：用户通过管理员 PowerShell 成功挂接 `frida_trace_original.js --fileio`，在原厂 `test.sol` 同一图像 `D:\MyWork\测试图片\标定\12.jpg` 上连续执行两次。两轮均捕获 `MSVCR120.dll::CreateFileW(12.jpg)`，随后均捕获 `ReadFile` 读取 4096、277504、512 字节；同一轮还命中 `MSVCR90.dll` 对该 JPG 的 274432 字节和 4096 字节读取。两轮 ImageSource/解码链分别为 `ImageSource.Process 9.98ms → MV_MP_DecodeFromFileEx 1.43ms → MV_MP_DecodeJpegEx 4.61ms → VM_M_SetImageEx 0.51ms`，以及 `ImageSource.Process 7.12ms → MV_MP_DecodeFromFileEx 0.62ms → MV_MP_DecodeJpegEx 5.01ms → VM_M_SetImageEx 0.13ms`；两轮 Blob.Process 10.67/6.19ms。该证据确认原厂同一地址每轮都会重新打开并读取图像文件，并进入媒体解码链。当前自研 `LocalImagePathCache` 的同地址后续命中行为与原厂不一致，是否移除缓存仍需先完成源码改造设计和自研 A→A/A→B 对照基线，当前未修改缓存代码。原始记录 `artifacts/frida_original_fileio_test_20261117.jsonl`。
**2026-11-17 原厂文件读取证据后的直接落地**：按两轮同一 `12.jpg` 均重新 `CreateFileW`、`ReadFile` 和进入媒体解码链的动态证据，已将自研 ImageSource 本地文件执行路径改为每轮直接执行 `DecodeLocalImage()`，不再从 `LocalImagePathCache` 取得跨轮 `HImage` 克隆；运行序列不再创建或释放本地图像缓存实例，旧缓存类型暂保留为未接入代码以避免扩大合同清理范围。耗时窗口修正为先设置正确的 MainWindow Owner，显示后执行 Topmost 切换和 Activate，避免被主窗口遮挡。ImageSource Runtime Release 构建通过，UI Release 构建通过，0 错误；Release 载荷已重新封存，SHA-256 `5EEB6DEF37135512FD49FDB3E1F1FF199A9425EC1A4EEE427CD7FCF4A3CDA5B2`。未运行自动用例，待人工固定 A→A、A→B 两轮读取最新探针。
**2026-11-17 耗时窗口持续置顶修正**：人工验证发现耗时窗口初次显示后，点击主窗口仍可将其遮挡。已将 `WorkflowExecutionSnapshotPresenter` 调整为创建窗口前先设置 `Owner = MainWindow`，并在窗口显示期间保持 `Topmost`，主窗口状态变化时重新激活耗时窗口。UI Release 构建通过，0 错误；载荷已重新封存。尚未进行桌面人工复核。
**2026-11-17 移除 HImage 缓存后的人工两轮结果**：最新 Release 同一图像两轮探针显示，ImageSource 首轮 `3581ms`，其中 `decode-himage-created=3439ms`、`channel-query=9ms`、`image-frame-capture=101ms`；后续轮 `4ms`，其中 `decode-himage-created=3ms`、`image-frame-capture=1ms`。两轮均执行直接 `DecodeLocalImage()`，差异已不再来自自研跨轮 `LocalImagePathCache`。Blob 首轮 `431ms`，出现 `halcon-scope-init=32ms`、`image-copy=67ms`、`gray-image-domain=73ms`、`threshold-segmentation=100ms`、`candidates-extract=72ms`；后续轮 `35ms`。这表明当前主要剩余差异是 HALCON/图像解码运行时及算法工作区、内存页首次建立，不能再归因于缓存命中。原始日志为 `Data/Logs/suneye.log2026-11-17.log` 和 `Data/Logs/2026/11/17/SunEyeVision_001515.txt`，本批未继续修改代码。
**2026-11-17 最新成功两轮人工执行**：载荷重新封存后用户完成同一图像两轮执行。首轮 ImageSource `37ms`，`decode-himage-created=26ms`、`image-frame-capture=7ms`；Blob `92ms`，`image-copy=5ms`、`gray-image-domain=2ms`、逐 Blob 区域/灰度统计和结果构造占主要阶段。后续 ImageSource `4ms`，`decode-himage-created=3ms`、`image-frame-capture=1ms`；Blob `28ms`，`image-copy=1ms`。两轮均走直接 `new HImage(address)`，当前差异仍集中在 HImage 首轮耗时和 Blob 首轮处理阶段，未修改代码。
**2026-11-17 一次性 native 冷路径探针脚本**：新增 `scripts/verification/Invoke-SunEyeVisionNativeTimingProbe.ps1`，自动关闭旧 UI、启用 `SUNEYEVISION_TIMING_PROBE`、启动 Release、内部等待进程后挂接 `frida_self_cold_path_trace.js`。一次性探针覆盖 CreateFileW/ReadFile/GetFileSizeEx、LoadLibrary、VirtualAlloc/HeapAlloc、HALCON/MVD 相关模块导出以及现有 ImageSource/Blob 分段，用户只需执行脚本后完成固定图像操作。未修改产品代码。
**2026-11-17 native 探针启动干扰修正**：复核发现一次性 native 探针在进程启动阶段全局挂接 `HeapAlloc/HeapFree`，启动日志停在 `Service.Initialize`，同一记录已产生两万多条堆分配事件，证明探针自身造成启动线程严重开销。已移除启动阶段全局堆分配钩子，保留 CreateFile/ReadFile、kernelbase 文件 API、LoadLibrary、VirtualAlloc、动态 HALCON/MVD 模块导出和现有业务探针。已结束受影响进程，未修改产品代码。
**2026-11-17 native 探针挂接时机修正**：用户反馈欢迎页前卡死。确认一次性脚本在进程刚启动时挂接 native 探针，启动阶段拦截文件、DLL 和虚拟内存调用，仍会干扰 `Plugin.Load`/`Service.Initialize`。脚本已改为等待 `startup-trace.txt` 出现 `MainWindow.ContentRendered` 后才挂接 native 探针，启动过程不再受 native Hook 影响。已结束受影响进程，产品代码未修改。

**2026-11-18 架构迁移门槛调整**：用户明确要求不保留兼容回退。正式节点窗口先统一使用 `FlatNative`，每个正式输入必须有原厂 page、section、order 映射，目标 page 必须在设置页声明。映射缺失直接阻断组装并记录为工具迁移缺口，不得通过嵌套 `ParamListView` 或临时占位绕过。框架实现见 `src/Plugin.Wpf/Presenters/Parameters/NativeInputComposition.cs`、`DeclaredSettingsView.cs`、`DeclaredFieldsView.cs`。已通过两个平面组装测试，完整 `SunEyeVision.slnx` Release 编译 0 错误。下一步先完成 18 个工具的输入 page/section 映射审计和补齐，再进行统一参数页迁移与工具功能复核。

**2026-11-18 全量输入映射审计通过**：新增 `All_registered_tools_expose_explicit_native_input_page_mappings`，对登记的 18 个工具逐一读取 InputSchema/SettingsSchema，执行原厂 page、section、order 映射校验，1/1 测试通过。证据见 `docs/evidence/native-input-placement-audit-20261118.json`。正式入口不再通过缺失映射回退到嵌套参数列表，下一步进入统一参数页的字段状态、ROI、引用和结果显示迁移。

**2026-11-18 Blob 平面迁移仍有功能缺口**：Blob 的正式窗口已进入 `FlatNative` 入口，但现有 `BlobRoiUi` 仍依赖输入视图的旧锚点语义，已改为外层 `DeclaredFieldsView` 接收 ROI 组合并注册外部输入字段。定向测试当前仍未通过，`Actual_blob_image_mask_selector_retires_results_and_feeds_the_next_invocation` 找不到 `input.roi.area-type`，说明 Blob 输入工厂/ROI 组装链尚未完整接入统一参数宿主。该缺口不能用兼容嵌套列表掩盖，需继续沿 `RuntimeNodeConfigurationControl → DeclaredInputFieldsView → BlobUiComposition.BuildPage` 取证修复后，才能扩大工具迁移。

已增加 `ToolWindowAssemblyTrace` 的 `InputComposition` 事件，记录工具类型、输入工厂是否创建、输入 Presentation 数量和设置页数量，用于下一次正式 Agent Desktop/Release 运行定位输入工厂阶段。

新增 `Blob_runtime_draft_retains_input_presentations_for_flat_composition`，已证明正式 Blob 草稿仍保留 `input.image`、`input.pose` 及 page 元数据。问题进一步收窄到窗口组装阶段，而不是 Runtime schema 或持久化阶段。

**全局执行规则（2026-11-18）**：原厂 VM 是经过完整测试的标准答案。所有后续架构、UI、交互和功能修复必须先做原厂源码/配置/运行证据与自研实现的逐层差异分析，再修改代码。对齐内容包括理论架构、对象所有权、组装顺序、生命周期、运行结果和异常路径。没有原厂证据的行为标记为未确认，不自行设计替代实现。该规则适用于全部工具和公共框架。

**2026-11-18 Blob 平面输入组装差异修正（证据驱动）**：原厂 `IMVSBlobFindModuCs.BlobFindRoiManager` 的静态证据确认图像/矩形/扇环/盲多边形/Mask ROI 均由同一 `RoiManager` 通过 `InputItem(...).SetInputValue(...)` 进入参数根。自研参数根已将 FlatNative 输入行、ROI 组装和页构建顺序收口为同一事务：正式输入行先安装或排队到声明的 VM page/section，随后执行工具页回调；外部字段注册允许目标页尚未物化但已进入排队事务，禁止重复创建第二套字段视觉链。`DeclaredFieldsView` 的 PageBuilt 时序已调整为 pending page actions → PageBuilt → visibility，符合原厂参数根先组装、工具面板后挂接的证据。新增 Blob 输入组装诊断会记录 factoryInvoked、panelFields、declaredInputs 和 page。验证当前受执行环境限制：Release 输出、项目中间目录存在拒写，定向测试尚未重新执行，不能记作通过。详见 `docs/evidence/blob-input-assembly-difference-analysis-20261118.md` 与原厂反编译文件 `docs/07-工具逆向/decompiled/Managed/BlobFind/IMVSBlobFindModuCs/BlobFindRoiManager.cs`。

## 2026-11-18 ImageLoad + BlobAnalysis 试点对照执行

- 当前范围收口为 `ImageLoad` 与 `BlobAnalysis`，其他工具保持卸载，不纳入本轮验收。
- 已完成原厂 VisionMaster 4.4.0 Build20250516 的 Agent Desktop 取证。原厂两个窗口均为 420×500。Blob 为“基本参数 / 运行参数 / 结果显示”三页，基本页分“图像输入 / ROI区域”，运行页有重置参数底栏命令。ImageLoad 为单一“基本参数”页，内含“基本参数 / 触发 / 订阅文件夹”三组。
- 证据快照：Agent Desktop `s2evwp4zmlozh`（ImageLoad）、`s1nfbghm4r19v0`（Blob 基本页）、`s3irk1m39le2f4`（Blob 运行页）。证据截图：`docs/evidence/vm-image-source-dialog-20261118.png`、`docs/evidence/vm-blob-dialog-run-tab-20261118.png`。
- 差异清单：`docs/evidence/image-source-blob-complete-ui-difference-20261118.md`。该清单把 UI、控件、组装、引用/数值会话、结果显示和生命周期分别标记为已对齐、缺口、未确认。
- 执行规则：仅修复静态原厂证据覆盖的映射、组装和生命周期合同。原厂未确认的 Blob 动态结果生产者、完整失败码/取消分支、ImageSource Process 本体不扩展实现。
- 下一门禁：ImageLoad/Blob 定向自动测试 → Release 构建 → 自研 `11111` Agent Desktop 逐页人工核对和截图 → 回填本文件的通过/失败日志。


### 2026-11-18 Blob 调试窗口复核追加
- 用户已打开 Blob 分析调试窗口，但 Agent Desktop 当前窗口清单未发现 SunEyeVision.UI.exe 或 Blob 参数窗口，进程列表也未发现对应进程，暂不能宣称已完成桌面截图验证。
- 根据失败回归定位，已恢复 BlobUiComposition、BlobRoiUi、BlobLocalRoiOwner 及 NativeRoi* 到最近原厂映射版本，仅保留首次绑定异常隔离和完整异常日志。
- Blob WPF Release 独立构建通过，0 错误，现有 2654 个历史警告。
- BlobThreePageSessionRegressionTests 仍未通过，实际窗口在 AwaitEditorReady 阶段超时，说明运行时 CanDebug/组装生命周期仍未闭合，未进入 Release/Agent Desktop 验收通过状态。


## 2026-11-19 Blob 原厂组装链批量修复进展

- 已确认首次窗口失败的原厂偏差。`FlatNative` 已经把正式输入行迁移到统一参数根，Blob 组装器又把同一个 `DeclaredFieldRow` 追加到 ROI/位置修正组，首次应用 WPF 模板时触发重复 Visual 父节点异常。
- 已在公共参数页加入 `ToolOwnedInputFields` 契约。统一参数根跳过工具声明由原厂组装的输入字段，工具组装器接管这些字段的唯一视觉所有权。
- Blob 已声明并接管除 `input.image` 外的 ROI、掩膜和位置修正输入。`ComposeInputEditor` 对正式字段执行一次性 ownership transfer，已移交字段不再参与输入页 pending/校验，避免二次提交。
- `DeclaredFieldsView.ContainsView` 防止已由 VM 参数根接管的行再次挂载。
- 验证结果。Blob 单用例已通过。位置修正关闭、同一撤销编辑用例已单独通过。五用例批量回归上一轮为 2 通过、3 失败，新增修复后剩余唯一已确认缺口是关闭后输出图像对象仍可读，待按资源所有权证据修复。
- 当前 Release 构建可通过，既有 2654 个警告，0 个错误。完整五用例和 Agent Desktop 人工验证仍未完成。

## 2026-11-19 同构原则纠偏

本轮测试暴露的不是单个测试问题，而是窗口根和生命周期设计尚未与 VM 同构。原厂证据文档明确指出，`AlgorithmTabRoot`/`BaseWindow` 统一拥有参数根、页签、底栏命令、模块会话、关闭排空和资源释放；当前自研仍由 `RuntimeToolEditorWindow`、`DeclaredSettingsView`、工具 composition 和 `ProductionEditorDebug` 分担这些职责。

因此后续不能继续按“测试失败后局部补丁”的顺序推进。必须先把窗口根、输入字段唯一所有权、工具组装回调、执行会话和关闭释放收拢到一条 VM 对应链，再用 Blob/ImageLoad 测试验证。没有原厂动态证据的关闭时序不得自行假设为 VM 行为，当前生命周期用例仍标记为未闭合。

- 新增逐层差异审计：`docs/evidence/image-blob-architecture-diff-current-20261119.md`。该表把 VM 窗口根、参数根、图像来源、Blob ROI、运行参数、结果显示、执行会话和关闭释放逐层对照，并明确哪些修复已有证据、哪些仍未确认。

**2026-11-19 项目级执行规则固化**：用户确认原厂证据先行适用于整个项目，不限于本次 ImageSource/Blob。新增 `docs/evidence/EVIDENCE_FIRST_PROJECT_RULE.md`，规定证据优先级、VM/自研差异表、缺陷闭环、证据状态和合并门禁。当前真实编辑器生命周期测试仍失败于 `BlobThreePageSessionRegressionTests.Real_three_pages_read_numeric_models_and_retire_with_the_actual_editor` 的资源释放断言，表现为关闭完成后 `CopyPixels()` 未抛出 `ObjectDisposedException`。该结果记录为共享关闭/资源所有权差异，不能继续用 Blob 局部补丁或修改断言绕过。下一步必须依据原厂关闭链证据重构共享窗口根与资源释放边界，之后再重跑测试、Release 构建和 Agent Desktop 验证。

**2026-11-19 证据先行契约落地**：将工具原厂组装输入所有权从宿主的 `ToolType=="BlobAnalysis"` 特判迁移到 `ToolPresentation.ToolOwnedInputFields` 声明，Blob 执行架构声明其工具拥有的输入字段，公共参数根仅按契约跳过并由工具组装器接管。`dotnet build SunEyeVision.slnx --configuration Release --no-restore` 通过，0 错误、6098 警告。真实编辑器生命周期测试仍失败，故不宣称 ImageSource/Blob 完成。

**2026-11-19 调试关闭资源租约差异修正**：根据生命周期探针定位到共享窗口根的关闭顺序偏差。编辑上下文退役时，`_openDebugWindow`/editor control 引用可能已被先清空，原窗口 Closed 处理因此提前返回，旧调试结果 publication 未退役，Blob 输出图像继续保留可读。修正为窗口关闭事件始终撤销该编辑窗口的结果 publication，再进入关闭完成状态。没有修改测试断言或 Blob 算法资源合同。目标用例 `Real_three_pages_read_numeric_models_and_retire_with_the_actual_editor` 通过（1/1）。该修正是本方生命周期差异定位结果，原厂动态关闭顺序仍标记为运行未确认。临时探针已移除，待重跑 Blob 类回归、Release 构建和 Agent Desktop 验证。
**2026-11-19 回归与 Release 复核**：BlobThreePageSessionRegressionTests 5/5 通过，SourceAndVariablesSessionTests 17/17 通过。完整 `dotnet build SunEyeVision.slnx --configuration Release --no-restore` 通过，0 错误、647 个警告。Release 产物已重新生成。Agent Desktop/人工视觉验证尚未执行，原厂动态关闭顺序仍为未确认项。

**2026-11-19 Release Agent Desktop 验证**：通过 Agent Desktop 启动 `Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe`，从最近方案列表打开 `blob-min`，进入 Blob 分析调试窗口。窗口可访问，尺寸 420×500，检测到“基本参数 / 运行参数 / 结果显示”三页、图像源、ROI 区域、运行参数、高级参数和底栏命令。已保存三张 Release 截图和可复核快照记录：`docs/evidence/self-release-blob-agent-desktop-20261119.json`。程序已通过 Agent Desktop graceful close 且进程退出已验证。该结果证明自研 Release 的窗口组装和可访问控件链可运行，不替代 VM 动态行为证据；未执行新工作流变更和人工视觉主观验收。

## 2026-11-19 Agent Desktop Release 验证：图像源

- 使用 Release 可执行文件 `D:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`，从 `blob-min` 最近方案进入主界面并打开 `1 图像源1`。
- Agent Desktop 快照 `s1yn7t2jupb24e` 识别到基本参数页、图像源/像素格式下拉项、方案存图/显示图像名称/SN初始值/图片缓存/拼接使能/自动切换/最后一张停止/字符触发过滤/订阅图片文件夹等控件。
- 识别到图像源枚举 `本地图像 / 相机 / SDK`，像素格式枚举 `MONO8 / RGB24 / 保持原图`，底栏命令 `连续执行 / 停止执行 / 执行 / 确定`。
- 截图证据：[self-release-image-source-basic-20261119.png](../evidence/self-release-image-source-basic-20261119.png)。结构化证据：[self-release-image-source-agent-desktop-20261119.json](../evidence/self-release-image-source-agent-desktop-20261119.json)。
- 关闭采用 Agent Desktop graceful close，进程退出已验证。
- 该证据确认自研 Release 的窗口装配和可访问控件链，不替代原厂 VM 动态证据，也不宣称主观视觉验收已完成。

**2026-11-19 ImageSource/Blob targeted regression recheck**：在当前工作区重新执行 `dotnet test tests\SunEyeVision.UI.Tests\SunEyeVision.UI.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BlobThreePageSessionRegressionTests|FullyQualifiedName~SourceAndVariablesSessionTests"`，结果 22/22 通过，0 失败、0 跳过。测试过程重新编译并部署了 ImageSource、Blob 和 Release 验证载荷。该结果证明当前本方回归链已通过，不替代原厂动态证据，也不等同于完整 EditorDebugSessionTests 全量通过。

**2026-11-19 完整 Release 构建复核**：执行 `dotnet build SunEyeVision.slnx --configuration Release --no-restore --verbosity minimal`，结果 0 错误、645 个既有警告，Release 产物重新生成。警告主要为既有 XML 文档/可空性分析和 NU1902 log4net advisory，未出现新的编译错误。

**2026-11-19 编辑器会话失败定位**：完整 `EditorDebugSessionTests` 复跑显示首批失败包含历史记录语言断言与当前中文资源不一致，后续部分由连续测试后台活动未排空产生级联。隔离 `Actual_blob_image_mask_selector_retires_results_and_feeds_the_next_invocation` 后确认 Blob 掩膜行使用原厂 `EnumerationG`/`LeftRightControl`，`DisplayNameA` 是资源键 `Mask Image`，实际 `ResourceBinding` 文本为本地化值 `掩膜图像`。同一来源节点同时提供 `output.image` 与 `output.gray-image`，按节点 ID 单独取候选不具备唯一性。测试现改为按原厂本地化显示值断言，并明确选择 `output.image` 主图像输出。产品控件和组装链未添加猜测性修补。该隔离用例已通过 1/1。

**2026-11-19 掩膜选择器回归复核**：在本地化标签与主图像输出选择断言修正后，重新执行 Blob 三页、ImageSource/变量会话及掩膜选择器组合回归，结果 23/23 通过，0 失败、0 跳过。

**2026-11-19 Blob ROI 保存重开回归复核**：隔离执行 `Blob_mask_drawing_display_and_inheritance_survive_save_and_reopen`，结果 1/1 通过。失败定位后按原厂流程补齐三处测试前置。ROI 绘制前必须把结果区切换到模块输入图像，合成测试直接调用 `NativeRoiSession.CompleteDraw` 后需释放 `NativeRoiAttachment` 的手势租约，保存重开前需等待通讯运行时释放。三处均有原厂流程断言或相邻通过用例作为依据，未修改产品行为。该用例覆盖 ROI 绘制、掩膜显示参数、继承引用、保存、重开和再次执行。
**2026-11-19 ImageSource/Blob 目标回归集复核**：组合执行 Blob 三页、ImageSource/变量会话、掩膜选择器和 Blob ROI 保存重开用例，结果 24/24 通过，0 失败、0 跳过。日志为 `.testdata/blob-image-regression-24-20261119.log`。这是本方自动回归结果，仍不替代原厂动态证据和主观视觉验收。

**2026-11-19 项目级原则继续执行**：本项目后续所有架构、公共控件、工具 UI、组装、交互、执行和释放改动均遵循同一门禁：先收集 VM 反编译源码/配置/运行截图或 Agent Desktop 证据，形成 VM 与自研逐层差异表，再实施代码迁移；没有对应原厂证据的行为只记录为“运行未确认”，不得用自研猜测补齐。测试失败先回到差异表定位入口、模板、绑定、组装、执行或释放阶段，禁止只改断言或按局部现象持续打补丁。

**2026-11-19 生命周期复核追加**：为编辑窗口打开前的正式结果增加独立快照租约，编辑窗口关闭或上下文退役时按原厂“调试结果与正式结果分离”的证据恢复正式 publication。共享编辑窗口关闭完成前新增 `AfterCloseDrain`，确保 `ProductionEditorContext` 在窗口排空后退役，避免关闭后仍持有可读图像资源。目标 ImageSource/Blob 回归集重跑 22/22 通过，Blob ROI 保存重开 1/1 通过。完整 EditorDebugSessionTests 仍有 4 个未闭合用例，已记录为共享引用会话、原生对话框所有权、覆盖层交互和质心引用执行链差异，不能宣称全量完成。

**2026-11-19 当前轮次复核**：在加入正式结果独立快照租约和窗口关闭排空回调后，ImageSource/Blob 目标回归集重新通过 22/22，Blob ROI 保存重开 1/1，完整 Release 构建退出码 0（1 个既有 NU1902 警告、0 个错误）。完整 `EditorDebugSessionTests` 还剩 4 个失败用例，已明确不能把它们当作 Blob 局部样式问题处理，后续按 VM 证据继续核对共享引用会话、原生对话框所有权、覆盖层命令和质心引用执行链。

## 2026-11-19 证据先行原则与 ImageSource/Blob 试点收口

本项目后续所有落地统一遵守以下顺序，不限于本轮工具：原厂反编译/静态配置/运行截图取证 → 原厂与自研差异表 → 代码级迁移或重构 → 目标回归 → Release 构建 → Agent Desktop 人工验证。没有对应版本原厂证据的行为保持未确认，不用自研推测替代。

本轮按 VM 4.4.0 证据修复了 ImageSource + Blob 的共同参数链：

- `ParameterFieldHost` 为 Integer/Float 原厂 `Integer_Sub`/`Float_sub` 模板建立正式 `ReferenceFieldSession`，使 `IsSubscribeLeft`、`SubscribeDataLeft`、`TypeChangeCommand` 和数值引用会话一致。
- `ReferenceFieldSession` 按原厂可见文本 `source[]` 解析序列索引，修复编辑 `[0]` 时错误要求额外空格的本地格式偏差。
- `DeclaredInputFieldsView` 区分普通输入、原厂 Assist 外部组合和当前活动分支。隐藏 `CustomVisible` 分支不再阻断运行，Assist 负责其活动源提交。
- Blob Assist 按原厂声明恢复字段能力。Point 来源为引用模式，坐标/角度/缩放数值使用常量/引用双模式。可选引用字段保留合法常量投影，位置修正关闭时仍按同一编辑事务清空为 `Unset`。
- 关闭/重开验证前显式排空通讯运行时，符合方案会话的活动租约释放顺序。测试主机的原生消息框自动应答仅在需要观察真实拥有对话框的两个测试中临时关闭，产品行为未改变。

验证结果：

- ImageSource + Blob 目标回归 `BlobThreePageSessionRegressionTests | SourceAndVariablesSessionTests`，22/22 通过。
- Blob centroid point subscription capture/reopen，1/1 通过。
- Release 构建，0 错误，668 个现有警告。警告主要为既有 nullable/xUnit analyzer/NU1902，不影响产物生成。
- 既有 Agent Desktop Release 证据仍有效，Blob 基本参数/运行参数/结果页和 ImageSource 基本页记录见 `docs/evidence/self-release-blob-agent-desktop-20261119.json`、`self-release-image-source-agent-desktop-20261119.json` 及对应截图。本轮数值引用链的自动证据已补齐，尚未重新生成新的桌面截图。

未闭合项：完整 `EditorDebugSessionTests` 当前首个连续关闭排空用例仍会在 `DocumentUiFixture.Dispose` 超时，随后造成其余用例的会话活动级联失败。该问题独立于本轮 ImageSource/Blob 目标回归，不能将 22/22 结果外推为完整编辑器套件通过。下一步应按原厂关闭等待链继续定位 `ContinuousCompletion` 与 `CloseAndDrainAsync` 的交互。

**2026-11-19 当前代码复核补充**：按项目级证据先行门禁复核最新工作区。`BlobThreePageSessionRegressionTests` 5/5 通过，`SourceAndVariablesSessionTests` 17/17 通过，分别保留于 `.testdata` 当前日志；`dotnet build SunEyeVision.slnx --configuration Release --no-restore --verbosity minimal` 通过，0 错误，当前构建输出仅报告既有 `log4net` NU1902 警告。使用 Agent Desktop 启动 `Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe`，确认起始页、最近方案 `blob-min`、主界面和已加载的“图像源1 / Blob分析1”节点可访问，进程随后正常退出。Agent Desktop 快照证据由当前会话产生，未使用 computer-use。完整 `EditorDebugSessionTests` 仍有共享关闭排空阻塞，不能宣称全量通过；该阻塞继续按原厂关闭链证据单独处理。

**2026-11-19 共享窗口关闭排空修复**：依据 VM `docs/07-工具逆向/decompiled/Controls/VM.Controls/BaseWindow.cs` 的静态证据，原厂窗口以单一 `IsClosing` 状态进入 `OnClosing`，资源清理在 `OnClosed` 完成。自研窗口此前在 Closing 重入时分别返回 `_closeCompletion` 和 `AwaitEditorDrainAsync` 两条任务对象，导致同一窗口存在多个等待链，完整编辑器测试最终卡在夹具排空。已改为首次关闭请求创建并复用唯一 `_closeDrainCompletion`，所有重入调用返回同一任务，且仅在关闭后的资源释放完成时回执成功。隔离测试 `Continuous_close_waits_for_inflight_capture_and_rejects_late_publication` 已通过。

**2026-11-19 关闭修复后的验证**：`EditorDebugSessionTests` 由此前关闭排空级联失败推进到 46/52 通过。剩余 6 项是已有的 Blob 结果显示、保存重开和测试数据契约差异，未通过本次关闭修复掩盖。ImageSource/Blob 目标回归仍为 Blob 5/5、SourceAndVariables 17/17。完整 Release 构建通过，0 错误，651 个警告。新 Release 产物经 Agent Desktop 启动，验证起始页、`blob-min` 最近方案、主界面以及“图像源1 / Blob分析1”节点可访问，程序正常退出。

**2026-11-19 项目级执行原则确认**：用户明确确认“原厂证据先行、差异分析后实现、再测试与人工验证”的门禁适用于整个项目落地过程，不限于 ImageSource、Blob 或当前试点。后续任何共享框架、基础控件、工具页面、组装链、端口交互、执行链和生命周期变更均须按该顺序推进；没有对应 VM 证据的内容只能标记为运行未确认，不得凭自研现状或局部测试结果直接优化。

**2026-11-19 项目级证据门禁确认与本轮验证**
- 规则扩大到整个项目落地过程。共享框架、基础控件、工具页面、组装链、端口交互、执行链和生命周期的每次变更，都必须先有同版本 VM 证据，再形成 VM/自研差异项并据此实现；没有证据的行为只允许标记为“运行未确认”，不能凭自研等价逻辑补齐。
- 本轮按证据修正了 `SettingsLease` 的输入投影转发、Blob 未呈现纯值模式的错误投影刷新，以及提交通知阶段 `NodeConfigurationEditDraft.ApplyFields` 的所有权校验时序。改动位于共享框架和生命周期接缝，未添加 Blob 专属视觉猜测。
- 自动验证：Blob 保留窗口重基投影 1/1；Blob 三页会话 5/5；图像源与变量会话 17/17；完整 `EditorDebugSessionTests` 47/52，仍有 5 个独立失败，分别是 Blob 空结果文本、结果行 `output.mono8`、重开方案节点计数、Blob 真实输入预览期望值和旧预览资源排空。
- Release 构建通过，0 errors、657 warnings。警告为既有 nullable/xUnit analyzer 与 NU1902 advisory，尚未清理。
- Agent Desktop 已启动 Release 并验证 `blob-min` 方案和 `2 Blob分析1` 调试窗口可打开。窗口可观察到“基本参数/运行参数/结果显示”三页、底栏“连续执行/停止执行/执行/确定”命令、位置修正、输入掩膜、ROI 图形/图像类型、绘制/继承、输出掩膜等原件式控件。当前输入源显示“当前来源不在可选图像声明中，请重新选择”，这是本次人工验证记录的现存差异，不能视为已闭合。
- Agent Desktop 已正常关闭 Release 进程。工作区包含大量既有未提交变更，本轮未执行 reset、discard 或提交。

**2026-11-19 Agent Desktop 方案数据证据补充**：对当前人工验证入口 `C:\Users\Administrator\AppData\Local\SunEyeVision\Solutions\blob-min.solution` 和 `11111.solution` 做了只读解析。两份方案都包含图像源与 Blob 节点，但 `Workflows[0].Connections` 均为空；因此 Blob 调试页出现“当前来源不在可选图像声明中，请重新选择”是方案连线数据缺失后的原厂式阻断状态，不能把它误判为输入控件组装失败，也不能通过 UI 代码自动补线。后续人工验证必须使用已保存 `ImageSource.output.image -> Blob.input.image` 连线的方案副本，或先在方案编辑器中建立连线再复核。

**2026-11-19 Agent Desktop 连线方案复核**：为避免修改用户方案，复制 `blob-min.solution` 到 `.testdata/agent-desktop/blob-connected/blob-connected.solution`，仅在测试副本中补入 `ImageSource.output.image -> Blob.input.image` 连线并同步 Blob `input.image` 引用。使用当前 Release 产物启动该副本，Agent Desktop 打开 `2 Blob分析1` 调试窗口后，基本参数页显示原件式 `输入源` 组合框，选中项为 `2 图像源1.图像`，`Enabled=True`，没有“当前来源不在可选图像声明中”或重新绑定提示；截图保留在 `.testdata/agent-desktop/blob-connected/blob-debug.png`。该证据闭合了输入来源控件在有效连线方案下的 UI/组装路径，也证明之前 `blob-min` 的禁用状态来自方案 `Connections=[]` 数据，不是通过 UI 猜测修复。Agent Desktop 进程已正常退出，用户方案文件未修改。

**2026-11-19 回归重跑记录**：尝试重新执行 ImageSource/Blob 目标回归时，测试宿主只输出构建与逐项启动计数，未产生最终汇总，不能把这次重跑当作新通过证据。项目记录仍以此前完整目标回归 22/22、Blob ROI 1/1 及 Release 构建 0 错误为准；未对产品代码追加无证据修改。

**2026-11-19 未准入工具方案重开差异定位**：隔离复现原“重开 11111 后画布无节点”失败后，保留外部打开返回值，实际状态为 `RejectedBeforeCommit`，错误为 `Required runtime is not registered: ContourMatch`。当前测试夹具按本轮范围只准入 ImageLoad 与 BlobAnalysis，`11111.solution` 含 19 个节点及尚未准入工具，候选被拒绝并保留当前会话符合未准入工具的安全准入边界。测试已改为断言拒绝状态、未提交替换和当前画布保持，并隔离通过 1/1。该项不修改产品运行时，也不把其他工具偷偷重新加载进本轮。
- 依据：`tests/SunEyeVision.UI.Tests/Session/EditorDebugSessionTests.cs`；用户方案静态解析显示 `ContourMatch` 等节点；错误来自正式 `PrepareOpen`/运行时注册检查。
- 重新 Release 构建通过，651 个既有警告、0 个错误。完整 EditorDebug 套件本次重跑未产生最终汇总，不能将其算作新的全量通过证据。

**2026-11-19 目标回归重跑完成**：释放前一次异常占用测试程序集的 `testhost` 后，使用 `--no-build --no-restore` 重跑 ImageSource/Blob 目标集，结果 22/22 通过、0 失败、0 跳过，日志为 `.testdata/blob-image-regression-after-contract-fix-20261119.log`。前一次 MSB3027 仅为测试宿主文件锁，已清理并复核。

**2026-11-19 完整 EditorDebug 套件复核**：清理测试宿主并使用已构建 Release 测试程序集重跑 `EditorDebugSessionTests`，结果 48/52 通过、4 失败、0 跳过。此前的未准入工具方案误报已消除。剩余四项为 Blob 空结果文本契约、结果行对虚拟化 `output.mono8` 行的视觉树断言、Blob 预览初始状态契约、以及重开后覆盖层测试的通讯服务排空。它们分别需要继续按 VM 证据核对测试契约或生命周期实现，不能通过放宽断言掩盖。

**2026-11-19 VM 参数行虚拟化证据**：`ParamListView.xaml` 启用 `VirtualizingPanel.IsVirtualizingWhenGrouping` 与 `VirtualizingStackPanel`。因此 `output.mono8` 可存在于正式声明和字段访问器中，但没有对应的当前视觉容器。测试已改为通过 `DeclaredFieldsView.Fields` 的正式访问器获取该字段行，保留对显隐和提交行为的检查，不改变原厂虚拟化。隔离执行随后推进到结果工作区顺序断言，说明原先的 `KeyNotFoundException` 属于过时视觉树观察入口，仍有后续结果顺序差异需按 VM 证据核对。

**2026-11-19 Blob 文本显示证据修正**：Blob 原厂资源 `tools/SunEyeVision.BlobAnalysis/Runtime/Resources/BlobDisplay.xml` 将 `circularity` 显示标签定义为“圆形度”。完整编辑器测试原先按“圆度”查找，导致无关的 `Sequence contains no matching element`。测试已按资源证据改为“圆形度”，隔离通过 1/1；未改变 Blob 运行或显示代码。

**2026-11-19 完整 EditorDebug 套件再次复核**：按 Blob 原厂标签和虚拟化字段访问器修正后重跑，结果推进到 49/52 通过、3 失败、0 跳过。剩余三项为结果输出顺序、Blob 预览初始运行状态、覆盖层测试中的通讯服务排空。前两项需要原厂运行结果/结果选择顺序证据，后一项需要继续核对正式关闭排空链。

**2026-11-19 EditorDebug 全量回归收口**：基于 VM 结果视图、参数虚拟化、Blob 本地化资源和窗口关闭排空证据，修正了 3 类过期测试契约。结果来源断言改为验证 Blob 二值图/掩膜与上游来源的证据顺序关系及节点集合，Blob 调试初始状态保留“已发布输入存在但当前节点尚未执行”的 `Rejected` 语义，结果视图跨背景切换保留原厂结果形状，重开方案前补齐通讯与相机运行时释放。没有修改 Blob 算法或添加无证据视觉特判。

- `EditorDebugSessionTests` 完整结果：52/52 通过，0 失败，0 跳过。日志：`.testdata/editor-debug-final-20261119.log`。
- ImageSource/Blob 目标回归仍为 22/22 通过，Blob ROI 保存重开 1/1 通过。
- 本轮测试程序集 Release 编译通过，0 错误。产品 Release 构建此前已通过，最终产物位于 `Release/SunEyeVision_v1.0.0/SunEyeVision`。
- 以上自动测试契约调整均已记录为原厂证据对齐，不代表新增 VM 动态证据；没有证据的行为仍按项目级规则标记“运行未确认”。
**2026-11-19 最终 Release 构建**：`dotnet build SunEyeVision.slnx --configuration Release --no-restore --verbosity minimal` 退出码 0，0 个错误、653 个既有警告。警告为既有 nullable、xUnit analyzer 和 NU1902 advisory，未出现新的编译错误。日志：`.testdata/release-build-final-20261119.log`。

**2026-11-19 同构结论纠正**：此前将 ImageSource/Blob 的目标回归、Release 构建和 Agent Desktop 可运行结果表述为“完成同构”不准确，现更正为“试点链路自动验证通过”。当前代码仍保留 `Runtime + Wpf` 双工程、`RuntimeToolEditorWindow`/`DeclaredSettingsView` 参数页链、工具级结果包装和 Blob 专用 `BlobUiComposition`，尚未满足原厂架构方案提出的平台统一参数存储、统一声明加载、统一 ROI 数据节点模板和统一组合结果视图等同构条件。后续审核按五道门分别记录，不再用自动测试通过替代架构同构结论。

**2026-11-19 验收范围调整**：用户确认当前阶段先不纳入人工验收。阶段门禁调整为原厂静态/配置证据 → VM/自研结构差异表 → 代码级迁移 → 自动化完整链路测试 → Release 构建。Agent Desktop 和人工视觉验收保留为后置门禁，不能用来替代前四项，也不作为当前阶段阻塞条件。

**2026-11-19 完整同构复核清单落地**：新增 `docs/collaboration/acceptance/完整同构复核清单与计划-20261119.html`。内容覆盖原厂证据等级、共享框架逐层复核、十个工具差异矩阵、参数/页面/资源/执行/持久化/生命周期自动测试、Release 门禁和后置人工验收。当前阶段不把人工验收作为阻塞条件，完整同构仍需先闭合共享框架与工具差异。

**2026-11-19 复核目标补充**：HTML 清单新增独立“总目标”章节，明确架构目标、十工具覆盖目标、功能链路目标、质量门禁和人工验收后置规则。完整同构的完成条件定义为共享框架差异闭合、十工具差异表闭合、自动化完整链路通过、Release 构建通过且未确认行为单独登记。

**2026-11-19 调整后目标结构补充**：复核 HTML 新增“调整后的目标结构”，明确工具工程层、共享框架层和基础控件层的职责、目录边界、依赖方向及禁止事项。目标是先统一三层架构，再把工具差异迁移到声明和有证据的专属适配器中，不再逐工具修补第二套 UI 链。

**2026-11-19 两份架构方案证据裁决**：核对 CodeBuddy 的《原厂架构逆向结论与优化方案》与现行复核清单。已确认 FeatureMatchCommonControl、ContourMatch/GrayMatch 专属 Dialog、MarkFind 独立 Control、模板列表和模型生命周期具备直接原厂静态证据。ParameterStore、单工程、7 个文件、XML 合并和 UI 归零属于迁移目标或推断，不能当作已证原厂事实。现行三层目标结构也仅是迁移组织方式，已在 HTML 中增加证据裁决和修订后的唯一目标结构。

**2026-11-19 同构目标纠偏**：确认“同构”目标必须保持原厂已取证的职责边界、数据流、组装关系、资源所有权和生命周期。此前按本方迁移便利提出的通用三层目录不能作为最终同构目标。工程数量、文件数量、类名和语言只有在不改变原厂职责的前提下允许变化。后续以原厂程序集边界和公共组件证据修订目标结构，禁止用功能等价或工程简化替代原厂架构。

**2026-11-19 发布单元同构纠正**：用户指出原厂 `IMVSBlobFindModu` 的 DLL/XML/ToolItemInfo 发布树与此前 `Tool.cs + Runtime/Wpf` 目标树不一致。现确认原厂发布树才是结构同构验收基线。源码可因语言和宿主拆分，但必须生成等价的算法单元、桥接单元、独立参数 XML、AlgorithmTab XML、映射 XML、Display XML 和 ToolItemInfo 发布边界。此前本方源码树只能作为内部实现映射，不能称为同构目标，也不能作为验收标准。

**2026-11-19 删除错误目标树**：从完整同构复核 HTML 中删除了 `Tool.cs + Parameters.cs + Schema.cs + Session.cs + Runtime/Wpf` 作为目标结构的旧段落。文档现在只保留原厂 DLL/XML/ToolItemInfo 发布单元、共享平台组件映射和有证据时的专属 Control 发布边界；旧源码树仅作为被否定的历史建议文字留痕。

**2026-11-19 共享架构首轮静态复核启动**：新增 `docs/evidence/architecture-conformance-matrix-20261119.json`。首轮确认 Blob 当前仍为 Runtime + Wpf 双工程、参数页使用 `BlobParameters.xml`、存在 BlobUiComposition 和工具级 ROI UI；原厂基线为算法单元、Cs 桥接单元、独立 Algorithm.xml、AlgorithmTab.xml、Cs.xml、Display.xml 和 ToolItemInfo.xml。共享层确认 `AlgorithmTabRoot`、`ROISelecter : NameValueItem`、`ModuleResultView`、FeatureMatchCommonControl 和 BaseWindow 证据路径。当前先闭合参数根、ROI 所有权和结果组合差异，不直接删除文件或合并 XML。

## 2026-11-19 Blob 基本参数页静态复核与顺序修复

- 已依据 VisionMaster 4.4.0 Build20250516 原厂 `IMVSBlobFindModuAlgorithmTab.xml` 与反编译 `Groups/Group.xml` 固定基本页顺序：`ImageSourceGroup` 位于 ROI 类别之前，ROI 组按 `OutputMaskImageGroup → InputMaskImageGroup → RoiSelectGroup → RoiType → InheritWayGroup2 → ExternROITypeGroup → AssistGroup`。
- 已新增静态证据清单：`docs/evidence/blob-basic-tab-static-diff-20261119.md` 与 `docs/evidence/blob-basic-tab-static-diff-20261119.json`。
- 已修复 `src/Plugin.Wpf/Fields/DeclaredFieldsView.cs` 的两个宿主接缝。显式索引的新输入分组按索引插入，正式输入注册后重放依赖输入字段的待处理锚定动作。该修复用于恢复原厂 ImageSource 首位和 ROI 组合的确定性组装。
- 本轮未启动 Agent Desktop，等待代码构建与自动化检查完成后再做人工运行复核。
- 回归测试尝试执行。构建进入测试前通过，但当前测试基线仍有未声明参数模板、FlatNative 测试缺少 `InputPlacements`、正式桌面目录未准入等失败，不能作为本次修复通过证据。
- 已知阻断：非自包含构建的 Release 载荷检查仍拒绝退役 `Plugin.SDK` 程序集。该问题来自当前仓库发布载荷基线，需单独清理后才能宣称 Release 通过。

**2026-11-19 目标驱动复核优化落地**

- G1 原厂结构目标：以 VM 4.4.0 Build20250516 的 `AlgorithmTab.xml`、`Group.xml` 和 Blob 发布单元为唯一静态基线，不以当前自研页面顺序反推原厂结构。
- G2 Blob 基本页目标：`ImageSourceGroup` 首位，随后按原厂顺序组装掩膜、ROI、继承、外部 ROI 和辅助组。静态门禁已覆盖顺序、关键字段、输入映射和宿主锚定重放。
- G3 宿主接缝目标：正式输入字段注册后必须重放依赖该字段的原厂组装动作，显式分组索引必须在目标页的确定位置生效，禁止创建第二套工具级视觉回退链。
- G4 自动验证目标：先运行静态证据门禁，再运行 Blob/ImageSource 目标回归和组件级 Release 编译。当前静态门禁 8/8 通过，Blob WPF 隔离 Release 编译 0 错误。
- G5 发布验证目标：完整产品 Release 需要在无运行进程占用且通过载荷检查后重建。当前完整构建仍受运行中的 `SunEyeVision.UI` 文件锁和退役 `Plugin.SDK` 载荷检查影响，暂不宣称产品 Release 已由本轮修复重新生成。
- G6 Agent Desktop 目标：只有 G1-G5 闭合后，使用新产物打开已连线的 Blob 方案，复核基本参数、运行参数、结果显示、关闭排空和资源释放。当前不把旧产物或未重新生成的产物截图当作本轮修复证据。

静态门禁脚本为 `scripts/verification/Verify-BlobBasicTabStatic.ps1`，结果为 `.testdata/blob-basic-tab-static-gate-20261119.json`。本阶段的完成条件是 G1-G4 全部通过，G5 的阻断项单独登记，G6 后置执行。

**2026-11-19 目标驱动门禁执行结果**

- G1-G3 已闭合。静态门禁 8/8 通过，确认原厂基本页首组、ROI 组顺序、InputMask 字段和本方输入锚定接缝。
- G4 已闭合。完整 `SunEyeVision.slnx` Release 构建退出码 0，0 个错误，5953 个既有警告。日志应以本次构建终端结果为准，警告主要来自既有 nullable、XML 文档、xUnit analyzer 和 NU1902 advisory。
- G5 已闭合。构建过程中用户已关闭旧程序，新的 `Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe` 产物完成生成，载荷封存成功。
- G6 已完成一次新产物 Agent Desktop 运行复核。打开最近方案 `blob-min` 后进入 `2 Blob分析1`，基本参数页的首项为 `输入源`，来源为 `2 图像源1.图像`，并观察到输出掩膜、ROI 区域类型、ROI 创建、形状、屏蔽区创建、位置修正和底栏命令。运行参数、结果显示页签也存在。程序随后由 Agent Desktop 优雅退出，未使用 computer-use。
- 运行复核只确认了 UI 组装和窗口生命周期入口。没有把截图中的控件状态、运行结果或未取证的动态行为扩展解释为原厂事实。

**2026-11-19 结构状态纠正**

- 当前 ImageSource 实际仍位于 `tools/SunEyeVision.ImageSource/Runtime` 与 `tools/SunEyeVision.ImageSource/Wpf`，Wpf 下继续包含 `Design`、`Views` 和 `ImageSourceEditorFactory.cs`。
- 该结构与原厂 `IMVS...Modu` 发布单元结构不一致。本轮只完成 Blob 基本参数页的静态顺序和输入锚定接缝修复，没有完成 ImageSource 工程结构、发布单元、XML 映射和共享组装链的同构迁移。
- 此前将局部 Blob 门禁通过描述为整体目标完成，范围判断不准确，现更正为“Blob 基本页局部目标完成”。后续必须先完成原厂 ImageSource 发布单元证据映射，再实施结构迁移和验证。

**2026-11-19 审定目标驱动完整落地计划**

- 已审定新的工程架构级目标。原厂发布单元、共享窗口/参数框架、基础控件资源、工具声明组装、执行和释放链必须按原厂证据逐层闭合。
- 已更新 `tasks/plan.md` 和 `tasks/todo.md`，新增 ImageSource/Blob 发布单元迁移、共享框架映射、十工具批量迁移、自动化、Release 和 Agent Desktop 四步门禁。
- 当前阶段仍为“框架与发布单元取证及迁移”，不能恢复轮廓匹配等局部 UI 补丁。ImageSource 当前 `Runtime + Wpf + Design + Views` 只记录为自研现状，不作为目标结构。
- 当前目标保持进行中，完成条件是结构矩阵、工具矩阵、自动链路、Release 和 Agent Desktop 全部闭合。此前 Blob 基本页修复只计入局部门禁。

**2026-11-19 ImageSource 原厂发布单元首轮取证**

- 已读取 VM 4.4.0 Build20250516 的 `Collection/ImageSourceModule` 发布目录，确认原厂包含算法 DLL、Control DLL、Cs DLL、独立 Algorithm/AlgorithmTab/Cs/Display XML、ToolItemInfo 和图标资源。
- 已提取原厂 `Tab_Basic Params` 的 25 项固定顺序，并确认原厂 Display XML 的两个图像对象和两类 Data Record 对象。
- 已建立 `docs/evidence/image-source-release-unit-diff-20261119.md` 与 `.json`。矩阵确认当前 ImageSource 仍是 `Runtime + Wpf` 双项目，参数和页签主要由 C# 声明动态组装，尚未形成原厂发布资产边界。
- 本轮没有修改 ImageSource 代码。下一步先继续取证 `ImageSourceModuleControl.dll` 的入口、资源和生命周期，再建立迁移映射，之后才允许结构代码变更。
- 已新增 `scripts/verification/Verify-ImageSourceReleaseUnitStatic.ps1`。静态门禁 14/14 通过，结果写入 `.testdata/image-source-release-unit-static-gate-20261119.json`。该门禁验证原厂发布文件、基本页 25 项顺序、Display 图像对象/Data Record 和当前 Runtime/Wpf 项目事实，不把当前结构判为同构。
- REA 用户字符串证据已确认原厂 Control 内嵌 `imagelistpanel.xaml`、`localimagelistview.xaml`，并出现 `IsShowLocalImageList`、`IsExpanded`、`IsSelectMode`、`运行全部`、`运行选中`。这些事实已加入 ImageSource 差异矩阵，原厂专属图像列表控件行为仍标记为运行未确认。
- 已将原厂 `Algorithm.xml`、`AlgorithmTab.xml`、`Cs.xml`、`Display.xml` 和 `ToolItemInfo.xml` 按原文件名复制到 `tools/SunEyeVision.ImageSource/Resources/OriginalReleaseUnit`，并加入带 SHA-256 的 `ImageSourceReleaseUnitManifest.json`。这些资产当前标记为 `evidence-locked-migration-input`，尚未接入运行时。
- ImageSource Runtime Release 编译通过，0 错误、20 个既有警告。该构建只证明证据资产引用不破坏现有项目，不能证明结构迁移完成。
- 原厂 `ImageSourceModuleControl.g.resources` 已枚举出 `localimagelistview.baml`、`imagelistpanel.baml` 和 `datatemplate/paramusercontroldatatemplate.baml` 三个 BAML 资源，并将字节摘要写入 ImageSource 差异矩阵。当前确认的是资源身份和发布边界，BAML 内部模板语义仍标记为未反编译。
- 已新增 `ImageSourceFieldMapping.json`，按原厂基本页 25 项建立当前字段身份映射。`CurrentImagePath` 明确标记为组合映射，原厂图像列表选择语义、刷新/触发事件时序和 BAML 可见性布局未被伪造为已确认。
- REA 方法签名证据已补齐。原厂 `ImageListPanel` 有 `ChangeShadowDataSource`，`LocalImageListView` 有 `IsExpanded`、`IsSelectMode`、`Root`、`ImageModes`、`AutoPlay`、`Interval`、`CurrentImagePath`、选择/模式变更和 `ClearParamListView`，`LocalImageList` 有 `IsShowLocalImageList` 和 `ImageAlgorithmTabRoot`。当前自研 WPF 入口没有等价专属控件边界，仍禁止用普通字段行模拟这些职责。

### ImageSource 审定增量（2026-11-19）

- [x] 固定原厂单一 Tab 内三个 Category 及 25 项字段顺序
- [x] 固定 Algorithm-only 参数 Interval、ExecuteType、SetCameraParamWaitTime，不误加入可见页
- [x] 自研可见字段顺序按原厂索引调整
- [x] 移除原厂 Tab 未声明的自研 CameraTimeout 视觉行，保留运行时字段并标记待证
- [x] 新增 Verify-ImageSourceMappingStatic.ps1，静态门禁通过
- [ ] 原厂相机枚举控件、图像列表选择控件和 ButtonSelecter 的同版本运行行为仍待证据闭合

### 目标架构迁移增量（2026-11-19）

- [x] ImageSource 原厂发布单元进入证据锁定目录
- [x] Blob 原厂发布单元进入证据锁定目录
- [x] 两个 Runtime 工程链接原厂证据资产但不复制到发布输出
- [x] ImageSource 主参数链收回 ShowImageName，消除主参数/显示参数双注册
- [x] ImageSource 图标键按 ToolItemInfo.xml 对齐
- [x] 新增 ImageSource/Blob 发布单元结构静态门禁
- [ ] 将原厂 Control 专属入口迁移为有证据的 WPF 接缝
- [ ] 将 Blob AlgorithmTab 全部 Category 与当前参数根完成代码级逐项映射
- [ ] 结构迁移收口后再运行一次完整测试和 Agent Desktop

### Blob 目标结构差异审定（2026-11-19）

原厂 Blob 静态 XML 已纳入 `tools/SunEyeVision.BlobAnalysis/Resources/OriginalReleaseUnit`。原厂声明四个 Category：`Tab_ROI Area`、`Tab_Run Params`、`Tab_Check Result`、`Tab_Image Display`。其中 ROI Category 明确包含 `ImageSourceGroup`、`OutputMaskImageGroup`、`InputMaskImageGroup`、`RoiSelectGroup`、`InheritWayGroup`、`ExternROITypeGroup`、`AssistGroup` 外部组装链接，底部还有 `TextDisplayGroup` 和 `BottomCommandGroup`。

自研 Blob 参数元数据现已为五个参数组写入 `vmCategory`，并由 `BlobParameterDefinition` 读取。Category 身份门禁通过，但外部组装链接仍未闭合，`Compare-BlobArchitectureStatic.ps1` 明确输出 `categoryPass=true`、`externalGroupLinksClosed=false`、`pass=false`。因此当前 Blob 仍不能称为完整同构，下一步必须先迁移这些原厂 GroupLinkItem 的声明和消费者映射。

Blob 新增 `BlobGroupLinkMapping.json`，记录原厂 9 个 GroupLinkItem 到当前消费者的静态映射和未确认项。映射只证明入口和消费者关系，不证明运行行为；所有条目仍保留 `runtime-unconfirmed` 或 `unconfirmed` 状态，直到有同版本运行证据。

Blob 新增 `BlobGroupLinkMapping.json`，记录原厂 9 个 GroupLinkItem 到当前消费者的静态映射和未确认项。映射只证明入口和消费者关系，不证明运行行为；所有条目仍保留 `runtime-unconfirmed` 或 `unconfirmed` 状态，直到有同版本运行证据。

### 共享参数合同增量（2026-11-19）

`FieldLayout` 新增可选 `VmCategory` 元数据字段。它不改变本地化 Page/Section 文本，只保存原厂 Category 身份，供静态组装审计使用。Blob 参数绑定已从证据锁定的 BlobParameters.xml 读取并写入 `VmCategory`。这一步是代码级结构迁移，当前不代表外部 GroupLinkItem 已经完成。
## 2026-11-19 结构同构纠偏

- 复核《完整同构复核清单与计划》后确认，原厂同构验收基线是工具发布单元，不是源码目录树。
- 当前已完成的 `SunEyeVision.Tool.BlobAnalysis*`、`SunEyeVision.Tool.ImageSource*` 目录和工程文件迁移，只改变了源码路径与工程文件名，仍保留 Runtime/Wpf 两个产品组件。
- 该状态不能称为完成同构。Blob 尚未按目标发布树生成 `Algorithm.dll`、`Bridge.dll` 及独立的 `Algorithm.xml`、`AlgorithmTab.xml`、`Bridge.xml`、`Display.xml` 消费链。
- 原厂静态证据只证明单一部署目录包含多个职责程序集和 XML 文件，不能据此直接把现有运行时和 WPF 源码强行合并成一个程序集。下一步必须先建立职责映射和单发布单元打包门禁，再迁移代码和组件注册。
- Blob 的算法职责由本方运行程序集实现并引用第三方算法库，不能凭原厂文件名强行生成 `Algorithm.dll`。同构验收改为职责、声明、依赖和加载链等价，第三方算法库依赖作为明确差异登记。
- 当前也不要求单独生成 `Bridge.dll`。Blob 的参数、结果、ROI 和宿主接缝先按现有运行程序集内的桥接职责闭合，只有原厂证据或实际加载链要求独立程序集时才拆分。
- 当前 Release 构建已通过，677 个既有警告、0 个错误。该结果只证明当前源码结构可编译，不代表发布单元同构完成。


- 算法实现层与类型适配层分开。算法层允许直接使用 HALCON/OpenCvSharp 处理方法，适配层只负责类型转换、资源所有权、初始化、释放和异常边界。工具入口只保留算法调用接缝。


- 2026-11-20：根据原厂安装目录的实际工具发布单元，新增《原厂工具工程结构分析与本方目标结构-20261120.md》。裁决原厂 DLL/XML 是职责发布边界，本方采用单项目、Algorithms/Parameters/Results/Editor/Resources 分区。公共 UI 组装和生命周期沿已验证 Plugin.Wpf 链路复用，第三方算法仅经适配层接入。算法黄金样本为空的工具保持 unconfirmed，不以构建或 UI 烟测宣称等价。

- 2026-11-20 PixelCount 样板迁移：完成单项目入口、参数/页签/工具/结果/工具箱 XML 资源嵌入，参数由 DeclaredParameterSchema 驱动，输入/ROI/掩膜端口由声明映射。PixelCount 专项 36/36，ControlNamingProbe 通过，产品 UI Release 0 错误，正式封包通过。封包中 Runtime/Editor 两个注册均来自同一 PixelCount Runtime 程序集。完整解决方案包含测试工程，会污染正式输出，样板验证使用 UI 项目入口。算法黄金样本为空，等价状态仍为 unconfirmed。
- 2026-11-20：按 PixelCount 样板将 Blob 物理源码迁移到 Algorithms/Parameters/Results/Editor/Resources，单项目 Release 编译 0 错误。
- 2026-11-20 PixelCount 调试窗口修复：原厂输入-only 基本参数页不应要求 SettingsSchema 存在持久化字段。调整 NativeInputPlacementMap 后，正式 Release Agent Desktop 隔离验证成功打开像素统计并显示三页签，正常关闭。
- 2026-11-20 PixelCount 调试窗口修复：原厂输入-only 基本参数页不应要求 SettingsSchema 存在持久化字段。调整 NativeInputPlacementMap 后，正式 Release Agent Desktop 隔离验证成功打开像素统计并显示三页签，正常关闭。
- 2026-11-20 工具生产流水线固化为项目技能 `.opencode/skills/tool-production/`：状态机、三层职责、算法等价层、单项目结构、六阶段失败归因、G0-G6 门禁命令和迭代报告模板。自带 `Check-ToolManifest.ps1` 清单自检，已对 12 个工具清单执行通过（12 个工具均无 algorithm 块，按 AlgorithmUnconfirmed 提示）。技能只固化流程与门禁，不新增或修改产品代码。
**2026-11-20 DivideImage 字段映射异常闭合**：划片拆分切换 ROI「按参数」时，工具层曾把 `center-x/width` 等裸成员名传给公共 ROI 组装器，实际 Schema 声明的是 `input.roi.rectangle.*` 完整叶字段，导致 `DeclaredInputFieldsView.ComposeInputEditor` 抛出“输入未声明”。已按 Blob 已验证的 `RoiInheritanceMaterializer.LeafId` 边界修正 DivideImage，并同步修正同批 PixelCount、IntensityMeasure 的同类映射。`Check-ToolManifest` 通过，DivideImage Release 专项测试 24/24 通过，Release 重新构建封存，Agent Desktop 已打开/关闭划片拆分窗口并确认基本/运行页可见。点击“确定”而未配置输入时的参数校验提示属于正常前置校验。算法边界取整、空 ROI、分片顺序和 HALCON 与原厂数值等价仍标记未确认。详细记录见 `docs/evidence/divideimage-single-project-review-20261120.md`。本轮只把可复用的正式叶字段经验沉淀进 `tool-production` 技能，没有扩大公共框架范围。

**2026-11-20 划片拆分算法证据对齐**：UI 人工复核无新增问题后，转做算法与非 UI 链路。对照原厂 `ProcessAlg@0x18001f510` 反编译伪代码，确认旧本方实现使用“向上取整+末片贴边”公式，与原厂直接输出浮点片宽/高和步进公式不一致。`DivideImageTiling` 已改为原厂公式，输出保持浮点几何，补充非整除、50% 重叠、行优先和窄图边界测试。DivideImage 专项 Release 测试 24/24 通过。最终 Release 构建 0 错误、0 警告，封存 `A184E9D15F77C3C20DA05A85D2A8AD737838B5785E155523AA03DDBCB92BFCE1`，Agent Desktop 启动进入起始页并确认进程退出。仍未确认像素采样取整、空 ROI、Full/Box 分支、JudgmentRoi/RSIntersection、FIXTURE 位置修正参与方式和算法数值等价，不能提前闭口。
# 2026-11-20 Blob 标签分析算法原厂静态复核

- REA 已对 `IMVSBlobFindLabelsModu.dll` 完成 Ghidra 12.2 静态反编译，原厂 DLL SHA-256 为 `cc9b5b7f75319affb3176b5fa36d0cd16ca1671bf38bd3aa07797d3d087a8e01`。
- 已确认原厂调用顺序 `GetProcessInput → ProcessAlg → LimitJudge → ProcessOutput`。`ProcessAlg` 委托 `MVDMultiCategoryBlobFindCpp.dll`，`LimitJudge` 写入逐 Blob 状态，`ProcessOutput` 发布 BlobNum、TotalArea、几何特征、BinaryImage、BlobImage、ContourInfo、BlobStatus 等输出。
- 已确认 `SetParam` 真实接受 `OverlapRatio`、`MaxOutPixelNumEnable`、`SelectByOutPixel`、`MaxOutPixelNum`、`LabelWiseAreaSelectEnable` 和 `ThresholdArea`。本方对未取得动态语义的参数继续稳定拒绝，不用 HALCON 推测冒充原厂。
- 本方 HALCON 链、单项目注册、30/30 BlobLabels 测试和 Release 构建通过。算法数值等价、逐 Blob 判定分层、ClassLabel/LabelValue、外部算法实例和输出资源释放时序仍未确认。
- 本轮完整 Release 封存再次通过，最新载荷封存哈希为 `423C0BACCBEE835254D9A37AFE1A41D2F990EB9FD2267A66FBD4A5576958D535`，编译器 0 个错误。
- 已定位同安装件 SDK Runtime 的 `MVDMultiCategoryBlobFindCpp.dll` 并完成 REA 取证，PDB 未加载且内部数值语义未闭合，因此 BlobLabels 算法数值等价仍保持未确认。
- 最新 Release 已由 Agent Desktop 启动到 `SunEyeVision · 起始页` 并确认进程可关闭。未把自动打开 BlobLabels 调试窗口记为通过。
- 证据包：[bloblabels-rea-algorithm-evidence-20261120.json](D:/MyWork/SunEyeVision_V2/docs/evidence/bloblabels-rea-algorithm-evidence-20261120.json)。完整复核：[bloblabels-single-project-review-20261120.md](D:/MyWork/SunEyeVision_V2/docs/evidence/bloblabels-single-project-review-20261120.md)。

**2026-11-20 BlobLabels 算法与外部库调用边界复核**：本轮 UI 未改，复核集中在算法、结果投影、参数回执、资源释放和发布封存。按用户要求暂停 UI 修改，沿原厂算法链复核参数映射、结果投影、资源释放和 HALCON 实现。原厂模块 `IMVSBlobFindLabelsModu.dll` 的 `Process → ProcessAlg → LimitJudge → ProcessOutput` 已由 REA 静态闭合。进一步定位到同安装件 SDK Runtime 的 `C:\Program Files\VisionMaster4.4.0\MVDAlgorithmSDK\Runtime\x64\MVDMultiCategoryBlobFindCpp.dll`，SHA-256 为 `7627e0698acef65892bdd56c199dcff6c42e893aba7718b394f3d2485754b76d`。外部 DLL 证据已导出 `docs/evidence/bloblabels-external-algorithm-evidence-20261120.json`，确认实例创建/销毁、`MVD_Algorithm_CreateHandle(0x139)`、`MVD_Algorithm_Process(0x139)`、结果图像输出，以及 working/result memory release 导入边界。由于 PDB 未加载且内部函数主要为 `FUN_...`，阈值合并、特征计算、OverlapRatio、MaxOutPixelNum、ClassLabel/Score 和逐值数值等价仍未确认，未据此改算法代码。当前本方 HALCON 链只可记录为结构对应、数值等价未闭合。已修正 `MinBoudingRect.Angle` 与顶层 `BlobAngle` 的映射分离，前者来自 `SmallestRectangle2`，后者来自 `OrientationRegion`。BlobLabels 静态测试仍为 30/30，最新完整 Release 封存为 0 错误，最新封存哈希为 `423C0BACCBEE835254D9A37AFE1A41D2F990EB9FD2267A66FBD4A5576958D535`，发布清单复核通过。Agent Desktop 已验证 Release 启动/关闭；用户已人工确认 UI。**



**2026-11-20 轮廓匹配单项目与算法链复核**：轮廓匹配已从 Runtime/Wpf 双项目迁移为 `tools/SunEyeVision.ContourMatch/SunEyeVision.Tool.ContourMatch.csproj`，解决方案、产品注册、测试引用和发布清单均已切换到新项目，Release 主载荷删除旧 `SunEyeVision.Tool.ContourMatch.Wpf.dll`。轮廓匹配专项测试 28/28 通过，`Check-ToolManifest.ps1 -ToolId ContourMatch` 通过，`ControlNamingProbe` 的 12 工具目录准入和轮廓匹配四个页签/模板面板接缝通过。RuntimeEditorProductionProbe 在目录准入后继续执行 ImageSource→Blob 长链路无输出，已主动终止，随后已重新封存 Release，当前载荷哈希 `E7BD3AC678F97D5ADC8C9C9C85CCFF2334E027DC874C81D2CAC4549FA0DFF271`。算法链已按代码事实复核为图像/ROI/Fixture → HALCON 搜索 → 轮廓与外接框 → 排序 → 限位判定 → 结果资源 → 释放。原厂 Process/ProcessAlg/ProcessOutput、模板训练和参数提供者证据已登记，但原厂算法数值、模型编码、重叠率/超时/延拓、多模板、失败清理和结果逐字段等价没有黄金样例，继续标记为未确认，不能宣称算法闭口。详见 `docs/evidence/contourmatch-single-project-review-20261120.md`。

**2026-11-21 灰度匹配单项目与算法链复核**：灰度匹配已从 Runtime/Wpf 双项目迁移为 `tools/SunEyeVision.GrayMatch/SunEyeVision.Tool.GrayMatch.csproj`，解决方案、产品注册、测试引用和发布清单均切换到新项目，旧 `SunEyeVision.Tool.GrayMatch.Wpf.dll` 已从主 Release 载荷移除。灰度匹配专项测试 30/30 通过，`Check-ToolManifest.ps1 -ToolId GrayMatch` 通过，ControlNamingProbe 的完整产品目录和灰度匹配四个页签/模板面板接缝通过。迁移时补齐模板序列化缺失的 `System.IO` 引用，算法链按代码事实复核为图像/ROI/Fixture → ReduceDomain → HALCON `FindNccModel` → 姿态矩形 → 排序 → 限位判定 → 结果资源 → 释放。原厂 VA 工具的模板训练、ROI 约束、掩膜/固定点、模型编码和数值算法仍没有同版本黄金样本，继续标记为未确认。最后一次探针构建后已重新封存，载荷哈希 `9905BB6EAD9DD68B08655D86B698914C66A36CFD9276E8C2E67C8253B6613003`。详见 `docs/evidence/graymatch-single-project-review-20261121.md`。

**2026-11-21 图形定位算法与单项目复核**：图形定位已从 Runtime+Wpf 双项目合并到 `tools/SunEyeVision.MarkFind/SunEyeVision.Tool.MarkFind.csproj`，解决方案、产品注册、测试引用和 Release 载荷均切换到共享程序集。按原厂静态证据复核了模型校验、图像/ROI/Fixture、HALCON 形状模板搜索、结果几何、排序/判定和资源释放链。专项测试 21/21 通过，ControlNamingProbe 确认「基本参数/特征模型/运行参数/结果显示」和特征模型面板物化，发布清单通过。原厂模型编码、训练参数、金字塔/阈值/投影/Mark 类型、掩膜、结果状态和失败清理没有同版本黄金样本，算法数值等价保持未确认。最终 Release 封存哈希 `8957452B278792256E06A91BEA2B86949EEA3F85D4CD4DF363959359AF294D2B`。证据：`docs/evidence/markfind-single-project-review-20261121.md`。

**2026-11-21 条件分支算法与单项目复核**：条件分支已从 Runtime+Wpf 双项目合并到 `tools/SunEyeVision.If/SunEyeVision.Tool.If.csproj`，旧项目定义和旧 Wpf 发布残留已移除。按原厂 IfBranchModule 静态证据复核了动态条件文本、来源/范围字段、类型范围、分支判定、BranchMatch、诊断和释放链。当前实现仍是四个固定槽位和闭区间比较，专项测试 29/29 通过，发布清单通过，ControlNamingProbe 确认基本参数页和动态条件面板物化。原厂动态增删行、int/float 切换、OK/NG、RunModuleId、动态输出端口、区间端点和多命中运行语义没有黄金样本，算法等价保持未确认。最终 Release 封存哈希 `8957452B278792256E06A91BEA2B86949EEA3F85D4CD4DF363959359AF294D2B`。证据：`docs/evidence/if-single-project-review-20261121.md`。


**2026-11-21 六工具算法与发布链复核收口**：在 UI 已由用户确认可用的基础上，继续复核 DivideImage、ContourMatch、GrayMatch、MarkFind、BlobLabels、If 的算法入口、参数校验、输入/ROI/Fixture 接缝、结果投影、取消/异常和资源释放。六个工具的发布清单门禁均通过，Release 中仅保留各自 `*.Runtime.dll`，未发现活动解决方案、产品注册、测试项目或工具源文件引用旧 Runtime/Wpf 项目。图形定位专项 21/21、条件分支专项 29/29 通过，ControlNamingProbe 确认六个工具均能在完整产品目录中准入，模板/模型/动态条件面板均物化。全量 `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false` 通过，0 错误、3108 个既有警告；最终封存哈希 `8957452B278792256E06A91BEA2B86949EEA3F85D4CD4DF363959359AF294D2B`；Release 启动并退出验证通过。算法等价仍统一保持 `unconfirmed`，原因是原厂模型/算法没有同版本黄金样本，不能把 HALCON 形状/NCC/Blob 或固定槽位条件判定当作原厂数值等价。逐工具差异和未确认项见 `docs/evidence/*-single-project-review-*.md` 与 `docs/evidence/tool-batch-manifest-20261120.json`。

**2026-11-21 六工具 Agent Desktop 精确节点批量验证**：使用隔离方案和精确节点名重新执行六个目标工具的 Release 窗口准入/关闭探针。DivideImage、BlobLabels、ContourMatch、GrayMatch、MarkFind、If 均通过，结果见 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json` 和 `Release/_validation/AgentDesktopToolReview/batch-all/batch-summary-exact.json`。早先使用带编号节点名的批次因节点名不匹配全部失败，该批次不作为结论，精确名称批次为权威结果。探针只确认窗口打开/关闭和资源生命周期入口，算法数值等价仍按各工具证据保持 `unconfirmed`。

**2026-11-21 六工具算法复核后的 Release 重建**：清理 ControlNamingProbe 和 RuntimeEditorProductionProbe 中重复的 If 单项目引用后，重新执行完整 Release 构建，0 错误，现有警告 5729 条。重新封存哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`，六个目标工具的单项目运行程序集仍全部进入载荷，旧 Wpf DLL 未进入主 Release。六个工具的算法链、参数校验、ROI/Fixture 接缝、结果投影、取消/异常和资源释放已按代码事实与原厂静态证据复核，专项测试和 Agent Desktop 窗口探针通过。原厂算法内部数值、模型编码、边界和结果逐字段等价仍没有同版本黄金样本，继续保持 `unconfirmed`，不把 HALCON 实现或 UI 通过替代算法证据。当前哈希已同步到六份单项目复核文档、历史 If/MarkFind 复核 JSON 和批量运行证据。

本次重建后的产品测试集复核通过：ImageProcessing 60/60、Inspection 109/109、LogicData 90/90。Release 哈希校验、启动和退出也通过。上述测试证明本方参数、结果、显示声明和算法调用接缝没有回归，不证明 HALCON 与原厂算法的数值等价。

## 2026-11-21 UI与算法重新复核

昨天完成的是共享参数页组装、专项声明测试和部分窗口开关验证。BlobLabels与PixelCount的算法只完成本方基础运行测试，未完成与VM固定输入输出对照。当前按全方位门禁重新复核，第一检查点为BlobLabels、PixelCount、ImageSource。证据基线见 docs/evidence/ui-algorithm-reaudit-20261121.json。
