# 协议解析 工具逆向主文档

- ID `dataanalysismodule`；证据状态 `source-partial`；P0 声明式参数页。

## Source-confirmed

`DataAnalysisModuParams` 暴露 `PyScriptPath`，通过 ParamItem 的字符串读写；Tool 构造创建 Params、Clone 返回新实例、Dispose 为空。`DataAnalysisResult` 深拷贝原生输出结果中的 int/float/string 指针并在析构释放；ResultIn 暴露 ModuStatus。XML/UI 与输入输出清单见 parameters.json/resources.json；通用执行入口为 CProcess.RunOnce→IMVS_PF_ExecuteOnce_V30_CS。

## Unknown

协议脚本执行 ABI、输出字段语义、UI 绑定、保存恢复、错误状态和原厂 VM 行为未确认。
