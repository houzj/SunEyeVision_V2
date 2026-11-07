# 全局匹配与心跳最终优化实施记录

全局数据触发已增加不等于并移除旧兼容；心跳发送内容收为单行，父节点整行及箭头支持单击展开。Release 已更新，等待用户实际设备和可见界面验收。

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main；当前执行者：Codex，本工作区唯一代码写入者。
- 任务目标：实施现行全局匹配及心跳最终优化基线。
- 允许修改目录：Workflow 通讯规则与判断、UI 通讯执行入口和通讯页面、对应测试及本地方案协作记录。
- 已修改文件：CommunicationAutomationDefinition.cs、CommunicationTriggerEvaluation.cs、CommunicationAutomationService.cs；CommunicationManagerAutomation.cs、CommunicationAutomationViewModels.cs、CommunicationTriggersView.xaml/.cs；CommunicationHeartbeatViewModels.cs、CommunicationHeartbeatsView.xaml/.cs；通讯核心、动作比较、全局上传、触发UI和心跳测试。删除此前用于值 A/B 的未使用 HeartbeatValueInput.xaml/.cs；SDK 和用户方案文件未删除或修改。
- 已完成：新全局规则 Source=GlobalData，旧 String 不加载或执行；通讯页提供重新创建说明，保存仍走原显式方案保存。移除 SourceDeviceId、UploadScope、旧名称转换和迁移按钮／处理，不修改其它事件兼容字段，不默默扩大旧范围。
- 已完成：等于／不等于按本次原生类型解析常量后比较，包含仅用于区分大小写的String，不检查内容仅放行有效支持标量。空参数、解析失败、未知模式、坏质量及未解析集合不执行；不等于不是对全部失败结果取反。逐次上传快照、重复通知、会话失效和忙碌跳过边界保留。
- 已完成：心跳显示单数据／多数据；一个 TextBox 或两个等宽 TextBox 加短横，删除值 A/B 标签和Boolean专用编辑器，普通TextBox仍按发送类型校验，无效输入在启用时拦截。多数据只交替发送两份内容，不是范围、索引或同时发送；后台 Single／Alternate、定时、取消和非重叠发送保持不变。
- 已完成：树使用本页局部模板，箭头点击区28×32、整行至少32，透明背景覆盖小三角周围。预览点击统一处理父项展开与叶子提交，抑制默认箭头／双击重复处理；父子事件隔离。右键不变目标，键盘导航不提交，Enter确认、Esc取消；重开展开并定位已绑定叶子，不提交配置。滚动条不由节点处理。
- 未完成：可见 Visual Studio 设计器、明暗主题、高DPI、三角周围实际鼠标像素命中、快速连续真实点击以及实际 TCP／OPC UA／Modbus 设备人工验收。
- 已执行构建与测试：Release 通讯195项、触发UI1项、心跳10项、发送UI2项，共208项通过，WPF分组运行。心跳界面回归覆盖箭头及整行单击、子项不切父项、叶子提交、键盘导航不提交、Enter／Esc、右键、重开定位和紧凑输入。全量Release851警告0错误，最终独立UI Release1024警告0错误；设计时TargetPath／OutputPath查询通过。
- 已执行启动验证：实际 Release EXE --release-probe 退出0，版本2.0.0.8、alpha、Dark、背景#FF1E1E1E；探针不初始化设备。未改变发布版本。
- 人工验收结果：本轮尚待用户验收；测试入口 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
- 已知风险或禁止改动：所有此前旧 String 全局规则都需重新创建，不能只去掉旧字段后放行。全局规则可能由多个设备及不同支持类型分别命中；需限定来源时使用接收事件。不新增Modbus线圈／浮点配置、Range心跳、节点赋值或SDK改造。
- 下一步：用户测试 Release，重点检查新建全局不等于、旧规则加载说明、发送内容与实际鼠标命中；发布另行申请。本轮未强制结束用户进程、打包、增号、提交推送或同步工作区，没有跨AI交接。

验证证据位于 artifacts/validation/communication-final-refinement-20261010：communication-runtime.trx、CommunicationAutomationUiTests.trx、CommunicationHeartbeatTests.trx、CommunicationSendEventUiTests.trx、full-release-build.log、ui-release-build.log、Release-startup.json。早期编译和旧断言失败已修复，最终结果文件全部通过；测试不是整个仓库所有测试，也不代替人工设备验收。
