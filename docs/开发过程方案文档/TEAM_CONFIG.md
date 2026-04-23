# Team 配置说明

## 📋 独立团队架构

本项目采用 **独立团队模式**，每个团队有独立的智能体和对话窗口，无需通过 lead 中转协作。

---

## 👥 团队概览

### 1. Camera Development Team (camera-dev)

- **团队名称**: `camera-dev`
- **智能体**: `camera-developer`
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-camera`
- **工作分支**: `feature/camera-type-support`
- **职责**: 相机管理器开发
  - 开发相机类型支持（IP/USB/GigE 相机）
  - 实现相机配置和参数管理
  - 优化相机性能和稳定性
  - 处理相机相关的错误和异常

- **配置文件**: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-dev/config.json`
- **消息邮箱**: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-dev/inboxes/`

### 2. Tool Development Team (tool-dev)

- **团队名称**: `tool-dev`
- **智能体**: `tool-developer`
- **工作目录**: `d:/MyWork/SunEyeVision_Dev-tool`
- **工作分支**: `feature/tool-improvement`
- **职责**: 工具插件开发
  - 优化现有工具插件的性能和用户体验
  - 开发新的工具插件功能
  - 改进工具的UI/UX设计
  - 处理工具相关的错误和异常

- **配置文件**: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/tool-dev/config.json`
- **消息邮箱**: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/tool-dev/inboxes/`

---

## 🎯 工作方式

### 独立对话模式

1. **Camera Developer**
   - 直接与 `camera-developer` 智能体对话
   - 讨论相机相关的开发任务
   - 在 `d:/MyWork/SunEyeVision_Dev-camera` 目录下工作
   - 提交到 `feature/camera-type-support` 分支

2. **Tool Developer**
   - 直接与 `tool-developer` 智能体对话
   - 讨论工具相关的开发任务
   - 在 `d:/MyWork/SunEyeVision_Dev-tool` 目录下工作
   - 提交到 `feature/tool-improvement` 分支

3. **手动协调**
   - 你自己决定何时与哪个智能体交互
   - 无需通过 lead 中转
   - 完全控制开发流程

### 优势

- ✅ **独立对话框**: 每个智能体有独立的对话窗口
- ✅ **直接交互**: 无需中转，沟通更直接
- ✅ **完全控制**: 你决定何时与哪个智能体交互
- ✅ **灵活切换**: 随时在不同智能体之间切换
- ✅ **并行开发**: 两个智能体可以同时在不同 worktree 上工作

---

## 🚀 使用指南

### 启动团队

团队已经启动，你可以直接与智能体交互：

1. **Camera Developer**
   - 在对话中选择 `camera-developer`
   - 开始相机相关的开发任务

2. **Tool Developer**
   - 在对话中选择 `tool-developer`
   - 开始工具相关的开发任务

### 典型工作流程

#### 场景 1: 开发相机功能
```
用户 → camera-developer: "请实现相机参数管理功能"
camera-developer: 在 d:/MyWork/SunEyeVision_Dev-camera 下开发
camera-developer: 完成后提交到 feature/camera-type-support 分支
```

#### 场景 2: 优化工具插件
```
用户 → tool-developer: "请优化 Threshold 工具的性能"
tool-developer: 在 d:/MyWork/SunEyeVision_Dev-tool 下优化
tool-developer: 完成后提交到 feature/tool-improvement 分支
```

#### 场景 3: 同时并行开发
```
用户 → camera-developer: "开始开发相机类型支持"
用户 → tool-developer: "同时优化工具插件"

两个智能体在不同 worktree 上并行工作
```

---

## 🔄 并行开发流程

### 1. 日常开发

两个智能体可以同时工作，互不干扰：

**Camera Developer** 在 `d:/MyWork/SunEyeVision_Dev-camera` 工作
```powershell
cd d:/MyWork/SunEyeVision_Dev-camera
git branch --show-current  # feature/camera-type-support
# 开发相机功能...
git add .
git commit -m "feat: 添加相机类型支持"
git push origin feature/camera-type-support
```

**Tool Developer** 在 `d:/MyWork/SunEyeVision_Dev-tool` 工作
```powershell
cd d:/MyWork/SunEyeVision_Dev-tool
git branch --show-current  # feature/tool-improvement
# 优化工具插件...
git add .
git commit -m "refactor: 优化工具性能"
git push origin feature/tool-improvement
```

### 2. 合并到主分支

当两个智能体都完成开发后，在主工作区合并：

```powershell
cd d:/MyWork/SunEyeVision_Dev
git pull origin main
git merge feature/tool-improvement
git merge feature/camera-type-support
git push origin main
```

---

## ⚠️ 注意事项

1. **独立工作**
   - camera-developer 只在 `feature/camera-type-support` 工作
   - tool-developer 只在 `feature/tool-improvement` 工作
   - 不要修改其他分支的代码

2. **定期同步**
   - 定期将主分支的更新合并到各自的分支
   - 避免合并时出现大量冲突

3. **解决冲突**
   - 合并时如果出现冲突，及时通知相关智能体
   - 协助解决冲突

4. **沟通协调**
   - 你负责协调两个智能体的工作
   - 决定何时合并代码

---

## 📝 配置文件

### Team 配置文件
- Camera Dev: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-dev/config.json`
- Tool Dev: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/tool-dev/config.json`

### 消息历史
- Camera Dev: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/camera-dev/inboxes/`
- Tool Dev: `d:/MyWork/SunEyeVision_Dev/.codebuddy/teams/tool-dev/inboxes/`

---

## ✅ 配置完成

- ✅ 两个独立团队已创建
- ✅ camera-developer 和 tool-developer 已启动
- ✅ 独立对话模式已配置
- ✅ Git Worktree 已设置
- ✅ 配置文档已更新

现在你可以：
1. 直接与 **camera-developer** 讨论相机开发任务
2. 直接与 **tool-developer** 讨论工具开发任务
3. 自己协调两个智能体的工作进度
4. 随时在不同智能体之间切换

开始你的并行开发吧！🚀
