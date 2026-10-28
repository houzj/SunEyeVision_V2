# 结果树与结果表同源集成交接

- 基线提交：正式产品 `b6f77679`；测试顺序增量 `a9cf86b0`。两者之间 src/tools 提交差分为空。
- 当前分支：main。
- 当前执行者：Codex，主仓唯一产品写者；工作流线仅独立 worktree 推进 D06。
- 任务目标：按 VM 设计统一结果树/表的 SDK 输出声明、同次运行快照、状态与生命周期，原方案同步维护。
- 允许修改目录：公共 Plugin.Wpf/Contracts 及必要宿主消费者由主线集中；工作流线按已授权边界独立交固定候选，不双写主仓。
- 已修改文件：统一 publication/lease/stamp、IToolResultView、真实 GeometrySource、原树/原表及 UI 唯一入口、必要 Blob 接口机械同步、实际回归、Catalog 及原方案。具体身份/来源与运行证据见 evidence/O06_RESULT_PUBLICATION_INTEGRATION_20261028.json。
- 已完成：一份 SDK reader 归 publication，树表分别借阅；正式、调试、历史和声明同入口；128 树分页及原表 500→1000→1205；同 run 不同 publication 替换；真实来源帧正负向；退休/通知重入/异常清理；树重挂/null 清选与同步换目标保护；错误窗口拒编辑/提交。
- 未完成：D06 正式变量绑定完整批；D07 同帧 viewport、真实 Domain/精确 Region 绘制、原 Shell 全生产消费和旧 SDK 主图退出；C09/C17 等原阶段一剩余项；真实 VS、VM 同态和用户人工。Blob 业务仍冻结，不据机械同步进入第二阶段。
- 已执行构建与测试：UI006 与 full006 均 0 错误、6579 警告；分别 111.26/61.45 秒。12 条同源 receipt 均成功：publication9、来源2、原表1、原样式2、ImageSources173、Host1、Design91、Components9、RuntimeEditor10、FullChain8、Resources22、Theme1。另 RootResource 真实 Fact1、部署18项通过。795 发布文件及142加载记录 SHA核对；临时移动的3文件与清单已原身份还原。
- 测试增量边界：仅 RootWorkspaceHostTests 合法编辑/错误窗口拒绝的顺序修正；BuildProjectReferences=false 单编测试助手 0错21警告6.71秒，正式 payload 保持。未重复构造产品或重写发布身份。
- 人工验收结果：本批未取得新的真实 VS/VM 同态或用户签收；既有三页/索引切页反馈保留，不能套本批。
- 已知风险或禁止改动：无同帧 proof 当前明确拒绝高亮，不是图形闭环；不保近似 Region/隐式图像 fallback。用户 BlobParameterEditor.xaml SHA256 `118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed` 未改、不暂存。旧失败日志保留。当前全部自有构建/验证终态，无 live 句柄。
- 下一步：继续 D06 独立完整批；D07 先冻结公共 frame/layer owner/epoch 窄接口和全部 UI/ROI 消费者后同批实施；设计详见 HANDOFF_D07_VIEWPORT_DESIGN_20261028.md 第7节。不关闭整体两阶段目标。
