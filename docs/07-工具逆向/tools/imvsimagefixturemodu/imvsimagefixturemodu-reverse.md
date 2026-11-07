# 图像修正 工具逆向主文档

- 稳定工具 ID：`imvsimagefixturemodu`
- 原厂工具：`IMVSImageFixtureModu`
- 原厂类别：ImageProcessing
- UI 组装类别：图像与 ROI
- 逆向优先级：P1
- 当前状态：source-partial（声明级）；图像/Fixture输入输出已登记，源码执行与释放链未恢复
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\ImageProcessing\IMVSImageFixtureModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 0 项，输入 2 项，输出 4 项，UI 控件 14 项，XML 资源 6 项。具体数据见同目录 JSON 附件。

## 当前证据状态（2026-11-05）

已确认：原件 XML 清单声明 2 个输入、4 个输出、无独立算法参数；资源/UI 共 6 份资源、14 个控件。输入/输出的具体名称、组合和强制性保存在本目录 JSON，并以 `docs/plugins/inventory.json` 的原件哈希为身份依据。

未知：托管 Run/结果桥、Fixture/ROI 与图像的实际传递、Process/ProcessAlg/ProcessOutput 调用顺序、校正变换数学、失败/空图像行为、保存恢复以及图像/Fixture/ROI/结果的释放所有权。未加载或运行原厂 DLL/VM。

## 证据索引

- `docs/plugins/inventory.json`（原件 XML/PE/资源身份）
- `parameters.json`、`resources.json`（当前声明快照）
