# 数据集合 工具逆向主文档

- 稳定工具 ID：`datasetmodule`；原厂工具：`DataSetModule`；UI：声明式参数页；P0
- 证据状态：`source-partial`

## source-confirmed

- `DataSetModuParams` 仅调用基类构造，没有托管参数属性。
- `DataSetModuTool` 构造以 `CVmObjectBase.handle, ModuleID` 创建 Params；`Clone()` 返回新工具；`Dispose()` 为空。
- `DataSetResult` 将 `IMVS_PF_DATASET_MODU_INFO` 的输出结果数组深拷贝到托管堆：按参数类型 1/2/3 分别复制 int/float/string 指针；析构释放各元素缓冲及结果数组指针。`DataSetResultIn` 仅暴露 `ModuStatus`。
- XML 清单显示隐藏 `ClearSignal` 输入、`ModuRunTime`/`ModuStatus` 输出，UI 含 `ClearSignal`、`DataSetFields` 与底部命令组；无 binding/condition 声明。
- 通用执行由 `CProcess.RunOnce([commandString])` 调用 `IMVS_PF_ExecuteOnce_V30_CS` 并等待状态清空。

## unknown

原生字段聚合与 Process/ProcessAlg ABI、ClearSignal/DataSetFields 事件绑定、保存恢复、错误状态映射及原厂 VM 行为未确认。
