# Blob 极性 setter 原生补证及本方对照

范围：VM 4.4.0 Build20250516@1555331 离线静态证据；本次没有运行原版 VM 或 SDK。原报告 `D:/MyWork/REA逆向测试/docs/joint-audit/B08-极性写入跳表与执行链-20261101.md` SHA256 `5BA47AA41BD268EA9AF34D4E9738CE32EB6F42E54AB0A94AB6F4B43CA96E882A`，原 MVD_Algorithm.dll SHA256 `85D17ECC87D52E9A62D8BB86C8D5208AC999BCD31EC6D0806453F8165733F549`。

资源声明 Polarity 对应 0x20602；原生 setter 操作码1经检查后，原始跳表索引0x600到 1805ffb2c，直接把输入第二DWORD写入同一执行句柄 +0x190。执行句柄传递及读取、单阈值模式分派，在所列能力/变更项/名称表条件下连接到已有标量路径：0→p≥t，1→p<t，符合此前已核八位标量及优化路径指令。UI资源/本地化把0标为亮于背景、1标为暗于背景。工具UI资源默认0与原生资源默认1是不同来源，不能合并为同一个默认事实。

本方 `BlobParameters.xml` 将 Binary 标为亮于背景，BinaryInv 标为暗于背景；`BlobSession.Execute` 分别取 HALCON [t,255] 和有效域差集 [0,t)。正式 `RuntimeAlgorithm attempt-007` 延续此前六组阈值0、100、255样本回归；逐字节历史对照见 `BLOB_THRESHOLD_SCALAR_COMPARISON_20261101.json`。在资源名称实际加载、UI枚举值传入及原生分派条件成立且限定已核8位/单阈值内，该方向与原静态条件链一致。没有改产品或开放新模式。

此证据修正历史记录中“未找到0x20602实际写入”的说法；历史 JSON 的未证列表是当时状态，保留不改。仍未证 UI 运行时实际绑定和原生名称表加载、全CPU派发实际运行、原机逐像素差分、ROI/掩膜和最终 BinaryImage Domain 等；不得声称完整原厂算法或全部像素结果等价。双阈值原完整执行和图文、ROI仍未完成。