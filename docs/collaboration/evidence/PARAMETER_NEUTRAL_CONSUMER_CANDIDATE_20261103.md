# Blob 参数职责命名：消费者候选交接

候选工作区 `D:/MyWork/line-aiproject/blob-display-neutral-20261103`，源码提交 `4e735338ba80f2c130687ab203f8e2a8eaa4dad2`。本批为产品职责命名和资源定位归一，不改变参数身份、控件功能或算法；候选验证不代主仓正式验收。

消费者测试提交 `e8028cdc0` 调整 17 个现行测试文件，按命名与 URI 反向映射后，与基线 `8962f6bdb` 逐行比较无其他差异。`src/UI` 和 `tools` 未发现本批需变更的实际引用；原厂来源记录及旧别名不存在的断言保留。公共产品 `a4300d81e` 和相对命名修复 `e567eed25` 互接为候选 4e。

标准候选 UI002 用时 2:18.23，6553 警告、0 错误；full002 用时 1:42.95，6515 警告、0 错误。原始日志按字节保存于 `Release/_validation/parameter-neutral-consumer-20261103/`，SHA 见同名 JSON。此前 UI001 的 5 处 `NativeRoiSession.cs` 旧相对命名错误和失败日志仍保留；公共修复后重建通过，未把失败版本记为通过。

同一候选产物的三轨自然退出 0：

| 测试 | 尝试 | 通过组数 | 实际加载核对 |
| --- | --- | ---: | --- |
| B08 混合窗口 | `B08NumericComposition/attempt-011` | 12 | 15 项路径及 SHA 一致 |
| ROI Preview，完整范围 | `BlobRoiPreview/attempt-005` | 24 | 15 项路径及 SHA 一致 |
| 图文显示 All | `BlobResultDisplay/attempt-005` | 44 | 15 项路径及 SHA 一致 |

各尝试位于候选 `Release/_validation/S4-20261025/`，原 `receipt.json`、`loaded-product.tsv` 的 SHA 均见同名 JSON。ROI 清理测试中的故障注入异常属于测试场景，整轨最终退出 0。

测试结束后候选 `release-payload.json` 的 201 条载荷逐文件 SHA 一致；包含清单共 202 文件，清单 SHA256 为 `842397D993B7F1847D2C5F020D14873CCB18E1C978834B47B97006A26E6E8120`，与测试前一致。候选载荷数量与主仓正式载荷分开记录。

实际 VS、自然键鼠和用户统一人工验收另记；缺 producer、位置修正、连续会话、原厂全域等仍按已有方案保护或待做，不能因本次命名及回归通过而称为已实现。
