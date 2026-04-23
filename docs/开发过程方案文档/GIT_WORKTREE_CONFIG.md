# Git Worktree 配置说明

## 📊 当前工作区配置

### Worktree 概览

| 工作区 | 路径 | 分支 | 负责人 | 任务 |
|--------|------|------|--------|------|
| 主工作区 | `d:/MyWork/SunEyeVision_Dev/` | `main` | team-lead | 协调团队 |
| 相机工作区 | `d:/MyWork/SunEyeVision_Dev-camera/` | `feature/camera-type-support` | camera-developer | 相机管理器开发 |
| 工具工作区 | `d:/MyWork/SunEyeVision_Dev-tool/` | `feature/tool-improvement` | tool-developer | 工具插件优化 |

### 目录结构

```
D:/MyWork/
├── SunEyeVision_Dev/                    # 主工作区 (main)
│   ├── .git/                            # Git 仓库（唯一的）
│   ├── src/
│   └── .codebuddy/
│       └── teams/
│           └── camera-tool-dev/          # Team 配置
│               ├── config.json           # Team 配置文件
│               └── inboxes/             # 成员邮箱
│
├── SunEyeVision_Dev-camera/              # 相机开发工作区 (feature/camera-type-support)
│   ├── .git (worktree 引用)            # 指向主仓库
│   ├── src/
│   │   └── Core/
│   │       └── Devices/                 # 相机设备类
│   │       └── Providers/               # 相机提供者
│   └── (其他代码...)
│
└── SunEyeVision_Dev-tool/               # 工具开发工作区 (feature/tool-improvement)
    ├── .git (worktree 引用)            # 指向主仓库
    ├── src/
    │   └── Plugin.SDK/
    │   └── tools/
    │       └── SunEyeVision.Tool.Threshold/  # 工具优化
    └── (其他代码...)
```

---

## 🚀 Git Worktree 状态

```bash
git worktree list
```

**输出**：
```
D:/MyWork/SunEyeVision_Dev        80e26f5 [main]
D:/MyWork/SunEyeVision_Dev-camera 80e26f5 [feature/camera-type-support]
D:/MyWork/SunEyeVision_Dev-tool   80e26f5 [feature/tool-improvement]
```

---

## 👥 Team 成员配置

### Team Lead (team-lead)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev`
- **工作分支**: `main`
- **职责**: 协调团队，与用户交互

### Camera Developer (camera-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-camera`
- **工作分支**: `feature/camera-type-support`
- **职责**: 开发相机管理器（IP/USB/GigE相机）

### Tool Developer (tool-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-tool`
- **工作分支**: `feature/tool-improvement`
- **职责**: 优化工具插件

---

## 🔄 并行开发流程

### 1. 日常开发

#### Camera Developer 工作
```powershell
# 切换到相机工作区
cd d:/MyWork/SunEyeVision_Dev-camera

# 查看当前分支
git branch --show-current
# 输出: feature/camera-type-support

# 开发相机管理器功能
# ...

# 提交代码
git add .
git commit -m "feat: 添加 Device 基类"
git push origin feature/camera-type-support
```

#### Tool Developer 工作（同时进行）
```powershell
# 切换到工具工作区
cd d:/MyWork/SunEyeVision_Dev-tool

# 查看当前分支
git branch --show-current
# 输出: feature/tool-improvement

# 优化工具插件
# ...

# 提交代码
git add .
git commit -m "refactor: 优化 ThresholdTool"
git push origin feature/tool-improvement
```

### 2. 合并到主分支

```powershell
# 切换到主工作区
cd d:/MyWork/SunEyeVision_Dev

# 拉取最新代码
git pull origin main

# 合并工具优化分支（先合并较小的分支）
git merge feature/tool-improvement
# 解决冲突（如果有）
git push origin main

# 合并相机管理器分支
git merge feature/camera-type-support
# 解决冲突（如果有）
git push origin main
```

---

## 💡 常用命令

### 查看 Worktree
```bash
git worktree list
```

### 创建 Worktree
```bash
git worktree add <路径> <分支>
```

### 删除 Worktree
```bash
git worktree remove <路径>
```

### 清理损坏的 Worktree
```bash
git worktree prune
```

---

## 🎯 关键特性

### 1. 独立工作
- ✅ 每个 Worktree 在不同分支上工作
- ✅ 互不干扰，可以同时提交
- ✅ 无需频繁切换分支

### 2. 共享 Git 仓库
- ✅ 所有 Worktree 共享同一个 `.git` 仓库
- ✅ Git 历史统一管理
- ✅ 节省磁盘空间

### 3. 并行开发
- ✅ camera-developer 和 tool-developer 可以同时工作
- ✅ 每个成员有自己的工作目录
- ✅ 代码提交互不影响

---

## ⚠️ 注意事项

1. **不要修改其他分支的代码**
   - camera-developer 只在 `feature/camera-type-support` 工作
   - tool-developer 只在 `feature/tool-improvement` 工作

2. **定期同步主分支**
   ```powershell
   # 在相机工作区
   cd d:/MyWork/SunEyeVision_Dev-camera
   git fetch origin main
   git rebase origin/main
   ```

3. **合并前先拉取最新代码**
   ```powershell
   # 在主工作区
   cd d:/MyWork/SunEyeVision_Dev
   git pull origin main
   ```

4. **解决冲突**
   - 合并时如果出现冲突，及时解决
   - 通知相关成员协助解决

---

## 📝 配置文件位置

### Team 配置
- 文件: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-tool-dev/config.json`
- 说明: 定义团队成员和工作目录

### 成员邮箱
- 目录: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-tool-dev/inboxes/`
- 说明: 存储成员之间的消息

---

## 🔄 下一步

1. ✅ Git Worktree 已创建
2. ✅ Team 配置已更新
3. ⏭️ 启动 camera-developer 和 tool-developer 并行开发

---

## 📞 联系方式

如果遇到问题，请联系：
- Team Lead: `team-lead@camera-tool-dev`
- Camera Developer: `camera-developer@camera-tool-dev`
- Tool Developer: `tool-developer@camera-tool-dev`
