# 插件项目职责、接口与任务设计

2026-10-17 依用户最新决定更新。用户自行控制两项目进度；本轮只更新职责文档，不增加算法功能、不取消当前人工验收门槛。

## 能力所有权

| 能力 | 实现所有者 | 主框架提供的接口/职责 |
|---|---|---|
| 工具算法、文件格式、专属资源/模型 | 插件项目 | 注册/发现、方案节点存储 |
| 公共配置模型/状态/模板/控件、配置及调试宿主 | 插件项目 | 主窗 Owner、选择/关闭通知、调用调试入口 |
| 参数与订阅 UI、类型桥接、输入解析适配 | 插件项目 | 流程 I/O、变量、Group 输入及文档事务的权威数据 |
| ROI/屏蔽区/位置修正、结果显示与预览桥接 | 插件项目 | 通用画布/图像表面、坐标与宿主生命周期接口 |
| 插件运行状态、单节点调试/连续执行适配 | 插件项目 | 主线通用工作流/Group 调度、取消/超时与方案身份 |
| 主界面、工作流画布/导航/Group | 主框架项目 | 插件线只提交必要桥接需求，不另造流程编辑器 |
| 方案/流程生命周期、通用持久化/发现装载、最终集成 | 主框架项目 | 插件线提供资源保存参与器、稳定元数据和状态释放适配 |

程序集中可以同时存在上述能力；不能把整个 VisionFramework 或 VisionEditor.Core 目录作为排除插件框架责任的理由。插件框架也不能再造一套长期并存的流程执行/缓存后端。

## 交叉文件单一提交所有者

- 本轮插件候选所有者：ToolConfiguration*、ToolDisplay*、插件模型/适配器、ModuleExecutionScope；当前在制 ToolExecutionScope/ToolDisplays 接线由本线保留。
- 主线当前所有者：ToolEditorHost 调试缓存的 WorkflowNodeIdentity 迁移、主程序/画布/Group、WaitTimeMs 流程配置和最终集成。
- Contracts.cs、GraphExecutor/Validator、WorkflowSubscriptions、MainWindow、WorkflowExecution 已有两分支存量差异，属于必须逐文件冲突审查的交叉文件，不声称已消除重复。当前候选先冻结；新增变更需在 CONTRACT-REQUESTS 写明能力所有者、具体文件、签名、兼容、冻结/交接时点。每次集成由一位提交者落盘，另一线不同时编辑集成目标。
- main 已有的调试/ROI/Group 能力由主线交接接口，本线负责适配或迁移其插件相关部分；不删除已有功能后宣称新模板接入。

## 插件任务卡（必填）

`工具/优先级；原厂证据路径与确认层级；公共框架 PF ID/缺口/本线负责人；主框架合同与交叉文件提交所有者；实现与回归；实际程序操作验收；剩余差异；提交号/集成基线。`

缺任何一栏均不能标记工具闭合。公共能力未实现时，与工具一起排入本项目任务，不退化成“等待框架线”。需要主界面/Group/生命周期变化才通过接口交接给主线。

## 当前任务配套表

| 工具/工作包 | 原厂证据入口 | 本线公共能力 | 主框架接口 | 实际产品验收/状态 |
|---|---|---|---|---|
| 图像源 | plugins/image-source-configuration-evidence.json、IMAGE-SOURCE.md | PF-001～009；模式、列表/目录、SDK状态、资源 | 流程身份/运行、资源、相机目录、保存/清理 | 新真实宿主已启动，三模式/本地运行待用户验收；无设备测试 |
| 标定加载 | plugins/read-calib-file-evidence.json、read-calib-file-configuration.json | 公共模型/文件/状态、只读列表、事务回滚 | 资源保存、文档事务、单节点运行 | 与图像源同一验收实例，尚未提交 |
| N 点标定 | plugins/N-POINT-CALIBRATION.md、n-point-configuration-template-gaps.json | PF-010/011及复杂叶模板、对象绑定/保存 | 数组/标定类型、ROI表面、Group/调试输入 | 算法历史保留，7个配置叶未实现；配置验收重新打开 |
| N 图像标定 | plugins/N-IMAGE-CALIBRATION.md、n-image-calibration-evidence.json | 点表/复杂模板/图像与标定状态 | 复合I/O、资源、运行/结果 | 在制证据/实现保留；未注册成闭合交付 |
| 多图采集、缓存图像、光源 | plugins/CHECKLIST.md 对应工具证据 | 公共模型迁移、数组/多图显示、设备抽象 | 资源目录/保存、图像输出、稳定注册 | 历史代码交付保留；公共模型迁移分别重新验收，不做设备测试 |
| 平移旋转、旋转标定 | plugins/TRANSLATION-CALIBRATION.md、rotate-calibration-evidence.json | 复杂点表、配置迁移、显示记录/状态 | 数组/标定类型、运行/保存/身份 | 旧运行证据保留，新的公共配置闭合不能由历史测试替代 |
| 其他标定、坐标系 | plugins/CALIBRATION-DEPENDENCIES.md | 随工具证据拆公共能力任务 | 先登记接口和单一文件所有者 | 其余按依赖推进；坐标系最后 |

全量162项声明仍见 plugins/CHECKLIST.md；新增任务必须使用上述任务卡，不能把静态声明直接算作运行覆盖。
