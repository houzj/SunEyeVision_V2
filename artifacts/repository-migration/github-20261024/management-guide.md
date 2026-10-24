# GitHub迁移与当前本地仓库

目标：`https://github.com/houzj/SunEyeVision_V2.git`

迁移只推送已提交历史。未提交改动、未跟踪文件及暂停中的实施成果不得被自动混合提交，也不因迁移视为验收通过。

推送验证成功后，计划采用以下远程职责：

- `origin`：GitHub，日常主远程。
- `gitee`：原Gitee仓库，保留作备用；不会自动双写。
- `upstream`：原本地 `D:/MyWork/SunEyeVision_Dev`，维持原用途。

常用操作：

```powershell
git status
git fetch origin
git log --oneline --left-right main...origin/main
git push origin main
```

干净工作树并确认远程更新后，使用 `git pull --ff-only` 更新当前跟踪分支。多人/多智能体实施沿用协作协议：独立分支或Worktree，禁止在同一工作目录并发写代码。

默认新分支：

```powershell
git switch -c codex/<任务名>
git push -u origin HEAD
```

当前存在未提交内容，不能直接把这两条命令当作立即切分支的操作指令；应先由其所有者核对保存范围。

迁移边界：6个Gitee正式分支的历史，其中GitHub main采用最新本地提交；迁移本地正式版本标签 `v2.0.0.1-alpha`。本地临时/Worktree分支与备份标签保留本地，不批量发布。

既有缺口：`third_party/adaptagrams` 是Gitlink，但没有 `.gitmodules` 中的路径映射。迁移保持原历史，不将它改成猜测的依赖地址；新环境递归初始化该子模块仍须另行恢复其确切来源。

本目录 `preflight.json` 记录迁移前提交、分支、文件大小和dirty状态；最终迁移状态以实际远程SHA核对回执为准。
