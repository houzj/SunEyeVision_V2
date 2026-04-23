# 双 Agent 并行开发指南

---

## 🎯 概述

本指南介绍如何使用两个独立的 Agent 对话并行开发两个功能分支：
- **对话1**: 专注于 `feature/camera-type-support` 分支（相机管理器）
- **对话2**: 专注于 `feature/tool-improvement` 分支（工具插件）

---

## 🚀 使用流程

### 步骤1：启动两个 Agent 对话

```
┌──────────────────────────────────────────────────────┐
│  对话1：相机管理器开发                             │
│  - 专注于 feature/camera-type-support 分支           │
│  - 任务：新增相机类型支持                          │
│  - 上下文：只处理相机相关任务                     │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  对话2：工具插件开发                               │
│  - 专注于 feature/tool-improvement 分支            │
│  - 任务：优化工具插件                              │
│  - 上下文：只处理工具相关任务                     │
└──────────────────────────────────────────────────────┘
```

### 步骤2：在对话1中设置上下文

```
用户：
🏷️ [CAMERA] 对话初始化
我现在要开发相机管理器功能，工作分支是 feature/camera-type-support。

我的任务：
1. 定义 Device 基类
2. 实现 IpCameraDevice、UsbCameraDevice、GigECameraDevice
3. 定义 ICameraProvider 接口
4. 实现 HikvisionProvider、DahuaProvider
5. 更新 CameraManagerViewModel 和 UI

请专注于相机管理器相关的任务，不要处理其他分支的任务。

Agent：
好的，我已设置为相机管理器开发模式。
分支：feature/camera-type-support
专注领域：相机类型支持
```

### 步骤3：在对话2中设置上下文

```
用户：
🏷️ [TOOL] 对话初始化
我现在要开发工具插件优化功能，工作分支是 feature/tool-improvement。

我的任务：
1. 确定优化目标
2. 分析现有工具代码
3. 设计优化方案
4. 实现优化
5. 添加测试

请专注于工具插件相关的任务，不要处理其他分支的任务。

Agent：
好的，我已设置为工具插件开发模式。
分支：feature/tool-improvement
专注领域：工具插件优化
```

---

## 📋 Git 分支操作

### 切换分支（使用脚本）

```bash
# 切换到相机管理器分支（对话1使用）
.\manage_branches.ps1 -Branch camera

# 切换到工具插件分支（对话2使用）
.\manage_branches.ps1 -Branch tool

# 切换到主分支（合并时使用）
.\manage_branches.ps1 -Branch main
```

### 切换分支（使用 Git 命令）

```bash
# 对话1：切换到相机管理器分支
git checkout feature/camera-type-support

# 对话2：切换到工具插件分支
git checkout feature/tool-improvement
```

### 提交代码

```bash
# 对话1：提交相机管理器代码
git add .
git commit -m "feat(camera): implement IpCameraDevice class"
git push origin feature/camera-type-support

# 对话2：提交工具插件代码
git add .
git commit -m "feat(tool): optimize parameter validation"
git push origin feature/tool-improvement
```

---

## 🔄 工作流程示例

### 场景：同时开发两个功能

```
时间线：
08:00 - 创建两个分支，启动两个 Agent 对话
       对话1：设置 [CAMERA] 上下文
       对话2：设置 [TOOL] 上下文

08:15 - 对话1：
       用户：🏷️ [CAMERA] 请帮我实现 Device 基类
       Agent：提供代码...
       用户：好的，我切换到相机分支并添加代码

08:30 - 对话2：
       用户：🏷️ [TOOL] 请帮我分析 ThresholdTool 的代码
       Agent：分析...
       用户：好的，我切换到工具分支进行优化

09:00 - 对话1：
       用户：🏷️ [CAMERA] 代码已提交
       Agent：很好！下一个任务是什么？
       用户：实现 IpCameraDevice

09:30 - 对话2：
       用户：🏷️ [TOOL] 代码已提交
       Agent：很好！还需要做什么？

... 两个对话独立进行 ...

17:00 - 两个分支都完成开发
       用户：准备合并分支
       检查冲突，解决冲突
       合并到主分支
```

---

## ⚠️ 重要注意事项

### 1. 避免修改同一文件

**原则**：两个分支不要修改相同的文件

**示例**：
```
✅ 好的做法：
- 分支1：src/Workflow/Models/CameraDevices/IpCameraDevice.cs
- 分支2：tools/SunEyeVision.Tool.Threshold/ThresholdTool.cs
两个分支修改不同的文件，不会有冲突

❌ 不好的做法：
- 分支1：src/Workflow/SolutionManager.cs
- 分支2：src/Workflow/SolutionManager.cs
两个分支修改同一个文件，会有冲突
```

### 2. 定期同步主分支

**建议**：每天同步一次主分支的最新代码

```bash
# 对话1：同步相机分支
git checkout feature/camera-type-support
git fetch origin
git rebase origin/main

# 对话2：同步工具分支
git checkout feature/tool-improvement
git fetch origin
git rebase origin/main
```

### 3. 使用明确的任务标记

**对话1示例**：
```
✅ 好的做法：
🏷️ [CAMERA] 请帮我实现 IpCameraDevice 类
文件路径：src/Workflow/Models/CameraDevices/IpCameraDevice.cs

❌ 不好的做法：
请帮我实现 IpCameraDevice 类
（缺少上下文）
```

**对话2示例**：
```
✅ 好的做法：
🏷️ [TOOL] 请帮我优化 ThresholdTool 的参数验证
文件路径：tools/SunEyeVision.Tool.Threshold/ThresholdTool.cs

❌ 不好的做法：
请帮我优化 ThresholdTool
（缺少上下文）
```

---

## 🔍 冲突检测和解决

### 检测冲突

```bash
# 1. 切换到主分支
.\manage_branches.ps1 -Branch main

# 2. 合并分支2（先合工具插件分支）
git merge feature/tool-improvement

# 3. 合并分支1（再合相机管理器分支）
git merge feature/camera-type-support

# 4. 如果有冲突，Git 会提示
CONFLICT (content): Merge conflict in src/Workflow/Models/Device.cs
```

### 解决冲突

```bash
# 1. 查看冲突文件
git status

# 2. 打开冲突文件，手动解决
# 冲突标记：
<<<<<<< HEAD
// 主分支的代码
=======
// 分支的代码
>>>>>>> feature/camera-type-support

# 3. 解决后标记为已解决
git add .

# 4. 完成合并
git commit

# 5. 推送到远程
git push origin main
```

---

## 📊 进度追踪

### 使用 branches_status.md

```markdown
## 分支1：feature/camera-type-support
**状态**: 🟢 开发中
**当前任务**: 实现 IpCameraDevice
**进度**: 30%

## 分支2：feature/tool-improvement
**状态**: 🟡 规划中
**当前任务**: 分析现有代码
**进度**: 10%
```

### 更新进度

```markdown
# 完成任务后更新状态
- [x] 定义 Device 基类
- [x] 实现 IpCameraDevice
- [ ] 实现 UsbCameraDevice
```

---

## 🎯 最佳实践总结

### ✅ 推荐做法

1. **明确上下文**：每个对话只处理一个分支的任务
2. **使用任务标记**：🏷️ [CAMERA] 或 🏷️ [TOOL]
3. **定期提交**：每完成一个小功能就提交
4. **独立测试**：每个分支单独测试后再合并
5. **及时同步**：定期同步主分支的最新代码

### ❌ 避免做法

1. **避免跨分支**：不要在一个对话中处理两个分支
2. **避免重复文件**：不要在两个分支中修改同一个文件
3. **避免大量提交**：不要积累太多提交再合并
4. **避免忽略测试**：不要跳过单元测试和集成测试
5. **避免强行合并**：有冲突时要仔细解决，不要盲目使用 --force

---

## 🚀 快速开始

### 1. 打开第一个 Agent 对话（相机管理器）

```
复制以下内容到对话1：

🏷️ [CAMERA] 对话初始化
我现在要开发相机管理器功能，工作分支是 feature/camera-type-support。
请专注于相机管理器相关的任务，不要处理其他分支的任务。
```

### 2. 打开第二个 Agent 对话（工具插件）

```
复制以下内容到对话2：

🏷️ [TOOL] 对话初始化
我现在要开发工具插件优化功能，工作分支是 feature/tool-improvement。
请专注于工具插件相关的任务，不要处理其他分支的任务。
```

### 3. 开始开发

- 在对话1中：切换到相机分支，开始开发相机管理器
- 在对话2中：切换到工具分支，开始开发工具插件

---

**祝您开发顺利！** 🚀
