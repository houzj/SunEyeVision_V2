# B08 形状四参数：算法元数据与键写入边界

只读检索原 VM 4.4 安装资源、原程序集及既有 Ghidra 分析；没有启动 SDK 或 VM。本文关注 `MinCircularity`、`MaxCircularity`、`MinRectangularity`、`MaxRectangularity`，不使用面积限制字段的解析结果替代。

## 四个名称的原厂资源

发现 `MVDAlgorithmSDK/MVDTools/ExportMVDCode/components/data/paramKey/BlobFind.xml` 四项均声明为 `Float`、RW、0–1、增量 0.01：

| 名称 | 算法 SDK 清单 AlgorithmIndex | 工具 AlgorithmTab 的 AlgorithmIndex |
|---|---:|---:|
| MinCircularity | `0x20618` | `0x0618` |
| MaxCircularity | `0x20619` | `0x0619` |
| MinRectangularity | `0x2061B` | `0x061b` |
| MaxRectangularity | `0x2061C` | `0x061c` |

两份资源的名称及低位相对应，但 SDK 清单用于导出代码工具，**尚未证明它是当前算法句柄运行时载入的元数据文件**。此外工具 AlgorithmTab 的两个下限默认 0.1，SDK 清单下限默认 0；这些分别是文件声明，不可据此推定实际运行初值。

## 已读的具体写链

四项在 Blob 模块原 `SetParam` 的逐名称分支均到 `0x180023a71`，参数名与文本原样经对象 `+0x60` 转发，指令及四名称地址见 [四参数追踪](B08圆度矩形度四参数原生写入追踪-20261031.md)。候选算法句柄 `MVD_Algorithm!0x18004b360` 先通过 `0x1800410c0` 遍历 `this+0x510` 名称表取对应编号；再调 `0x180002af0` 从 `this+0x138` 读取该名称的元数据并按类型解析。未找到名称、元数据、算法对象及解析错误均有独立返回路径。

`0x180002af0` 的 **type=4 条件分支**在 `0x180002bbf` 从元数据结构 `+0x1a0` 取 32 位浮点作为缩放参数，调用 `0x180001650`；后者做字符检查、用 C++ stringstream 提取 float，再除以该缩放值并按正负分支调整为整数。这是可复用的实现路径；当前尚无证据证明四个运行时条目各自都进入 type=4，或其 `+0x1a0` 具体数值。

候选普通写入路径把查到的编号和解析值组成两项 32 位值，以原名称传给 `0x180007020 → 0x180006ec0 → 0x180006c70 → 0x180007790`。`7790` 按名称查找/建立关联条目：已有记录的 `+0x40/+0x44` 与两项相同时直接返回；否则写入一组 8 字节，并将对应名称的标志和对象 `+0x110` 标志置 1。它是按名称管理的记录，**不是已确认这四项的固定对象字段偏移**。前置回调失败时 `7020` 不进入该写链；其他特殊算法类型/名称条件和后续消费不能一概推断。

## 尚需的具体证据

下一步是定位当前 Blob 句柄 `+0x510` 名称表和 `+0x138` 元数据来源、查明四名称的实际表项、类型与 `+0x1a0`。现有 SDK 清单可用于定位，不能单独证明运行时内容或比例。由于仍有具体入口和资源可追，本次不把静态取证标为穷尽，也不把自研缩放假设写成 VM 行为。XML 的 0–1 范围不证明输入文本经解析后的内部整数、边界及错误事务。

## 证据身份

- 原 SDK `BlobFind.xml` SHA-256 `3AF9DBA5B328888E13F7CCF66C74C257E61B6A2D5FA6B81BFE77489AFEC6B7EC`；原模块 `IMVSBlobFindModuAlgorithm.xml` `D5DE2D5888D0767552C3077457EADD796171B4447F1C92F93BC9BB60854A7B2B`；AlgorithmTab 与两原程序集 SHA 见 [四参数追踪](B08圆度矩形度四参数原生写入追踪-20261031.md)。
- `MVD_Algorithm.dll` SHA-256 `85D17ECC87D52E9A62D8BB86C8D5208AC999BCD31EC6D0806453F8165733F549`；`analysis/full-audit/b08-blob-low-20261030/output/core-map/0x1800410c0-1800410c0.asm.txt` SHA-256 `555C1E21EAFA9EA3683AFE91545F118F751899E7871BACCB543C21D2D4C96AD1`；`output/core-put/0x180007790-180007790.asm.txt` `4C53F56B4BC6F8628453B1D99B7F2CDE8777FF98749566D532D1EEB3C23BEEE1`；`output/core-storage/0x180006c70-180006c70.asm.txt` `F074C77F0F53A63469ACF219BDAD87D77B4C9E1334800ADB55BF001FD0E7FD34`。`002af0` 和 `001650` 的汇编身份见 [四参数追踪](B08圆度矩形度四参数原生写入追踪-20261031.md)。
