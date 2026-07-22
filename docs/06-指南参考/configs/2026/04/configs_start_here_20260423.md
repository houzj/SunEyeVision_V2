# 🚀 双 Agent 并行开发 - 快速开始

---

## ✅ 准备完成

您的双 Agent 并行开发环境已准备就绪！

---

## 📊 当前状态

### Git 分支

```
* feature/tool-improvement    # 当前分支（工具插件）
  feature/camera-type-support # 相机管理器分支
  main                       # 主分支
```

### 已创建的文件

```
d:\MyWork\SunEyeVision_Dev\
├── manage_branches.ps1           # 分支管理脚本
├── branches_status.md            # 分支状态看板
├── DUAL_AGENT_GUIDE.md          # 双 Agent 开发指南
├── GIT_BRANCH_REFERENCE.md      # Git 分支参考
└── START_HERE.md               # 本文件（快速开始）
```

---

## 🎯 第一步：启动两个 Agent 对话

### 对话1：相机管理器开发

```
复制以下内容到新打开的 Agent 对话：

🏷️ [CAMERA] 对话初始化
我现在要开发相机管理器功能，工作分支是 feature/camera-type-support。

我的任务：
1. 定义 Device 基类
2. 实现 IpCameraDevice、UsbCameraDevice、GigECameraDevice
3. 定义 ICameraProvider 接口
4. 实现 HikvisionProvider、DahuaProvider
5. 更新 CameraManagerViewModel 和 UI

请专注于相机管理器相关的任务，不要处理其他分支的任务。
```

### 对话2：工具插件开发

```
复制以下内容到新打开的 Agent 对话：

🏷️ [TOOL] 对话初始化
我现在要开发工具插件优化功能，工作分支是 feature/tool-improvement。

我的任务：
1. 确定优化目标
2. 分析现有工具代码
3. 设计优化方案
4. 实现优化
5. 添加测试

请专注于工具插件相关的任务，不要处理其他分支的任务。
```

---

## 🔄 第二步：开始开发

### 在对话1中（相机管理器）

```bash
# 1. 切换到相机管理器分支
.\manage_branches.ps1 -Branch camera

# 2. 让 Agent 生成代码
# 示例：
🏷️ [CAMERA] 请帮我实现 Device 基类
文件路径：src/Workflow/Models/Device.cs

# 3. 将代码复制到 IDE
# 4. 编译并测试
# 5. 提交代码
git add .
git commit -m "feat(camera): define Device base class"
```

### 在对话2中（工具插件）

```bash
# 1. 切换到工具插件分支
.\manage_branches.ps1 -Branch tool

# 2. 让 Agent 生成代码
# 示例：
🏷️ [TOOL] 请帮我分析 ThresholdTool 的代码
文件路径：tools/SunEyeVision.Tool.Threshold/ThresholdTool.cs

# 3. 将代码复制到 IDE
# 4. 编译并测试
# 5. 提交代码
git add .
git commit -m "feat(tool): optimize parameter validation"
```

---

## 📋 第三步：进度追踪

### 更新 branches_status.md

```markdown
## 分支1：feature/camera-type-support
**状态**: 🟢 开发中
**当前任务**: 实现 IpCameraDevice
**进度**: 30%

### 待办
- [x] 定义 Device 基类
- [x] 实现 IpCameraDevice
- [ ] 实现 UsbCameraDevice
- [ ] 实现 GigECameraDevice
```

---

## 🤖 第四步：日常操作

### 每日同步

```bash
# 对话1：同步相机分支
.\manage_branches.ps1 -Branch camera
git fetch origin
git rebase origin/main

# 对话2：同步工具分支
.\manage_branches.ps1 -Branch tool
git fetch origin
git rebase origin/main
```

### 定期提交

```bash
# 对话1：提交相机分支
git add .
git commit -m "feat(camera): implement IpCameraDevice"
git push origin feature/camera-type-support

# 对话2：提交工具分支
git add .
git commit -m "feat(tool): optimize parameter validation"
git push origin feature/tool-improvement
```

---

## 🔍 第五步：冲突检测和解决

### 检测冲突

```bash
# 1. 切换到主分支
.\manage_branches.ps1 -Branch main

# 2. 合并分支2（先合工具插件）
git merge feature/tool-improvement

# 3. 合并分支1（再合相机管理器）
git merge feature/camera-type-support
```

### 解决冲突

```bash
# 1. 查看冲突文件
git status

# 2. 打开文件，手动解决冲突
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

## 📚 参考文档

- **双 Agent 开发指南**: `DUAL_AGENT_GUIDE.md`
- **Git 分支参考**: `GIT_BRANCH_REFERENCE.md`
- **分支状态看板**: `branches_status.md`

---

## ⚠️ 重要提醒

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

## 🎯 快速命令参考

```bash
# 切换到相机管理器分支
.\manage_branches.ps1 -Branch camera

# 切换到工具插件分支
.\manage_branches.ps1 -Branch tool

# 切换到主分支
.\manage_branches.ps1 -Branch main

# 查看所有分支
git branch -a

# 查看当前分支
git branch --show-current

# 查看修改状态
git status

# 查看提交历史
git log --oneline -5

# 同步主分支
git pull origin main

# 推送分支
git push origin feature/camera-type-support
```

---

## 🚀 开始您的并行开发之旅！

**现在您可以**：

1. ✅ 打开两个 Agent 对话（相机管理器 + 工具插件）
2. ✅ 使用 `manage_branches.ps1` 快速切换分支
3. ✅ 独立开发两个功能，互不干扰
4. ✅ 定期提交代码，追踪进度
5. ✅ 查看文档，解决冲突

**祝您开发顺利！** 🎉

---

**创建时间**: 2026-04-08 08:14
**最后更新**: 2026-04-08 08:14
