# C05 单数值消费者整链候选交接

- 基线：`a3b3390f894191482a84bd969cb533aecdbfcc5c`。
- 分支／工作区：`codex/c05-number-model-consumers-20261103`，`D:/MyWork/line-aiproject/blob-display-neutral-20261103`；复用原工作区，没有新建。
- 消费者产品提交：`616fdb3df`（Blob／Runtime 显式回读）、`81f2fe21c`（第三个真实 ImageSource 视图接口转发）、`4d2204b13`（按裁决删除 Runtime 重复初始化）。
- 公共源码原提交及本线对应：`093c6f76→4ce3c9007`、`b179b18c→74dd0c459`、`bf5bac40→4081dcf49`、`66c25c70→f2e794533`。
- 最终产品源码固定：`f2e794533`。消费者专项 `d55efcc83`，加载态及参数测试隐藏桌面隔离 `8cf0e3267`／`b78362f38`。
- 写入边界：仅三个真实视图／宿主消费者、生产专项及既有综合 runner／受影响测试、本交接和候选证据；Plugin.Wpf 只消费公共唯一写者的固定提交。SDK、Workflow、Catalog、O06、CURRENT_TASK 未修改；原 CURRENT_TASK 脏内容保留。

## 已实施及完整生产出口

Blob 和 ImageSource 两个真实工具视图把 `ReadNumericModels` 转发给其真实 `DeclaredSettingsView`。盘点确认共三个真实 `IToolSettingsView` 实现，未以默认空实现过编译。ImageSource 本批只有两行接口转发，没有修改其业务、布局或工作流合同。

Runtime 宿主提供明确的单项／当前页／全部回读转发，前后核对视图、代次、节点实例、编辑上下文及有效性。既有普通参数 callback 也前后核对上下文、节点及 Runtime 配置引用，防止同 ID 新文档或回调重绑将旧值投给新根。

初始化唯一归公共 `DeclaredSettingsView.Rebuild` 的成功根安装末尾：`_fields`、输入所有权、Content 安装并确认当前后 All 一次。Runtime 不再重复 All，不挂 Loaded、重显或运行完成。此入口覆盖 ImageSource 内部 Editor.Bind、Blob 内部重绑及设计数据真实组装链。

真实 C05 专项复用 B08 的实际插件、图像源、变量、生产编辑上下文、调试窗口和执行器，完整检验：

1. 已保存引用的新根初始化普通值 137；Field／页／All 精确回读计数，内部 Editor.Bind 新 state 初始化一次、旧 state 不再读取、Hide/Show 不重复。
2. 实际基本页及 All 包含 11 个继承数值序列输入，无普通值返回 Unavailable；未打开图形样式 Popup 仍登记 opacity／width；文本 XY ButtonSelecter 和 C06 范围返回 NotApplicable。
3. 原引用选择保持本地 137，真实私有执行消费并接纳 173，但不自动回读；显式读取才投影 173，解绑再走原常量提案。
4. 回读不写 Fields、CurrentValues、历史、文件，不清除非法 raw 或伪发字段接纳。
5. 无值及读取异常不借旧模型补值；可进入未接纳的新常量输入态，正式 Accepted 仍保引用，pending／invalid 保护，取消不发布。
6. 读取期间同节点换根和关闭返回 Retired；真实确认、取消、Copy、历史、保存重开及实际执行保完整关系和普通参数值。

历史断言沿现有合同：Undo／Redo 恢复编辑关系，不能要求旧执行 173 被重放。Redo 保 live 137 后，再真实 Debug 接纳 173 并确认、保存重开验证。未修改 Workflow 来迁就测试。

## 公共实现复用和边界

复用原数值控件、原模板／订阅交互、已有 double／ToEven、原字段提案、B08 引用会话及当前性和清理保护。新模型是字段所有者的窗口内 CurValue，不是第二套可保存业务配置。

合法原 typed/storage/bounds 与 UI 投影分别核验：Accepted 8 投影模型 5，或者合法 Float32 1.25 投影 1.2，不因二者不同伪报初值错误；真正新候选仍严格 Freeze。普通序列回读须原 typed 序列，不能用裸 double 冒充单元素序列；0 项、多项及错误形状明确 Failed。只读数值不能在读值前越过 Constant 权限。上述公共边界以公共线独立专项及主线正式回归另行核收。

## 构建与验证证据

本线真实离线 restore 使用已存在 NuGet 缓存成功，未复制 assets／主仓 Release／厂商运行库。UI 和全量 Release 按规定命令加实际 HalconSourceRoot 构建。最终实际加载 SHA、源码身份、构建日志及各轨回执见 `C05_NUMBER_MODEL_CONSUMERS_CANDIDATE_20261103.json`。

最终 UI002／full002 均自然 exit0，分别 6553／6541 警告、45.08／33.36 秒。最终 C05 `attempt-002` 6 组、B08 `attempt-006` 12 组、NumericContract `attempt-002` Loaded 68 项、BlobParameterProduction `attempt-002` 21 组全部自然 exit0。四轨实际产品加载数分别 15／15／6／13，逐项 SHA 与本工作区最终产物核对一致。Loaded 数值报告 68 项 Passed 全 true；参数报告 failed=0。

公共线另有自己的同版 9 组及 Loaded RangeRoundtrip 42 组，固定证据提交 `1341d00ae5c2fafa864e341673844ae036253e78`；这是另一候选工作区的独立测试，未冒称使用本消费者载荷执行。由主线统一核收两线证据及正式产物。

中间 b179 产物 UI001/full001 以及第一轮四轨通过只属于中间载荷；公共合法原值／投影分离、权限和序列检查修正后，统一 UI002/full002 并重新验证最终载荷，不移签中间成绩。

所有实际窗口专项均在自有隐藏桌面运行，不切换用户桌面、不启动原版 VM／SDK。原 NumericContract Loaded 和可选参数 WPF 窗口原会直接 Show，已只对测试入口加隐藏桌面包装，原断言不变；独立真实设计数据构造不代表 VS 实际设计器验证。

## 未实现／未验范围

- 宿主 CurrentValues 仅是本方普通参数适配，不能宣称等于 VM `IMVS_GetParamValue` 或实时来源执行值。
- 未证明 VM 全安装存在运行完成自动回读，本批没有添加该机制；后续有真实调用者证据再适配。
- 原 VM 读失败可能失败通知、回写、再成功及标志非 finally；本方采用安全接纳和清理，不称原版原子语义。
- C06 双端比较、镜像、夹值、排队顺序和整组提案未扩展。本批只对既有范围作相称回归。
- 11 个继承输入登记回读不代表新增普通参数 producer；无值明确 Unavailable，不造零或默认。
- 未启动 VS、未作用户原厂并排人工验收、未做算法优化。候选测试不代主仓正式 Release 验收。

## 下一步

由主线按固定源码、规范 blob 和实际候选回执统一集成，更新 Catalog／O06并执行主仓自身正式构建与相称回归。工作区产品保持冻结；没有以候选或保护占位冒称全部 B08／全部控件或算法完成。
