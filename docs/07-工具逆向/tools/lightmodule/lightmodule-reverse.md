# 光源 工具逆向主文档

- 稳定工具 ID：`lightmodule`
- 原厂工具：`LightModule`
- 原厂类别：Collection
- UI 组装类别：图像与 ROI
- 逆向优先级：P1
- 当前状态：source-partial；资源、参数、图像/ROI声明及 Light 专项托管证据已登记，Process/设备与释放体未闭合
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Collection\LightModule`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 32 项，输入 7 项，输出 26 项，UI 控件 58 项，XML 资源 6 项。具体数据见同目录 JSON 附件。

## 当前证据状态（2026-11-05）

`docs/plugins/LIGHT.md`、`light-evidence.json`、`light-managed-evidence.json` 已确认原目录资源身份、XML 参数/输入/输出声明、托管配置/执行边界及 Light 结果字段。输入为图像/ROI相关组合，输出含运行状态与光源结果；具体字段以本卡 `parameters.json`/`resources.json` 为准。

已确认：资源和 UI 声明来自原件清单；托管层存在配置与结果桥的独立证据；工具属于设备/光源侧，不能将图像源模块行为代替其执行语义。

未知：原生 Process/ProcessAlg 的设备调用、图像/ROI 实参、失败/超时与重连分支，结果缓存与跨轮清理，方案保存恢复以及 DeInit/Destroy 的设备句柄和结果释放顺序。未运行原厂设备或 VM。

## 证据索引

- `docs/plugins/LIGHT.md`
- `docs/plugins/light-evidence.json`
- `docs/plugins/light-managed-evidence.json`
