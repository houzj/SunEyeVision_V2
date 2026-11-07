# C01原参数宿主集中批交接

- 基线提交：b07a870b190602681a19df124bdbbecfe5e5dc62；本批随后固定中文检查点，产品验收身份是该基线加证据记录的源码增量。
- 当前分支：main；工作流独立分支共同基线仍为b07a870b，按用户要求同步后各自开发。
- 当前执行者：Codex主线唯一产品写者；三路只读核证/评审，核收后自动补位。
- 任务目标：第一阶段Blob所需原控件库；本批接原参数行组宿主，不提前启动Blob业务重组。
- 允许修改目录：Plugin.Wpf公共组合、配套验证及原方案；UI仅调整Runtime输入视图所有权消费者。
- 已修改文件：原ParamListView/ParamUserControl/LeftRight和行/组/滚动模板；NativeParameterRows；DeclaredFieldsView/FieldEditor/InputFieldsView/SettingsView；Range/Display/C07选择清除及RuntimeNodeConfigurationControl；Catalog与原方案、验证源码。
- 已完成：原89列宽/32边距及同宿主双端行，输入使用原NoScro模板且由外页唯一滚动；字段值保持原权威状态，读黄写红/弹层子字段错误汇总；源和组序重入门禁、正常通知与释放异常续清理。SettingsView成功接纳后拥有输入，同实例重绑保留、替换/关闭释放；创建方仅释放未被接纳的候选，Runtime借用Fields。共享影响已评估：所有DeclaredSettingsView消费者复用此生命周期，没有算法/UI调度/API参数变更，Wpf可选回归通过。
- 未完成：C15原高级按钮与同级行；独立同链NativeRoiScene设计入口；绘制/继承/位姿与创建删除粘贴和宿主历史；实际VS/VM/指针/用户视觉验收。完整C01/C08和阶段一未验收，不进入第二阶段。
- 已执行构建与测试：UI006零错6060警告56.82秒；全量Release002零错6577警告63.48秒。Window090=53、Field070=31、Design025=29、RuntimeEditor051=10、FullChain111=8、Components193=9同正式产物通过。正式EXE005 exit0，22可用工具/15分类；782发布项/86加载SHA核对。证据O06_PARAMETER_HOST_CHECKPOINT_20261028.json含增量及失败，不套旧成绩。
- 人工验收结果：实际PNG已查看；本轮未取得真实VS设计器/VM同状态/物理指针/用户验收。自动独立渲染不代签。
- 已知风险或禁止改动：用户BlobParameterEditor.xaml页索引2 SHA118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed保留未提交；不恢复旧SDK控件，不扩大非Blob全库，不推送。正式Release仍需单窗口构建/部署。GroupIndexConverter原源未得，明确使用本方组序metadata；原业务扫描/其他同名程序集命令未启用，不冒称原件原封不动。
- 下一步：C15源已核清，用已迁ExpandButtonStyle和同级原行显隐，收起不得清订阅/跳过字段校验。之后原Scene+库Design样例。跨聊天通知此前被自动审批拒绝，未绕过；用户明确授权后再通知工作流。
