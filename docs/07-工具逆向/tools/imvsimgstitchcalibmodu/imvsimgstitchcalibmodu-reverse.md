# 图像拼接 工具逆向主文档

- 稳定工具 ID：`imvsimgstitchcalibmodu`
- 原厂工具：`IMVSImgStitchCalibModu`
- 原厂类别：ImageProcessing
- UI 组装类别：结果视图
- 逆向优先级：P2
- 当前状态：清单登记，尚未开始源码级闭环逆向
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\ImageProcessing\IMVSImgStitchCalibModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 13 项，输入 2 项，输出 6 项，UI 控件 21 项，XML 资源 6 项。具体数据见同目录 JSON 附件。

## 当前证据状态

本页只完成工具身份和资源清单登记。源码成员、原生调用、运行时序、失败路径、结果所有权和释放链仍待 S0–S7 逆向卡闭合。

## 下一步

先核对原厂版本和资源哈希，再获取 UI 入口与执行入口，之后补参数读写、输入输出、结果、保存恢复和关闭释放。
