## 修订2：独立反馈定向修复

- 原固定7cb44ead交付保留于artifacts/validation/plugin-foundation-01/revision1；独立25原场景与仓库外接入通过，但新增6探针复现PF-01/PF-02/PF-03（原始DEFECTS与结果仍保留）。
- PF-01：结构指纹改为长度前缀字段编码，允许合法ID含分隔字符仍不碰撞。
- PF-02：只在本轮令牌确已请求且异常令牌完全匹配时归取消，外来令牌即使与本轮停止重叠也报失败。
- PF-03：最小编辑控件按InvariantCulture显示初始已提交值；程序刷新不生成草稿；引用态只读显示。
- 本地新增/强化回归后27/0/0通过；SDK包3.0.0-foundation.2外部控制台通过。基础契约仍3.0，无旧兼容。产品PointPair内核未再变，不重复旧全量构建成绩为新测试；首次全量Release及探针仅证明其范围。
- 当前状态：修复已本地验证，待独立用新固定包对3缺陷及原场景回归。新提交与SHA以最新delivery.json为准；未把已修复直接写独立已验证。

# PLUGIN-FOUNDATION-01 第一批交接

- 基线提交：504191d0dbcf2f022fbc9ce196dc646b9f69e39b（整树有既有dirty，不能仅凭HEAD重建全部产品）。
- 当前执行者：Codex，唯一产品写者；开发侧已冻结本批源码，身份见 artifacts/validation/plugin-foundation-01/delivery.json。
- 用户目标：接口基础、四类代表样本；无旧方案迁移、旧插件兼容、全量工具切换。
- 新源：src/Plugin.Contracts、src/Plugin.Wpf、src/Tool.Kernels、samples/PluginFoundation。
- 必要既有源改动：PointMeasurementGeometry.MeasurePair委托共用纯数值内核；Plugin.SDK csproj与lock增加该引用，原计算公式未改变。
- 已完成：纯net9合同与3.0版本门、定义/会话/单异步入口、冻结输入/输出结构、限定上下文、Gray8输入借用与输出转移/Retain、节点会话互斥/共享资源键/真实排空、最小WPF编辑消费。
- 代表样本：点点测量共用内核；实际Halcon全域Gray8 Binary切片链接生产ThresholdOperations/LinearFilters源；等待与模型是明确替身；脚本只测动态结构不执行脚本。
- 本地验证：25场景通过/0失败/0受阻；实际STA控件事件与真实Halcon像素；临时仓库外仅本地NuGet包控制台接入通过；全量Release 0错误/1085警告；Release toolbox探针退出0（设备与会话初始化之前，不是正常UI验收）。设计时属性求值通过不等VS设计器通过。
- 独立测试：角色01a1620c-5a60-7fb1-a1b1-9b85173896e9，最终交接后只在其独立源/程序/数据副本运行；报告通过/失败/受阻/未验分列，不改产品或重建共享Release。
- 运行：samples/PluginFoundation/Verify.ps1；必要时指定独立-NativeDirectory；源码包含托管Halcon库，真实运行依赖已记录vendor SHA和许可证。无真实设备/VM操作。
- 未完成：生产22工具/运行引擎/画布切换，完整ROI/几何类型和零拷贝native模型，全部阈值模式与ROI，真实设备/原生强停，脚本执行/沙箱，完整加载器私有库冲突，VS设计器与人工视觉验收；A11私有库冲突及A12独立报告尚未覆盖。不得由25条推成A01-A12全闭环。
- 无第二产品入口：基础Host仅独立验证，不注册产品工具箱；生产旧SDK本批仍在用，下一批统一源码升级后才移除，不是兼容层。
- 追加启动专题：只读分析见 artifacts/ui-prototypes/architecture-health/startup-preparation.html；重点消费者/Owner设计，不改启动预热；VM普通预热内部仍有缺证，无冷启动A/B数字。
- 文档：主控原型只由主控写；架构文档角色在其目录增量接入，不能标生产已切换或提前独立验收通过。
- 下一步：独立测试核固定源/产物、复核A01-A12覆盖差距并独立运行；失败回主控修复再交新身份。本批状态“交付待独立验收”。
