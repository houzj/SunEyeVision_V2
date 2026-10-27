# 原整数单选与同链设计入口交接

- 基线提交：a20a25a4；当前正式编译基线同该提交，加检查点11项源增量（含用户未提交XAML）。
- 当前分支：main。工作流独立分支仍为共同基线863c0ce1；本批不自动再合并、不推送。
- 当前执行者：Codex主线唯一写者；三路只读取证/评审已核收并补位C09后续组合。
- 任务目标：第一阶段Blob所需原整数单选基础和独立真实设计/运行链，不冒领完整C09或阶段一。
- 允许修改目录：现Plugin.Wpf、验证脚本及原方案/交接证据，Blob业务冻结。
- 已修改文件：IntegerRadioResources、NativeIntegerRadioOptions/Presenter、DeclaredFieldEditor及ImageSource清理、FieldsView、SettingsView/Options；Design单选Sample与Scene.xaml；Window/Design用例；Catalog、原O06生成器/JSON/HTML/队列及证据。
- 已完成：原两槽/五槽模板与正确原转换器/图标；同一正式long状态、int模板投影；未知值保留报错、同值不绕验；空槽null、负数和边界合法；拒绝回投影；原行不重复边距/列宽；构造及多子项异常续清理、旧命令失效和重入释放。真实XAML入口InitializeComponent和无Application同链实例已验。
- 未完成：继承模式/整区与分量来源、图形/图像模式与位姿、共用修订、多对象菜单及宿主历史；C13文本、C14列表缺字段、C15底栏；真实VS/VM同态/物理键鼠及用户本批验收。阶段一未准入，Blob业务不改。
- 已执行构建与测试：UI003零错误6559警告98.38秒；全量Release002零错误6487警告70.59秒；八轨249组通过，正式EXE011正常退出/22可用工具15分类；787发布项及107加载记录同源，146唯一原来源SHA复核。
- 人工验收结果：实际本批运行PNG及独立XAML入口PNG已查看；只验证当前进程语言，真实VS与用户未代签。
- 已知风险或禁止改动：保留UI001示例错误、Window134测试构造编译失败。早期UI002/full001/Window136/Design039只覆盖内部Sample而漏入口构造，不套成绩；补InitializeComponent/入口断言后最终003/002/040重新验证。Catalog构建后仅非编译来源与验收元数据更改。用户BlobParameterEditor.xaml页索引2/SHA118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed原样不提交；不推送、不并行覆盖Release。
- 下一步：C09 FieldsView组合TrySetFields/纯prepared组校验，同值也验；保原Range两端候选上下文。宿主writer只装不可变字段/几何/Revision根，全部accepted安装后Changed刷新Session。整体ROI继承必须正式几何类型/形状/角度/索引/物理帧校验，不偷换Region资源；原展示清空不等于已证删除持久引用。

权威证据：evidence/O06_NATIVE_INTEGER_RADIO_CHECKPOINT_20261028.json；来源O06_NATIVE_INTEGER_RADIO_SOURCE_AUDIT_20261028.json；后续静态O06_INHERITANCE_COMPOSITION_STATIC_20261028.json。设计入口src/Plugin.Wpf/Design/NativeIntegerRadioScene.xaml。原方案710唯一锚点/生成幂等。完整目标仍active。
