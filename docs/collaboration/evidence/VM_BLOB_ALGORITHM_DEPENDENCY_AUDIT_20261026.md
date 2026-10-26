# Blob 算法依赖与自研边界（2026-10-26）

## 结论

**能确认 VisionMaster 的 Blob 处理链并非完全自包含：其本地算法包依赖 Intel IPP 图像处理/色彩转换库，以及 POSIX Threads for Windows 等第三方运行库。** 同时，Blob 专属模块和 `MVD_Algorithm.dll` 属于 VisionMaster 安装包内的 `MVDAlgorithmSDK` 交付体系，组件划分和专属 Blob 算法编排是 VM/MVD 自己的模块层证据。

因此更准确的结论是：**VM/MVD 自有模块/算法层 + 第三方数值与图像处理基础库的混合实现**。不能据此声称 Blob 的连通域、筛选、排序等高层算法本身是第三方实现；也不能声称它们完全由海康自研。高层算法归属仍需 `MVD_Algorithm.dll` 的具体算法入口、调用图及函数体证据。

## 观察到的静态依赖链

```text
IMVSBlobFindModu.dll                         (VM Blob 专属 x64 native module)
  ├─ ModuleFrame.dll                         (VM module SDK boundary)
  ├─ MVDBlobFindCpp.dll                      (MVD Blob algorithm adapter)
  ├─ MVDImageCpp.dll / MVDShapeCpp.dll
  ├─ MVDPositionFixCpp.dll / MVDPreproMaskCpp.dll
  └─ MSVCP120.dll / MSVCR120.dll              (Microsoft Visual C++ 2013 runtime)

MVDBlobFindCpp.dll
  ├─ MVD_Algorithm.dll                       (exports used: MVD_Algorithm_* including Process)
  ├─ MVDImageCpp.dll / MVDShapeCpp.dll / MVDPreproMaskCpp.dll
  └─ MSVCP90.dll / MSVCR90.dll                (Microsoft Visual C++ 2008 runtime)

MVD_Algorithm.dll
  ├─ ippcc.dll / ippi.dll                     (Intel IPP color conversion / image processing)
  ├─ pthreadVC2.dll                           (POSIX Threads for Windows, LGPL metadata)
  ├─ libmmd.dll / svml_dispmd.dll              (Intel compiler math runtime)
  ├─ libCNN_V4.1.dll                           (exports HKANN_* APIs; ownership/Blob usage unknown)
  └─ MVDCommon.dll / MVDSDKLogC.dll            (VM/MVD-named support libraries)
```

第一、二层的导入表给出明确的静态 DLL 依赖关系；依赖名称、导入函数名和被依赖文件身份均可复核。`MVDBlobFindCpp.dll` 导入 `MVD_Algorithm_Process`，证明其使用 MVD 算法接口；**静态导入不证明每个 Blob 分支都调用 IPP 的哪个函数**，也不证明动态加载或静态链接的其他库不存在。

此次继续下钻后还确认：`MVDBlobFindCpp.dll` 的两个导出为 `CreateBlobFindToolInstance` / `DestroyBlobFindToolInstance`，其导入的 `MVD_Algorithm.dll` API 包括创建/销毁句柄、读写配置、释放工作区/结果、更新时间和 `MVD_Algorithm_Process`。`MVD_Algorithm.dll` 导出通用 `MVD_Algorithm_*` 接口；其导入的 IPP 符号包括 `ippiRGBToYUV_8u_P3R`、`ippiColorToGray_8u_C3C1R`、`ippiRGBToGray_8u_C3C1R`、`ippiCompare_8u_C1R`、`ippiAddC_8u_C1RSfs`、`ippiMean_8u_C1R`、`ippiFilterBorder_32f_C1R`、`ippiFilterBorderInit_32f`、`ippiFilterBorderGetSize` 和 `ippiSubC_8u_C1RSfs`。这证实 MVD 算法引擎声明了对 IPP 的真实函数级依赖，而不只是安装目录里碰巧存在 IPP 文件。

另一个新增观察：`MVD_Algorithm.dll` 从 `libCNN_V4.1.dll` 导入 `HKANN_CreateModel`、`HKANN_Create`、`HKANN_Process` 等 ANN 接口；目标 DLL 自身没有版本/公司/签名元数据。`HKANN` 命名可能表示 Hikrobot ANN，但**命名本身不是所有权证据**。这表明 VM 算法引擎还具有一条 ANN 依赖接口，不表示 Blob 工具使用了该路径。

## 已核验的文件身份

| 文件 | 位置 | SHA-256 | 静态身份/版本 | 结论 |
|---|---|---|---|---|
| `IMVSBlobFindModu.dll` | `Applications/Module(sp)/x64/Location/IMVSBlobFindModu/` | `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A` | x64 PE；导入 MVD Blob 算法和支持模块 | Blob 专属 native 交付物 |
| `MVDBlobFindCpp.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `D819529A0A29A935C994CBF8136A8841834B90437A2D842DD22D407BF8743932` | Product `MVDBlobFindCpp Dynamic Link Library`; 4.4.1.3 | VM/MVD 包内 Blob 算法接口模块 |
| `MVD_Algorithm.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `85D17ECC87D52E9A62D8BB86C8D5208AC999BCD31EC6D0806453F8165733F549` | Product `MVD_Algorithm Dynamic Link Library`; 4.4.1.3 | 被 Blob 算法接口调用的 MVD 算法引擎交付物 |
| `ippi.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `AFCD9939AF8815887BC6B41C8639C70AB4DCABAD129F459648F0C05F9AF18BD3` | Intel IPP Image Processing 2021.3.0.1083；Authenticode 有效，Intel 签名 | 明确的第三方图像处理库 |
| `ippcc.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `9E347C592A75FBA66C2BA728C04620A47879BDCC50F5E5D524E98DE6F0B3E10C` | Intel IPP Color Conversion 2021.3.0.992；Authenticode 有效，Intel 签名 | 明确的第三方色彩转换库 |
| `pthreadVC2.dll` | 安装包公共运行库（同 SHA 文件也位于 Applications） | `0E6AF724609EF6846982EF717013426C359C455FFF324E906D8D55C8BB88D16E` | Product `POSIX Threads for Windows LPGL`; 2.9.1.0 | 第三方线程运行库；MVD_Algorithm 导入它 |
| `libmmd.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `6142BDE15261BD7177252AA8F716560A5344004E1B97A0B3C6280FC10D22829B` | Intel C/C++/Fortran compiler runtime；有效 Intel 签名 | 第三方编译器数学运行库 |
| `svml_dispmd.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `86567C3432A669C9489558B7410B7440D432650F30D878C997E621DAEA218CF9` | Intel compiler runtime；有效 Intel 签名 | 第三方向量数学运行库 |
| `libCNN_V4.1.dll` | `MVDAlgorithmSDK/Runtime/x64/` | `1D7CA30691B9240A9BC6F545B77396D48EA2E28FDE616A30C7528A7A433A927B` | 无版本/签名元数据 | 来源、所有权、Blob 本路径是否使用均未知 |

完整 PE imports/exports 解析结果及可复验脚本分别见 `blob-algorithm-pe-static-20261026.json` 与 `inspect_blob_algorithm_pe.ps1`；该 JSON 记录 Blob native DLL（16 个命名导出）、`MVDBlobFindCpp.dll`（3 个命名导出）和 `MVD_Algorithm.dll`（19 个命名导出）的完整名称表与导入符号。

## SDK 随包源码能证明到哪一层

安装 SDK 还包含公开接口头文件 `MVDAlgorithmSDK/Includes/Algorithms/BlobFindCpp.h`（SHA-256 `4305608BC0BF620C85BCAF5862A34D39F2F96218B8064B438ED6BD81F3C2C805`）及官方示例 `MVDAlgorithmSDK/Samples/Cpp/BasicSamples/MVD_GeoFind/BlobFindCppDemo/src/BlobFindCppDemoDlg.cpp`（SHA-256 `2EA4AB0A689319168EC02E31096B14EBA6C7468C7B15CE7513E298FB2BCC13A3`）。

- 头文件公开 `VisionDesigner::BlobFind::IBlobFindTool`，包括 `SetInputImage`、`SetROI`、参数配置、`SetRegionImage`、`Run`、`GetResult`，结果经 `IBlobFindResult` / `IBlobInfo` 暴露 Blob 数量、面积、轮廓、边界框等。接口本身为纯虚抽象，不含算法函数体。
- 示例 `OnBnClickedInitButton` 创建 Blob 和 PreproMask 实例；`OnBnClickedRunButton` 设置输入图与 ROI；若有 Mask，先运行 PreproMask，再把结果图交给 Blob 工具；之后调用 Blob `Run()`，读取 `GetResult()` 和 Blob 几何/灰度属性。示例可确认“SDK API 的操作顺序和对象边界”，不能替代 VM 主程序实际调用证据，也没有暴露 DLL 算法实现。
- SDK 的 `Runtime`/`Includes`/`Samples` 范围内未找到相关 `MVD_Algorithm.pdb` 或算法实现 `.cpp`；现有 `.cpp` 是调用示例，不是 SDK 核心算法源码。因此核心判定仍须依靠原生二进制函数体。

## `MVD_Algorithm_Process` 的机器码下钻

当前机器已找到 Visual Studio `dumpbin.exe`，所以继续用该工具对安装原件做了只读反汇编，不依赖 REA native decompiler。对 `MVD_Algorithm.dll` 导出 `MVD_Algorithm_Process`（RVA `0x152B0`，x64 image base `0x180000000`）反汇编至返回指令：

- 该导出先保存 `RDX/R8/R9` 的入参寄存器值，并检查 `RCX` 是否为空；为空返回 `0x80100006`。
- 非空路径读取一个全局指针，调用两个内部 helper，再通过对象首指针的 vtable `+0x18` 做间接调用，并原样传递保留的三个参数寄存器；全局/状态门失败返回 `0x80100015`。
- 这证明导出的 `MVD_Algorithm_Process` 是带状态检查、同步/辅助操作和虚调用分发的 API 外壳；导出本身不是可读的像素算法实现。间接目标没有 PDB/原生分析数据库符号，不能把它武断命名成具体 Blob 算法类，也不能仅凭这段汇编判断它是否调用 IPP。

完整反汇编摘录为 `blob-mvd-process-disassembly-20261026.txt`（SHA-256 `7D09998312B5B24833214E236C86F5148954F20A1BAE36336EFA84AB8AECA899`）。证据验证了“算法引擎存在多态/虚接口实现层”的结构，而不是完整算法数据流闭合。

## 新增静态证据：IPP 的机器码调用者

我扫描了 `MVD_Algorithm.dll` 的 `.text` 原始字节中的 x64 `E8 rel32` 调用，并用 PE `.pdata` 的 `RUNTIME_FUNCTION` 范围给调用点定界；随后用 `dumpbin /DISASM /RANGE` 读取调用者和跳板的原始指令。可复验材料为 `scan_ipp_thunk_calls.ps1`、`ipp-thunk-calls-20261026.json`、`disassemble_ipp_callers.ps1` 和 `ipp-callers-disassembly-20261026.txt`。目标 DLL SHA-256 为 `85D17ECC87D52E9A62D8BB86C8D5208AC999BCD31EC6D0806453F8165733F549`。

扫描得出 **18 个直接相对调用点，命中 11 个 IPP 跳板中的 10 个，归属 10 个 `.pdata` 函数范围**。跳板机器码为 `jmp qword ptr [IAT]`；根据其实际 IAT 槽位并与 `dumpbin /IMPORTS` 名称表对应，确认下列代码确实直接转入 IPP：

| 函数范围 | 直接转入的 IPP 符号 | 调用点数 |
|---|---|---:|
| `0x180987C50–0x180987DDC` | `ippcc!ippiColorToGray_8u_C3C1R` | 4 |
| `0x180988402–0x180988470` | `ippcc!ippiColorToGray_8u_C3C1R`、`ippiRGBToGray_8u_C3C1R` | 2 |
| `0x180988D12–0x180988D5A` | `ippcc!ippiColorToGray_8u_C3C1R` | 1 |
| `0x180D6E460–0x180D6E680` | `ippiSubC_8u_C1RSfs`、`ippiCompare_8u_C1R`、`ippiAddC_8u_C1RSfs` | 4 |
| `0x180DA5B60–0x180DA5BB0` | `ippiSubC_8u_C1RSfs` | 1 |
| `0x180DA5EA0–0x180DA5EF0` | `ippiAddC_8u_C1RSfs` | 1 |
| `0x1814A88C0–0x1814A8DD0` | `ippiFilterBorderGetSize` | 1 |
| `0x1814A9EFF–0x1814A9F8B` | `ippiFilterBorderInit_32f`、`ippiFilterBorder_32f_C1R` | 2 |
| `0x1814A9F8B–0x1814AA520` | `ippiMean_8u_C1R` | 1 |
| `0x1814AA520–0x1814AA8E0` | `ippiFilterBorderGetSize` | 1 |

例如，`0x180987C50` 先读取一个结构字段、减 2，并对 0..6 作跳转表选择；多个分支调用灰度转换 IPP。可以确认这里有按七种记录值选择的色彩转换机器码路径，但字段含义和调用者工具尚未闭合。`0x180D6E460` 的反汇编中确实依次出现 SubC、Compare、AddC 等像素函数调用；不能只凭函数组合将它命名为“二值化”，也不能据此归属某个具体工具。

### 与 Blob 操作码 `0x12D` 的关联仍未闭合

对 Blob 操作码分支复核了 `.pdata`：`0x18003F940` 的分发表把 `0x12D` 指向 `0x1800414F0`。该入口先检查参数指针和对象字段，随后代码延续到 `0x180041530` 区域。需特别更正范围解释：之前反汇编的 `0x1800414F0–0x180041F89` 是一个连续观察区间，`.pdata` 在其中包含多个运行时函数/片段，不能把整个范围说成单一函数体。

在这段 Blob 分支范围看到的直接调用包括 `0x180040A10`、`0x18005FCE30`、`0x180009810`、`0x180009B60`、`0x180009DE0`、`0x180009E10`、`0x180076940` 和 `0x180076950` 等；进一步检查 `0x18005FCE30` 的直接调用，目标为 `0x180602EC0/2ED0/2EB0` 与 `0x180606B70`。已核对的 Blob 分支代码及这些直接边中，没有出现上述 10 个 IPP 调用者函数入口。由于仍缺完整间接调用解析及全部后续 helper 闭包，这只能说明**目前证据没有把 Blob `0x12D` 连到 IPP 调用点**，不能断言该关联不存在。

所以这次可提高的结论是：**MVD 通用算法引擎的本机机器码确实调用 Intel IPP；Blob 特定操作码是否经完整传递调用图使用这些 IPP 函数，仍未知。** 这不证明 Blob 连通域或特征筛选来自第三方，也不证明其高层算法完全自研。

## 对“自研还是第三方”的回答边界

1. **已证实有第三方实现组件参与软件包的图像/数值处理依赖闭包：** Intel 的签名和产品元数据确认 `ippi.dll`、`ippcc.dll`；`MVD_Algorithm.dll` 的 PE 导入表确认它依赖二者。`pthreadVC2.dll` 也由该 DLL 导入，文件元数据标为 POSIX Threads for Windows/LGPL。
2. **已证实 VM/MVD 有自有模块层：** Blob native 模块导入 `ModuleFrame.dll` 和多种 `MVD*Cpp.dll`；`MVDBlobFindCpp.dll` 再调用 `MVD_Algorithm.dll` 的 `MVD_Algorithm_*` 接口。安装路径、MVD 产品名/版本和模块命名都支持它们属于同一发行技术栈；这些属性单独不能证明代码作者或法律所有权。
3. **已经证明 MVD_Algorithm 的某些机器码函数实际调用 IPP，但没有证明 Blob `0x12D` 分支是否到达这些调用点。** 因此仍不能确定高层 Blob 算法完全自研还是来自第三方。闭合 Blob 需恢复 `MVDBlobFindCpp` 的图像/参数路径，并将 `0x12D` 全部直接及间接调用边追到结果生成和上述 IPP 调用者；作者/许可归属还需要可验证的供应商、源代码或授权证据。
4. **没有在 Blob 入口 DLL 的直接导入表中看到 OpenCV 或 HALCON。** 这只排除了该 PE 的显式直接导入；不能排除由 `MVD_Algorithm.dll` 或其他依赖再导入、运行时 `LoadLibrary`、静态链接、改名封装或自有 wrapper。
5. `libCNN_V4.1.dll` 是一项重要待查依赖。当前仅观察到 `MVD_Algorithm.dll` 导入它；其命名不足以断言是 VM、Intel、开源或其他供应商实现，不能把它直接等同 Blob 算法核心。

## 证据类型和限制

- 文件身份、SHA、PE 导入、版本资源和 Authenticode 签名是安装原件的静态观察；离线解析，无目标进程执行。
- `IMVSBlobFindModu → MVDBlobFindCpp → MVD_Algorithm` 是根据 PE 静态导入关系得出的依赖边；它不是对某次运行的模块加载或具体 Blob 输入调用的动态跟踪。
- REA 已成功打开目标，但当前没有可用 native analysis provider：Ghidra 未配置、Hopper 不支持 Windows、IDA 未配置。此次使用 `dumpbin` 获取有限指令范围，支持直接调用结论；它不能替代完整函数恢复、间接调用/ABI 分析或完整调用图。
- PE 正常导入表不能覆盖全部动态加载、手写解析、静态库代码或打包之外运行期依赖。

## 后续取证顺序

1. 配置可用 native provider，反编译 `MVDBlobFindCpp.dll` 的 `CreateBlobFindToolInstance`、参数设置/执行和 `MVD_Algorithm_Process` 调用处，核对实际输入/输出和版本/配置读写。
2. 继续恢复 `0x12D` 分支的 `.pdata` 函数片段和所有直接/间接边，优先检查 `0x180040A10`、`0x18005FCE30` 的后继；确认是否能到达已列 IPP 调用者。不能从通用引擎存在 IPP 调用推定 Blob 每次运行都走 IPP。
3. 静态检查 `libCNN_V4.1.dll` 的签名、元数据、导入、导出及被哪些算法路径调用；没有供应商证据时继续标记来源未知。
4. 若需运行期确认，仅在用户批准/既有受控验证环境中观察实际模块加载和调用；本次没有启动 VisionMaster。

## 高层功能流程材料

针对“像 HALCON Blob 一样的高层处理流程（不要求底层算法）”，见 `blob-processing-flow-20261026.md`。它把 SDK 样例实际调用顺序、头文件契约、Blob 参数 XML 和 VM 模块包装器分层呈现；明确说明阈值/连通/筛选/排序来自参数架构，Run 内部具体算法顺序仍未知。参数 XML 的极性枚举包含名称/说明不一致，流程材料不据此推断亮暗极性数值。
