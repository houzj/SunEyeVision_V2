# 快速匹配 工具逆向主文档

- 稳定工具 ID：`imvsfastfeaturematchmodu`
- 原厂工具：`IMVSFastFeatureMatchModu`
- 原厂类别：Location
- UI 组装类别：结果视图
- 逆向优先级：P1（图像/ROI工具批次）
- 当前状态：source-partial；已闭合托管调用、资源身份、图像/ROI/遮罩入口和原生入口索引，算法体与释放体未闭合
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSFastFeatureMatchModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 41 项，输入 5 项，输出 17 项，UI 控件 49 项，XML 资源 6 项。具体数据见同目录 JSON 附件。

## 当前证据状态（2026-11-05）

### 已确认（原件/源码证据）

- 原件身份与六份 XML、Control/Cs、SDK 依赖哈希见 `docs/plugins/IMVSFASTFEATUREMATCHMODU-SOURCE-INVENTORY.md`。
- Cs IL 已确认 `Run → ServerSDKManager.mModuleManager.SyncSelfRun(ID,true)`；`get_ModuResult` 创建结果对象并经 `QueryModuleOneResult`、`HasValue`、`SetModuleResult` 返回。`GetModel`、`ImportModel`、`ImportModelData`、`ClearModelData` 入口均有精确 IL/RVA 记录。
- `ClearModelData` 实际调用 `IMVS_SetParamValue(handle, ID, "ClearCandidate", "")`，语义是清模板候选参数，不等同销毁 tool/session。
- 原生导出入口已定位：`Process 0x23ef0`、`ProcessAlg 0x24320`、`ProcessOutput 0x25320`、`ProcessOutput4AllPatterns 0x24670`、`SetInputImage 0x26400`、`SetROI 0x273f0`、`SetMask 0x26430`、`SetMaskImage 0x264a0`、`SaveModuleData 0x260d0`、`LoadModuleData 0x23840`、`DeInit 0x21780`、`DeleteModuBuf 0x217f0`、`DestroyModule 0x28750`。
- Display.xml 声明 `InImage`、ROI、模板轮廓/匹配点、结果列表、`OutputMask` 等消费者；Fast 工具首图对象名为 `InImage`，与 HP/Contour 的 `InputImage` 差异必须保留。

### 独立重建（尚未证明原生等价）

可据 XML/IL 建立参数、图像输入、ROI/Mask 传入和结果查询的适配层；不得据导出名推断 SDK 调用顺序、排序/阈值算法或失败返回值。任何托管实现均需将模板训练、运行结果和显示发布分开记录。

### 未知/未闭合

`Process → ProcessAlg → ProcessOutput` 的实际 call chain、异常分支、图像/ROI/Mask 所有权、匹配排序与 LimitJudge 规则、模型二进制格式、重复运行旧结果清理、`DeInit/DestroyModule/DeleteModuBuf` 的完整释放顺序仍未知。未运行原厂 VM、未加载 DLL、未作设备或桌面验收。

## 证据索引

- `docs/plugins/IMVSFASTFEATUREMATCHMODU-SOURCE-INVENTORY.md`
- `docs/plugins/IMVSFASTFEATUREMATCHMODU-CORE-ENTRY-CHAIN.md`（若后续补入原生体，以该文件记录的 ABI/调用点为准）
- 原始 IL/PE 导出与哈希保存在 `analysis/full-audit` 对应工具目录；本卡不把导出表当作方法体。
