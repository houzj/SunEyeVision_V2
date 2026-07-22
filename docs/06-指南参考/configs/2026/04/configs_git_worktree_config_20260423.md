# Git Worktree 配置说明

## 📊 当前工作区配置

### Worktree 概览

| 工作区 | 路径 | 分支 | 负责人 | 任务 |
|--------|------|------|--------|------|
| 主工作区 | `d:/MyWork/SunEyeVision_V2/` | `main` | team-lead | 协调团队、工具插件开发 |
| 相机工作区 | `d:/MyWork/SunEyeVision_Dev-camera/` | `feature/camera-type-support` | camera-developer | 相机管理器开发 |
| 脚本工作区 | `d:/MyWork/SunEyeVision_V2-script/` | `feature/script-tool` | script-developer | 脚本工具开发 |

### 目录结构

```
D:/MyWork/
├── SunEyeVision_V2/                     # 主工作区 (main)
│   ├── .git/                            # Git 仓库（唯一的）
│   ├── src/
│   ├── tools/                           # 工具插件（主分支开发）
│   └── .codebuddy/
│       └── teams/
│           ├── camera-tool-dev/          # 相机 Team 配置
│           │   ├── config.json           # Team 配置文件
│           │   └── inboxes/             # 成员邮箱
│           └── script-tool-dev/          # 脚本工具 Team 配置
│               └── config.json           # Team 配置文件
│
├── SunEyeVision_Dev-camera/              # 相机开发工作区 (feature/camera-type-support)
│   ├── .git (worktree 引用)            # 指向主仓库
│   ├── src/
│   │   └── Core/
│   │       └── Devices/                 # 相机设备类
│   │       └── Providers/               # 相机提供者
│   └── (其他代码...)
│
└── SunEyeVision_V2-script/               # 脚本工具开发工作区 (feature/script-tool)
    ├── .git (worktree 引用)            # 指向主仓库
    ├── src/
    └── tools/                           # 脚本工具插件
```

---

## 🚀 Git Worktree 状态

```bash
git worktree list
```

**输出**：
```
D:/MyWork/SunEyeVision_V2        b949a1e [main]
D:/MyWork/SunEyeVision_Dev-camera 6c2f9ce [feature/camera-type-support]
D:/MyWork/SunEyeVision_V2-script b949a1e [feature/script-tool]
```

---

## 👥 Team 成员配置

### Team Lead (team-lead)
- **工作目录**: `d:/MyWork/SunEyeVision_V2`
- **工作分支**: `main`
- **职责**: 协调团队、工具插件开发、与用户交互

### Camera Developer (camera-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-camera`
- **工作分支**: `feature/camera-type-support`
- **职责**: 开发相机管理器（IP/USB/GigE相机）

### Script Developer (script-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_V2-script`
- **工作分支**: `feature/script-tool`
- **职责**: 开发脚本工具（C# Script 执行引擎）

---

## 🔄 并行开发流程

### 1. 日常开发

#### Script Developer 工作
```powershell
# 切换到脚本工作区
cd d:/MyWork/SunEyeVision_V2-script

# 查看当前分支
git branch --show-current
# 输出: feature/script-tool

# 开发脚本工具功能
# ...

# 提交代码
git add .
git commit -m "feat: 添加脚本工具"
git push origin feature/script-tool
```

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

### 2. 合并到主分支

```powershell
# 切换到主工作区
cd d:/MyWork/SunEyeVision_V2

# 拉取最新代码
git pull origin main

# 合并相机管理器分支
git merge feature/camera-type-support
# 解决冲突（如果有）
git push origin main

# 合并脚本工具分支
git merge feature/script-tool
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
- ✅ 主工作区（main分支）、相机 Worktree（feature/camera-type-support）、脚本 Worktree（feature/script-tool）互不干扰
- ✅ 可以同时提交
- ✅ 无需频繁切换分支

### 2. 共享 Git 仓库
- ✅ 所有 Worktree 共享同一个 `.git` 仓库
- ✅ Git 历史统一管理
- ✅ 节省磁盘空间

### 3. 并行开发
- ✅ camera-developer 在相机 Worktree 上工作
- ✅ script-developer 在脚本 Worktree 上工作
- ✅ team-lead 在主分支直接进行工具插件开发
- ✅ 工具代码直接在 main 分支提交，无需额外合并

---

## ⚠️ 注意事项

1. **不要修改其他分支的代码**
   - camera-developer 只在 `feature/camera-type-support` 工作
   - 工具开发直接在 `main` 分支进行

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
- 文件: `d:/MyWork/SunEyeVision_V2/.codebuddy/teams/camera-tool-dev/config.json`
- 文件: `d:/MyWork/SunEyeVision_V2/.codebuddy/teams/script-tool-dev/config.json`
- 说明: 定义团队成员和工作目录

### 成员邮箱
- 目录: `d:/MyWork/SunEyeVision_V2/.codebuddy/teams/camera-tool-dev/inboxes/`
- 说明: 存储成员之间的消息

---

## 🔄 下一步

1. ✅ Git Worktree 已创建
2. ✅ Team 配置已更新
3. ✅ 脚本工具 Worktree 已创建
4. ⏭️ 启动 script-developer 并行开发

---

## 📞 联系方式

如果遇到问题，请联系：
- Team Lead: `team-lead@camera-tool-dev` / `team-lead@script-tool-dev`
- Camera Developer: `camera-developer@camera-tool-dev`
- Script Developer: `script-developer@script-tool-dev`
