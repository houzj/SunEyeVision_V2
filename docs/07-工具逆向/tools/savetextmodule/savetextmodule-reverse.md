# 文本保存 工具逆向主文档

- 稳定工具 ID：`savetextmodule`；原厂工具：`SaveTextModule`；UI：声明式参数页；P0
- 证据状态：`source-partial`

## source-confirmed

- `SaveTextModuParams` 为 9 个托管 ParamItem 提供读写：`SaveTrigger`、`SaveCondition`（ALL/OK/NG/NONE）、`Path`、`FileCount`、`FileLen`、`SaveType`（OverWrite/NoSave）、`TimeStamp`、`SaveByDateTrigger`、`MaxDayCount`。
- `SaveTextModuTool` 构造创建 Params，`Clone()` 返回新工具，`Dispose()` 为空。
- `SaveTextResult` 暴露原生结果结构；`SaveTextResultIn` 暴露 `ModuStatus`、`SavePath`、`FileName`。
- XML 清单含 `OutputType`、`TextInput`、100 个 `SaveTextItem`、保存条件/目录/轮转控件及无共享 UI 组；输入含字符串列、文件名和触发信号，输出含状态、保存路径和文件名；未声明 binding/condition。
- 通用执行入口为 `CProcess.RunOnce([commandString])` → `IMVS_PF_ExecuteOnce_V30_CS`。

## unknown

原生写文件/异步与轮转 ABI、`NameType`/`FileName`/`OutputType`/`GlobalVar*` 的托管绑定、触发条件副作用、保存恢复及原厂 VM 行为未确认。
