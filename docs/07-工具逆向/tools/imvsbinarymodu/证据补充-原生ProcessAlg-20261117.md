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
