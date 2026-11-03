# C06公共双端原件迁移候选交接（2026-11-03）

- 基线：0c4b454de2a939bfcb51c010d928cb79778460b0；分支codex/c06-range-model-public-20261103，复用blob-reference-chain-20261101工作树。
- 最终产品：c507b5bcdac5872e7f7bf942ec80602a08bab6fe。首完整源37b0f5fe8；集中恢复/激活/投影修正4dc7d3cb5、c5767c8d4、b7781d60b、6337deeec、c507b5bcd。f5bd1361c仅诊断；消费者b3bfb168a按主线授权cherry为c026e4ee0，仅测试接入且未双写。
- 唯一产品范围：8个Plugin.Wpf文件，精确SHA256和Git blob见同名JSON。未改SDK、Workflow、Blob业务或执行接口、主runner、Catalog、O06、主方案和CURRENT；原账本脏内容及两份旧untracked证据保留。

## 原件迁入与现宿主复用

原Integer/Float、IntegerBettween/FloatBettween/ValueBettween、ValueBase/NameValueItem/RangeChecker、single/between模板与SliderArrangeBase沿已有完整反编译源码核对；身份及10份原源码SHA见JSON。继续复用原Native模板、T1/T2、订阅槽、Slider/箭头/滚轮/失焦/Popup关闭。未启动VM/SDK或重复逆向。

同一字段owner的NativeNumericModel承接typed0 CurValue、原IsInitValue、OldValue和版本，HasValue另表已知状态。原NativeValue双向setter先改变本地模型，保raw与原Cur比较、Int32 Min相对Inc格点及double ToEven，再经原模型比较和现宿主接纳；不存在第二套持久范围参数。合法原值与UI规范值分离，不因原8→格点5或Float32合法原值的显示投影差异回写正式值；新候选仍严格Freeze。

左右Check直接按原不对称分支：右用左CurValue（不超过右Max时作为下界）；左仅在右IsInitValue=false时普通读右字符串作临时比较，原TryParse失败的局部0、读取异常的本端Max均只作该次比较，不安装对端模型/已知值，更不写正式/执行默认。原RangeChecker的1e-8与原叶RangeRule仍是两层；宿主正式范围严格关系不被削弱。int between原XML极值标志true、float between原默认false，以及两端/组属性、精度、步长和绑定均按原家族。

paired两端进入既有Field/CurrentPage/All和同一根初始化All一次，All/页按左后右；Field临时依赖不冒充另一端更新结果。普通typed回调和InvariantCulture字符串是明确宿主适配，不宣称等同native GetParamValue或最新订阅执行值。单端正式提案保另一端身份；完整组沿已有BeginRange/SubmitRange/TrySetGroup一次原子接纳。拒绝使用预先捕获OldValue，逐项前后核两端Acceptance/ModelVersion/模式/Edit/leaf所有权，遇新读取或编辑停止旧恢复；保pending原文。

原native排队事件体及正常顺序保留，移除非原的StartValue==proposed丢事件门禁；仅增加borrowed owner代次，覆盖程序投影、隐藏/禁用/卸载/换根/Dispose。原控件内部没有新增调度框架。左右commit借原Start/End事件，在业务写入门禁之外仅处理仍Current/owned、引用且无用户dirty的数值槽；投影前后退役原旧队列，尾部inner.UpdateTarget收束非dirty。不UpdateSource、不清referenceSession/实际坏raw、不过滤全局Validation、不提出常量。构造异常和Dispose对称尝试退订并保原异常。

宿主普通读/接纳仍先采用同一原setter数据规则，再经已有Changed/state owner受保护投影；原NativeValue用户setter同步INotify。此接缝避免普通回读伪造用户dirty或覆盖pending，不增加通知/事务架构。原native全局写入和PreExecuteRun调度不复制入控件，仍接现宿主草稿/调试/执行快照；平台和S3缺基础范围不计本C06已实现。

## 最终同版验证

所有构建在本候选树真实进行，HALCON指定D:/MyWork/SunEyeVision_V2/libs/halcon，无复制Release或厂商库。原11811自然exit0，源码在全流水冻结。

| 检查 | 实际结果 | 原日志 |
|---|---|---|
| UI009 | 0错，6553警告，1:42.68 | Release/_validation/C06NumericRange-20261103/ui-build-009.log |
| full010 | 0错，6555警告，1:00.20 | Release/_validation/C06NumericRange-20261103/full-build-010.log |
| C06公共attempt006 | 自然exit0，实9组 | Release/_validation/C06NumericRange-20261103/attempt-006 |
| C05公共attempt017 | 自然exit0，实10组 | Release/_validation/C05NumericModel-20261103/attempt-017 |

两次receipt的BuildSourceCommit/TestHEAD/payload/productSourceCommit均为最终c507；payloadPassed/identityPassed=true，产品源dirty为空。实际Wpf/Contracts均加载本候选Release且路径/SHA匹配，未借旧版本计数：

- 封包SHA256：D565AD4A4D7ECCFEBB102270D0386625A9FFD8CC0E8A32BD11CEF523414D0790。
- Wpf：082E507406EF8BD67E87FDC947A1C33F87411A5589DF568773514460BDE65111。
- Contracts：3E86BFE54D2D0F0B77E6EA2BC97534B6CC44720CA5719BA4E6E94D461BE2E3AE。

C06九组覆盖typed0已知/未知与isInit分离、raw同舍入通知、真实TwoWay先本地后单端接纳、普通读后无编辑事件/坏raw、左右Check/parse/exception、Field/页/All原序且不Dirty、原raw绝对界与完整组原子拒绝恢复、真实Popup已知/未知×禁用/隐藏/程序关闭/卸载及恢复、禁用前加载/禁用隐藏重显及恢复有效原事件、旧Dispatcher和Dispose、临时读换根/关闭退役。已知15/version1、未知0/version0在完整关闭组合内保持，全部owned binding dirty0/errors0且零回读、正式引用不变。C05十组保整数/浮点存储与原值投影、引用、失败解绑、原文、scope、序列、图形及换根；旧阶段“C06排除”改为真实两端登记，私有方法签名测试随源码更新，无兼容入口。

## 失败保留、边界和下一出口

中间构建及C06 attempt001–005、C05 attempt016均保留，不挪签：首轮Dispose比较误用清binding前DP基线；随后实证普通INotify投影污染dirty；中间九组曾过但C05私有反射参数数旧；最终关闭矩阵定位到known投影尾部SetCurrentValue留inner dirty，完整实体phase诊断随日志保留。最终两整组和实际SHA是独立新回执。

Int32极端中间溢出域沿C05用Int64计算后严格收窄，是安全适配，不称VM溢出逐值等价。原普通读取失败回写/双通知未复制为宿主副作用；parse/exception临时比较与显式Unavailable/Failed分别处理。未扩大C07、算法、producer、Workflow或SDK。

本公共候选已交消费者。Area/Circularity/Rectangularity真实完整生产scope、引用/索引/变量、调试/历史/片段复制/新对象保存重开/结果publication由消费者同版回执另记；主线集中集成、正式UI/full和直接整轨后验收。VS实际设计器、自然键鼠及用户人工仍独立记录，候选不代签。原完整清单和S2/S3边界不缩减。
