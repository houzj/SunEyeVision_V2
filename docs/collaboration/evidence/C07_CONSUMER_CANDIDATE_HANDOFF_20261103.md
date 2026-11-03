# C07 消费者与公共组合候选交接

候选工作区：`D:/MyWork/line-aiproject/blob-display-neutral-20261103`；最终候选提交 `e77f92a2c53bee2541f17babc1ca0e3206c8d6e4`。候选结果不等于主仓正式验收，也不代表 B08 或 Blob 工具整体完成。

## 最终组合入口候选

`e77f92a2c` 接入 `fe8276611`、`fad3bf336` 的真实默认组合索引入口修正；`git diff --name-status fad3bf336 e77f92a2c -- src tools` 无输出，两个固定提交的全部产品源码同源。Catalog 不在该对比或候选构建结论内。

在候选工作区独立构建，UI 50.43 秒、6553 警告、0 错误；全量 Release 43.59 秒、6555 警告、0 错误。公共 WPF `Release/_validation/C07Reference-20261103/attempt-001/receipt.json` 自然退出 0，十组通过，包括默认组合可编辑索引、普通数值只读、原树及弹窗、引用后缀、来源变更和范围事务。源测试 SHA256 为 `A64322A13CAEB206DECCF12CEFEAED2C47022FEDCB1A629BA71EAA077F3FA93A`；实际加载 `SunEyeVision.Plugin.Contracts` 与 `SunEyeVision.Plugin.Wpf` 两项路径和 SHA 均与候选一致，详情以回执和 `loaded-product.tsv` 为准。

候选 `release-payload.json` 来源为 e77，201 条载荷全部逐文件哈希一致，目录含清单 202 文件；清单 SHA256 为 `08C27B4D808FDEF90794060B737E0E784A66AAC3A67C7348DF8D89019498EAA4`。不与主仓正式载荷数量混称。

## 旧版本证据与失败保留

- `280d735a3`：候选真实引用窗口 `S4-20261025/BlobReferenceWindow/attempt-004` 九组、B08 混合窗口 `S4-20261025/B08NumericComposition/attempt-010` 十二组自然通过；两轨各 15 个实际加载程序集当时均与候选 SHA 一致。候选载荷 201 项清单 SHA `F1B7AC5B525718A0923B1FD0D71A5F3085399CA7B751CFBC811DB743DA9E782E`。不转签到 e77。
- `5818f03a5`：`BlobRangeProduction/Circularity/All/attempt-002`、`Rectangularity/All/attempt-002` 各 16 组通过；`Area/All/attempt-003` 前 13 组通过、第 14 组因旧测试假设切换标量时自动丢弃索引而失败，原失败及日志保留。`c88c2d0fe` 更新整数范围测试为真实清来源再选标量；不得说该旧 Area 整轨通过。
- 引用窗口旧失败分别保留：原切标量未处理保索引拒绝；撤销断边后直接消费已失效的上游 publication。`4836b1b93` 补真实 UI 清来源重选，`05a063033` 断言旧结果失效后重跑上游；修正后的九组通过属于上述 280d 证据。

## 边界

来源文字只作显示，身份、类型、成员路径和索引仍使用结构化引用；图像显示组合父项而保留资源成员路径。未声明生产者、Group、偏好持久化、连续运行及算法等未完成范围不因候选通过而实现。真实 VS、自然键鼠及用户统一人工验收单独记录；主仓正式构建和生产矩阵由主线独立执行，不能将候选回执移签。
