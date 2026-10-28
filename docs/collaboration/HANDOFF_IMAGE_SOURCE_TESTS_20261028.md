# 图像源新架构测试整批交接（2026-10-28）

## 基线与边界

- 独立分支 `codex/image-source-tests`，基线 `48d2d162`，工作树 `workflow-overview/SunEyeVision_V2`。本线唯一测试写者；未写src/tools/主仓/Release，未构建私有程序或复制DLL。
- root后续授权扩大至 ImagePreviewInteractionTests、NodeConfigurationEditHostTests、SolutionManagerUiTests、SolutionPersistenceContractTests 和 ImageSourceCases正式选择脚本；其余产品及共享项目接缝由root统一。

## 整体迁移与实质范围

- 原接纳/拒绝/集合防反改/三游标测试迁到 `CapturePolicy`、`LoadFromPolicy`，策略仅是Runtime字段的非持久投影。正式节点真值统一 `RuntimeConfiguration`，不保旧ImageSourceConfig节点属性或测试兼容桥。
- `ImageSourceTestRuntime` 通过宿主正式plugins目录与PluginLoader/RuntimeToolConfiguration真实注册；不再使用Probe或改名旧SDK假图像源。
- 历史测试保留同节点接纳/UndoRedo/正式Session.History、无关结构rebase、旧编辑器拒绝、失败回滚和原publication身份、空列表编辑及七种非法提案；增加复制配置独立性。
- 保存/快照测试保留路径原引用、冻结base、不借预览列表/当前目录、重复引用独立位置、运行选择过滤、相机字段不被本地列表覆盖、资源保存A/B与旧版本拒绝。
- 连续/单次执行改为实际ImageLoad Runtime，通过正式RuntimeToolResults声明输出观察 `output.file`。同源复位、双源游标、成功/失败/取消、无预览执行、旧显示源不获新游标、整方案不自动推进均保留。取消由真实输入等待边界控制；另外新Runtime单测补实际输出scope建立后的取消释放，不假装有算法前OnRun钩子。
- 旧PreviewSolutionFixture曾用单字节假PNG和PluginLoader.MarkAsLoaded，真实Runtime无法解码且可能跳过注册；现生成真实2x1 Gray8 PNG并退出假加载。旧PreviewPolicy SDK参数/工具全部删除。
- 真实RootWorkspaceHost保留原根流程/画布/主题全部断言。源节点改实际Runtime，设置走真实ExecuteOpenDebugWindow→RuntimeNodeConfigurationControl→稳定FieldId编辑；私有草稿先不增历史、TryCommitEditor才接纳，保卸载重载、无关窗口拒绝、UndoRedo、关闭后旧编辑器不可提交。不是为测试扩大生产setter。
- 旧SDK ImageSourceConfigSerializationTests退出，由新 `SunEyeVision.ImageSource.Runtime.Tests` 的真实Runtime唯一JSON往返承接；断言不再出现节点ImageSourceConfig/InputSource/OutputCache/LastResult。
- 新Runtime测试覆盖冻结集合/无效字段不反改、空结构可编辑、缺调度SN、无输入/错误来源不回退、错设备/无设备、本地真实解码、相机复制真实Domain与ProducerRun/SourcePort、Gray同资源别名、输出Dispose后已有pixels/domain reader仍可读、原输入不被释放、输出捕获中取消及重复Dispose。
- `ImageSourceCases` 扩入原导入/清理等用例，非法矩阵仍7展开，真实数量动态记录，不强守旧45，不套旧成绩。

## 必需项目接缝（root集中处理）

1. 新 `tests/SunEyeVision.ImageSource.Runtime.Tests/SunEyeVision.ImageSource.Runtime.Tests.csproj` 加入Solution。引用实际Runtime项目Private=false、Workflow和Plugin.Runtime；共享ToolPluginTestLoader注册host解析，专用ModuleInitializer仅从正式plugins/SunEyeVision.Tool.ImageSource.Runtime加载实际组件。
2. UI测试仅经正式registry+稳定字段读写Runtime，避免工具CLR强耦合；原ImageLoad项目引用由root整批移除。测试插件目录必须当前同源新Runtime/Wpf，不可残留旧ImageLoad声明竞争。
3. 新生产冻结输入：`WorkflowRunRequest.sourceInputs` / `ImageSourceRunInput` 由root实现；CaptureLocalImagePlan保持3参，接受ImageSourcePolicy。source端口不能持久Constant旁路；运行Session明确校验SN。
4. 旧SDK测试文件已删除，若脚本显式列旧类需同步退出。现有公共C13及其他测试不借本批更改语义。

## 验证状态

- 只做纯静态核查：PowerShell实际自带Roslyn ParseText；项目XML与git diff --check。没有执行dotnet build/test、没有部署正式Release或私有Release。
- 未语义编译、未运行、未真实VS/硬件/人工验收；新测试的存在不是通过证据。必须root集中集成全部生产消费者后，在主仓唯一正式同源Release重新构建、按真实展开数量运行并保留所有失败。
- 旧图像预览控件已从本次测试入口退出，实际公共ImageSourceStrip接真实UI.Services.Images.ImageSourceStripSession；按source_native冻结构造与真实private ImportAsync反射可控枚举边界，以及正式RequestAsync(ImportFiles/ImportDirectory, Paths)入口验证。不是测试adapter模拟生产导入。生产session由另路同批集成，当前本分支不含它，禁止单独编译/以缺依赖判断产品完成。
- 原two-way索引绑定用例按新原件真实语义改为one-way恢复/明确意图；普通点击在运行选择模式仍Activate，Ctrl才切勾选，首图SelectStart不执行；脚本同步准确名称，不保原错误交互期待。
