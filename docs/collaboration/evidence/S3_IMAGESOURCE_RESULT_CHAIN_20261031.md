# 图像源模块结果整链对照与实施边界

**最新决策：先完整逆向分析，再整体迁移。** 用户已明确停止在当前实现上逐项补丁；本文件的早期“拟保留平铺端口并扩展分组声明”仅为未经批准的设计探索，已撤回相应未验证试验，不能作为最终方案。已联系逆向负责人集中提供原件完整证据和缺口；须先依据原件的数据/宿主完整链重新设计，并评估现有架构、消费者及资源所有权后集中实施。

## 逆向负责人首版证据核收

已只读 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源与模块结果树整体迁移证据-20261031.md`，SHA256 为 `F9E112A6DE3215B53F99C92BFD421AF47C715C0267A48F4B2C51CF955A89801B`，与提供值一致。首版区分原件静态、原 SDK 有限样本、本地现状及未知，不能当完整状态覆盖。原 XML `analysis/full-audit/image-source-audit/ImageSourceModule.xml` 已独立核读：`ModuStatus`、`ModuRunTime`、`ImageData` 和 `ImageDataMono8` 两组，每组四叶（图像、宽、高、格式），另有路径、帧、丢帧、失败计数及 SN；并非凭标签推测。原 `ModuleResultView.xaml` 通过 `VmModule.Outputs` 和 `VmIO.Children` 构树，`RefreshResultTree` 刷新叶值而非每次重建树；未运行由当前模块建立声明树。

原 MONO8 样本为 17301505，RGB24 相机样本为 35127316；UI 配置枚举与输出编码不同，额外 Mono8 可出现长度 0 但格式字段仍为 MONO8，不能以格式判断资源有效。参数 XML、算法 XML 的 OutMono8/StitchEnable/AutoStop 默认值相互冲突，不能任取一份；图像所有权、所有失败/切换及完整默认/持久化仍待证。已请求负责人继续集中补证。

负责人报告中的 SunEye `ImageSourceOutput.cs` 快照 SHA 为 `FFEDE953...`，本工作区当前文件原始字节 SHA 为 `6BBF6EEE3A1D52E264E7324BAD6C568002238B43C1100058347ED7B45772D3CB`，二者不能当作同一字节版本；当前源码已独立读取，设置文件 SHA 与其报告一致。对比以本 worktree 实际文件为准，差异尚未归因，不能据此说内容已不同或一致。

## 目标和证据

补充本地逆向项目的具体输出证据：`D:/MyWork/REA逆向测试/src/VisionPlugins.ImageSource/ImageSourcePlugin.cs:10` 声明 `ModuStatus`、`ModuRunTime`、`Image`、`Width`、`Height`、`PixelFormat` 及 `ImageMono8`、`WidthMono8`、`HeightMono8`、`PixelFormatMono8` 等独立输出。`docs/architecture-recovery/tool-rendering-entry-audit.resources/ImageSourceModuleDisplay.xml` 又明确将 Image/Width/Height/PixelFormat 和对应 Mono8 字段组装为两个图像对象。两者与用户 VM 结果树截图相互印证，但本地逆向项目仍属恢复实现，不能单凭此文件断言 VM 原生完整契约或全部状态；产品层级映射仍须通过正式工具声明及现行资源所有权承载，不能按输出 ID 字符串猜测。

用户 VM 截图显示模块根行、模块状态、耗时、图像与灰度图像分组及其图像数据、宽度、高度和像素格式等成员。原件 `ModuleResultView.xaml` 以模块 Outputs 和 IO Children 构造树；`VM.Core/VmIO.cs` 的 Children 是实际 IO 集合。`iMVS_6000PlatformSDKCS/ImvsSdkPFDefine.cs` 的 `IMVS_PF_IMAGE_INFO` 含图像数据、长度、宽高和像素格式，`IMVS_PF_IMAGESOURCEMODULE_INFO` 含图像信息和相机统计；截图可证明展示层级，SDK 结构不能单独证明所有 UI 行、灰度图或绑定行为。

## 当前链及差异

`ImageSourceTool.ResultProjection` 声明图像、灰度图像、宽、高、格式等中文名称；`ImageSourceSession` 按真实源产生图像与灰度图资源、原图宽高、格式和来源等 11 个平铺输出。`DeclaredOutputSnapshot` 冻结 Schema、字段、状态、实际耗时 `ExecutionTimeMs` 与 reader；不得用图像视口替代输出身份或自行产生耗时。

`MainWindowResultPublication.PresentSnapshot` → `ToolResultViews.FromSnapshot` → `ResultPresentationPresenter` → `PropertyListView` 读取快照 Fields；本工作区先前未提交修改已在 Schema 一致且投影校验通过时将显示名称接入该链，缺失或不匹配时仍使用已有字段名/端口 ID。这只是名称对齐，不证明层级、资源与生命周期已迁移。图像来源选择器已经按输出 Schema 指纹和投影校验复用投影列名。结果呈现器 `ValueText` 对资源统一写“资源待接入”，但工具和图像视口已有真实图像资源；该文本只反映当前树的格式化能力，不能判定图像没有加载。

## 映射和限制

| VM 展示 | 现有正式来源 | 处理约束 |
|---|---|---|
| 模块名称、状态 | 当前被接纳节点、快照 Status | 按真实执行状态映射；未运行、失败与成功分开 |
| 耗时 | 同一快照 `ExecutionTimeMs` | 不从最新状态栏或其他运行借值；无快照时不伪造 |
| 图像、灰度图像 | 对应已声明的两个图像资源端口 | 保留原端口身份和 reader 所有权，不复制像素、不强制解码 |
| 图像宽高 | 原图宽高输出和图像资源 layout | 需按实际资源确认灰度图尺寸，不以 UI 位置猜值 |
| 像素格式 | 图像资源 layout 和声明的格式文本 | 原 SDK 有限样本已确认 MONO8 为 17301505、RGB24 为 35127316；与本方 BGR/BGRA 布局不能直接等同，其余格式不得编造 |
| 图片数据 | 实际 image resource | 使用原件视觉语义的资源占位；不得显示指针或伪造数值 |
| 扩展来源、文件、帧号等 | 图像源正式端口 | 是否出现在 VM 结果树需原件可见性/工具元数据证据，不擅自删除执行输出 |

现有 `ResultProjectionDescriptor` 列指定输出身份和名称，但没有显式 UI 分组层级；`DeclaredOutputField.Group` 存在于快照，是否由新图像源正式声明可靠生产尚未闭合。成员级可见性、状态/耗时作为可选行的通用声明、VM 像素格式映射和原件资源行文字尚未证实；不可在宿主用 ImageLoad 特判拼一个不可维护的第二套结果模型，也不扩公共合同绕过当前约束。

## 早期实施探索（已撤回，非最终设计）

2026-10-31 用户再次明确要求完整迁移图像与灰度图分组、子字段和资源显示。当时曾探索保留已有平铺输出的正式端口身份，以工具拥有的声明描述显示分组（分组是投影，不是第二套输出）；新增灰度宽、高及格式的真实工具输出，均从同一次灰度图资源读取，不能指向原图数值冒充灰度值。图像和灰度图的数据子行使用各自已有资源端口，宽高/格式子行使用各自真实输出端口；模块状态及耗时继续取同一 publication/快照。用于分组的描述必须是通用、显式声明，不能按图像源 ID 或中文名称推断；未声明分组的其他工具继续按其声明显示。资源数据行只显示原件可见状态，不泄露底层指针或生成假图；继续由 publication 持有 reader。生成者、图像选择器、结果树、绑定选择及测试消费者同批适配。VM MONO8 编码有原件常量；本机 BGR/BGRA 与 VM RGB24 并非同一内存布局，未知数值不得编造。跨方案保存、复制、撤销及原有端口保持单一真实身份；执行/退场过程中以同一 publication 保护，避免灰度和原图来自不同运行。用户已关闭验收程序；源码、构建和实际运行尚待实施和验证。

1. 将一致 Schema 的工具显示投影接入结果 publication，要求未运行和运行时一致；保留正式 PortId、成员路径、snapshot 可见性和其他工具行为，冲突/不匹配时安全退回快照正式名称。
2. 沿正式工具声明确认分组、子项与来源，优先复用已存在 Group 和资源布局；无法由现有合同明确表达的层级先保留依赖并禁止虚假绑定，不将视觉近似当完整迁移。
3. 状态和耗时只取同一快照，资源行只反映实际可用状态。绑定使用 `OutputReference`，不得用中文或行顺序做身份；节点/流程切换和旧结果退场需清理 UI 与 reader。
4. 同批覆盖声明态、成功/失败/取消、原图和灰度图、名称冲突、不同 Schema、切换/重入/释放以及其他工具不回归；在正式 Release 构建与真实宿主回归后，启动同版交用户对照外观。VS 设计器与主线集成分别记录，未确认不称完成。

本设计记录当前证据和缺口；尚无本批源码实施、构建、测试或人工验收。S4 暂停，全局变量人工验收按用户决定暂不要求。

## 补证矩阵核收与尚需完成的逆向

逆向负责人补证：`D:/MyWork/REA逆向测试/docs/joint-audit/图像源与结果树迁移补证矩阵-20261031.md`，核验 SHA256 `2A146D0E6691F76110482472C7935277C8E8399CD0D2B279E35D591DFC2BD3CC`。其 25 项参数来自原算法和界面 XML 的静态声明；初始可见不等于执行条件之后的真实界面，空默认值不等于零。OutMono8、StitchEnable、AutoStop 的算法/UI 声明不同，必须继续追初始化和持久化，不能自行确定。

| 功能链 | 已有原件证据 | 本工作区可确认现状 | 尚待证据或整体决策 |
| --- | --- | --- | --- |
| 配置及条件 | 25 项算法/XML 参数与 trigger，包含本地、相机和 SDK | ImageSourceFields、ImageSourceSettings 和参数编辑器已有部分字段 | 逐项真实默认、条件、读写、保存恢复及相机/SDK 分支 |
| 输出声明和层级 | VmModule.Outputs 经 SDK 动态 IO 递归构造 VmIO.Children；两组 IMAGE 各含四叶 | ImageSourceOutput 11 个平铺输出，ResultProjection 是平铺列 | 原动态输出失败/重试和成员生命周期；正式身份与层级适配 |
| 未运行与刷新 | 原模板默认展开，静态声明和叶 DisplayValue 刷新分离 | 声明恢复、状态/耗时已有未提交实现，平铺及资源文案问题仍在 | 真实 GUI 各状态、更新/选择/切换及重开顺序 |
| 原图与灰度资源 | 原 SDK 样本中灰度数据可为空，像素格式仍有值 | 同时有原图/灰度图端口及资源 reader；当前树显示资源待接入 | 图像 buffer 拷贝、借用和释放；格式与颜色通道语义 |
| 变量与可见性 | 原模板整行可见和第三列可见使用不同 converter 分支 | 结果树、绑定和快照消费分布在 UI/工具合同 | 成员级可见性、正式绑定身份、删除撤销及退出/异步竞态 |
| 宿主和生命周期 | 原 ModuleDisplayDecorator 有订阅、计时器、解订阅；叶值通知不等同重建树 | 当前 publication、presenter 和 reader 管理结果快照 | 对照完整调用链，确认替换边界、所有消费者与资源交接 |

补证明确尚无完整原厂 GUI 状态时序、25 项实际运行/保存和 native 图像所有权闭环；已要求逆向负责人继续沿 SDK 查询、setter/getter、资源释放和保存加载取证。没有证据的行为保持未知；用户要求的完整逆向尚未完成，以上不是实施批准或测试结论。
### 原生参数、缓冲与保存补证

已核验 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源原生参数缓冲与保存覆盖矩阵-20261031.md` SHA256 `5DE7937DFB52FB2BDDDF864F98B616370571E9EA7C143075974A492BD8E8C039`。25 项声明逐项列出 GetParam/SetParam 精确名称命中；4 个输入订阅名未在这两处直接命中，必须沿 Process 和输入订阅链追踪，名称命中不等于各分支已验证。

原托管回调路径涉及共享内存、原指针、复制和可回绕的静态非托管池；不能假设返回指针永久独占。发送端、延迟消费者及关闭时在途资源所有权未闭合。原 SaveModuleData 有附件复制和动态输入输出保存分支，LoadModuleData 有建目录及文件写入分支，具体模式与失败路径仍待核实；不能将其简化为只保存文件路径。原 DeInit 和目录监听释放包含有界等待，但等待成功及在途释放未经证明。实际 GUI 状态与保存重开尚缺独立测试环境；这些未证实内容不计完成。
## 本工作区生产链盘点（2026-10-31）

以下为现有实现事实，不表示已满足 VM，也不预先确定需要保留的接口。

| 链路 | 当前实现与职责 | 对整体迁移的影响 |
| --- | --- | --- |
| 工具与参数 | `tools/SunEyeVision.ImageSource/Runtime/ImageSourceFields.cs` 声明 11 个设置字段和 6 个条件显示项目；`ImageSourceSettings.cs` 保存 local/camera 设置，`ImageSourceSession.cs` 消费冻结设置 | VM 的 25 个 UI 声明不等于 25 个独立底层参数；需要逐项对照类型、条件、输入订阅、运行与持久化，不能仅增加控件 |
| 图像条与运行 | `src/UI/Services/Images/ImageSourceStripSession.cs`、`MainWindowImageSourceEditing.cs`、`src/Workflow/ImageSourcePolicy.cs` 连接编辑、游标和工作流执行 | 需整体核对导入、选择、自动切换、执行、撤销与方案重开；现有已人工验证行为作为回归基线，原件差异由证据决定 |
| 结果所有权 | `src/Plugin.Runtime/Results/DeclaredOutputSnapshot.cs` 冻结声明、执行状态和值；通过 `ResultResourceOwner` 和 reader 借用/释放 | 图像条、结果树、视口和调试消费者必须共享真实运行身份并按资源所有权同步更改；不得将原件临时 native 指针长期借出 |
| 结果结构 | `ResultPresentationModel.cs` 按 `ToolSchema` 创建输出节点，已有 record/sequence 成员地址与延迟展开能力；资源叶当前被明确标记 ResourcePending | 不能简单认定完全没有树能力，也不能把已有嵌套读取当成 VM 的声明、可见性及资源行为已经实现；需核对 IMAGE 组合如何由真实工具声明生产 |
| 结果宿主 | `MainWindowResultPublication.cs` 校验方案、流程、节点和执行身份，再发布给 `ResultPresentationPresenter`、工具结果视图和图像视口；退场清理 publication 和视图 | 同一结果生命周期涉及多消费者；结构和值的更新必须核切换、失败、取消、撤销、关闭和在途回调，不能单改表格 |
| 绑定与其他消费者 | `src/Workflow/OutputBindingChangeService.cs` 对输出引用和作用域做校验；通信服务、双图来源和根流程调试窗口也读取输出声明或快照 | 字段/成员身份、类型和可见性发生变化时，同批核对绑定选择、持久化、通信及图像来源，避免界面与真实输出脱节 |

原件 `ModuleDisplayDecorator` 的选择、关联/解除关联、初始化和解除订阅以及 `moduleviewstyle.xaml` 的展开和可见性已有静态定位。输出初始化失败、真实回调先后和资源最终释放仍在逆向负责人补证中；尚未将这些记为动态验证通过。
### 订阅输入与动态输出补证（2026-10-31）

已核验 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源输入订阅与动态输出原生补证-20261031.md`，SHA256 `B3DCD2DC1ECA4A42CB36B6EC20E04A7568B6A9ED10DD434D35A0EF7A68634099`。原 XML 的四类 UI 订阅对应五个输入：TriggerString、TriggerFilter、ExposureTimeInput、GainInput、ClearTriggerValue；UI 的 ClearTriggerCondition 对应 ClearTriggerValue。反编译 Process 已定位过滤与静默特殊值、拼接/清除双条件和成功读取后相机参数写入，但调用 ABI、异常与订阅的保存/恢复不能仅凭伪代码签名断言。

原图像源 GetParam/SetParam 的 DynamicOutData 缓冲复制与 AnalyzeExtraIO 调用已有独立证据；尚无充分证据证明平台同名请求如何派发并与静态 XML 合成，不能把相同名称作为调用链闭合。加载原生分支有 DeleteFileW，但路径容器归属未明；相机分支有立即 FreeBuf 和队列阈值释放，不能推广为所有模式的生命周期。逆向负责人继续追这几条边；原厂 GUI 运行观察尚未执行。
### 动态输出、初始化与清理续证（2026-10-31）

已核验 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源动态输出加载清理和初始化追踪-20261031.md`，SHA256 `D78CA88668352C1083316C779118AD84DC96F69C1DE6625B036CCFEED5316DA3`。模块 SetParam 的 DynamicOutData 会进入 AnalyzeExtraIO，筛选 Others 中 IsResultShow=t/T 的项并保留名称和类型；Process 的已见末尾调用 OutputExtra 按类型读写。SDK 平台分发及静态/动态树合成的中间调用边未证，浮点 setter 的隐藏参数仍须核原指令。

Init 已见读取 ImageSourceModuleAlgorithm.xml 后逐项调用 setter；不能直接把算法 XML 或 UI XML 的冲突默认值视为最终 GUI 状态。LoadModuleData 可将部分附件恢复路径加入删除容器；后续加载及 destruction 分支遍历该容器删除，但尚未证明所有元素的来源。DeInit 有遍历队列 FreeBuf 的代码，不能证明有界等待后所有在途消费者均已退出。五输入订阅保存重开、完整错误分支与实机状态仍待补证。
### 动态 IO 和订阅恢复调用边（2026-10-31）

已核验 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源动态IO和订阅恢复调用边-20261031.md`，SHA256 `7D378DEC8C43B4EE25DAC99DFB69E8C8D8F159105F3885C649265E0F2DCECFE9`。原模块 DynamicIOInit 读取 XML，按 Input/Output 分类序列化后写入动态参数；平台派发和最终 SDK 结构合成仍未证。VmIO.RelateIO 首次读取时先置模块级标志，再查询 SDK 已存订阅表，按模块、输入、输出和索引还原托管引用；这不是五项订阅落盘并在新进程恢复的证据，也不证明查询失败自动重试。析构路径可见相机解绑、DeInit 与目录监听清理的顺序；等待超时和在途回调仍需证据。负责人继续对剩余可静态查证的分支开展有界追踪。
### 平台导出与方案存档有界核查（2026-10-31）

已读取 `D:/MyWork/REA逆向测试/docs/joint-audit/图像源平台派发与订阅存档有界核查-20261031.md`，本文 SHA256 `38818DF3B879B4BBED0B22C614A145B39832056DEB0A5D45107E14A3CDA8BAD7`。既有原厂方案样本的 VmServer.xml 将图像订阅拆为图像、宽、高、像素格式四叶；样本未覆盖五个图像源输入，也不能单凭静态文件证明加载器或重开运行行为。原 PlatformManager.dll 导出 PF_M_GetBinaryData 的 RVA 已定位到 0x6a0520，原 DLL SHA 和方案条目 SHA 见报告；函数体本次因 Ghidra 启动路径失败尚未取得，不能据此推断派发。已请负责人处理本地分析启动并继续有界静态取证。
### 平台函数只读续追（2026-10-31）

上述平台派发报告已更新，当前 SHA256 `10C8E725AAEE397BCF10CDDA12EA818D628FD81A7AE434584AFC366383DE6717`；此前 `38818...` 为更新前版本。负责人绕过批处理启动问题，以只读 -noanalysis 提取 Ghidra 伪代码及汇编；Log4j 中文路径错误仍存在但本次退出码 0。已见 PF_M_GetBinaryData 的 0xbd3 分支经 FUN_1805d32d0 和 FUN_1805f6750，通过 GetProcAddress 查找并缓存 VM_PROXY_RecvServerCommand 后间接调用；参数传递有汇编佐证。服务端到具体模块 getter 和 IO 合成次序仍未证明。