# 字符比较 工具逆向主文档

- 稳定工具 ID：`stringcomparemodule`；原厂工具：`StringCompareModule`；UI：声明式参数页；P0
- 证据状态：`source-partial`

## source-confirmed

- `StringCompareModuParams` 仅调用基类构造，无托管参数属性；XML 另列 `StrCompareText` 参数。
- `StringCompareModuTool` 构造创建 Params，`Clone()` 返回新工具，`Dispose()` 为空。
- `StringCompareResult` 暴露原生结果结构；`StringCompareResultIn` 暴露 `StringMatch`、`Index`、`ModuStatus`。
- XML 清单声明强制 `StringA` 输入及运行时、匹配字符串、索引、状态输出；UI 含 `TextInput` 与 `StringCompareFields`；未声明 binding/condition。
- 通用执行入口为 `CProcess.RunOnce([commandString])` → `IMVS_PF_ExecuteOnce_V30_CS`。

## unknown

原生比较算法与 Process/ProcessAlg ABI、`StrCompareText` 与 UI/ParamItem 的绑定、匹配规则、保存恢复、错误状态及原厂 VM 行为未确认。
