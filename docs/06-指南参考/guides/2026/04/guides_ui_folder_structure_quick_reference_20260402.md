# UI层文件夹结构优化 - 快速参考

## 🎯 核心变更

### 文件夹移动

```
UI/Controls/Helpers/      →  UI/Helpers/
UI/Controls/Rendering/    →  UI/Rendering/
UI/Views/Controls/        →  UI/Controls/
```

### 命名空间变更

```csharp
// 变更前
using SunEyeVision.UI.Controls.Helpers;
using SunEyeVision.UI.Controls.Rendering;
using SunEyeVision.UI.Views.Controls;

// 变更后
using SunEyeVision.UI.Helpers;
using SunEyeVision.UI.Rendering;
using SunEyeVision.UI.Controls;
```

---

## 📋 快速迁移脚本

### PowerShell 脚本

```powershell
# 1. 移动文件夹
Move-Item "UI\Controls\Helpers" "UI\Helpers"
Move-Item "UI\Controls\Rendering" "UI\Rendering"
Move-Item "UI\Views\Controls" "UI\Controls"

# 2. 更新命名空间（C# 文件）
Get-ChildItem -Path "UI\Helpers" -Filter "*.cs" -Recurse | 
    ForEach-Object {
        (Get-Content $_.FullName) -replace 
            'namespace SunEyeVision\.UI\.Controls\.Helpers', 
            'namespace SunEyeVision.UI.Helpers' | 
        Set-Content $_.FullName
    }

Get-ChildItem -Path "UI\Rendering" -Filter "*.cs" -Recurse | 
    ForEach-Object {
        (Get-Content $_.FullName) -replace 
            'namespace SunEyeVision\.UI\.Controls\.Rendering', 
            'namespace SunEyeVision.UI.Rendering' | 
        Set-Content $_.FullName
    }

Get-ChildItem -Path "UI\Controls" -Filter "*.cs" -Recurse | 
    ForEach-Object {
        (Get-Content $_.FullName) -replace 
            'namespace SunEyeVision\.UI\.Views\.Controls', 
            'namespace SunEyeVision.UI.Controls' | 
        Set-Content $_.FullName
    }

# 3. 更新引用（所有 C# 文件）
Get-ChildItem -Path "src" -Filter "*.cs" -Recurse | 
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        $content = $content -replace 'using SunEyeVision\.UI\.Controls\.Helpers', 'using SunEyeVision.UI.Helpers'
        $content = $content -replace 'using SunEyeVision\.UI\.Controls\.Rendering', 'using SunEyeVision.UI.Rendering'
        $content = $content -replace 'using SunEyeVision\.UI\.Views\.Controls', 'using SunEyeVision.UI.Controls'
        Set-Content $_.FullName $content
    }

# 4. 更新 XAML 引用
Get-ChildItem -Path "src" -Filter "*.xaml" -Recurse | 
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        $content = $content -replace 'clr-namespace:SunEyeVision\.UI\.Controls\.Helpers', 'clr-namespace:SunEyeVision.UI.Helpers'
        $content = $content -replace 'clr-namespace:SunEyeVision\.UI\.Controls\.Rendering', 'clr-namespace:SunEyeVision.UI.Rendering'
        $content = $content -replace 'clr-namespace:SunEyeVision\.UI\.Views\.Controls', 'clr-namespace:SunEyeVision.UI.Controls'
        Set-Content $_.FullName $content
    }

Write-Host "迁移完成！" -ForegroundColor Green
```

### Bash 脚本（Git Bash / WSL）

```bash
#!/bin/bash

# 1. 移动文件夹
git mv UI/Controls/Helpers UI/Helpers
git mv UI/Controls/Rendering UI/Rendering
git mv UI/Views/Controls UI/Controls

# 2. 更新命名空间（C# 文件）
find UI/Helpers -name "*.cs" -exec sed -i 's/namespace SunEyeVision\.UI\.Controls\.Helpers/namespace SunEyeVision.UI.Helpers/g' {} +
find UI/Rendering -name "*.cs" -exec sed -i 's/namespace SunEyeVision\.UI\.Controls\.Rendering/namespace SunEyeVision.UI.Rendering/g' {} +
find UI/Controls -name "*.cs" -exec sed -i 's/namespace SunEyeVision\.UI\.Views\.Controls/namespace SunEyeVision.UI.Controls/g' {} +

# 3. 更新引用（所有 C# 文件）
find src/ -name "*.cs" -exec sed -i 's/using SunEyeVision\.UI\.Controls\.Helpers/using SunEyeVision.UI.Helpers/g' {} +
find src/ -name "*.cs" -exec sed -i 's/using SunEyeVision\.UI\.Controls\.Rendering/using SunEyeVision.UI.Rendering/g' {} +
find src/ -name "*.cs" -exec sed -i 's/using SunEyeVision\.UI\.Views\.Controls/using SunEyeVision.UI.Controls/g' {} +

# 4. 更新 XAML 引用
find src/ -name "*.xaml" -exec sed -i 's/clr-namespace:SunEyeVision\.UI\.Controls\.Helpers/clr-namespace:SunEyeVision.UI.Helpers/g' {} +
find src/ -name "*.xaml" -exec sed -i 's/clr-namespace:SunEyeVision\.UI\.Controls\.Rendering/clr-namespace:SunEyeVision.UI.Rendering/g' {} +
find src/ -name "*.xaml" -exec sed -i 's/clr-namespace:SunEyeVision\.UI\.Views\.Controls/clr-namespace:SunEyeVision.UI.Controls/g' {} +

echo "迁移完成！"
```

---

## ✅ 验证步骤

```bash
# 1. 编译项目
dotnet build

# 2. 运行测试
dotnet test

# 3. 检查命名空间
grep -r "SunEyeVision.UI.Controls.Helpers" src/    # 应该无结果
grep -r "SunEyeVision.UI.Controls.Rendering" src/  # 应该无结果
grep -r "SunEyeVision.UI.Views.Controls" src/      # 应该无结果

# 4. 检查新命名空间
grep -r "SunEyeVision.UI.Helpers" src/             # 应该有结果
grep -r "SunEyeVision.UI.Rendering" src/           # 应该有结果
grep -r "SunEyeVision.UI.Controls" src/            # 应该有结果
```

---

## 🚨 常见问题

### Q1: 编译错误 - 找不到类型

**问题**:
```
error CS0246: The type or namespace name 'WorkflowCanvasControl' could not be found
```

**解决**:
```csharp
// 添加 using 语句
using SunEyeVision.UI.Controls.Canvas;
```

### Q2: XAML 解析错误

**问题**:
```
error MC3074: XML 命名空间中不存在标记
```

**解决**:
```xml
<!-- 更新 xmlns 引用 -->
xmlns:controls="clr-namespace:SunEyeVision.UI.Controls.Canvas"
```

### Q3: 运行时错误 - 类型初始化失败

**问题**:
```
TypeInitializationException: 类型初始化设定项引发异常
```

**解决**:
1. 检查所有 XAML 文件的命名空间引用
2. 清理并重新编译: `dotnet clean && dotnet build`

---

## 📞 需要帮助？

如果遇到问题，请：

1. 查看详细文档: `docs/refactor/ui-folder-structure-optimization.md`
2. 检查 Git 历史: `git log --oneline --graph`
3. 回退到备份分支: `git checkout backup/ui-structure-backup`

---

## 📊 进度跟踪

- [ ] 文件夹移动完成
- [ ] 命名空间更新完成
- [ ] C# 引用更新完成
- [ ] XAML 引用更新完成
- [ ] 编译成功
- [ ] 测试通过
- [ ] 文档更新
- [ ] 代码提交

---

**预计时间**: 3天  
**影响文件**: 约50个  
**影响范围**: UI层全部
