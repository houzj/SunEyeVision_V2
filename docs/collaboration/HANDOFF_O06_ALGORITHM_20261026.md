# O06 算法实施交接

- 产品基线提交：`25d3b1a4f0b123586c805c0008334cad08b6a2e5`，main。该中文检查点已保存窗口重组及持久回归入口；正式开始前以主对话派工给出的最新提交核对。
- 当前执行者：从主对话交给 `blob_algorithm_scope`；收到主对话明确放权消息后才开始。主对话转只读审查和文档，不同时改产品。
- 目标：把已批准的 Blob 分割职责和上游承接真正落地，不只隐藏旧参数或改文案。依据为原O06方案及 `evidence/O06_ALGORITHM_TRANSFER_20261026.md`；不声称未知VM原生公式等价。

允许修改：

- `tools/SunEyeVision.BlobAnalysis/` 中算法、参数、输入/输出合同、适配及声明；不改只读结果WPF样式以外的无关UI。
- `tools/SunEyeVision.Tool.ImageFilter/`、`tools/SunEyeVision.Tool.ImageThreshold/`、`tools/SunEyeVision.Tool.Morphology/` 必要的参数、算法、选项/视图和发布元数据。
- `src/SunEyeVision.HalAdapter/` 必要的严格二值准入、灰度转换等可复用算子；保持实际Domain和来源身份。
- `src/Workflow/Persistence/` 必要的旧字段明确拒绝，不恢复旧变量收口；以及对应现有测试、正式Release验证用例。改到其他目录前先报具体必要性。
- 本交接单可补结果；分析原报告SHA已冻结，不覆写历史报告。主对话持有CURRENT_TASK、ACTIVE_GOAL、协议、原方案与文档生成器写权。

必须完成：

1. Blob单阈值/双阈值/不二值化与极性分开；模式、活动字段、默认、schema/稳定ID、保存、执行和实际阈值输出一致。不二值化直接检查原始Gray8及输入Domain内0/255，不能先转灰度或隐式阈值化。无阈值时输出明确nullable，声明与消费者一起适配。
2. 从Blob删除通用预处理/形态学的实际执行与设置，不留双轨。ImageFilter承接均值/高斯/中值/均衡化；Morphology严格消费二值、支持现有操作并明确Circle与Ellipse的尺寸差异，不能枚举强转或继续固定128分割。真实链明确选择ImageThreshold.BinaryMask。
3. 保存遇到旧已启用功能、旧参数或绑定时明确拒绝/提示重新配置，不能删除CLR属性后静默吞掉。软件未发布，不为旧开发方案妥协保留重复实现。
4. 保持有效Domain∩检测区−屏蔽区、反向不越域、最终筛选排序/限数/NG统计、完整边界、资源reader生命周期及按需输出。算法流程变化造成参数/统计差异明示，不能伪装VM数值等价。
5. 全量Release编译与有意义的正式目录回归；不为旧测试保留错误算法，不削弱错帧拒绝、严格输入或资源约束。新增二值边界/nullable输出/上游链/保存拒绝断言。

已完成和现存状态：

- 最新正式全量 `o06-canonical-frame-full-build.log`：0错误/786警告/31.90秒。窗口4组和现行实际宿主整链8组独测通过，总回执 `Release/_validation/O06-Window-20261026/receipt-final-window-fullchain-independent.json`；不是新算法完成或整体UI达标。
- attempt018曾在ROI确认后二次生产错帧；已恢复HEAD既有canonical图像override，attempt019根和020独测通过。必须保留该override；不能把GetOutputVariables的SDK图像RuntimeValue改为中立图像，现行SDK输入仍消费IVisionImage。
- 原外部改动仍未提交：`src/UI/packages.lock.json`、`tests/SunEyeVision.UI.Tests/packages.lock.json`、`BlobAnalysisResults.cs`中min/max属性与映射删除及白像素标签变化。不得擅自重置或混作本次成果。Results须先保存原diff并核对新输出合同；如新合同确需整合，明确逐项依据，不静默覆盖。
- ROI四种继承与整体视觉仍由后续阶段承接，本次不要用像素Region改名充数。真实VS设计器/用户未验。

运行与交付：

- 不新建副本/Worktree/代理；不提交或推送，完成后由主对话审查并按中文有意义提交保存。不得清理其他测试或归档。
- 测试仅基于正式 `Release/SunEyeVision_v1.0.0/SunEyeVision`，日志置于Release下，不复制payload。HALCON沿用加密狗，不单独查许可。
- 产品写者可以构建；独测开始前明确冻结产物并交独测，冻结期间不覆盖。每个实际检查点向主对话报告已改文件、实际构建/测试、问题与预计下一检查点，检查点不停工。
- 交回时列出改动、失败及修复、证据位置、未完成、源与产物身份，并明确释放产品写权。不能代用户通过，不将局部PASS称全交付。
