# IMVSGrayMatchModu 工具逆向主文档

- 稳定工具 ID：`imvsgraymatchmodu`
- 原厂工具：`IMVSGrayMatchModu`
- 原厂类别：Location
- UI 组装类别：结果视图
- 逆向优先级：P1（图像/ROI工具批次）
- 当前状态：source-partial；已闭合资源/输入输出声明、托管 Run/结果桥和原生 Process/ROI 入口索引，算法及释放体未闭合
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSGrayMatchModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主和自研接入映射。

## 已登记清单

参数 31 项，输入 3 项，输出 13 项，UI 控件 39 项，XML 资源 5 项。具体数据见同目录 JSON 附件。

## 当前证据状态（2026-11-05）

### 已确认（原件/源码证据）

- 目标是 `IMVSGrayMatchModuVA`（不与同目录 `IMVSGrayMatchVAModu` 混淆）；主 native、SDK 候选及资源哈希见 `docs/plugins/GRAY-MATCH-IDENTITY-SOURCE-INVENTORY.md`。
- Algorithm.xml 声明图像输入含 `InImage/Width/Height/PixelFormat`，并有 ROI、Fixture 输入；Tab 为 Basic Params、Gray Scale Template、Run Params、ResultShow。Display.xml 的匹配框、Fixture 点、OutputMask 等对象映射已登记。
- Cs IL 确认 `Run → serverSDKManager/ID/SyncSelfRun`，结果经 `QueryModuleOneResult → HasValue/Value → SetModuleResult` 桥回托管对象。
- 原生入口已定位：`Process 0x180023f60`、`ProcessAlg 0x180024320`、`ProcessOutput 0x1800245d0`、`DeInit 0x180022620`、`DestroyModule 0x1800261b0`。SDK header 仅确认候选接口 `Create/DestroyGrayPatMatchV2ToolInstance`、`SetInputImage/ROI`、`Run/GetResult`，不证明实际调用顺序。
- `GrayMatchVAConfigPanalData` 使用 `*.gmvaxml` 模型过滤；参数面板继承 FeatureMatch 公共控件，模板/角度参数 setter 可触发匹配命令，这些是托管源码关系而非 native 算法等价证明。

### 独立重建（尚未证明原生等价）

可据 XML/托管 IL 建立 typed 图像、ROI、Fixture、模型和结果字段的适配层；应保留 `MatchNum/Status`、模型索引、匹配框/点/分数、ROI、BlindPolygon、OutputMask 的独立生命周期，不把显示字段数量当算法输出合同。

### 未知/未闭合

`Process` 的 GetInput 空帧/失败分支，`ProcessAlg` 中 PreproMask、PositionFix、匹配 Run 的实际顺序和 ABI，候选过滤/排序/阈值、ProcessOutput 成功/失败发布门、跨轮旧结果清理，以及 `DeInit/DestroyModule` 对图像、ROI、结果和 SDK 实例的释放顺序均未取体。未运行原厂 VM、native DLL、设备或桌面宿主。

## 证据索引

- `docs/plugins/GRAY-MATCH-IDENTITY-SOURCE-INVENTORY.md`
- `docs/plugins/GRAY-MATCH-MODULE-FIRST-CALLER-SOURCE.md`
- `docs/plugins/GRAY-MATCH-PARAMETER-MODEL-CORE-CHAIN.md`
- 原始 IL/PE 导出与哈希保存在 `analysis/full-audit` 对应工具目录；本卡明确区分入口定位与已恢复方法体。
