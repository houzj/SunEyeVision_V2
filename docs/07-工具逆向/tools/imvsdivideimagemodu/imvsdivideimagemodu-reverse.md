# 划片拆分 工具逆向主文档

- 稳定工具 ID：`imvsdivideimagemodu`
- 原厂工具：`IMVSDivideImageModu`
- 原厂类别：SplitCombination
- UI 组装类别：图像与 ROI
- 逆向优先级：P1
- 当前状态：source-partial（声明级）；分片图像/ROI端口与参数已登记，执行、输出集合和释放链未恢复
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\SplitCombination\IMVSDivideImageModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 4 项，输入 3 项，输出 6 项，UI 控件 8 项，XML 资源 6 项。具体数据见同目录 JSON 附件。

## 当前证据状态（2026-11-05）

已确认：原件 XML 清单声明 4 个参数、3 个输入、6 个输出和 8 个 UI 控件；工具类别为 SplitCombination，图像/ROI 分片声明保存在 `parameters.json` 与 `resources.json`。

未知：分片网格/边界参数如何进入 ProcessAlg，ROI 与输入图像坐标是否裁剪或复制，输出数组顺序/数量、越界与空输入错误，结果缓存、方案保存恢复和 DeInit/Destroy 对各分片图像缓冲区的释放。未运行原厂 VM 或 DLL。

## 证据索引

- `docs/plugins/inventory.json`（原件 XML/PE/资源身份）
- `parameters.json`、`resources.json`（当前声明快照）
