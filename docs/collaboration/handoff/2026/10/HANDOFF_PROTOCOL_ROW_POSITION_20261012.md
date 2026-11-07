# 协议列表重排序号修复交接

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，保留此前所有未提交修改；无跨AI转交或临时提交。
- 当前分支：feature/comm。
- 当前执行者：Codex，唯一代码写入者。
- 任务目标：修复item上下移动后序号2147483647以及移动按钮边界错位。
- 允许修改目录：SDK协议编辑器局部WPF组件、两协议插件XAML、测试及协作记录。
- 已修改文件：新增src/Plugin.SDK/UI/Controls/Communication/ProtocolRowPosition.cs；更新ProtocolEditorResources.xaml及两ProtocolParse／ProtocolAssemble视图序号和移动边界绑定；新增tests/SunEyeVision.UI.Tests/ProtocolRowPositionTests.cs，更新CURRENT_TASK和本交接。
- 已完成：不再用交替行索引作列表位置。集合变化后在容器重分配完成时刷新实际Items.IndexOf位置；LoadingRow覆盖长列表生成／复用。序号显示1起，首行上移／末行下移禁用。未增加持久化序号或改变字段身份、解析规则。
- 未完成：Visual Studio设计器及用户实际主窗口重排验收；常用Release部署已完成。
- 已执行构建与测试：最终UI相关277项通过，其中新增7项连续按钮重排、跨首末移动、增删、4／30项列表滚动及文本／字节输出语义验证。首轮长列表测试因离屏ScrollIntoView没有实现末行而失败，改用ScrollViewer实际滚动后通过；生产修复未靠跳过断言规避。

  全量Release与UI Release使用独立PackagingRoot／OutDir／OutputPath构建，分别0错误1148警告、0错误1060警告。实际启动artifacts/validation/protocol-row-position-20261012/release/app/SunEyeVision.UI.exe --toolbox-probe，退出0、两协议工具加载正常，产品2.0.0.8／SDK2.1；证据为同阶段release-probe.json。git diff --check通过。启动探针在设备初始化前退出，不是设备业务验收。

  用户再次确认关闭后核验无运行进程，执行标准全量Release和标准UI Release构建，分别0错误1148警告、0错误1060警告。常用Release/SunEyeVision_v1.0.0/SunEyeVision已更新；实际该目录SunEyeVision.UI.exe --toolbox-probe退出0，两协议插件加载正常，产品和SDK版本不变。证据artifacts/validation/protocol-row-position-20261012/standard-release-probe.json。本部署轮不修改代码、不重复计入此前277项测试。
- 人工验收结果：WPF STA实际行内容和按钮断言通过；Visual Studio设计器及用户实际主窗口上下移动验收待完成。
- 已知风险或禁止改动：不杀用户进程，后续部署前仍核验程序关闭。共享行为只对ProtocolEditorResources局部DataGrid启用，不改全局表格、工作流调度、SDK契约、标定、设备、Debug或主项目。无设备收发验收，不宣称完整SDK全套通过。
- 下一步：用户从常用Release打开解析／组装并反复上下移item，验收序号、首末按钮和滚动。文本移动仍改变片段对应关系，字节移动仍按各字段原区间读取，不改变这些业务规则。
