# Blob分析 VM 算法语义

- 工具 ID：`imvsblobfindmodu`
- 组装类别：result-view
- 优先级：P2
- 当前状态：source-partial
- 语义状态：**环节级已确认**（confirmed 表示该环节有原厂反编译源码或资源直读支撑，不等于算法整体等价）

本文 9 步与 `evidence.json` 的 `semanticChain` 同步；页面由卡片数据渲染，改这里也要同步改卡片。

## 证据入口

| 文件（本仓库内） | SHA-256 前缀 | 承担的步骤 |
|---|---|---|
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂整工具证据链.md` | `1EA1DCABCC31` | init / process / resultWrite / release |
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂参数资源索引.json` | `999B170188E1` | parameters |
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂输入输出声明.json` | `002530E78813` | resultWrite |
| `docs/collaboration/evidence/B08_BINDING_ORDER_20261031/B08参数绑定执行顺序原生证据-20261031.md` | `B2D5E39B33EE` | parameters |
| `docs/collaboration/evidence/B08_BINDING_ORDER_20261031/B08绑定序列化及重载顺序-20261031.md` | `3419634ECA07` | persistence |
| `docs/collaboration/evidence/B08_NATIVE_DECISION_20261030/B08-Blob低阈值原生参数对象追踪-20261030.md` | `83C94AB916E6` | process（单参数路径） |

原件身份：`IMVSBlobFindModu.dll` = `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`，`IMVSBlobFindModuAlgorithmTab.xml` = `34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC`。

## 执行语义链

| 步骤 | 结论 | 原版证据 | 仍需补什么 |
|---|---|---|---|
| 工具创建/初始化 | inferred | Blob 构造 `18001e230` 写模块 vtable；模块控件由 `ParamsConfigControlBase` 加载模块 DLL 并按 `AlgorithmTabRoot` 绑定 | 构造函数完整体、注册链、初始化异常分支 |
| 参数读取与条件显隐 | confirmed | 96 项参数与 8 个引用组的机器可读索引；`ThresholdType` 默认 2、`connectivity` 默认 8、`HoleMinArea` 默认 0 范围 0–999999999；绑定由 `ModuleFrame!1800bd110` 登记、`1800b98c0` 按执行链逐项处理 | — |
| 输入/ROI/变量准备 | inferred | `InputImage` 含 InImage/Width/Height/PixelFormat 四个强制叶，另声明 `InputROI`、`InputROIAnnulus`、`InputMask`；前驱由 `VmModule.LoadNextAndPreModules` 从 SDK 连接表构建，`GetFrontParamItems` 递归去重 | Domain、坐标变换、掩膜合并顺序、像素格式转换、空值错误 |
| Process/ProcessAlg/原生桥 | confirmed | `CVmAlgModuleBase::Process@180010840` → `18000e810` / `180010a70` / `180013cc0` → Blob `Process@18001fa80` → `ProcessAlg@18001fec0` | — |
| 分支、失败与取消 | inferred | `180013cc0` 失败后仍继续的分支按原证据保留；执行链逐项处理失败即停止 | 失败码集合、取消与连续运行停止路径、异常分支 |
| 结果写回与判定 | confirmed | 成功分支 `LimitJudge` → `ProcessOutput@180020310`；39 输入 / 95 输出节点静态声明；默认 Result List 的 `Angle` 对应外接矩形角度而非 `BlobAngle` | — |
| 图形/文本显示发布 | inferred | `ProcessOutput` 有条件取 `BinaryImage` 与 `BlobImage` 并分别写名称/宽高/格式；9 个 `ImageViewConfig` 视图项已声明 | DisplayType 选择、动态生产者与失效通知、两图像的像素与 Domain |
| 保存恢复 | confirmed | `1800bf230` / `1800bf800` 保存树遍历与按记录顺序导入；单绑定原样本已证保存后读回绑定 | — |
| DeInit/Destroy/释放 | inferred | `Window_Closed` 关闭连续自执行、注销相机等委托并调 `AlgorithmTabRoot.Dispose/DoFinish`；`VM_M_CloseSolution → 180047570 → 180046b20 → 180108c30` 已核分支停止等待 | 算法句柄、图像缓冲、全部订阅与事件、异常路径的释放顺序 |

## 未证实项

1. 具体算法步骤：阈值等号取闭或开、填孔 `T-1/T/T+1`、4/8 连通的对角接触、特征公式、组件排序顺序、ROI 与掩膜作用方式。
2. `1805fd2c0` 大函数产出未取到（既有定点取体超时）。
3. 两路输出图像的像素、格式、尺寸、筛选关系与 Domain。
4. 动态 IO 的平台/代理生产链。
5. 原机差分未执行（无隔离 Windows 环境）。
6. 我方 HALCON 实现与原版的数值等价性未建立基线。

## 规则

只接受原厂反编译源码、资源、汇编/ABI 或受控运行证据。产品意图、截图或自研实现不能单独证明 VM 算法语义。`confirmed` 只表示该环节的调用边或声明已被源码支撑，不表示整条算法链等价。

## 2026-11-08 证据迁移回填

此前引用但未随库迁入的细项原文已迁入 docs/collaboration/evidence/，索引见 Blob-原厂证据迁移清单-20261108.md。参数窗口组装、参数合同、量化校验和结果列映射证据已可复查。原文仍明确保留的算法边界和原机差分缺口不因迁移而升级为 confirmed。
