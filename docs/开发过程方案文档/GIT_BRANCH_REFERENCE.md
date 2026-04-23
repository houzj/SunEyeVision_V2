# Git 分支管理参考

---

## 📋 常用命令

### 分支操作

```bash
# 查看所有分支
git branch -a

# 查看当前分支
git branch --show-current

# 创建新分支
git checkout -b feature/new-feature

# 切换分支
git checkout branch-name

# 删除本地分支
git branch -d branch-name

# 删除远程分支
git push origin --delete branch-name
```

### 提交和推送

```bash
# 查看修改状态
git status

# 添加所有修改
git add .

# 添加指定文件
git add file.cs

# 提交
git commit -m "提交信息"

# 推送到远程
git push origin branch-name

# 推送并设置上游分支
git push -u origin branch-name
```

### 同步和合并

```bash
# 拉取最新代码
git pull origin main

# 获取远程更新（不合并）
git fetch origin

# 合并分支
git merge branch-name

# 变基分支
git rebase branch-name
```

### 查看历史

```bash
# 查看提交历史
git log

# 查看简化历史
git log --oneline

# 查看图形历史
git log --graph --oneline --all

# 查看文件修改
git diff file.cs
```

---

## 🔧 分支管理脚本使用

### 基本用法

```powershell
# 切换到相机管理器分支
.\manage_branches.ps1 -Branch camera

# 切换到工具插件分支
.\manage_branches.ps1 -Branch tool

# 切换到主分支
.\manage_branches.ps1 -Branch main
```

### 输出说明

```
====================================
切换到分支: feature/camera-type-support
====================================
✓ 切换成功

当前分支:
  feature/camera-type-support

最近3次提交:
  abc1234 - feat(camera): implement IpCameraDevice
  def5678 - feat(camera): define Device base class
  ghi9012 - docs: add camera architecture

当前修改状态:
  M - 修改: src/Workflow/Models/Device.cs
  A - 新增: src/Workflow/Models/CameraDevices/IpCameraDevice.cs
====================================
```

---

## 🔄 典型工作流

### 功能开发流程

```bash
# 1. 从主分支创建功能分支
git checkout main
git pull origin main
git checkout -b feature/new-feature

# 2. 开发功能
# ... 编写代码 ...

# 3. 查看修改
git status

# 4. 添加并提交
git add .
git commit -m "feat: add new feature"

# 5. 推送到远程
git push -u origin feature/new-feature

# 6. 创建 Pull Request（使用 GitHub/GitLab）

# 7. 代码审查通过后，合并到主分支

# 8. 删除功能分支
git branch -d feature/new-feature
```

### 冲突解决流程

```bash
# 1. 合并分支
git checkout main
git merge feature/new-feature

# 2. 如果有冲突，Git 会提示
CONFLICT (content): Merge conflict in src/Models/Device.cs

# 3. 查看冲突文件
git status

# 4. 打开文件，手动解决冲突
<<<<<<< HEAD
// 主分支的代码
=======
// 分支的代码
>>>>>>> feature/new-feature

# 5. 删除冲突标记，保留需要的代码

# 6. 标记冲突已解决
git add src/Models/Device.cs

# 7. 完成合并
git commit

# 8. 推送到远程
git push origin main
```

---

## 📝 提交信息规范

### 提交信息格式

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type 类型

- `feat`: 新功能
- `fix`: 修复 Bug
- `docs`: 文档更新
- `style`: 代码格式（不影响功能）
- `refactor`: 重构（不是新功能，也不是修复）
- `test`: 测试相关
- `chore`: 构建/工具相关

### Scope 范围

- `camera`: 相机管理器
- `tool`: 工具插件
- `ui`: 用户界面
- `workflow`: 工作流
- `core`: 核心服务
- `sdk`: 插件 SDK

### 示例

```
feat(camera): add IP camera support

- Add IpCameraDevice class
- Add ICameraProvider interface
- Implement HikvisionProvider

Closes #123

---

fix(tool): resolve parameter validation issue

Fix issue where invalid parameters could be saved
to the solution file.

Fixes #124
```

---

## 🚨 常见问题

### Q1: 如何查看两个分支的差异？

```bash
# 查看两个分支的差异
git diff branch1 branch2

# 查看当前分支和主分支的差异
git diff main

# 查看文件在两个分支中的差异
git diff main feature/camera -- src/Workflow/Models/Device.cs
```

### Q2: 如何撤销未提交的修改？

```bash
# 撤销工作区的修改
git checkout -- file.cs

# 撤销暂存区的修改
git reset HEAD file.cs

# 撤销最近一次提交（保留修改）
git reset --soft HEAD~1

# 撤销最近一次提交（丢弃修改）
git reset --hard HEAD~1
```

### Q3: 如何查看提交历史？

```bash
# 查看当前分支的提交历史
git log

# 查看所有分支的提交历史
git log --all

# 查看图形化提交历史
git log --graph --oneline --all

# 查看某个文件的提交历史
git log --follow -- file.cs
```

### Q4: 如何解决合并冲突？

```bash
# 1. 查看冲突文件
git status

# 2. 打开文件，找到冲突标记
<<<<<<< HEAD
# 主分支的代码
=======
# 分支的代码
>>>>>>> branch-name

# 3. 手动编辑，解决冲突

# 4. 标记冲突已解决
git add .

# 5. 完成合并
git commit
```

---

## 📚 参考资源

- [Git 官方文档](https://git-scm.com/doc)
- [GitHub Git 指南](https://guides.github.com/introduction/git/)
- [Pro Git 中文版](https://git-scm.com/book/zh/v2)

---

**最后更新**: 2026-04-08
