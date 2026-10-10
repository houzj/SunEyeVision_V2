# 触发参数自动比较实施记录

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main；当前执行者：Codex。
- 任务目标：移除开关，手填或绑定自动比较，未填写且未绑定不比较。
- 允许修改目录：通讯判断模型、触发配置 UI、对应回归与本地方案记录，保留其它用户改动。
- 已修改文件：CommunicationTriggerEvaluation.cs、CommunicationAutomationDefinition.cs、CommunicationTriggersView.xaml 及代码文件，CommunicationActionEqualityTests.cs、CommunicationTriggerOptimizationTests.cs、CommunicationAutomationUiTests.cs，方案基线及 CURRENT_TASK.md。
- 已完成：删除开关和局部回写事件，ParamBinding 始终可编辑。EvaluateEquality 以来源是否为空决定比较，旧开关字段忽略。类型适配、等于比较和稳定输出身份保留，不向节点赋值。
- 未完成：真实设备及亮暗主题、失焦、高 DPI、可见 VS 设计器人工验收。
- 已执行构建与测试：Release 通讯/OPC UA 158项及独立UI 1项，共159项通过。补充绑定空字符串仍比较、失效绑定拦截、解除绑定清空放行、空格作为文本及旧开关失效的回归；Final UI Release 1060警告0错误。原路径实际 EXE 无硬件启动探针退出0、2.0.0.8、Dark。设计时输出属性查询通过。
- 人工验收结果：待用户测试 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
- 已知风险或禁止改动：已有非空参数会按新要求自动比较，即使旧开关为 false；空输入统一表示不比较，不支持手填空字符串条件，绑定输出的空字符串仍可用于等于判断。失效绑定不静默放行，清空参数才解除条件。保留旧配置字段，不变更 SDK、字符串来源、发布规则或节点传参边界。
- 下一步：验收有值相等/不等、清空、重新绑定与失效路径；发布需另行申请。未打包、增号、提交、推送或同步其它工作区，没有强制结束用户进程。本次没有跨 AI 交接，接手前按项目规则确认状态。

证据在 artifacts/validation/communication-trigger-auto-condition-20261010，最终 communication-runtime.trx、trigger-ui.trx、Release-startup.json。本轮159项为独立结果，不与前轮重叠用例累计。
