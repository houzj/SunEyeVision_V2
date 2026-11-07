# 全局变量 工具逆向主文档

- ID `globalvariablemodule`；证据状态 `source-partial`；P0 声明式参数页。

## Source-confirmed

反编译 UI 证据包含 `GlobalVariableItem`、`GlobalVariableDialog.xaml/.cs`，说明全局变量选择对话框及条目控件存在；工具目录 XML/UI 与参数、输入输出清单见 parameters.json/resources.json。通用执行入口以 CProcess.RunOnce→IMVS_PF_ExecuteOnce_V30_CS 表示。

## Unknown

原厂 GlobalVariableModule 托管参数类、注册/持久化 API、绑定更新事件、执行与结果结构、释放链和原厂 VM 行为尚未从当前证据恢复。
