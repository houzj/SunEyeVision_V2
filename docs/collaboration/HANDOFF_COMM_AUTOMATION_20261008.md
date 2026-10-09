# 通讯触发与响应接入交接记录

- 基线提交：c92d480480ee84c76aead7a32037d11acea77f8a
- 当前分支：feature/comm
- 当前执行者：Codex；并行助手只读分析，未写文件。
- 任务目标：接入已确认的触发和响应导航、交互及后台执行链。忙时立即跳过并 Warning；Modbus 严格按分隔符和类型编码，失败整条不写。
- 允许修改目录：src/UI、src/Workflow、tests/SunEyeVision.UI.Tests、docs。
- 已修改文件：新增 CommunicationAutomationDefinition、CommunicationAutomationService、CommunicationReceiveStateFactory、CommunicationResponseFormatter；新增触发/响应视图及视图模型、CommunicationManagerAutomation、MainWindowCommunicationAutomation；修改 Solution、CommunicationApplicationService、WorkflowExecutionManager、MainWindowViewModel、ServiceInitializer、App 退出顺序和 CameraManagerViewModel 目录刷新；通讯管理器导航/配置同步；新增 CommunicationAutomationTests、CommunicationAutomationUiTests；本地事实基线第 9 节及 CURRENT_TASK。
- 已完成：事件/字符串触发双页签；完全、部分和不检查匹配；稳定事件、字段与目标绑定；指定/全流程、方案切换及单模块执行；与手动/连续执行共享的非等待租约；后台真实样本解析；方案加载、流程状态和相机状态响应；同设备有序发送、稳定对外编号；Modbus 拆分/严格类型/容量/权限校验；共享方案实时同步及持久化。
- 未完成：通用模块动作契约，UI 选项禁用；事件输出到工作流输入/参数映射按用户要求暂缓；S7/CIP 发送；OPC UA 多数据点状态/编号映射。OPC UA 单原生数值节点不接受带编号的多项状态响应，明确报错，不丢弃编号。
- 已执行构建与测试：122/122 串行通讯/OPC UA/相机相关 Release 回归通过，其中本批新增 18 项自动执行/UI 用例；真实 TCP 和 Modbus 均用隔离回环设备，不写用户端口/设备。全量 Release 构建 0 错误、779 警告；Debug UI 构建 0 错误、889 警告，dev/Debug 已更新。标准 UI Release 结果见随后复验记录；git diff --check 通过。
- 人工验收结果：运行时 WPF 初始化/布局、动态明暗色资源与配置绑定由 STA 测试验证；完整应用启动/交互、VS 设计时、实际主题视觉及用户真实设备尚未人工验收。
- 已知风险或禁止改动：共享 WPF/应用单例测试的并行广范围运行有 4 项初始化超时，单独/串行均通过，不能宣称并行通过。只读复审助手因沙箱初始化失败未完成源码复审，不作为评审通过。保留用户 plans/plan.json；保留观察到的 OpcUaAdapter.cs BOM 差异，未改其业务逻辑。未修改 SDK、插件依赖或发布规则。
- 下一步：用户重启 dev/Debug 验收两个导航，先用隔离测试设备校验源事件、目标地址容量/位宽、对外编号和忙碌日志。实际设备及完整应用人工结果记录到 CURRENT_TASK。新增 SDK 模块动作契约或 OPC UA 多点映射前另行确认，不在本批强行扩展。

### 实施细节

最终标准 UI Release 构建通过：0 错误、1049 警告。全量 Release 与 Debug 的警告数因增量编译范围不同，不据此宣称警告减少。

运行租约在样本到达时立即由宿主取得/拒绝，而不是先投递线程池再检查 busy。执行完成、失败或取消均释放租约；后台 UI 展示回调在方案取消时可终止等待，避免退出阻塞。

方案切换先加载成功再提交；方案生命周期取消旧自动任务并等待清理，通讯停止在相机释放之前。相机观察周期为项目选定的 500ms，初次仅建立基线。

数值响应的编号作为最后一项参与相同类型编码；string 输出完整文本。编号从 1 起保存稳定映射，删除不复用，重排/改名不改变已有编号。方案加载响应无附加编号。执行模块不执行上游或引用过期上游结果，全局变量快照仍可用于绑定。

本轮未跨 AI 移交代码，修改留在当前工作区。后续跨 AI 编码交接前按规则创建说明明确的临时提交并记录新基线。

### 响应开关显示修订

最终标准 UI Release 构建通过：0 错误、936 警告；git diff --check 通过。

2026-10-08 用户确认关闭开关时对应配置不可见。三组配置区域及说明按 Enabled 折叠，标题/开关保留，关闭时收起目标弹层；重新开启保留原配置。不改变后台发送行为。相关 Release 回归 18/18 通过，Debug 构建 0 错误、764 警告，已更新 dev/Debug；标准 Release 结果见复验记录。实际视觉和 VS 设计时人工验收待确认。

### 发送页紧凑布局修订

最终标准 UI Release 构建通过：0 错误、1049 警告；git diff --check 通过。

2026-10-08 用户要求合并重复结果框并将发送测试移到参数列表右侧。已去掉单次结果框，详细内容只保留发送历史；简短状态和发送测试放在参数标题右侧，组装页放在组装标题右侧。文本结束符仍保留并移到输入配置。未改协议、发送命令或保存行为。12/12 相关 Release 测试通过，Debug 构建 0 错误、877 警告，dev/Debug 已更新；标准 Release 最终结果见复验记录。实际布局/VS 设计时待验收。
