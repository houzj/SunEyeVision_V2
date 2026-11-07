# 协议编辑器布局优化交接

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入；保留此前未提交工作，未提交或推送。
- 任务目标：协议组装及协议解析窗口收窄，列表与通信管理交互对齐并完整适配主题。
- 允许修改目录：通信工作区SDK局部控件、两协议插件、测试与本地方案。未同步主项目或Debug。
- 已修改文件：ProtocolEditorResources.xaml、ProtocolLayoutConverters.cs、ParamBinding.cs；两插件ProtocolAssembleEditor／ProtocolParseEditor的XAML与代码；ProtocolAssembleLayoutTests／ProtocolParseLayoutTests；两份通信工具方案及CURRENT_TASK。
- 已完成：用户追加反馈后，最终组装400×550、解析580×600（WPF逻辑单位）。解析参数独立行、左侧标签120宽、行间距10；表格保留可读列宽，默认短列表完整显示。两页移除常驻说明，解释放到Tip；行内↑↓×、首末禁用、添加选中与＋紧跟表格均保留。不改解析、组装与输出规则。
- 共享影响：SDK局部表格、输入和下拉主题资源供两个插件复用，不改全局表格。ParamBinding尊重调用方样式，并在模板重建与程序回填期间防止TextChanged误改BindingSource；旧模板事件先解除。不修改绑定语法、工作流调度或通信算法。
- 已执行构建与测试：最终布局及协议工具34项通过，包含无常驻说明、标签对齐、行间距、默认表格完整和行内增删排序。全量Release0错误1145警告、标准UI Release0错误1045警告；git diff --check通过；实际Release无设备toolbox-probe退出0，22工具含两协议工具。前轮265项UI及26项SDK通过，本次未重跑该扩展范围，不混计为本轮全套。
- 人工验收结果：用户否定400宽解析页的拥挤表现；已改为580宽逐行对齐布局，STA深浅色渲染检查完成，图片位于artifacts/validation/protocol-assemble-layout-20261012。用户尚未验收最终版，Visual Studio设计器待验收；探针不等于完整GUI或硬件验收。
- 已知风险或禁止改动：共享SDK涉及两项目后续同步，当前仅通信项目更新；SDK全套此前两项失败未修复，本轮仅选定26项，不宣称全套通过。不连接／写用户设备，不覆盖用户方案，不修改标定算法。
- 下一步：用户在本通信项目Release验收两编辑器的缩放、下拉、绑定、编辑及长列表；随后按计划继续接收／发送工具。
