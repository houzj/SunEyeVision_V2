# 变量选择窗口主题修复交接

- 基线提交：b2beb4017d6607ee44d6d5ba1aeb0376eb60572a；保留已有全部未提交修改，本轮无跨AI转交或提交。
- 当前分支：feature/comm。
- 当前执行者：Codex，唯一写入者。
- 任务目标：修复已有变量选择窗口item在深色主题白底浅色字问题，仍复用既有窗口。
- 允许修改目录：src/UI/Features/GlobalVariableManager、相关tests、docs及通信工作区Release产物。
- 已修改文件：GlobalVariableSelectDialog.xaml、GlobalVariableSelectThemeTests.cs、接收数据方案、CURRENT_TASK及本交接。
- 已完成：移除DataGrid内覆盖主题的隐式行样式，行／单元格／表头使用显式命名主题样式；普通单元格透明、选中使用主题色、失焦选中不回退系统配色。避免本地RowBackground属性压过悬停／选中触发器，保留只读整行选择。按钮动态引用共用样式支持资源后载入与主题变化；选择逻辑与变量模型未改。
- 未完成：实际VS设计器、高DPI与用户界面最终验收；发送、队列等其他工具任务仍按既有实施计划，未纳入本次修复。
- 已执行构建与测试：新增实际STA窗口回归验证深→浅→深动态资源、普通／悬停／选中／失焦／解除选中及分组搜索；最终变量选择／接收／协议串行64项通过。全量标准Release0错误1147警告，标准UI Release0错误961警告。实际常用Release toolbox-probe退出0、9类23工具、三通信插件正常；产品2.0.0.8／SDK2.1未改。git diff --check通过，证据artifacts/validation/variable-select-theme-20261012。
- 人工验收结果：已目视检查variables-dark.png、variables-light.png离屏渲染；未自动打开真实方案或连接设备，用户Release界面待反馈。用户已确认关闭，部署前复核无运行UI。
- 已知风险或禁止改动：只改变复用窗口局部样式，不修改全局主题、变量值、筛选／选择行为、SDK、工作流、主项目或标定；测试不代替实际VS设计器与用户高DPI验收，64项不等于全套测试。
- 下一步：用户在常用Release打开选择变量窗口，检查普通／鼠标悬停／选中／失焦及明暗主题；继续原通信工具链实施计划。
