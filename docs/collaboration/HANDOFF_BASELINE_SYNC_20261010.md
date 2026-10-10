# 主程序与通讯项目统一开发基线记录

通讯项目通过快进取得主线已提交的通讯优化和依赖，双方以同一个交接提交为后续开发起点。保留独立工作目录和分支，不用文件复制形成两套源码。

- 基线提交：同步前main为a7c0d8a9ba11fa75c4fd67313bbdde8b51a4f2f6，feature/comm为1173baedc23eb879811f53242282c6072b10101a。共同交接提交由恢复引用codex/baseline-sync-20261010固定，可用git rev-parse该引用取得完整SHA。
- 当前分支：主程序main；通讯项目feature/comm。当前执行者Codex。
- 任务目标：同步主程序通讯优化，统一开发基线，再各自开发。
- 允许修改目录：本轮只添加主目录协作记录；通讯工作区通过git merge --ff-only取得主线已提交文件，构建和测试输出仅写该工作区。其它工作区不动。
- 已修改文件：CURRENT_TASK.md及本交接单；129项原主线已提交文件由快进同步到通讯工作区，本轮没有另外修改业务代码。
- 已完成：检查双方提交关系、工作区和任务状态，通讯分支无独立提交且工作区干净；快进无冲突，主目录五个未跟踪文件保留且不暂存。最新触发、上传、OPC UA和心跳优化及其依赖一并同步。
- 已完成：创建同步前恢复引用codex/pre-sync-main-20261010与codex/pre-sync-comm-20261010。记录验证后创建明确说明的交接提交，并将通讯分支快进到该提交；共同基线固定为codex/baseline-sync-20261010。
- 未完成：用户真实设备、可见VS设计器、主题及高DPI人工验收；不把无硬件启动探针当成实际设备业务验收。
- 已执行构建与测试：全部在通讯工作区执行。全量Release1261警告0错误，独立UI Release1060警告0错误。Release通讯195项、心跳11项、触发UI1项、发送UI2项共209项通过；三个WPF组分别运行。
- 已执行启动验证：通讯工作区自己的Release EXE --release-probe退出0，2.0.0.8、alpha、Dark，源码标识a7c0d8a9；之后的交接提交只修改文档，业务源码不变。设计时TargetPath及OutputPath指向通讯工作区，不覆盖主程序输出。
- 人工验收结果：同步完成后待用户继续验证；不自动打开真实方案或连接设备。
- 已知风险或禁止改动：旧String全局规则按现行设计不加载执行，需要重新创建；本轮不恢复迁移兼容。不得覆盖各分支后续独立开发内容，下一次同步重新检查差异。恢复引用仅保留历史，本轮不执行重置或回退。
- 下一步：主程序在D:/MyWork/SunEyeVision_V2的main开发；通讯项目在D:/MyWork/SunEyeVision_V2-comm的feature/comm开发。使用独立分支和目录，一处同时只允许一个AI写入。本轮无推送、打包、增号、强制结束进程或其它工作区同步。

证据目录：D:/MyWork/SunEyeVision_V2-comm/artifacts/validation/baseline-sync-20261010，含full-release-build.log、ui-release-build.log、core.trx、CommunicationHeartbeatTests.trx、CommunicationAutomationUiTests.trx、CommunicationSendEventUiTests.trx和Release-startup.json。
