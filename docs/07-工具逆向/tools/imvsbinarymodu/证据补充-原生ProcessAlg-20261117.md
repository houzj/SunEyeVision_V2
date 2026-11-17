# 图像二值化 · 原生 ProcessAlg 证据补充（2026-11-17）

## 一、取证方法（可复现）

| 步骤 | 命令 / 事实 |
|---|---|
| 目标文件 | `C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\ImageProcessing\IMVSBinaryModu\IMVSBinaryModu.dll` |
| SHA256 | `c03bcb19ef456206e3abf4d2a8d5279be37c12eb5662e191f0965029cdabeb48`（215,552 字节） |
| 托管判定 | `rea inspect-managed-artifact` → `classification.status = not-managed`（PE 无 CLI 目录）→ 算法不在托管层 |
| 导出表 | `rea inspect-native-exports`，类 `CIMVSImageBinaryModu`：`Process`(0x18001e8d0)、`ProcessAlg`(0x18001ec00)、`ProcessOutput`(0x18001f100)、`Init`(0x18001e750)、`DeInit`(0x18001e260)、`GetParam`/`SetParam`、`DeleteModuBuf`、`ResetMember`、`SetAlgToolSharedMemNodeNum` |
| 反编译 | `rea decompile <dll> 0x18001ec00 --format json`（Ghidra 12.1.4），伪代码 6,402 字符 159 行 |

托管侧证据见同目录上一批：`docs/07-工具逆向/decompiled/Managed/Binary/Cs/IMVSBinaryModuCs/`（参数 15 项、结果结构 9 项、SDK 调用链）。

## 二、ProcessAlg 实际职责（已闭合）

伪代码显示 `ProcessAlg(this, input, output)` 不含任何阈值运算，职责是**装配与转发**：

1. 入参空校验 → 失败记 `HSlog_LogPrint` 并抛 `0xe0000001 / 0xe0000002`
2. `SetAlgToolSharedMemNodeNum(this)`：设置算法工具的共享内存节点数
3. 通过 `this + 0x15d98` 指向的**算法对象虚表**下发输入图像（`param_1 + 0x10`）与每个固定 ROI（`vtFixRoiShapeObj`，`param_1 + 0x20` 起）
4. 掩膜分支：当 `*(int*)(param_1 + 0x110) == 0` 且 ROI 多于一个时调用 `GenerateMaskImage`（基类虚函数 `+0x48`），失败直接返回错误码
5. 否则把掩膜图像指针（`param_1 + 0x118`）下发给算法对象（虚表 `+0x80`），并用 `GetRegionImageAssistant`（虚表 `+0x90`）逐个下发 ROI
6. 结束时置 `*(this + 0x15d90) = 1`，把耗时（`FUN_180019ee0()` 差值转 float）写入输出结构，`return 0`

**结论**：`IMVSBinaryModu.dll` 是模块外壳；二值化的 5 种类型（Hard=1 / Mean=2 / Gaussian=3 / OTSU=4 / Sauvola=8）判定与像素运算发生在**共享算法对象**（基类 `CVmAlgModuleBase` 体系，虚表位于 `this+0x15d98`）所在的算法库里，本 DLL 内不可见。

## 三、未闭合项（需下一目标取证）

| 项 | 说明 | 下一步 |
|---|---|---|
| 算法对象所属模块 | 虚表来源未定，需定位 `this+0x15d98` 实例由哪个 DLL 创建 | 反编译 `Init`(0x18001e750) 找 `new`/工厂调用与导入的算法库名 |
| 5 种二值化类型的参数使用 | 阈值/核尺寸/偏移/ Sauvola 系数与窗口如何进入算法库 | 在算法库内按 `BinaryType` 常量分派取证 |
| 端点与边界行为 | 大于等于 / 小于等于 / 等于 / 不等于的比较语义、越界像素处理（`NonROIPrcType` Zero/Src） | 同上，结合运行时受控输入验证 |
| 结果与掩膜所有权 | `OutputImage`/`OutputMask` 与 ROI 结果的生命周期 | 反编译 `ProcessOutput`(0x18001f100)、`DeleteModuBuf` |

## 四、对开发的直接约束

- 在算法库证据补齐前，**不得**以"标准二值化语义"直接实现工具；`BinaryType` 各分支的阈值口径、Sauvola 公式、OTSU 统计域均属未确认。
- 已确认可用的部分：参数名/类型/范围/默认值与枚举值（来自 `ImageBinaryParam` 反编译）、输入项（`InImage/InImageWidth/InImageHeight/InImagePixelFormat`）、结果字段结构、ROI 类型集合（Full/Box/SectorRing/Polygon/Sector）。

## 五、算法链路追查结果（同日续查）

| 层 | 文件 | 大小 | 事实 |
|---|---|---|---|
| 1 模块外壳 | `IMVSBinaryModu.dll` | 215KB | 已闭合（见上） |
| 2 算法实现入口 | `MVDAlgorithmSDK\Runtime\x64\MVDImageBinaryCpp.dll` | 40KB | 导出仅 3 个：`CreateImageBinaryToolInstance`/`DestroyImageBinaryToolInstance`/`_Init_locks`；工厂 `operator_new(0x70)` 后挂 `MVDInternal::ImageBinaryImpl::CImageBinaryToolImpl::vftable` |
| 3 核心算法库 | `MVDAlgorithmSDK\Runtime\x64\MVD_Algorithm.dll` | **51,454KB** | 导出仅 19 个且无 Threshold/Binary/Otsu/Sauvola 命名；算法体在 C++ 内部符号中，未导出 |

`MVDImageBinaryCpp.dll` 导入：`MVD_Algorithm.dll`、`MVDImageCpp.dll`、`MVDShapeCpp.dll`、`MVDPreproMaskCpp.dll`、`MVDCommon.dll`。

`CImageBinaryToolImpl` 的伪代码中不含参数名字符串（`LowThreshold`/`BinaryType`/`Sauvola` 均未出现），说明参数按索引访问、算法继续下沉。

**结论**：静态取证继续下探需在 51MB、无导出符号的核心库内做符号重建与调用点定位，成本极高且收益不确定（最终仍是等价实现）。

## 六、建议的替代取证路线（受控黑盒）

用原厂程序 + 受控输入做黑盒测定，比静态挖 51MB 核心库更可靠：

1. 构造已知灰度梯度图（0..255 全值），分别用 5 种 `BinaryType` 执行，读取输出二值图，反推：
   - 硬阈值：`LowThreshold`/`HighThreshold` 与 `CompareType` 的端点包含关系（>= / <= / = / !=）
   - OTSU：实际分割阈值（与原图直方图对比即可算出）
   - 均值/高斯：`KernelWidth/Height`、`GaussKernelSize/GaussSigma`、`ThresholdOffset` 的参与方式（改参数看阈值漂移）
   - Sauvola：`SauvolaAdjustCoef/Range/SegType/WinWidth/WinHeight` 的影响曲线
2. `NonROIPrcType`（Zero/Src）对 ROI 外像素的处理，用单 ROI + 半幅梯度图直接观察
3. 交叉验证：同一输入用 HALCON 等价算子（`threshold`/`binary_threshold`/`local_threshold`/`dyn_threshold`）复算，逐像素比对差异率

该方法与项目 11-16 已用过的 Frida/Trace 受控取证一致，且产出的是**可判定的数值结论**而非伪代码推测。
## 七、RTTI + 调用链定位结果（B 路线第一段）

用 REA + Ghidra 对 `MVDImageBinaryCpp.dll`（40KB）做 RTTI 恢复，链路已闭合到核心库调用点：

| 环节 | 证据 |
|---|---|
| 虚表 | 工厂 `CreateImageBinaryToolInstance @0x180001000` 中 `LEA RAX,[0x180007958]; MOV [RBX],RAX` → `CImageBinaryToolImpl::vftable = 0x180007958` |
| 虚表规模 | 读取 160 字节得 20 个槽位，函数地址 0x1800019f0 … 0x180007f28 |
| RTTI 类型名 | `. ?AVCImageBinaryToolImpl@ImageBinaryImpl@MVDInternal@@`、`IImageBinaryTool@ImageBinary@VisionDesigner@@`、`CImageBinaryResultImpl` |
| 参数通道 | `FUN_180001f90` → `MVD_Algorithm_GetValueByString(handle, name, 0x100, value)`；`FUN_180002000` → `MVD_Algorithm_SetValueByString` |
| **执行入口** | `FUN_1800021b0` → **`MVD_Algorithm_Process(handle)`** |
| **内核 ID** | `MVD_Algorithm_CreateHandle(&handle, 0x25a, 0)`（`FUN_180002a30` 内）→ **算法内核 = 0x25a (602)** |
| 前置 | 同时 `CreatePreproMaskToolInstance(param_1 + 8)` 创建掩膜预处理工具（`MVDPreproMaskCpp.dll`） |

**核心库导出面**（`MVD_Algorithm.dll`，51MB，仅 19 个导出）：`CreateHandle` / `UpdateHandle` / `DestroyHandle` / `Process` / `SetValueByString` / `GetValueByString` / `SaveConfigData` / `LoadConfigData` / `GetAlgorithmRunTime` / `ReleaseResultMemory` / `ReleaseWorkingMemory` / 加密与狗信息等。均为薄 C 接口，具体内核实现按 ID 内部分发。

**下一步（进行中）**：反编译 `MVD_Algorithm_CreateHandle @0x180014980`，定位「ID → 内核对象」的注册表，取得内核 602 的对象构造与 `Process` 实现，再按 `BinaryType` 常量（1/2/3/4/8）取各分支的阈值/核/系数实际用法。