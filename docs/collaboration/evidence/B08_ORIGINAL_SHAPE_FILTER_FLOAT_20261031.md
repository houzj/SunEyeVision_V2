# B08 圆度、矩形度四参数原生写入

更正范围：此前 `%f → atof → double` 的报告核的是 `BlobAreaLimitLow/High`、`BlobTotalAreaLimitLow/High`，**不适用于本页四个形状筛选参数**。本次静态核对 4.4 x64 原资源和汇编，未运行 SDK。

## 资源与 Blob 模块分支

原 `IMVSBlobFindModuAlgorithmTab.xml` 将以下四项声明为 `Float`、RW、范围 0–1：`MinCircularity` / `0x0618`、`MaxCircularity` / `0x0619`、`MinRectangularity` / `0x061b`、`MaxRectangularity` / `0x061c`。这是资源声明，不能仅凭它断言运行时算法元数据表必然以同一索引解析。

原 Blob `SetParam@0x180022500` 指令中，这四个名称的 PE 字符串地址分别为 `0x180031268`、`0x180031278`、`0x180031288`、`0x1800312a0`。`0x180023587–5da` 四次比较，命中均跳转 `0x180023a71`；该处分支取得 `this+0x15eb8` 算法对象，在 `0x180023a7b–81` 以 RDX 原参数名、R8 输入文本调用该对象 vtable `+0x60`。这与四个面积限制字段在模块内调用 `atof` 写 double 的分支不同。

## 算法 setter 与解析边界

在已核原对象构造和 vtable 条件成立时，算法对象 `+0x60` 对应 `MVDBlobFindCpp!0x1800033f0`，再经 `MVD_Algorithm_SetValueByString` 到句柄 vtable `+0x28`；已核候选句柄实现为 `MVD_Algorithm!0x18004b360`。这一链有句柄、库身份和状态前提，不代表所有实例必然成功。详细身份见 [原生参数对象追踪](B08-Blob低阈值原生参数对象追踪-20261030.md)。

候选 setter `0x18004b360` 先按名称查询，再通过 `0x180002af0` 依据运行时参数元数据分支解析，成功后走 `0x180007020` 存储路径。`002af0` 的元数据类型为 4 时调用 `0x180001650`：先对文本进行字符检查，经 C++ stringstream 提取 **float**，再按元数据参数进行除法、正负方向调整及转整数。这个类型 4 是条件分支；**四个参数在实际算法句柄内的元数据类型、缩放值、表项与最终字段尚未逐项核实**，不能把该条件路径写成四项已证的最终数值行为。

框架的变量来源 float 路径在调用目标 setter 前确经 `ModuleFrame!0x1800bc730` 以 `MSVCR120!sprintf_s` 格式 `%f` 生成文本；本次四名称的 Blob `SetParam` 会转发该文本。目标算法侧不是此前四个面积字段的 `MSVCR120!atof → double` 路径。当前证据不足以认定四项文本最终解析的 locale、数值缩放或特殊值行为；这些点存在可追的算法元数据，继续静态取证，不标为已穷尽。

## 身份

- 原资源 `docs/architecture-recovery/tool-rendering-entry-audit.resources/IMVSBlobFindModuAlgorithmTab.xml` SHA-256 `34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC`。
- `IMVSBlobFindModu.dll` SHA-256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`；`analysis/full-audit/b08-blob-low-20261030/output/0x180022500-180022500.asm.txt` SHA-256 `C1EB5366F58BECFA50288EBA0F310F97BC081ABD2AC102ACF39A4CE9E3E74F2A`。
- `MVD_Algorithm.dll` SHA-256 `85D17ECC87D52E9A62D8BB86C8D5208AC999BCD31EC6D0806453F8165733F549`；`analysis/full-audit/b08-blob-low-20261030/output/core-targets/0x18004b360-18004b360.asm.txt` SHA-256 `DAB3CF11AFD731203F70DD9B2673C2023F24FB3778A01D7699B19092CC0E6C33`；`output/core-helpers/0x180002af0-180002af0.asm.txt` `44B40F7E416AE7EB8862520966C13D458D61EA816C3B58433A03134646027537`；本次只读提取 `analysis/full-audit/b08-shape-float-20261031/0x180001650-180001650.asm.txt` `379324C9C7C919B9CC8C1572F6C28B25CCA3730B049C0DF57131075E171B2848`。
