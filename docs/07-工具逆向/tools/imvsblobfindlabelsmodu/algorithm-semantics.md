# Blob标签分析 VM 算法语义

- 工具 ID：imvsblobfindlabelsmodu
- 组装类别：result-view
- 优先级：P2
- 当前状态：native-host-boundary-confirmed
- 语义状态：partially-confirmed（宿主调用顺序、参数名、结果字段和外部算法库 ABI 已有原厂静态证据，算法库内部数值语义仍未确认）

## 证据入口

- 原厂模块与外部算法库的 REA 证据已归档：`docs/evidence/bloblabels-rea-algorithm-evidence-20261120.json`、`docs/evidence/bloblabels-external-algorithm-evidence-20261120.json`。

## 执行语义链

| 步骤 | VM 原厂证据 | 当前结论 |
|---|---|---|
| 工具创建/初始化 | 原厂模块构造及外部算法实例创建边界 | source-confirmed |
| 参数读取与条件显隐 | `GetAllParamList`/`SetParam` 参数名已确认，完整 UI 触发器仍待运行证据 | partial |
| 输入/ROI/变量准备 | `ProcessAlg` 设置图像、掩膜和阈值区域的宿主边界 | source-confirmed |
| Process/ProcessAlg/原生桥 | `Process → ProcessAlg → LimitJudge → ProcessOutput`，并调用 `MVD_Algorithm_CreateHandle/Process` | source-confirmed |
| 分支、失败与取消 | 返回码和空结果错误路径可见，取消语义未确认 | partial |
| 结果写回与判定 | `LimitJudge` 和 `ProcessOutput` 输出名已确认，逐值数值语义未确认 | partial |
| 图形/文本显示发布 | 输出图像和结果字段入口已确认，动态显示通知链未确认 | partial |
| 保存恢复 | 原厂配置接口存在，版本迁移和重绑未取证 | partial |
| DeInit/Destroy/释放 | 外部实例销毁及 working/result memory release 导入已确认，精确时序未确认 | partial |

## 本方实现边界

- 本方使用 HALCON 完成图像、区域、连通域和特征计算，HALCON 资源通过 `HalAdapter.Runtime` 适配边界进入工具算法。
- `SmallestRectangle2` 的角度进入嵌套最小外接矩形，`OrientationRegion` 的区域主方向进入顶层 `BlobAngle`。两者保持独立。
- `OverlapRatio`、`MaxOutPixelNum`、`ClassLabel/LabelValue` 的具体语义没有原厂数值证据，本方在执行前给出稳定诊断，不以 HALCON 推测代替。
- `SetParam("BolbOutLineEnable")` 写入 `this+0x15e88`，`ProcessOutput` 读取同一字段，参数到 native 开关的映射已确认。当前仍未把“有 Blob/无 Blob”两条输出路径与本方轮廓投影逐值闭合，本方暂不改变轮廓结果行为。

## 规则

此文件只接受原厂反编译源码、资源、汇编/ABI或受控运行证据。产品意图、截图或自研实现不能单独证明 VM 算法语义。当前本方 HALCON 实现标记为结构对应、数值等价未闭合。

