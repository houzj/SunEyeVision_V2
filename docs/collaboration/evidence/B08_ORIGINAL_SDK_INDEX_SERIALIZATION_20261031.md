# B08 SDK 索引序列化与接收端字段

以 VM 4.4 x64 SDK 的 `FUN_180685490` 为限定范围，只读现存 Ghidra 工程提取伪代码及汇编；没有调用 SDK。安装原件与分析副本 SHA-256 均为 `B62D7E15588CCDA6671228AEBC11FEC3E27ACA878F0E03067E44B17D5A54A9A1`。分析脚本在 `-readOnly -noanalysis` 下退出码 0；Log4j 中文 URI 配置仍报错，但不阻止输出。

## 结构与字段

`VM.Core.VmParamSubManager.SetModuleParamBinding` 将枚举和 UTF-8 固定缓冲写入 SDK 托管结构；`IMVS_SetModuleParamBinding` 的 SDK 请求链调用 `FUN_180685490`。该函数以输入列表为基址，按每条 `0x398` 字节遍历；下述偏移均相对该函数 `param_2` 列表基址加 `i * 0x398`，不是相对单条结构起始的泛化 ABI：

| 内容 | 字节偏移 | `FUN_180685490` 行为及汇编 |
|---|---:|---|
| 来源类型 | `+0x8c` | 值 3 进入变量来源路径 |
| 索引类型 | `+0x190` | `0x1806863d9` 起按值 1、2、3 分支 |
| 固定索引文本 | `+0x194` | 1 时与 `All` 比较，但无论是否匹配都序列化规范字面量 `All`；2 时以 NUL 结尾扫描并原样放到 `index.constant`。`0x180686459–4b8`、`0x1806865c9–656` |
| 动态索引模块 ID | `+0x294` | 类型 3 时写入 `index.variable.moduleId` |
| 动态索引名称 | `+0x298` | 类型 3 时 NUL 结尾扫描并写入 `index.variable.varName`，`0x18068678c` 等 |
| 目标来源模块 ID / 名称 | `+0x318` / `+0x31c` | 放入 `variableValue.dstModuleId` / `dstVarName` |

以上字段名称由 SDK PE 字符串 `All`、`constant`、`variable`、`moduleId`、`varName`、`index`、`variableValue`、`dstModuleId`、`dstVarName` 及 JSON 构造调用核对。接收端 `ModuleProxy!FUN_180081f90` 解析同名字段：`constant` 为 `All` 时构造标签 1；其他值以 `atoi` 构造标签 2；`variable` 构造标签 3 和模块/名称，之后 ModuleFrame 的转换器保留标签及整数。详见 [接收端到原生记录](B08索引绑定协议到原生记录转换-20261031.md)。这闭合正常变量来源下三种索引字段的静态对应，不是运行差分或方案落盘证明。

## 失败和边界

- `param_2 == null` 分支设置 `0xe000000a` 并调用内部异常/错误处理函数；伪代码有不可达块警告，完整异常控制流不据此推断。
- 索引类型不为 1/2/3 时，该变量来源分支记录日志后继续组装变量对象、跳过已见索引构造；不可直接断言最终 API 返回成功或错误，须追序列化和接收端完整异常流。
- 类型 1 即使输入文本与 `All` 不同也走规范 `All` 构造，差异只在日志分支；这不是对未终止或非法缓冲的安全保证。
- 字符串按 NUL 扫描，非 NUL 终止、整数溢出和接收端各类畸形数据的行为没有在本次取证中全部证明。实际方案保存与重开另属独立链路。

## 证据身份

- `analysis/full-audit/b08-sdk-index-serialization-20261031/0x180685490-180685490.c` SHA-256 `A6B6E7F56FF55441FBA6D7018B977BCB4E1329620D6B3309BDFD9DF172F2BBF0`。
- 同目录 `0x180685490-180685490.asm.txt` SHA-256 `5E0B1B2B09FBADA0B2C58761CC91E84D23C36BC35771BBA852A497C74EB0F510`。
- 上游封送和控制路由详见 [P01](../../analysis/vm-framework-architecture-20261030/parallel-P01-binding-request-route.md)；接收端及 ModuleFrame 原件、反编译、汇编哈希见前述接收端报告。
