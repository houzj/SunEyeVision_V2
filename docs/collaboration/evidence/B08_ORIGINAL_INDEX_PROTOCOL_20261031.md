# B08 索引绑定：协议到原生记录的静态转换

仅静态读取原 VM 4.4 SDK、ModuleProxy、ModuleFrame 和既有分析工程；未运行 SDK 或 VM。此处“写入”指运行时绑定表记录，不代表方案磁盘持久化。

## 上游与协议解析

`VM.Core.VmParamSubManager.SetModuleParamBinding` 将 `indexType` 写入 SDK 枚举，将 `indexConstantValue` 经 `UTF8GetFixLenBytes(...,256)` 写入 `strIndexConstant`，动态索引则写模块 ID 和 128 字节名称；`ServerSDKAPI.IMVS_SetModuleParamBinding` 封送并调用原生 API。SDK → 请求 → ModuleProxy `0xc32` → `VM_M_SetModuleParamBinding` 静态控制路由见 [P01](../../analysis/vm-framework-architecture-20261030/parallel-P01-binding-request-route.md)。SDK 的 `FUN_180685490` 具体序列化输出尚未逐字段复核，**因此不能仅凭下述接收端代码断言 SDK 各字节已端到端验证**。

ModuleProxy `FUN_18004a610` 的 `0xc32` case 调 `FUN_180081f90` 解析请求，再通过 `FUN_180024af0` 发送记录给 ModuleFrame。`081f90` 获取 `dstVar → variableValue → index`：

| 接收端索引字段 | 静态解析结果 |
|---|---|
| `constant` 字符串大小写不敏感等于 `All` | 记录 `+0x8c` 写标签 1 |
| 其他 `constant` 字符串 | `+0x8c` 写标签 2；对文本调用 `atoi`，结果写 `+0x90` |
| `variable` 分支 | `+0x8c` 写标签 3；模块 ID 写 `+0x90`，`varName` 复制到 `+0x94`，长度受 `_snprintf_s` 参数约束 |
| 索引缺少或不满足解析条件 | 相应分支写标签 0；该局部分支有日志，并不等同所有请求整体返回失败 |

`All`、`constant`、`variable`、`moduleId`、`varName` 等名称从候选原 `ModuleProxy.dll` 的只读 PE 字符串解析。`_stricmp`/`atoi` 由反编译器及调用位点支持；对于溢出、非数字字符串和缺失字段组合的完整接受行为不作超出已见分支的结论。

## 内部转换与表写入

`ModuleFrame!VM_M_SetModuleParamBinding@0x180145410` 遍历 0x298 字节记录，在 `FUN_180143f80` 把来源 tag 3 转为内部 0x110 字节结构；它调用 `FUN_180144080`。后者原指令 `0x180144080–101`：索引标签 0/1 原样写入；标签 2 将输入 `+4` 的 4 字节直接写到输出 `+4`；标签 3 同样写入 ID 并复制后续 128 字节。目标模块 ID 与名称复制在 `0x180144101` 之后。`0x180145410` 再将转换后结构送入 `FUN_1800bd110`，后者将该记录复制到绑定表。结合 [整数索引追踪](B08整数绑定索引解析静态追踪-20261031.md) 的 `GetBindingInt` 运行读取，可证协议接收后的内部 tag 1/2/3 与整数执行路径对应关系；这不证明其它类型行为或具体实机结果。

请求缺 `body` 等结构、记录数为 0、申请失败等分支分别记录错误；`VM_M_SetModuleParamBinding` 在登记返回非零时停止继续遍历，已写条目没有由此证明自动回滚。托管层对原生非零抛 `VmException`，上层捕获并返回 false。更完整的畸形 JSON、异常终止及持久化仍需具体证据，不用上述正常路径推断。

## 证据身份

- 原 `ModuleProxy.dll` SHA-256 `76F09A40184B2B5F99B7173EDDE23A716D339E3C64955696D6927495EFDF68A9`；`FUN_180081f90` 伪代码 `analysis/full-audit/b08-index-conversion-20261031/0x180081f90-180081f90.c` SHA-256 `7263A0A29FAE36BF105D60722C5D3541B266A48AD22BC238B36F21148811AE34`，汇编同目录 SHA-256 `8DAD6BEC675D824FC55BCD59D62055188ACF67A311917A7C817DCB2A9FA9985B`。
- 原 `ModuleFrame.dll` SHA-256 `45C57BC46F72FD1F9E0B3A6F9183FF974D14590CFD05ECF4E9FA97A4FF95AE9B`；`analysis/global-variable-followup/sdk-handler-write/output/180143f80.asm.txt` SHA-256 `80F294B688347041C9144D2AB0263307A382039567F7E614FB7E14680C7C8CD6`；`analysis/global-variable-followup/sdk-handler-helper-chain/output/180144080.asm.txt` SHA-256 `9FD39D4FCDC214B94F5CBD203186D8CFC143337BED8E949984D9BD352C71EC1A`；`analysis/global-variable-followup/frame-param-binding-index-api/output/180145410.asm.txt` SHA-256 `BE695998BAFE4670A6AD7E62BC8AC1546F3494457151772B70EDCE6AAA2D6E28`。
- `analysis/decompiled/VM.Core/VM.Core/VmParamSubManager.cs` SHA-256 `42B0363C9A4FF1376FC5980865ACE43FBAE7D5A2FDF00156C55CEFC5CCC6703E`。
