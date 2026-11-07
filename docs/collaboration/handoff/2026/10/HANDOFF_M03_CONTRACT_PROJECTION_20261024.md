# AI 协作交接单 — FT-M03 合同显示投影子能力

- 基线提交：97f1326c42dbfe75fb516731be9df0ec76ed2da7
- 当前分支：detached HEAD，独立 m03-contract-projection worktree
- 当前执行者：Codex，唯一写者
- 任务目标：仅将冻结 OrientedRectangleGeometry 投影为现 SDK 闭合四角 ContourGeometry，保留 Frame。
- 允许修改目录：仅新增 src/UI/Services/Rendering/ContractGeometryDisplayProjection.cs、tests/SunEyeVision.UI.Tests/ContractGeometryDisplayProjectionTests.cs；本工作区任务和交接记录；隔离验证 artifacts。
- 已修改文件：上述两源码/测试文件、CURRENT_TASK.md、本交接记录。
- 已完成：Image/Pixel 白名单；弧度直接计算且整宽高只折半一次；TL/TR/BR/BL 局部角点顺序；每次独立数组；非 Image/Pixel、null、溢出明确拒绝。未接入 NodeResultManager 或生产发布。
- 未完成：生产显示接线、七类几何交互、完整 M03/J02、全量 Release 闭环不在本片范围。
- 已执行构建与测试：本 worktree artifacts/validation/m03-projection/verify.ps1；离线 restore exit0；UI Release --no-restore exit0（1175 条现有链警告/0错）；定向 test exit0，filter FullyQualifiedName~ContractGeometryDisplayProjectionTests，11/11 pass、0skip，TRX results/m03-projection.trx。完整 argv/输出/文件 SHA 位于同目录 verify.ps1、ui-build04.log、tests04.log、receipt.json。
- 人工验收结果：Visual Studio 设计时与真实运行人工验收 pending，留待集中验收，不声称通过。
- 已知风险或禁止改动：仅 internal helper 未发布接线；不解释标定帧、不变更 SDK/Contracts/Host/Scene/Asset/RequestId/生产 csproj。投影不能校验图像 provenance。所有 obj/bin/package 在 owned artifacts；HALCON native 从主仓库只读源复制到 owned 输出。前三次设施失败日志保留；wpftmp 初次缺 assets 的 ui-build04 被同名重试覆盖，该失败已在此明确记录，不能冒称日志完整。最终测试不依赖失败构建。
- 下一步：主实施者核冻结接口及有限提交后合入；如接生产显示链，须独立安排上游身份/帧对应与集中人工验收。
- Oracle 限定：测试比较的是 SDK RoiEditor.Rendering.RotatedRectangleHelper.GetCorners（正度角），并有手算 0/±90/180/45 度锚点；不是旧 IRoiRenderer.GetRotatedRectangleCorners（内部负角）的等价承诺。
