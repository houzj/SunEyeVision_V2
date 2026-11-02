# B08 浮点格式与 Blob 四项面积参数解析

仅复核原 VM 4.4 的 `ModuleFrame.dll`、`IMVSBlobFindModu.dll`、既有伪代码和汇编，未加载或运行原厂 SDK。范围是变量来源浮点绑定进入以下四项的条件路径，不外推其他参数与来源。

## 可由原件确认

1. `ModuleFrame!FUN_1800bc730` 在 `0x1800bc755` 将传入 XMM3 的单精度转 double，`0x1800bc79a` 通过 IAT `0x1802046b0` 调用 `MSVCR120.dll!sprintf_s`；格式字符串原件地址 `0x18022a76c` 为 `%f`，输出缓冲容量 64。未显式传入 locale 参数或指定精度数字。之后扫描生成的 NUL 终止文本，并将文本及长度传 `FUN_1800bc8a0`。
2. `bc8a0 → 0x180043120` 的模块对象分发及 Blob 目标对象 vtable `+0x18 → 0x180022500` 的条件路径，参见 [执行注入](B08执行订阅注入边界-20261030.md)和[Blob 对象](B08-Blob低阈值原生参数对象追踪-20261030.md)。是否命中目标模块及目标 setter 接受成功仍取决于具体运行状态。
3. Blob 的 `SetParam@0x180022500` 中，`BlobAreaLimitLow`、`BlobAreaLimitHigh`、`BlobTotalAreaLimitLow`、`BlobTotalAreaLimitHigh` 分支均调用 IAT `0x18002d270 = MSVCR120.dll!atof`，返回的 XMM0 经 `MOVSD` 分别写入对象偏移 `+0x15dd8`、`+0x15de0`、`+0x15dc0`、`+0x15dc8`。相应指令依次为 `0x1800226cd–6d3`、`0x1800226f7–6fd`、`0x18002276c–772`、`0x180022796–79c`。这四项目标为 64 位 double 存储，来源的中间值先为 32 位 float，再经过十进制文本；不能直接认定保留输入 float 的全部位模式。

## 当前边界与可替换假设

本次按 PE 普通导入表核对，两个指定模块都没有直接导入 `setlocale`、`_configthreadlocale`、`_create_locale`、`_atof_l` 或 `_sprintf_s_l`；这一有限结果**不能排除**其他模块、动态查找、运行时库或进程中其他调用改变 locale。也未证明当前进程 CRT 实际版本/locale、`%f` 对舍入和 NaN/Inf 的实测结果、`atof` 对特殊值的最终写入或异常处理。静态路径尚可继续寻找进程级 locale 调用者和目标 CRT 版本；目前不能称这些点的静态取证已经穷尽。

若实施需要先行，建议在调用方明确标为**自研假设占位**：限定有效有限数，在可替换的“float → 格式化文本 → double”转换适配器内使用项目指定的固定区域设置和明确精度；非有限值由项目明确定义处理。适配器替换位置是 `bc730` 同等的参数写入前转换边界，影响四个面积阈值及精度/格式。此为设计建议，尚未修改项目实现，也不是原 VM 的 locale 或特殊值结论。

## 证据身份

- `ModuleFrame.dll` SHA-256 `45C57BC46F72FD1F9E0B3A6F9183FF974D14590CFD05ECF4E9FA97A4FF95AE9B`；`analysis/full-audit/b08-float-elements-20261031/0x1800bc730-1800bc730.asm.txt` SHA-256 `29A930729D1D800D431FC56A7246A942B6641C7C57625DCF9714DF60357080F2`；`analysis/full-audit/b08-execution-inject-20261030/module-sink/0x1800bc8a0-1800bc8a0.asm.txt` `D5CD4AA3B5A6EB3F7E10988F33C91E487AF252817892795264E78290DBCE408B`。
- `IMVSBlobFindModu.dll` SHA-256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`；`analysis/full-audit/b08-blob-low-20261030/output/0x180022500-180022500.asm.txt` SHA-256 `C1EB5366F58BECFA50288EBA0F310F97BC081ABD2AC102ACF39A4CE9E3E74F2A`。
- IAT 名称以两原件 PE64 import descriptor、name thunk 和 IAT 槽位只读解析；地址为这两个镜像各自的静态 VA，不是运行时装载地址。
