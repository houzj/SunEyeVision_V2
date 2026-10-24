# AI 协作交接单

- 基线提交：115f77b040e11e14b457875daf8e27b600e71a73；当前包含主控文档9f052ce4，无产品冲突。
- 当前分支：main。
- 当前执行者：Codex，唯一共享产品写者；原Editor作者仅只读分析。
- 任务目标：FT-H02-EDITOR-BASELINE-SAVE-01，原主窗口保存链修复；与FT-D02-CUSTOM-CONFIRM-ENTRY-01同候选收口。
- 允许修改目录：原授权Workflow编辑网关、UI定向测试、协作记录；不改程序集载体或新增Blob实现。
- 已修改文件：NodeConfigurationEditHost.cs；NodeConfigurationEditHostTests.cs；ImageThresholdProductionEditorTests.cs；CURRENT_TASK及本交接。
- 已完成：定位顶层SelectedDisplaySource独立显示偏好与参数CAS边界不对称；Host专用签名在Begin/Check/Shape/Apply排该字段，完整Store签名和所有真实配置保护保留。预览字段仍原路径持久化，绝不被该Host覆盖，不重基线。
- 未完成：新完整Release/实际程序/受影响自测/独立MainWindow保存重开与真实Run。
- 已执行构建与测试：前继115f全Release0错1279警告189.67秒；本次代码尚待新编译。受控无Show生产诊断02自然0，前三阶段0diff，预览写入为唯一差异并重现同一保护异常。诊断SHA A923A04BBA7262786E424B3D4CB3EEC32BAE862120F2FA7FC212AD452D12BB59。
- 人工验收结果：尚未组织，最终同Release合并一次。
- 已知风险或禁止改动：不能用19项或producer自测代原MainWindow独验；17bc frozen及失败证据不改，不重已知失败。01诊断缺SolutionManager初始化在Host前自然1保留，非产品失败。不可无条件rebase/禁用CAS，不能排除参数内部同名字段。
- 下一步：新exact源码owned路径前检、离线force+strict、全Release，真实程序与仅受影响回归，固定包直交独测继续。18:35–45仅条件fixed目标。
