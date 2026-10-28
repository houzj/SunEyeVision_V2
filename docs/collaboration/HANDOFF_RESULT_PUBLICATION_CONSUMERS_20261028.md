# 结果发布消费者集中交接

- 基线提交：158b55c6；混合记录模型集成e171e81f。
- 当前分支：codex/workflow-toolbox-dataflow；87f4唯一产品写者。
- 目标：A01–E07之D/E结果唯一发布与完整消费者清理，不代表全部完成。
- 允许修改：UI消费者、专属PropertyList及测试；共享ResultPublication/Registry/正式Release由主线负责。
- 已修改：MainWindowViewModel、MainWindowResultPublication、ToolResultViews、IRootCoordinator、ImageResultViewModel及OutputContractTreeSelectionTests/正式ImageSourceCases。

## 2026-10-28 结果发布消费者整批候选（待正式验证）

树、工具表、正式完成事件、调试回调、选节点声明及历史结果统一到宿主拥有的同一IResultPublication；树表各借自己的lease，不重复查询latest，历史按完整RunIdentity从选定run取唯一snapshot且仅发布处Acquire。换选择先退休publication，再清树表；独立选择intent及逐次epoch门禁防清理、属性通知同步重入覆盖新选择。表首次或重复呈现失败均退休两面并释放借用，重绑保留字段绑定元数据。异步正式事件在UI线程按实际方案节点实例定位所属工作流；调试核session/editor/tab/context/node，旧回调不串流程。

D05混合记录现在按稳定字段/索引路径及弱publication/lease按128分页，标量、几何可展开，仅资源叶明确待接入，不读取像素或延长资源寿命。公共publication重复冻结问题已交主线修复声明校验并借原InputValues。无同帧viewport证明的结果选择只清高亮并明确诊断，不伪造当前图像来源。D06正式变量端口绑定、D07中立图像视口及完整Shell消费者迁移仍未完成；无result的调试异常状态、真实VS/硬件/人工验收继续单列，A01–E07完整范围不缩减。

本批新增同一发布/最后租约释放、表呈现失败、清理重入选择的真实宿主回归源码；四个主要C#纯语法0错及diff检查通过，仅为静态结果。尚未规定UI/全量构建或运行新回归，不引用旧成绩。Group冻结，主线唯一正式Release构建，当前无本线live构建。

- 下一步：主线修公共声明冻结、按projection.Kind选择fallback，合入本批后规定UI/全量Release与全部关联新回归；真实失败交原写者集中修。之后继续D06/D07完整生产链，不保SDK可视双轨。
- 人工验收：未进行；不得标记目标完成。
