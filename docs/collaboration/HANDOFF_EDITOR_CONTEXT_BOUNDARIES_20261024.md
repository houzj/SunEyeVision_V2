# AI 协作交接单 — EditorContext 两边界后继

- 基线：c79525cf6bd3a83e2c17caf0ad1c9b10775bafb7（其原15 PASS及固定证据不改）。
- 分支/唯一写入树：codex/editor-context-production；C:/Users/houzhongjie/.codex/worktrees/editor-context-production/SunEyeVision_V2。
- 本提交仅四产品文件、一个新增定向测试文件及本交接。主树/A11/旧fixed未写；CURRENT与未知plans漂移不纳提交。

## 修复

1. ProductionEditorContext 提供一次性的内部 Deactivated 通知；Discard、Dispose、作用域失效、已发布后的重绑失败均释放活动归属，重复调用不重复释放。
2. MainWindowProductionEditing 的真实factory调用统一 TrackProductionEditor：Window目标直接接 Closed；ToolDebugControlBase目标在已有/后续Loaded时定位所属Window并接Closed。临时Unloaded保留草稿。通知移除活动HashSet、退订Closed/Loaded，最后活动上下文离开即清空Host和Session引用。重复track幂等。Session切换和初始化失败使用同一释放链。
3. MainWindowViewModel 的正式 CurrentSolutionChanged 最先释放所有活动Context；初始化异常Dispose走上述通知，不另维持平行集合释放规则。
4. NumericUpDown 对整数先用当前Culture的BigInteger精确解析（含原科学计数/小数点/分组格式），再校验实际有符号/无符号CLR范围，最后必须能无损往返double通路。2^53+1、long/ulong边界失真和超范围值保留文本/原值并拒绝；不先double解析再判断整数，不clamp。

## 实际路径与范围

当前全部源码对InitializeToolControlAsync只有ExecuteOpenDebugWindow一个Window调用；factory现有ToolDebugControlBase支持分支仍必须遵守生命周期，不能以当前调用只有Window掩盖泄漏。测试直接运行生产同一Track/Release钩子与实际ProtocolParse控件/Window，验证强引用活动计数、临时移出再放回、close→外部正式修改→旧Host拒绝/新Host可重开、Discard/初始化失败对应Dispose/Session释放与幂等。未伪造完整MainWindow构造，不宣称实际主窗操作。

配置显式Minimum/Maximum可把Int64/UInt64 Numeric范围放宽到2^53以上，因此精度缺陷可达，不能用默认int范围否认。实读并实测ConfigSetting现有UInt64边界：其内部值适配与可见数值编辑器不支持UInt64，真实模板Numeric为collapsed且初值0，正式UInt64模型仍128；测试保留这一事实，用真实模板核新pending API拒绝失真且不改变两者。未改ConfigSetting、未冒UInt64完整可见编辑器或Int64/UInt64全range支持；当前double控件拒绝不可无损值。

## 验证设施与历史

全新 owned artifacts/validation/editor-context-boundaries-02，原editor-context-01停止写。19项目实际TargetPath/OutDir/obj/targets/PackagingRoot均owned；本地props先导入再覆盖、WPF临时名去suffix。沿c795同resolved的5浮动包exact pins与owned locks，严格离线restore通过；vendor只读复制c795固定vendor并SHA核同，不拼主线live/A11 DLL。

- build01：实际0错1193警告81.82秒。
- boundary01：PID58840自然退出，实际11 PASS/1 FAIL/0 skip。唯一失败是夹具假定UInt64内部Numeric初值128；实际现有适配初值0，正式model仍128。原日志/TRX保留；修夹具断言真实初值与collapsed状态，不扩产品支持。
- build02最终：实际0错775警告18.05秒。
- boundary02最终：PID23356自然0，实际12 PASS/0 FAIL/0 skip，1秒。新增5（生命周期2、long/ulong theory2、科学计数与文化1）+直接受影响原7；未重无关原15整套、旧保存7或组件25。
- 真实ConfigSetting模板覆盖long/ulong的9007199254740993、各CLR最大值/越界、极细非零小数、原值/文本保持；Int64精确2^53科学计数可提交、2^53+1拒绝、fr-FR整数科学格式可提交/非整数拒绝。
- PEReader实读最终UI.Tests HostDebugRoot为本C owned build/app；SDK/Contracts/Workflow/vendor app/test同源SHA。实际命令和过滤在run-targeted-tests.ps1、boundary02-runtime-receipt.json，TRX results/boundary02/boundary02.trx。

## 可访问交付

按父授权，后继提交完成后新建 D:/MyWork/SunEyeVision_V2/artifacts/validation/tool-framework-contract/editor-context-independent-ready-<后继shortsha>，不覆盖既有root。Source来自精确git archive提交；TestedSource按实际build02源manifest复制与SHA绑定，完整app/testbin来自本owned成功产物；receipt绑定提交、source/raw与Git正规化blob、bin、实际props/targets、历史及TRX。

实际HostDebugRoot仍为C构建根，导出不改DLL/loader/metadata。UI.Tests非Tool命名，现ModuleInitializer只注册fallback，不主动加载Tool插件；导出同目录完整依赖闭包另做有限D运行，实际C访问边界和结果写导出receipt，不能把原C stamp偷换成D构建。复制的props/targets是实际C编译输入供审计，不声称已在D重新构建。

## 仍开放

原E06/E07真实主窗保存两FAIL仍待新集成fixed独验；本片不是原9项端到端验收。其余默认/专业/None挂接、NodeOpen UI职责迁移、完整MainWindow/实际菜单保存、全Release及最终集中GUI/VS/性能仍原队列。未修改Contracts/runtime/AssetRef/loader/serializer/注册authority/ConfigSetting载体。
