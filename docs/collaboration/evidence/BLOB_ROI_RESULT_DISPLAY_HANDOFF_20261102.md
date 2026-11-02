# Blob 同次区域结果显示候选交接

- 基线提交：c8311cffe1f93c9ad379c6f110e629891a97ca2e（后续主线ab514dbd只增文档，产品不变）。
- 当前分支：codex/blob-roi-result-display-20261102；唯一候选目录 D:/MyWork/line-aiproject/blob-reference-chain-20261101。旧分支保留，不改主仓/正式Release或用户方案。
- 目标与已完成：成功BlobOutput冻结本次采用的名义检测/屏蔽/扇环几何；复用publication reader和原图文scene、IAnnularSector/Fill。三个原槽正式眼睛/样式，Constant-only单owner28显示字段、83编辑、算法55；确认取消/UndoRedo、保存重开对象正式执行、同publication不重跑、错图/新帧退役均有定向检查。无可订阅假输出。
- 原件与适配：原ROI为rect、屏蔽为polygon且固定橙填充、Annular为annularsector；四形/多项mask为用户扩展。冻结的是本次实际采用的名义形状，不是Domain裁剪后像素区域。FullDomain/None不造框，像素Region/位置变换无名义几何为Unavailable。总BusinessJudgment颜色不称VM ModuStatus/LimitJudge等价。
- 构建：产品冻结002，标准HALCON源 D:/MyWork/SunEyeVision_V2/libs/halcon；UI与全量均自然exit0/0错，未复制正式Release。source-freeze-002的1761源文件审计0变更。原构建session71083已终态。
- 最终候选验证：Core attempt026 5、Roi attempt029 10、Window attempt028 15、RuntimeDebug attempt004 7、FieldEditing attempt008 38、BlobRoiPreview attempt015 24，均自然exit0。Roi/Window/Field/Preview是CreateDesktop+独立STA隐藏WPF桌面，不SwitchDesktop、不回退用户桌面；事件/字段提案/Host命令驱动，非物理键鼠。Debug非窗口算法轨。全部Loaded程序集路径与SHA一致（15/15/15/13/12/15）。Field旧固定摘要31，实际38条通过；不将其混为31或额外设计器验收。
- 原失败保留：BlobResultDisplay attempt025旧68数量断言、Field attempt007夹具只声明一个图形组。均已定位为夹具，强合同/断言未放宽；原Roi attempt027九组保留，新增三角色完整交互和总NG色后由029十组替代当前结论。
- 回执与源码身份：BLOB_ROI_RESULT_DISPLAY_CANDIDATE_20261102.json 含每轨receipt、实际测试SHA、Loaded DLL及修改源SHA。Core/Window的实际测试源SHA早于仅Roi补强，不冒称最终测试文件同SHA；产品始终冻结002。
- 未完成/未验：四个缺真实生产值原显示对象仍保护；原VM坐标、角度、Domain裁剪及像素/时序等价未证；图像mask、屏蔽继承、位置、动态文本位置不扩基础。没有新增专门双pane、跨文档同ID、proof异常注入矩阵；沿既有保护，不宣称本批新实测。重开对象执行已验，重新打开完整主窗渲染未新增直接验证；自然键鼠、VS设计器、用户人工及主仓正式集成未验。
- 允许集成边界：Contracts Results/Presentation、Plugin.Wpf原Shape投影/DisplayScene、UI现显示状态/Presenter、Blob私有Results/Session/组装，以及两个专属脚本与证据。无Workflow/Script/工作流工作区产品改动。主线集成时排除候选CURRENT_TASK，按原件证据合入。

## 准确运行命令

在候选根目录先清 `S4_BLOB_DISPLAY_CASE`、`B08_REFERENCE_CASE_SCOPE`、`S2_DIAGNOSTIC_CASE`；其余模块scope勿残留。所有命令均使用本候选已构建Release，日志在Release/_validation按attempt隔离。

```powershell
$env:S4_BLOB_DISPLAY_SCOPE='Core' # 另以独立进程分别 Roi、Window
& scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track BlobResultDisplay -ValidationScope Candidate
Remove-Item Env:S4_BLOB_DISPLAY_SCOPE -ErrorAction SilentlyContinue
& scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track RuntimeDebug -ValidationScope Candidate
& scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track FieldEditing -ValidationScope Candidate
$env:S4_BLOB_PREVIEW_SCOPE='All'
& scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track BlobRoiPreview -ValidationScope Candidate
```

默认超时：图文各480秒，Debug90秒，Field180秒，Preview480秒。正式验证须在主仓已集成同版构建后将ValidationScope改Formal，不能将本候选结果冒充正式。完整UI/全量构建命令和原失败日志见 artifacts/blob-roi-result-display-20261102；冻结后不需重复构建。
