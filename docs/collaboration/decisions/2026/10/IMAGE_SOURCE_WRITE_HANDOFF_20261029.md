## 2026-10-30 正式整合批交接及人工验收冻结

- 基线提交：main仍为ddc59a9dbfb0f4edf64dd68045adf8b718e05c7c；当前集成源码保存点d7e98fb7e00856aea6743635cfae319d4dc5d91d，临时ref为codex/controls-image-source-20261030。保存点使用独立索引创建，main与真实暂存区未移动；用于定位和按文件核对，不能整分支覆盖任何一线现行工作。
- 当前执行者/目录：主线唯一写者负责主仓及正式产物；工作流对话继续在87f4独立worktree。正式Release统一由主线替换。
- 已完成：全量Release10、正式EXE探针退出0，Recovery034完整342/0；实际自动范围和6项相机旧行为退役映射见IMAGE_SOURCE_FORMAL_INTEGRATION_AUDIT_20261030.json，不冒称原断言原样通过。
- 人工验收：工作流按用户要求启动主仓正式程序，主线已核PID76744存活；尚无本轮人工验收结果。当前冻结正式Release，不关闭、不构建、不替换DLL；待用户明确结束本轮试用再解除。
- 已修改/共享边界：源码保存点保全完整集成源及B01—B08增量。B08三候选消费者已接NodeDisplayName，正式ScopeId/NodeId不变；后续以hunk核对，不能取旧SDK消费者覆盖。两个对话可继续各自源码，不要求工作流等待全控件完成。
- 未完成：VS、人工、真实相机/VM同态、历史存储产品、B08递归候选树和精确类型筛选、Blob整链及算法。
- 下一步：主线据B08已回完整静态证据设计和成片实施；动态索引数组原生后端未证，现行单项整数合同保留并注明未知。工作流独立线继续其已授权范围，集成另核对，不覆盖试用产物。
# 图像源单写交接决定（2026-10-29）

- 主线基线：ddc59a9dbfb0f4edf64dd68045adf8b718e05c7c（main）。
- 图像源保全提交：89f57f656d334119e5864778a0d0fb271e78c452，引用 codex/image-source-handoff-20261029。使用独立索引固定源码，不移动 main，不改变现有暂存区；不是已验证整合提交。
- 执行者：工作流对话在现有独立 worktree 立即接手；无需等待 Blob/S2。
- 允许范围：Plugin.Wpf 的 Presenters/Input/ImageSources、Controls/Native/Composition/ImageSources、Themes/Native/Composition/ImageSources、Design/ImageSources；tools/SunEyeVision.ImageSource；UI/Services/Images 和图像源相关宿主消费者；工作流图像取用、接纳、导航与执行链。
- 本对话保留：Blob、其他公共基础组件及其组合。主线停止修改上述图像源范围，禁止双方同时修改同一工作目录。
- 共享合同：以保全提交内 FieldSettings、ToolSettingsFields、ToolSettingsEditSession、NodeConfigurationEditHost 为当前接入基准。工作流可在独立分支同步修改图像源必需的共享合同及消费者，无需逐个接口再等审批；不得覆盖主线当前合同、退回 typed settings 双轨或触及 Blob/Group。基础合同有其他消费者时提交完整差异清单，由主线集中整合，不用旧 DLL 替代验证。
- 保全方式：保全提交包含相关现行源码及未跟踪文件。工作流按其 3c1adc02 基线三方核对，保留双方正确增量；不得直接整提交覆盖独立成果。保全提交不是全仓新基线，Blob/其他依赖缺口按主线现行内容核对。
- 设计依据：既有 VM 图像源根流程完整功能链证据及 VM_IMAGE_SOURCE_CONSUMPTION_HANDOFF_20261029.md。按完整控件批次集中迁移，未知项明确记录。
- 正式构建：下一次正式 Release 集成窗口优先交图像源批次，由本对话在主仓统一整合和执行全量 Release；工作流提交可整合候选和文件/合同清单即进入该窗口，不等待 Blob 完成。图像源尚未交付时，主线可使用空闲窗口验证自己的独立批次，图像源就绪后优先下一窗口；不以排期冻结双方源码开发。工作流不得在独立 worktree 覆盖正式 Release。
- 当前产物：用户已明确确认检查结束，可以正常关闭并构建；57964已正常退出。B01主仓批次已构建验证，未合入图像源未交付候选。后续进入新人工试用时仍保持其产物稳定，产物冻结不冻结源码开发。
- 验收：正式同版 Release 运行、VM 同态检查和用户人工验收分开记录。按用户在工作流对话明确要求，图像源本批验证后由工作流协调启动供验收；不把本对话此前“不自动启动 Blob”的指令扩大为阻止该请求。
- 当前状态：交接批准并固定源码；未合并候选，未进行本批构建/运行/人工验收。
