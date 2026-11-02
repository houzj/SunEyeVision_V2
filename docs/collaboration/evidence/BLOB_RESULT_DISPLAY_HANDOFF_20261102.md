# Blob正式图文组合候选交接

- 基线提交：a2ff34e4fd2bd3357bd76a88b0bc66eef3368bf4。
- 分支：codex/blob-result-display-20261101；工作区 D:/MyWork/line-aiproject/blob-reference-chain-20261101。
- 目标：C11–C13原九行与文本七字段的正式组合；仅真实外接矩形与Area接通，其余七项原位保护。
- 已实施：独立Editor显示阶段与唯一Fields owner；严格JSON/默认/复制；真实Host预览/确认取消/UndoRedo；稳定窗口scope＋新DraftNode token；同次publication与当前样式修订；每pane原图层/实际frame-domain lease；保守执行失效签名。
- 边界：Contracts/Runtime/Workflow编辑阶段、Plugin.Wpf原组合薄适配、UI实际Host及结果工作区、Blob正式声明/组装和专属测试。没有改Script、工作流独立工作区、VM/SDK可视或正式Release。

## 实际验收

冻结011标准UI/全量均0错且自然exit0。Core5、Window15、RuntimeDebug7、RuntimeKernel19、FieldEditing38条通过日志均exit0；字段旧固定总结31为陈旧文本。对应实际加载DLL15/15/13/4/12项全部与候选匹配，源码零漂移、测试SHA一致。详细路径/PID/SHA见[BLOB_RESULT_DISPLAY_CANDIDATE_20261102.json](BLOB_RESULT_DISPLAY_CANDIDATE_20261102.json)。候选不是主仓正式同版。

Core/Window及最终字段轨使用CreateDesktop+新STA线程，不SwitchDesktop，不回退普通桌面；RuntimeDebug/Kernel非GUI。此前Field019普通桌面失败已终止，明确保留，不混称隐藏。WPF按钮/文本事件与显式TryCommitPending不等于自然鼠标/系统焦点。设计样例消费同一Schema只作回归，不代VS验证。

## 主仓正式四轨命令

在集成源码、正式UI/全量Release构建后，从主仓根目录执行，保留默认正式产物路径。先清除所有诊断筛选：

```powershell
Remove-Item Env:S4_BLOB_DISPLAY_CASE,Env:S4_BLOB_DISPLAY_SCOPE,Env:B08_REFERENCE_CASE_SCOPE,Env:B08_REFERENCE_WINDOW_CULTURE,Env:S2_DIAGNOSTIC_CASE -ErrorAction SilentlyContinue
$env:S4_BLOB_DISPLAY_SCOPE='Core'
pwsh -NoProfile -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track BlobResultDisplay -TimeoutSeconds 480
$env:S4_BLOB_DISPLAY_SCOPE='Window'
pwsh -NoProfile -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track BlobResultDisplay -TimeoutSeconds 480
Remove-Item Env:S4_BLOB_DISPLAY_SCOPE -ErrorAction SilentlyContinue
pwsh -NoProfile -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S3 -Track RuntimeDebug -TimeoutSeconds 180
pwsh -NoProfile -File scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track FieldEditing -TimeoutSeconds 180
```

预期分别5、15、7、38条通过日志；Field末尾31不可用作计数。补充非GUI回归：Stage S3、Track RuntimeKernel、TimeoutSeconds180，预期19组。主仓使用默认Formal；候选实际同命令显式加`-ValidationScope Candidate`。原Core/Window源码SHA5AF47FE259E64AE28A09CE4DABAF1BE468F99EC0F5929DB2F23A58EBE37D5F1E；字段SHA DCEDACBE1C6639AA2AD2F33C75D2EA0EC3D2FE717ED0669AAEFF79030ABC91B7。

## 未完成及人工边界

七项缺真实本轮结果对象，动态结果XY、中文{面积}/完整表达式未接；入口禁用或明确拒绝，不伪存。保存仅LoadCandidate值与复制，不代重开宿主渲染/执行。双pane同时显示、空结果/失败运行、跨文档同ID、provider/proof异常重入及最后reader异常释放，本批未新增直接注入验收；真实NG颜色像素、任意DPI/缩放位置差分、自然键鼠、VS、VM等价及人工未验。主线可基于明确范围集成，但不称九项、Blob或S2/S3全部完成。

直接Host.Commit诊断有正确失效集但未消费UI发布通知，不能代正式Context提交；实际真实窗口阈值提交已证缓存/null、旧publication失效、场景撤销，Undo不复活。算法诊断016预算480秒超时，未据此推断产品死锁，原日志保留。全部历史失败在Release/_validation/BlobResultDisplay-20261102及各track attempt目录，工作区继续保留供审查。
