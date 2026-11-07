# 结果树表同源链与可选字段修复交接

- 基线提交：正式产物9065e86a；独立SDK修复已合为96433a86。
- 当前分支：main。
- 当前执行者：Codex，主仓唯一产品写者；工作流线在独立worktree改实际UI消费者。
- 任务目标：按VM设计，由同一SDK输出声明、同次运行快照及publication驱动树和表，不分别查询latest。
- 允许修改目录：主线公共Plugin.Wpf结果表与正式验证脚本；UI消费者由工作流线修改。
- 已修改文件：DeclaredResultTable.cs、O06WindowPresentationCases.cs、原O06方案/证据/协作记录。
- 已完成：正式9065e86a两规定构建0错误、EXE探针0、ImageSources176/0、publication9/0、实际来源2/0、原表真实分页1/0；795发布文件及四receipt31加载记录SHA核对。详见evidence/O06_RESULT_OWNER_INTEGRATION_20261028.json。
- 本批修复：合法省略可选输出时显示空单元格，空或省略父记录路径返回空；不放宽错误类型、不补猜几何来源、不更改缺失Rows源的拒绝语义。
- 未完成：本批新源码规定构建/回归；GlobalBinding新attempt002真实失败及超时；跨workflow同nodeId定位/准备失败reader释放；真实历史调用入口、D07生产图形及SDK主图退出；Blob专属旧表第二阶段迁移。
- 已执行构建与测试：旧正式成绩独立归档；新增Window219用例对旧正式产物复现KeyNotFoundException optional，日志artifacts/result-owner-optional-before-20261028-001.log。修复后成绩另记，不套旧绿测。
- 人工验收结果：真实VS/VM同态与用户人工未代签。
- 已知风险或禁止改动：用户BlobParameterEditor.xaml不修改、不暂存；Blob业务阶段一仍冻结；不把历史接口定义当真实历史入口、不把独立原表通过当Blob已接。GlobalBinding超时无总计，不编造通过/失败总数。
- 下一步：固定本批提交，集中UI/full及正式EXE和原表/发布/来源相关新验；工作流原写者交完整UI消费者及失败修复后再集中整链回归。

## 本批正式结果

正式52beb99b UI/full零错误；EXE探针66928 exit0。Window220 publication10/0、221原表1/0、222来源2/0，GlobalBinding003仅SDK组22/0（fullTrack=false）。795发布文件和本轮21加载记录SHA核对，见evidence/O06_RESULT_OPTIONAL_FIELDS_INTEGRATION_20261028.json。完整GlobalBinding002仍失败/超时，生产D07和实际历史入口继续，不关闭阶段目标。当前无live。
