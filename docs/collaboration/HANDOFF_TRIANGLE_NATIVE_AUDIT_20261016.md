# Triangle阈值原生等价性审计交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，保留并存修改，未提交推送。
- 任务目标：先确认Triangle是否有兼容的HALCON原生流程，满足兼容前提才实施。
- 允许修改目录：本地算法方案与docs/collaboration；本轮无须修改源码。
- 已修改文件：全算法工具HALCON标准流程分批优化方案-20261015.md、CURRENT_TASK.md及本交接。

## 结论

针对项目当前使用的HALCON 24.05.0.0，未找到可直接等价替换当前Triangle的官方原生流程。审计完成，保留现有兼容实现，不将Otsu、平滑直方图法、字符阈值或多区间分割冒充Triangle。没有实施算法替换，也没有新增替代模式。

核对范围包括libs/halcon/net/halcondotnetxl.xml、本机HALCON版本和阈值算子参考、本机已安装过程库，以及ImageThreshold实际执行、结果和区域处理代码。本机过程库Triangle文本命中为结构光标定板三角标记，并非阈值过程；已安装examples目录不包含完整Segmentation示例，不能据此宣称检索过全部官方示例。结论限定当前版本和已核对接口，不外推所有未来版本。

## 原生候选兼容比较

| 流程 | 阈值选择规则 | 不能直接替换的原因 |
| --- | --- | --- |
| BinaryThreshold max_separability | Otsu类间可分离性最大化 | 准则不同，ThresholdUsed与区域可能改变 |
| BinaryThreshold smooth_histo | 平滑到仅一个极小值后取该位置 | 准则不同，不是峰值到尾部的最大距离 |
| AutoThreshold / HistoToThresh后Threshold | 平滑直方图极小值产生多个区间 | 可能返回多个区域/上下界，需新增区间选择及阈值回写规则 |
| CharThreshold | 峰值左侧按频次比例提取暗字符 | 方向、比例参数及目标假设不同，不含当前长尾最大距离规则 |
| GrayHisto后使用元组/距离算子实现Triangle公式 | 仍由应用定义峰值、尾部和最大距离选择 | 只改变公式执行载体，不是官方标准Triangle流程，也不减少自定义算法语义 |

依据：[BinaryThreshold方法列表及定义](https://www.mvtec.com/doc/halcon/2405/en/binary_threshold.html)、[HistoToThresh区间规则](https://www.mvtec.com/doc/halcon/2405/en/histo_to_thresh.html)、[阈值算子总览](https://www.mvtec.com/doc/halcon/2405/en/toc_segmentation_threshold.html)、[CharThreshold方向及比例规则](https://www.mvtec.com/doc/halcon/2405/en/char_threshold.html)。

## 当前实现及保留的契约

仅ImageThreshold提供Triangle=16；Threshold工具没有该枚举。本轮不为形式统一而增加入口，不修改枚举值、参数类型、默认方法或显示文本。

现有流程在检测区减屏蔽区后的实际图像domain内调用GrayHisto，仅接受byte。首个最大频次灰度为峰值；非零范围外可用时各扩一格零频次；两侧选较长尾，等长取右侧；按灰度升序取峰尾直线距离最大点，等距保持先到项。单灰度返回峰值，空直方图当前返回0；本轮不改变这些兼容行为，也不声称与其他库Triangle逐像素等价。

选择区域仍为HALCON Threshold的亮侧含阈值区间，Invert仅对有效domain取差集。OutputImage使用MaxValue亮值，BinaryMask保持0/255；ThresholdUsed、MaxValueUsed、MethodUsed=Triangle、面积/比例/灰度统计和检测/屏蔽几何输出均保持。订阅、拼装、位置修正及绑定身份路径没有修改，不自动压缩位深，不回退另一种算法，不修改标定。

## 完成与验收

- 已完成：当前版本原生能力审计、候选流程语义比较、现有绑定/输出/屏蔽路径静态核对、本地计划状态收尾。
- 未实施：原生Triangle替换，因为等价与兼容前提不成立；不是遗漏的实现待办。
- 已执行构建与测试：本轮只改文档，未构建、未运行测试、程序或设计器，没有新的发布产物。
- 人工验收结果：未开展；历史阈值批次的原图、屏蔽边界和位深验收仍待人工，不因本次审计标为通过。
- 已知风险或禁止改动：不得复用Triangle枚举静默替换为其他自动阈值，不能按输出均为区域就判断算法等价；不修改旧方案、绑定、输出、通信或标定。
- 下一步：本项按保留兼容实现收尾。如未来明确选择其他原生阈值，应独立定义模式和迁移影响；不在本次追加新功能。
