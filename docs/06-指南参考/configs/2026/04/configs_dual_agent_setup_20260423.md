# 双Agent并行开发方案 - 完整配置说明

## 📋 方案概述

本方案通过 **Git Worktree** 实现两个Agent在不同分支上并行开发，互不干扰。

### 核心架构

```
主仓库：d:/MyWork/SunEyeVision_Dev/ (.git)
│
├── 主工作区
│   └── d:/MyWork/SunEyeVision_Dev/
│       └── main 分支
│       └── team-lead Agent
│
├── 相机开发工作区
│   └── d:/MyWork/SunEyeVision_Dev-camera/
│       └── feature/camera-type-support 分支
│       └── camera-developer Agent
│
└── 工具开发工作区
    └── d:/MyWork/SunEyeVision_Dev-tool/
        └── feature/tool-improvement 分支
        └── tool-developer Agent
```

---

## ✅ 配置完成清单

### 1. Git Worktree 已创建
- [x] 主工作区：`d:/MyWork/SunEyeVision_Dev/` (main)
- [x] 相机工作区：`d:/MyWork/SunEyeVision_Dev-camera/` (feature/camera-type-support)
- [x] 工具工作区：`d:/MyWork/SunEyeVision_Dev-tool/` (feature/tool-improvement)

### 2. Team 配置已更新
- [x] Team 名称：`camera-tool-dev`
- [x] team-lead：工作目录 `d:/MyWork/SunEyeVision_Dev` (main)
- [x] camera-developer：工作目录 `d:/MyWork/SunEyeVision_Dev-camera` (feature/camera-type-support)
- [x] tool-developer：工作目录 `d:/MyWork/SunEyeVision_Dev-tool` (feature/tool-improvement)

### 3. 文档已创建
- [x] 配置说明：`GIT_WORKTREE_CONFIG.md`
- [x] camera-developer任务：`CAMERA_DEVELOPER_TASKS.md`
- [x] tool-developer任务：`TOOL_DEVELOPER_TASKS.md`
- [x] 完整方案：`DUAL_AGENT_SETUP.md`

---

## 🚀 快速启动

### 方式1：手动启动（推荐）

由于 `send_message` 工具不可用，建议手动启动两个Agent：

#### 启动 camera-developer
```powershell
# 切换到相机工作区
cd d:/MyWork/SunEyeVision_Dev-camera

# 查看任务清单
cat ../CAMERA_DEVELOPER_TASKS.md

# 开始开发
# ...
```

#### 启动 tool-developer
```powershell
# 切换到工具工作区
cd d:/MyWork/SunEyeVision_Dev-tool

# 查看任务清单
cat ../TOOL_DEVELOPER_TASKS.md

# 开始开发
# ...
```

### 方式2：使用Team框架（待send_message工具可用）

如果 `send_message` 工具变得可用，可以自动启动两个Agent：

```powershell
# 启动 camera-developer
send_message type="message" recipient="camera-developer" content="开始执行任务：开发相机管理器"

# 启动 tool-developer
send_message type="message" recipient="tool-developer" content="开始执行任务：优化工具插件"
```

---

## 📊 工作区状态验证

### 验证 Worktree
```powershell
cd d:/MyWork/SunEyeVision_Dev
git worktree list
```

**预期输出**：
```
D:/MyWork/SunEyeVision_Dev        80e26f5 [main]
D:/MyWork/SunEyeVision_Dev-camera 80e26f5 [feature/camera-type-support]
D:/MyWork/SunEyeVision_Dev-tool   80e26f5 [feature/tool-improvement]
```

### 验证分支
```powershell
# 验证主工作区
cd d:/MyWork/SunEyeVision_Dev
git branch --show-current
# 输出: main

# 验证相机工作区
cd d:/MyWork/SunEyeVision_Dev-camera
git branch --show-current
# 输出: feature/camera-type-support

# 验证工具工作区
cd d:/MyWork/SunEyeVision_Dev-tool
git branch --show-current
# 输出: feature/tool-improvement
```

---

## 🔄 并行开发流程

### 日常开发

#### Camera Developer
```powershell
# 切换到相机工作区
cd d:/MyWork/SunEyeVision_Dev-camera

# 查看任务
cat ../CAMERA_DEVELOPER_TASKS.md

# 拉取最新代码
git pull origin feature/camera-type-support

# 开发功能
# ... 编写代码 ...

# 编译验证
dotnet build

# 提交代码
git add .
git commit -m "feat: 添加 Device 基类"
git push origin feature/camera-type-support
```

#### Tool Developer（同时进行）
```powershell
# 切换到工具工作区
cd d:/MyWork/SunEyeVision_Dev-tool

# 查看任务
cat ../TOOL_DEVELOPER_TASKS.md

# 拉取最新代码
git pull origin feature/tool-improvement

# 开发功能
# ... 编写代码 ...

# 编译验证
dotnet build

# 提交代码
git add .
git commit -m "refactor: 优化 ThresholdTool"
git push origin feature/tool-improvement
```

### 合并到主分支

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

## 📁 文件结构

```
d:/MyWork/SunEyeVision_Dev/
├── .git/                                    # Git 仓库
├── .codebuddy/
│   └── teams/
│       └── camera-tool-dev/                  # Team 配置
│           ├── config.json                     # Team 配置文件
│           └── inboxes/                       # 成员邮箱
│               ├── camera-developer.json
│               ├── tool-developer.json
│               └── team-lead.json
├── src/
├── tools/
├── docs/
├── GIT_WORKTREE_CONFIG.md                     # Worktree 配置说明
├── CAMERA_DEVELOPER_TASKS.md                 # camera-developer 任务
├── TOOL_DEVELOPER_TASKS.md                   # tool-developer 任务
├── DUAL_AGENT_SETUP.md                       # 本文档
└── (其他项目文件...)

d:/MyWork/SunEyeVision_Dev-camera/           # 相机开发工作区
├── .git (worktree 引用)                    # 指向主仓库
├── src/
│   ├── Core/
│   │   ├── Devices/                         # 相机设备类
│   │   │   ├── Device.cs
│   │   │   ├── IpCameraDevice.cs
│   │   │   ├── UsbCameraDevice.cs
│   │   │   └── GigECameraDevice.cs
│   │   ├── Providers/                       # 相机提供者
│   │   │   ├── ICameraProvider.cs
│   │   │   ├── HikvisionProvider.cs
│   │   │   └── DahuaProvider.cs
│   │   └── Managers/
│   │       └── CameraManager.cs
│   └── UI/
│       ├── ViewModels/
│       │   └── CameraManagerViewModel.cs
│       └── Dialogs/
│           └── CameraManagerDialog.*
└── (其他代码...)

d:/MyWork/SunEyeVision_Dev-tool/            # 工具开发工作区
├── .git (worktree 引用)                    # 指向主仓库
├── src/
│   └── Plugin.SDK/
├── tools/
│   └── SunEyeVision.Tool.Threshold/          # 工具优化
│       ├── Models/
│       │   ├── ThresholdParameters.cs
│       │   └── ThresholdDisplayConfig.cs
│       ├── ViewModels/
│       │   └── ThresholdToolViewModel.cs
│       ├── Views/
│       │   └── ThresholdToolDebugControl.*
│       └── Core/
│           └── ThresholdProcessor.cs
└── (其他代码...)
```

---

## 👥 Team 成员职责

### Team Lead (team-lead)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev`
- **工作分支**: `main`
- **职责**:
  - 协调团队工作
  - 与用户交互
  - 合并代码到主分支
  - 解决冲突

### Camera Developer (camera-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-camera`
- **工作分支**: `feature/camera-type-support`
- **职责**:
  - 开发相机管理器
  - 实现IP/USB/GigE相机支持
  - 集成相机SDK
  - 更新UI

### Tool Developer (tool-developer)
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-tool`
- **工作分支**: `feature/tool-improvement`
- **职责**:
  - 优化工具插件
  - 改进参数系统
  - 优化UI交互
  - 提高性能

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

### 1. 分支隔离
- camera-developer 只在 `feature/camera-type-support` 工作
- tool-developer 只在 `feature/tool-improvement` 工作
- 不要修改其他分支的代码

### 2. 定期同步
```powershell
# 定期同步主分支的最新修改
cd d:/MyWork/SunEyeVision_Dev-camera
git fetch origin main
git rebase origin/main
```

### 3. 提交规范
- 提交前先编译验证
- 使用清晰的提交信息
- 遵循项目编码规范

### 4. 合并流程
- 合并前先拉取最新代码
- 先合并较小的分支
- 及时解决冲突

---

## 📞 联系方式

- Team Lead: `team-lead@camera-tool-dev`
- Camera Developer: `camera-developer@camera-tool-dev`
- Tool Developer: `tool-developer@camera-tool-dev`

---

## 📚 相关文档

- [Git Worktree 配置说明](./GIT_WORKTREE_CONFIG.md)
- [Camera Developer 任务清单](./CAMERA_DEVELOPER_TASKS.md)
- [Tool Developer 任务清单](./TOOL_DEVELOPER_TASKS.md)
- [项目编码规范](./docs/)

---

## 🎉 配置完成！

现在您可以：

1. ✅ 查看 Worktree 配置：`cat GIT_WORKTREE_CONFIG.md`
2. ✅ 启动 camera-developer：`cd d:/MyWork/SunEyeVision_Dev-camera` 并查看 `CAMERA_DEVELOPER_TASKS.md`
3. ✅ 启动 tool-developer：`cd d:/MyWork/SunEyeVision_Dev-tool` 并查看 `TOOL_DEVELOPER_TASKS.md`
4. ✅ 开始并行开发！

---

## 🔄 下一步

### 如果 send_message 工具变得可用：

1. 启动 camera-developer：
   ```powershell
   send_message type="message" recipient="camera-developer" content="开始执行任务：开发相机管理器"
   ```

2. 启动 tool-developer：
   ```powershell
   send_message type="message" recipient="tool-developer" content="开始执行任务：优化工具插件"
   ```

### 如果 send_message 工具不可用：

1. 手动切换到对应工作区：
   ```powershell
   # camera-developer
   cd d:/MyWork/SunEyeVision_Dev-camera

   # tool-developer
   cd d:/MyWork/SunEyeVision_Dev-tool
   ```

2. 查看任务清单并开始开发

---

## 📊 状态检查

### 检查 Worktree
```powershell
git worktree list
```

### 检查分支
```powershell
git branch -a
```

### 检查远程仓库
```powershell
git remote -v
```

### 检查 Team 配置
```powershell
cat .codebuddy/teams/camera-tool-dev/config.json
```

---

## 🚨 常见问题

### Q1: 如何删除 Worktree？
```powershell
cd d:/MyWork/SunEyeVision_Dev
git worktree remove ../SunEyeVision_Dev-camera
git worktree remove ../SunEyeVision_Dev-tool
```

### Q2: 如何清理损坏的 Worktree？
```powershell
cd d:/MyWork/SunEyeVision_Dev
git worktree prune
```

### Q3: 如何同步主分支？
```powershell
# 在相机工作区
cd d:/MyWork/SunEyeVision_Dev-camera
git fetch origin main
git rebase origin/main
```

### Q4: 如何解决合并冲突？
```powershell
# 在主工作区
cd d:/MyWork/SunEyeVision_Dev
git merge feature/tool-improvement
# 解决冲突...
git add .
git commit -m "merge: 解决冲突"
git push origin main
```

---

**配置完成！现在可以开始并行开发了！** 🎉
