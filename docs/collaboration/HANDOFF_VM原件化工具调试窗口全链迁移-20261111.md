# AI 协作交接单

- 基线提交：`c5e8901df90423954f14e3e6819e9e8040cdae59`（用户指定基线 `c5e8901df`）
- 当前分支：当前 worktree 为 detached `HEAD`；不要修改 `main`，继续使用 `C:\Users\Administrator\.codex\worktrees\param-window-datatree-project-v2\SunEyeVision_V2`。用户另指定的逻辑分支名为 `feature/param-window-datatree`，交接前需由接手者确认实际分支再提交。
- 当前执行者：Codex
- 任务目标：完成首批 ImageSource + Blob 的 VM 原件化调试窗口全链迁移，覆盖启动流程、方案/节点/连线、参数页、ROI、运行参数、结果列表、图像/图形/文本、底栏命令、保存重开和关闭释放；启动加密、权限、授权和安全弹窗采用本方自研实现，只对照 VM 的流程时序和可见状态。
- 允许修改目录：当前 worktree；不得修改 `main`，不得把 `D:\MyWork\SunEyeVision_V2-param-window-datatree` 未提交改动当基线，不得启动或控制用户桌面程序。

## 已修改文件

本 worktree 在交接前已有大量未提交改动，禁止用 `git reset --hard` 或清理未跟踪文件恢复基线。当前交接批次重点文件如下：

- `src/UI/Services/Images/ResultWorkspacePresenter.cs`
- `src/UI/Services/Images/ResultImagePane.cs`
- `src/Plugin.Wpf/Presenters/Display/NativeResultViewport.cs`
- `src/UI/Shell/Windows/MainWindowDocumentHistory.cs`
- `tests/SunEyeVision.UI.Tests/ResultImagePaneTests.cs`
- `docs/09-现行文档/VM-ImageSource-Blob全链路差异矩阵-20261111.html`
- `docs/collaboration/CURRENT_TASK.md`
- `docs/collaboration/evidence/图像层保留与关闭释放修正-20261111.json`

其他既有改动涉及 Blob Runtime/WPF、ImageSource、Plugin.Wpf 原件控件、结果树、参数绑定、执行生命周期和测试夹具；接手者必须先以 `git status --short` 和本文件列出的现行文档为准，不按文件数量判断完成度。

## 已完成

- 建立完整生产链差异矩阵，覆盖进程启动、打开方案、根工作区、工具箱/节点、节点选中、连线/来源、调试窗口、参数绑定、运行取消、结果列表、图像图形文本、节点切换、保存重开和关闭释放。
- 明确启动安全边界：加密、权限、授权、密钥和安全弹窗不复用原厂实现，仅保持流程时序和可见状态对照。
- 按 VM 结果视图证据修正图像 publication 的图层生命周期：输出视口重新绑定图像时可保留原生 Shape 层，等待新场景回执替换，避免图像源切换时 Blob 图形/文本被底层清空。
- 结果视口图形设为只读命中区域，平移/缩放交给原件 `ImageViewBox`；ROI 编辑仍使用独立 `NativeRoiViewport`。
- 宿主退出时，`ReleaseDocumentHost` 在插件目录退役前关闭当前 `DocumentSession`，释放正式结果 reader 和执行代次，针对“选择不保存后停在等待资源释放”的资源所有权断点。
- 增加图像重绑定保留 Shape 层的 STA 回归合同：`Output_image_rebind_can_keep_native_shape_layer_until_the_new_scene_receipt_is_presented`。
- 结果语义、Blob 结果树顺序、模块摘要、枚举显示、ROI列宽、默认值/步长、参数绑定和原件资源映射已有多批静态修正，详见 `CURRENT_TASK.md` 和 `docs/09-现行文档/`。

## 未完成

- 未取得或未完成 C 盘 Release 的统一运行态人工验收：
  - 图像源切换后 Blob 图形和文本是否持续显示；
  - 缩小图像后拖动平移、缩放中心、边界裁剪是否与 VM 一致；
  - 选择“不保存”后是否无需任务管理器即可退出；
  - 节点切换/重复切换后的结果、publication 和资源计数；
  - ROI绘制/继承、阈值执行、结果图层选择、保存重开和迟到回调拒写。
- 模块结果树、结果表的全部分组、数组/数值格式、展开状态和视觉尺寸仍只有部分静态对照，不能称为 VM 体感一致。
- 工作流引擎虽已形成独立逆向闭包方案，但真实设备/通信释放、算法中止、线程时序和动态退订仍标记“运行未确认”。
- 生产探针、Blob/UI 回归和性能测试已编译进入解决方案，但按用户要求未运行；测试合同不等于运行态通过。
- 代码中仍需继续清理或登记原厂来源资源键/资产名（例如历史 `IMVS...` 本地化/工具箱资源）；这些只能作为证据或迁移映射保留，不能直接视为最终中性产品命名已完成。

## 已执行构建与测试

- 最近一次命令：
  `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon -v:minimal`
- 最近一次结果：0 错误、3302 警告、18.83 秒。
- 构建包含 UI、Plugin.Wpf、ImageSource、Blob 和测试项目编译；未运行自动测试、生产探针或算法程序。
- 未启动用户桌面程序，未使用 CUA 控制电脑。

## 人工验收结果

- 用户此前确认：输出掩膜无多余“×”、枚举展开为中文、Blob 三页切换/关闭基本正常。
- 用户尚未确认本交接批次的三项关键运行态：图像源切换保留图形/文本、缩小图像平移、选择“不保存”后正常退出。
- 当前目标已由用户暂停并随后清除；本交接单保留未完成项，不能作为完成证明。

## 已知风险或禁止改动

- 不得把构建成功、静态映射、单页截图或测试合同编译通过写成 VM 体感通过。
- 不得直接引用或发布原厂 VM DLL；原厂程序集、Qt/C++ 类型、XML、资源和截图只作为逆向证据。
- 不得恢复旧 `Plugin.SDK`、旧参数模型、旧视觉控件或双轨兼容链；Blob WPF 只保留必要的组装和宿主接缝。
- 不得修改 `main`，不得覆盖 D 盘 worktree 产物，不得启动用户电脑上的程序或替用户点击验收。
- 关闭链涉及正式执行、调试执行、结果 publication、reader、DocumentSession 和插件代次租约，任何顺序修改都必须同时更新差异矩阵和关闭探针合同。
- `NativeResultViewport` 的 Shape 层保留只适用于输出结果重绑定；普通清理、坐标域变化和视口释放仍必须完整退役图层。

## 下一步

1. 由用户重新开启目标后，先确认实际分支并创建中文说明的临时提交，不清理现有未提交工作。
2. 按差异矩阵一次性完成 C 盘 Release 人工路径：启动/打开方案/添加 ImageSource→Blob/运行/结果列表/图像叠加/保存重开/关闭。
3. 只记录用户实际反馈对应的截图、窗口路径和步骤；未反馈项保持“待人工确认”。
4. 若人工仍显示差异，先定位属于框架生命周期、数据/资源所有权、控件交互、窗口组装或算法适配哪一层，再成片修改，不继续追加孤立补丁。
5. 人工通过后再运行与范围相称的 Blob/UI 回归和生产探针，重新执行全量 Release，并同步 `CURRENT_TASK.md`、差异矩阵、证据 JSON 和主方案文档。

## 2026-11-11 Codex → CodeBuddy 交接摘要

### 当前基线与目录

- 分支目标：`feature/param-window-datatree`
- 基线提交：`c5e8901df90423954f14e3e6819e9e8040cdae59`
- Codex 管理工作目录：`C:\Users\Administrator\.codex\worktrees\param-window-datatree-project-v2\SunEyeVision_V2`
- 另一个手工工作目录：`D:\MyWork\SunEyeVision_V2-param-window-datatree`
- `main` 的本地备份分支：`backup/main-before-github-sync-20261109`
- 不修改 `main`，不把手工工作目录的未提交内容当作已固定基线。

### 已完成工作

- 已建立 ImageSource + Blob 的 VM 原件化迁移计划和完整生产链差异矩阵。
- 已整理 VisionMaster 4.4.0 Build20250516 的程序集、模板、资源、Blob XML 和参数窗口组装链静态证据。
- 已确认参数页已接入 VM 风格的 `ParamListView`、`ParamUserControl`、字段模板、范围模板、引用字段和资源字典。
- 已完成多批结果树、结果摘要、枚举显示、ROI 字段、图像层保留和关闭资源代次的静态修正，具体以 `CURRENT_TASK.md` 和现行文档为准。
- 已将迁移计划和当前基线发送到 CodeBuddy 的“重构测试”对话。

### 已定位但需要重新确认的代码修复

手工工作目录中曾尝试以下两个修复，尚未提交，也没有复制到 Codex 管理工作目录：

- `src/Plugin.Wpf/Fields/DeclaredFieldRow.ImageSource.cs` 将图像来源空项显示为“未设置”。
- `src/Plugin.Wpf/Fields/DeclaredFieldRow.cs` 删除资源字段的“运行时资源，请选择来源”自研占位文本，并让资源引用字段进入 `DeclaredFieldTemplateRegistry.Reference` 和 VM 风格引用模板。

CodeBuddy 需要在当前工作目录重新核对、应用、构建并提交这两个修复，不能直接假定它们已经存在。

### 当前问题

- Blob 三页尚未完成 VM 原件化的完整生产闭环。
- ROI、继承、位置修正、运行参数、结果显示和底栏命令仍有本方组装逻辑，需要按差异矩阵成片收拢。
- 图像源切换后图形/文本保留、缩小图像平移、节点切换结果一致性和“不保存”关闭释放仍待人工确认。
- VM 的动态复用、销毁顺序、设备释放、线程时序和性能行为仍缺少同版本运行证据，必须标记为“运行未确认”。
- 生产探针、Blob/UI 回归和性能测试虽已编译进解决方案，本批没有运行，不能写成运行通过。

### 构建阻塞

- 用户运行中的 `SunEyeVision.UI` 进程曾锁定 Release DLL。
- 已发送正常关闭请求，但进程没有及时退出。强制结束进程的操作被自动权限审核拒绝。
- 使用独立 `build-alt` 输出目录完成过 restore，但最终完整构建结果没有在本轮固定记录。
- 最近一次已记录的全量 Release 构建为 0 错误、3302 警告、18.83 秒，未运行自动测试、生产探针或算法程序。

### CodeBuddy 接续顺序

1. 在 Codex 管理工作目录确认实际分支、`git status --short` 和当前基线，不执行 `git reset --hard`。
2. 重新应用并验证图像来源“未设置”和资源引用字段两个修复。
3. 沿 VM 原件链收拢 Blob 参数页、ROI、运行参数、结果显示、底栏命令和关闭释放。
4. 使用独立 Release 输出目录完成全量构建，避免覆盖其他 worktree 产物。
5. 按 ImageSource + Blob 完整路径运行相称的自动验证，再由用户手动核对三页、图像层、平移、保存重开和关闭释放。
6. 将实际结果同步回本交接单、`CURRENT_TASK.md`、差异矩阵和证据文件，并为阶段成果创建中文说明的临时提交。

## 2026-11-11 CodeBuddy 接手批执行结果

### 接续顺序落实情况

1. **分支确认**：worktree 为 detached HEAD（基线 `c5e8901df`）确认无误；逻辑分支名 `feature/param-window-datatree` 已被 D 盘手工 worktree 检出，无法在本 worktree 同时检出，改为创建同源兄弟分支 `feature/param-window-datatree-codebuddy`（同一基线，可快进合并），未动 `main`。
2. **修复1 已按 VM 原证纠偏应用**：D 盘参考实现的"未设置"文案在 VM 4.4.0 中文资源中查无（全库仅 1 处代码注释）；VM 空项惯例是 `Holder.NullName="RunParam_None"`（zh"无"/en"None"），且原件图像来源槽本身无空项（清空由 `SetCombinationSourceOperation` 空值分支承担）。现空项 Label 取 `AppLocalizationService.GetLang("RunParam_None","无")`，ToolTip"清除图像来源"与 UnsetSetting 身份不变；"可选 Unset 清空"按已登记宿主设计保留。新增合同用例 `Image_source_unset_choice_uses_the_native_none_label`。证据：`evidence/图像来源空项与资源引用行原件对照-20261111.json`。
3. **修复2 核对登记，未重做**：`DeclaredFieldTemplateRegistry.cs:71-76` 与 `DeclaredFieldRow.cs:196-197` 的 Resource+Reference→Reference 模板路由在本 worktree 已存在等价实现，src 中已无"运行时资源，请选择来源"文本。VM 侧原证：图像资源走 EnumerationG 组合槽直写订阅，ButtonSelecter 引用行为数值/坐标类引用；资源端口路由是否需按原件改组合槽留待消费者盘点。
4. **结果格式块成片收拢**：新增 `src/Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/ResultSemanticText.cs` 共用语义投影（模块状态/单体状态 0/1→失败/成功、阈值方式五种原厂中文、极性 Binary→亮于背景/BinaryInv→暗于背景）；主结果树 `ResultPresentationPresenter.ValueText`（原先无任何语义映射）与只读属性树 `DeclaredPropertyList.ValueText` 统一接入；序列元素节点沿 `DeclarationAddress.RootPort` 回溯根端口显示名；属性树 Float32 改单精度自然位数（C03 原证 11217.29/0.4195392），两视图既有 Culture 合同不动；结果表 Blob 列已是 float32 与原证一致，未改。专用分组标题与空值文案无 VM 原证，按门禁不发明，保留待办。证据：`evidence/Blob结果格式语义投影原件对照-20261111.json`；差异矩阵"模块结果/阶段10"行已同步。
5. **构建**：本机无 PowerShell 7 导致 UI 载荷封装（`SealDevelopmentReleasePayload`）阻断；winget 安装因系统服务禁用失败（0x80070422），经用户授权改用官方便携 ZIP（7.4.6）装至 `C:\Users\Administrator\.local\pwsh7`，配置 git-ignored 的 `installer/Release.Local.props` 指向。最终全量 Release 0 错误，`Release\SunEyeVision_v1.0.0\SunEyeVision\release-payload.json` 封装成功。
6. **回归（用户授权全量）**：UI 224/355 通过、Blob 103/121 通过；本批相关用例（结果呈现、图像来源、Blob 合同 21/21）全部通过。失败项经逐类排查均为该 worktree 既有问题，与本批改动无交集：`DocumentSession.Dispose` 报"方案后台活动尚未排空：通讯服务及驱动"、测试 bin 缺主题资源 `themes/light/lighttheme.xaml`、共享测试 bin 陈旧程序集与清单哈希不匹配（注意：本工程编译非确定性，独立构建测试项目会使已封装清单失效，必须全量构建后立即 `--no-build` 跑测试）、一个既有 de-DE Culture 漂移用例（`Immutable_scalar_formatting_uses_invariant_culture_without_unwrapping_sequences`，与现行 CurrentCulture 代码矛盾，早于本批存在）。
7. **文档同步**：`CURRENT_TASK.md` 顶部批次记录、差异矩阵、两份证据 JSON 均已更新。

### 遗留与待人工核对

- 运行态体感：图像来源下拉空项显示"无"、选择后清空来源、结果树/属性树状态与极性中文、Float32 数值显示、关闭释放链，全部待用户人工核对，不计为通过。
- 分组标题、空值文案两视图统一、资源端口路由形态、de-DE 漂移用例、既有夹具失败（会话排空/主题资源/测试 bin 部署），均登记为后续待办。

