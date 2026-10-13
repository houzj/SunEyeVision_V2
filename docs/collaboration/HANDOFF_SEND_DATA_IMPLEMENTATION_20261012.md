# 发送数据首批实施交接

- 基线提交：91199b7529b0e5de2a0b67d9f047c985e2e912e1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：P4首批设备／事件／变量发送，保留统一传递重构后置。
- 允许修改目录：本通信工作区SDK、Workflow、UI服务适配、新SendData工具、测试、构建引用和docs；不改主项目或标定逻辑。
- 已修改文件：SDK CommunicationSendService及请求／上下文／执行器、数据提供器和绑定类型；Workflow运行请求、AlgorithmNode、计划注入和发送行依赖；UI WorkflowCommunicationSendService及应用服务、运行管理器、数据查询工厂；tools/SunEyeVision.Tool.SendData；解决方案和测试引用／锁文件；发送专项与OPC集成测试；方案、计划和CURRENT_TASK。
- 已完成：全部预检后发送、单次计划、原生错误跳过、取消／失败诊断；实际来源和类型约束，事件稳定参数无测试值回退；变量唯一生产绑定与本次输出发布；主题编辑器与模块结果／分析、Release部署与实际加载。
- 未完成：队列、单客户端身份选择、变量逐项忽略错误、全部OPC原生类型／真实设备验收、编码UI及其他P4边界和P6；直接全局绑定统一、方案原子冻结及U1至U3仍后置。
- 已执行构建与测试：最终39项通信／15项新增发送包含在其中，20项SDK执行专项，共59项通过，不是全套。标准全量Release0错误1152警告；UI Release0错误1047警告。真实常用Release toolbox-probe退出0，9类24工具，ReceiveData／ProtocolParse／ProtocolAssemble／SendData顺序及图标正常；版本2.0.0.8、SDK2.1不变。git diff --check及本地文档链接复核。
- 证据：artifacts/validation/send-data-20261012/editor-dark.png、editor-light.png、release-toolbox.json。主题渲染已目视检查，设计时TargetPath对应实际Release插件目录；未开启VS设计器或用户硬件。
- 人工验收结果：用户尚未验收配置窗口、高DPI和设备；实际模拟TCP／Modbus／OPC成功不替代用户设备验收。
- 已知风险或禁止改动：TCP服务端是现有广播，成功不等于所有客户端或业务成功；超时／取消可能已有副作用，不自动重试；变量当前首批原子结果，不改持久化初值。SDK注入跨模块但不恢复旧插件兼容。不要误称P4全部完成或提前启动U1至U3。保留原文档修改，无提交／推送或跨AI移交。
- 下一步：完成剩余P4边界与验收、必要编码UI、P5队列和P6；工具链完成后复核实际代码，再按六项设计执行U1至U3。
