# 仓库迁移后的未提交内容分类

基线：`25d33b73787f47fd0d2e3eac34bb1c8c08de09c5`。GitHub主远程为 `https://github.com/houzj/SunEyeVision_V2.git`，Gitee保留为备用。

用户授权核对原91项未提交内容、按用途分类提交、排除临时产物；所有后续提交的标题与正文使用中文。原变量修复、产品测试、固定交付与统一Release收口继续暂停，提交不代表验收或恢复。

## 分类与取舍

| 用途 | 纳入内容 | 状态 |
|---|---|---|
| 提交规则与临时文件边界 | AGENTS、协作协议、根目录临时抓取忽略规则 | 新增中文提交要求，历史提交不重写 |
| 依赖锁快照 | src、tools、tests中的53份packages.lock.json | 保留既有项目依赖图及版本快照；未重新restore/build，不能认定锁与实际构建已验证 |
| 文档导航与生成源码 | 页面、规范页面底稿、导航/搜索与生成脚本；补齐直接使用的生成源码 | 仅语法及静态结构核对，未全站再生成或视觉验收 |
| 交付与暂停记录 | 原O06动态区及底稿、4份交付状态、CURRENT_TASK、暂停交接及精选最终回执 | 历史有限PASS/FAIL保留，原整体验收未闭环，用户主动暂停 |
| 历史计划登记 | plans/plan.json中的已有提交和模块历史记录 | 保留原历史状态；不启动任务、不将in_progress或提交记录当作完成证明 |
| 仓库管理记录 | 迁移检查、分支身份核对、日常Git说明及本分类清单 | GitHub迁移已核对；不自动提交临时工作分支或恢复优化 |

依赖锁主要反映既有alpha.8项目依赖及Contracts、Runtime、Tool.Kernels等引用；Blob测试与Script锁还存在传递依赖变化。本次只保存现状，不将其包装为已验证的依赖升级。

规范HTML底稿是文档维护输入，不是CAS临时副本，需与可浏览页面共同保存。生成器使用的本地冻结取证输入、截图、测试运行目录及原固定包不批量纳入本次提交；缺输入时交付总览发布明确跳过，不以空数据覆盖页面。具体约束见 `artifacts/validation/README_DOCUMENT_INPUTS.md`。

## 排除项

以下5件文件校验SHA后移至本地忽略目录 `artifacts/repository-migration/github-20261024/excluded-temporary/`，未执行、未删除、未上传：

- `_capture_log.txt`
- `_capture_log2.txt`
- `_capture_main.ps1`
- `_capture_main2.ps1`
- `_probe.txt`

CAS proposed/canonical-before副本、运行日志、二进制产物、隔离Worktree及大批原始测试/逆向快照继续留在本地忽略范围，不因本次整理而恢复执行。

## 本次核对的实际边界

- 60份变更JSON可以解析；12份文档JavaScript、1份Python完成语法检查。
- 14份HTML及底稿未新增重复ID；按规范页面路径解析的新增本地链接存在。
- 文档总览缺输入保护完成一次不写页面检查。
- 未运行NuGet还原、产品构建、产品测试、GUI、独立验收或Release部署；原变量FAIL与源码未验状态保留。
- 当前post-commit钩子会后台改写计划，本次使用仅对各提交命令生效的空hook目录，避免后台写入；不修改长期钩子配置。

精确文件分类、摘要与本次提交回执存放于 `artifacts/repository-migration/github-20261024/`；被排除文件的内容不进入提交。
