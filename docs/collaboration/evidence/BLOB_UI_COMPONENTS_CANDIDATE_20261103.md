# 公共三页UI组合候选交接（2026-11-03）

- 基线提交：d87a5577b；开始工作时41835d16d1ec1c32d6123406ab8304db4d1a1f5f（已保全两份WIP）。
- 当前分支：codex/blob-ui-preview-components-20261103；工作区D:/MyWork/line-aiproject/blob-reference-chain-20261101。
- 当前执行者：ui_preview_components；唯一产品写入范围src/Plugin.Wpf。
- 任务目标：按冻结合同补齐原六阈值、15判定、范围/条件及四缺值显示的实际公共UI组合，供Blob真实三页组装消费，不扩执行/模型/保存合同。
- 固定源码：依序41835d16d → d0ad357dbbfddc5d0e98cc3f9f120a7d0dd6993e → eb4db3644fe09a61131448d8b2b8bcdeb359f848 → e645ad9aa20aa25e9e343ac777f4ccc8526e5c5e。Blob线已确认接入。最终身份门禁脚本单独固定19193b3cd31ec80c8a18f0ffe0127d8eeaec2791，产品仍e645。
- 已修改文件：精确相对路径、SHA256及测试来源见同目录BLOB_UI_COMPONENTS_CANDIDATE_20261103.json；产品共9个文件，另有独立公共专轨C#/PowerShell两个文件。

## 实施与效果

阈值使用六个原XML key；Single外部真实字段保留，内部重复字段隐藏，五个分支保持原组合和留值。只读Single seed接收真实setting与pending原文，沿既有NativeNumericCommit.RestorePending恢复，更新UI基线；编辑版本区分初始pending与用户编辑，用户改回相同原文仍按真实pending/invalid处理。合法正式引用基线不会因预览内空候选误封Single。未增加正式算法模式或可保存字段。

判定复用接续WIP的15组原范围；逐端保留数量low min1/high min0、原默认与增量，小数显示沿原默认2位。条件/范围/判定/显示组合集中提供Changed、变化/pending/invalid与退役接口。导航、Advanced展开及初始眼true不算新增修改。

退役复用字段IParameterVisibility、原popup/来源树与numeric suspend/generation机制，不接纳或清空pending。逐项清理尝试全部资源并报告异常；关闭异常后仍尝试恢复仍存活可见组件，关闭期间Dispose不复活。四显示行借出到真实布局后按实际行可见性恢复，不依赖未入视树的持有容器；原样式窗关闭、隐藏重显与raw恢复走同一组件。

## 候选构建与专轨

采用本机NuGet缓存只读离线restore成功，HALCON仅指定-p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon；未复制其他Release、DLL或算法资产。

- 最终UI：ui-build-011.log，自然exit0，6545警告/0错误，49.43秒。
- 最终全量：full-build-012.log，自然exit0，6442警告/0错误，40.09秒。
- 公共专轨：scripts/verification/Run-BlobUiComponentValidation.ps1。最后全量后因实际载荷SHA更新，针对最终同版程序集重跑八组；最终attempt-007自然exit0，8组实测通过且载荷身份门禁通过；载荷SHA见机器回执。
- 候选实际程序目录：D:/MyWork/line-aiproject/blob-reference-chain-20261101/Release/SunEyeVision_v1.0.0/SunEyeVision。测试自建WPF宿主只引用该目录产物，Private=false，记录实际加载路径和SHA。

八组覆盖：六模式实际显隐与回基线；外部Single只读seed原文Suspend/Resume、隐藏重显与版本保护；合法ReferenceSetting往返Single不误封；15组逐原XML核每端默认/min/max/inc/原浮点精度及Advanced无修改；范围逐端min/pending退役；真实Combo关闭异常恢复与关闭中Dispose不复活；条件显隐与恢复；四借出显示行初始眼/样式打开/invalid-opacity原文Changed/pending/退役关窗/隐藏重显/原文重开。

失败历史全部保留于Release/_validation/BlobUiComponents-20261103：首组件build-001因资产不同步缺OpenCvSharp引用失败，离线restore修正；attempt-003仅新增测试宿主缺System.IO导致编译失败，补using后通过。早期构建和测试均不拼为最终同版结论。

## 未完成、保护范围与下一步

未改Blob.Wpf、Catalog/O06、现O06验证/主runner、Runtime、SDK执行/引用模型、Workflow或主仓产物。未支持算法和producer仍未实现；此公共UI组合通过不等于真实Blob三页96项/8共享组、S2/S3或原厂视觉/时序/像素等价通过。

实际VS设计器和用户自然键鼠/人工验收未进行；真实Blob运行/设计同组装、UI-only集中门禁、不持久化及已支持完整链由Blob线/主线整页专轨与正式Release集中验收。原完整范围不缩小。

工作区原CURRENT_TASK脏内容及两个未跟踪旧证据完整保留，本交接不覆盖或提交其既有变更。主线负责将本批结果统一回原主方案与任务导航。

最终实际载荷：Plugin.Wpf SHA256 17878FEA9FA4AFBCA645216F775254E4E6B4D04C5458F50CB72CB9A4468544A2；Plugin.Contracts SHA256 BFDE677728C9C01B5D22FCED2303525B772B94492B99AE1C887B33B95DEDFEB4。两者均由attempt-007从上述候选Release实际加载，路径与SHA门禁全true。

