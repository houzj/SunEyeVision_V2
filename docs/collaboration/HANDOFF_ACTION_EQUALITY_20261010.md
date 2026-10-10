# 动作等于判断实施记录

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main；当前执行者：Codex。
- 任务目标：按最新本地基线接入触发参数的动作等于判断，明确字符串来源范围及通讯编号，更新 Release 测试程序。
- 允许修改目录：Workflow 通讯模型及纯比较器、UI 通讯处理与配置、对应测试和协作记录。保留原发布、工作流执行副本能力及其它用户改动。

## 已修改文件

CommunicationAutomationDefinition.cs 增加判断开关、判断输出稳定 ID 和字符串来源设备。CommunicationTriggerEvaluation.cs 提供纯 EvaluateEquality 及结果模型；CommunicationAutomationService.cs 在派发前调用比较并过滤字符串来源。

CommunicationAutomationViewModels.cs、CommunicationManagerAutomation.cs、CommunicationTriggersView.xaml 及代码文件更新行内配置、候选列表、主题化开关、长名称提示和工作流显示名称。CommunicationActionEqualityTests.cs 新增27项测试用例，CommunicationAutomationUiTests.cs 和 CommunicationTriggerOptimizationTests.cs 更新对应断言。方案基线和 CURRENT_TASK.md 同步当前状态。

## 已完成

动作条件只做等于。ParamBinding 常量按判断输出类型解析，绑定取自同一次事件快照；支持现有 Boolean、Int16、UInt16、Int32、UInt32、Float 和 Double 及 String 标量，不统一转字符串，不支持对象或集合隐式比较。数值绑定转换须范围有效并可往返无损；浮点非有限值拒绝，文本区分大小写并保留空白。

单输出自动确定并在界面配置中保存身份，多输出必须选择。启用开关区分不限制与空字符串；旧规则默认关闭。比较不相等不派发，转换失败、缺失输出和失效绑定告警。关闭判断时残留失效参数不会阻止事件执行。保留原接收边沿、监视和忙碌跳过、不排队，不将触发参数赋给节点。

复用现有主题 ToggleSwitch；交互测试发现视觉切换未更新本页配置，已通过本页 CheckedChanged 回写并恢复双向绑定修正，不修改共用 SDK。开关启用、关闭、模型更新和再次点击均已覆盖。

字符串触发新建规则必须选择来源设备；null 旧范围保留并明确显示全部来源，显式星号表示全部来源，空来源不执行。保留数据上传及 UTF-8 校验，不检查内容与旧不匹配兼容，未知模式不静默放行。移除执行目标的编号后缀，实际目标仍用工作流 Id；通讯状态响应的编号映射不删除、不重排。

## 已执行构建与测试

- Release 通讯与 OPC UA 157项、独立触发 UI 1项、发送 UI 2项、心跳10项，共170项通过。新增27项包含各类型、范围、非法值、多输出、重命名和失效绑定、配置持久化、上升沿与动作条件独立、组装输出及来源范围。原忙碌和执行链回归保留。
- 全量 Release 964警告、0错误；最终独立 UI Release 1060警告、0错误。存在历史编译警告，不表示零警告。
- Release 原路径实际 EXE 无硬件启动探针退出0，2.0.0.8、alpha、Dark。已检查 Release DLL 的 Accept 方法调用 EvaluateEquality、读取 Matches 后再 Dispatch，避免仅按源码推断运行行为。
- Release 设计时 TargetPath、OutDir、ApplicationIcon 查询通过，不等于可见 Visual Studio 设计器验收。

证据目录为 artifacts/validation/communication-action-equality-20261010。最终回归以 communication-runtime.trx、trigger-ui.trx、CommunicationSendEventUiTests.trx、CommunicationHeartbeatTests.trx 为准，Release-startup.json 为启动探针结果。早期测试类构造错误、测试宿主启动失败和主题开关断言失败已经修正，不将早期结果作为通过依据。

测试 Release 目录的三个历史宿主 DLL 阻止 testhost 查找系统 .NET，已定向移至 artifacts/runtime-host-backups/Tests-Release/62becf1b607f40778423fe65f50a11fe 并逐一校验哈希。文件可恢复，没有删除、清空目录、修改系统运行时或更改发布规则。

## 人工验收与未完成项

尚未收到本轮人工验收。用户运行 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe，先打开判断开关，再选择判断输出并设置触发参数。旧规则默认关闭，不能只填参数后期待过滤生效。验证相等与不相等、空字符串、多输出、来源设备、保存重开及亮暗主题、失焦、窄窗口、高 DPI。

真实 OPC UA 及其它设备业务、可见 VS 设计器、完整交互显示待现场验收；探针没有连接真实硬件。通信工具链向节点传参仍不在本轮范围。Release 文件夹的 v1.0.0 是历史路径名，实际版本仍2.0.0.8。

## 已知风险与下一步

未制作安装包、递增版本、提交、推送或同步通讯分支，没有强制结束用户进程。不要清空原 Release、方案数据、图标关联或其它未提交改动。后续跨 AI 接手前按项目规则确认状态并创建有说明的交接提交，本次没有跨 AI 交接。

用户验收 Release，记录结果后再决定是否发布；打包需用户单独申请。
