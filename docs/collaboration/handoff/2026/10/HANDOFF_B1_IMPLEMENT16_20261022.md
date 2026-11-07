# B1 保存与读回实现交接（交付待验）

- 基线提交：504191d0dbcf2f022fbc9ce196dc646b9f69e39b；本批临时提交另见 delivery.json 的 implementationCommit。
- 当前执行者：Codex，唯一产品写者；子智能体只读评审。
- 授权：用户明确“可以优化代码”；随后确认占用程序已关闭。仅实施已定 B1，不自动实施 B2—B6。
- 目标：严格候选读取、最终快照与语义基线、明确资源引用、Open/Reload 及提交后降级。
- 已完成：上述代码接入；非法变量/连线/设备配置不静默退空；未知扩展保留；图像 PNG 保留受支持的位深/通道；资源只在 I/O 解析，缺失图像与勾选列表不因装载丢失；Reload 不隐式保存；旧调试窗口和缓存退出。
- 构建：标准全量 Release 退出 0。占用期间的独立 PackagingRoot 构建也是成功的中间输出，但不是最终交付。
- 定向测试：30/30 通过，详见 artifacts/validation/b1-implementation/b1-contracts.trx。测试条数不是 27 个业务场景全部通过。
- Release 执行：工具箱/插件元数据探针退出 0，22 个工具，没有已删除的 Threshold、NPointVerification；在设备与方案服务初始化前退出，不证明正常 UI/设备链通过。
- 身份：source-manifest.json 记录 1122 个产品源/构建输入，release-manifest.json 记录 623 个交付文件；均位于 artifacts/validation/b1-implementation/。已有工作树前置改动仍保留，不能只 checkout 临时提交就声称完全复现交付；N 点已有大范围改动及本批窄资源适配依然由完整源身份登记，不混称全部属于本批重做。
- 最终程序：Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
- 未完成：新版独立验收、完整方案/子程序/ROI绑定/真实模型读回整链、系统保存与 Reload 交互、设备迟到结果替身、VS设计时和人工UI验收。没有连接真实设备。
- 测试协作：B0 的用户直接续派任务在旧固定副本继续，不停止其窗口、不覆盖其程序；B1 新版按身份复制后回归，分层列通过/失败/阻塞，不以本地测试替代独立验收。
- 文档：历史分析证据保留为实施前基线；当前状态见 save-roundtrip/#execution 与优化总看板。未验证不得标已闭环。
- 下一步：收回独立报告，按同一身份定位反馈；有修复就产生新构建身份与定向回归。
