# 冻结结果、Blob 声明与现有主窗接线（有限交付，继续原计划）

- 基线提交：353f12f9（上游 fc08012d 数值修复及 07b0413f Presenter）。当前执行者：Codex，唯一主树产品写者。
- 目标：原 J02/J03/L01 依赖中的正式结果声明、实际 Blob 代表工具、现有结果表按需读取。无载体/ALC变更，无算法、过滤或排序新增。
- 修改范围：SDK 输出声明/元数据/显式 ROI 与图像适配、Infrastructure 元数据缓存、Workflow 冻结目录/根输入接线、现有 MainVM/结果控件、Blob 及定向测试。
- 实施：复制声明保留稳定 FieldId/TypeRef/Nullable/显示配置，不复制 RuntimeValue；结果按准入时声明一次冻结，成功/失败/未执行皆保冻结名称元数据及完整性。指定 run 原资源 Owner reader 保留，切节点/关闭释放。现有 FlatResultRows 消费公共 Presenter，数组展开仅取一页；现代完整声明才走新路径，未支持声明不冒完整。
- Blob：原候选区域索引在排序前写 invocation-local ElementId；新增正式 record sequence，与原统计并存。轮廓/旋转框的地址从同一个 Blob 对象建立，不以长度、位置、Label猜对应。原空 HALCON region 不应成为 Blob，面积0跳过。原输出图像/region由唯一 ResultResourceOwner 释放，输入图像仍借用调用者。
- 跨模块发现并修复：WorkflowRunRequest.InputImage 原先只在 Context 中，AlgorithmNode 真工具准备收到null；现只给本次可执行节点传借用根输入，显式缺失上游图像仍失败，不回退根输入，不回写设计参数。
- 已执行：owned result-bridge-01：Blob native04 原4PASS；native05 新地址断言4PASS。MainVM host01 1PASS，最终受影响 Presenter13+MainVM1=14PASS。ROI适配2PASS（七类既有参数约定/冻结/未知拒绝）。UI build05 0错813警告24.09s；SDK build08 0错1警告3.70s。后补失败输出冻结元数据断言待本轮最终Blob构建/运行。早期真实失败日志/TRX均保留。
- 未完成：此为有限片，资源/几何展示仍占位，按既有配置分组、树图实际选择消费、三代表工具和完整编辑/资产/性能验收仍沿原48项推进；不计三投影或整包通过。已有 a45 数值独验缺陷由协调独立fc080包再验，旧包不覆盖。
- 人工验收：尚未请求；最终同一Release运行与VS设计时统一一次清单。
- 下一步：协调释放窗口后全量Release、实际Release程序、固定源/二进制新包交独测，继续原剩余依赖，不停交接。禁止写旧immutable根、重复旧guard/a45已闭项、扩载体范围。
- 身份可信度补充：新增不等面积9/20/42、不等位置、面积筛掉9且降序确实改变native顺序的真实HALCON样本；逐条地址唯一并核record ElementId/centroid与轮廓和旋转框空间中心一致。blob-address01实际1PASS，不重复原同面积4项；树图选择消费仍未冒称完成。
