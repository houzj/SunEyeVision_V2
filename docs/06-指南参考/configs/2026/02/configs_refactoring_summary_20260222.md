# SunEyeVision 插件系统重构总结

## 重构目标
将单一的`SunEyeVision.PluginSystem`项目拆分为清晰的三层架构：
1. **SunEyeVision.PluginSystem.Base** - 插件基础框架（无WPF依赖，可独立分发）
2. **SunEyeVision.PluginSystem** - 插件管理+UI支持
3. **SunEyeVision.Tools** - 工具插件独立项目（可独立开发）

## 重构进度

### ✅ 已完成

#### 1. 项目结构创建
- 创建`SunEyeVision.PluginSystem.Base`项目
- 创建`SunEyeVision.Tools`项目
- 项目文件结构完整

#### 2. 文件迁移
**迁移到Base项目的文件：**
- `Interfaces/IPluginManager.cs` → `Base/Interfaces/`
- `Interfaces/IToolPlugin.cs` → `Base/Interfaces/`
- `Interfaces/IVisionPlugin.cs` → `Base/Interfaces/`
- `Models/ToolMetadata.cs` → `Base/Models/`
- `Models/ParameterMetadata.cs` → `Base/Models/`
- `Services/PluginLoader.cs` → `Base/Services/`
- `Services/ToolRegistry.cs` → `Base/Services/`
- `Infrastructure/Base/ObservableObject.cs` → `Base/Base/`
- `Parameters/ParameterItem.cs` → `Base/Base/`

**迁移到Tools项目的文件：**
- 整个`Tools/`文件夹 → `SunEyeVision.Tools/Tools/`
- 所有工具实现（ColorConvertTool、EdgeDetectionTool等）
- 所有ViewModel和调试窗口

#### 3. 命名空间更新
- 所有Base项目文件命名空间从`SunEyeVision.PluginSystem.Core.*`更新为`SunEyeVision.PluginSystem.Base.*`
- 所有Tools项目文件命名空间从`SunEyeVision.PluginSystem.Tools`更新为`SunEyeVision.Tools`

#### 4. 项目引用更新
- `SunEyeVision.PluginSystem` 引用 `SunEyeVision.PluginSystem.Base`
- `SunEyeVision.Workflow` 引用 `SunEyeVision.PluginSystem.Base`

#### 5. WPF依赖移除
- Base项目`UseWPF=false`
- 移除ParameterItem中的Control依赖
- 移除相关using语句

### ⚠️ 待完成

#### 1. 编译错误修复
**SunEyeVision.PluginSystem项目：**
- `obj/`目录中的生成文件需要重新编译
- 部分XAML文件需要更新命名空间
- ParameterRepository、ParameterValidator等类需要保留或重构

**建议方案：**
```
A. 删除obj和bin目录，重新编译
B. 将ParameterRepository、ParameterValidator保留在PluginSystem项目（WPF相关）
C. 更新XAML文件的x:Class路径
```

#### 2. Tools项目编译
Tools项目中的ViewModel仍然引用旧的命名空间，需要：
- 更新所有using语句
- 确保没有引用PluginSystem项目的WPF相关类（除非需要UI支持）

#### 3. 解决方案文件更新
需要手动将新项目添加到解决方案：
```bash
# 方法1：使用dotnet CLI（如果.sln文件格式正确）
dotnet sln add SunEyeVision.PluginSystem.Base/SunEyeVision.PluginSystem.Base.csproj
dotnet sln add SunEyeVision.Tools/SunEyeVision.Tools.csproj

# 方法2：使用Visual Studio
# 右键解决方案 -> 添加 -> 现有项目 -> 选择两个新项目
```

#### 4. 测试验证
- 单元测试
- 集成测试
- 功能验证

## 最终架构

```
SunEyeVision.Core.dll                      # 核心基础层
  ↓
SunEyeVision.PluginSystem.Base.dll          # 插件基础框架（可独立分发）
  ↓
  ├── SunEyeVision.PluginSystem.dll         # 插件管理+UI
  └── SunEyeVision.Tools.dll                # 工具插件集合
      ↓
SunEyeVision.UI.dll                        # 主UI
SunEyeVision.Workflow.dll                  # 工作流引擎
```

## 第三方开发者只需

开发独立插件时，只需引用：
- `SunEyeVision.Core.dll`
- `SunEyeVision.PluginSystem.Base.dll`

**示例代码：**
```csharp
using SunEyeVision.PluginSystem.Base.Interfaces;
using SunEyeVision.PluginSystem.Base.Models;
using SunEyeVision.Models;

public class MyTool : IToolPlugin
{
    public string Name => "My Tool";
    public string Version => "1.0.0";
    // ... 实现IToolPlugin接口

    public List<ToolMetadata> GetToolMetadata()
    {
        return new List<ToolMetadata>
        {
            new ToolMetadata
            {
                Id = "MyTool",
                Name = "My Tool",
                Category = "Custom",
                // ...
            }
        };
    }

    public IImageProcessor CreateToolInstance(string toolId)
    {
        return new MyToolAlgorithm();
    }
}
```

## 手动修复步骤

### 步骤1：清理生成文件
```bash
cd SunEyeVision.PluginSystem
rmdir /s /q obj
rmdir /s /q bin
```

### 步骤2：重新编译
```bash
dotnet build SunEyeVision.PluginSystem.Base/SunEyeVision.PluginSystem.Base.csproj
dotnet build SunEyeVision.PluginSystem/SunEyeVision.PluginSystem.csproj
dotnet build SunEyeVision.Tools/SunEyeVision.Tools.csproj
```

### 步骤3：检查并修复剩余错误
- 打开Visual Studio
- 查看错误列表
- 逐个修复（主要是命名空间和using语句）

### 步骤4：添加项目到解决方案
- 在Visual Studio中打开`SunEyeVision.sln`
- 右键解决方案 -> 添加 -> 现有项目
- 添加`SunEyeVision.PluginSystem.Base.csproj`
- 添加`SunEyeVision.Tools.csproj`

### 步骤5：验证编译
```bash
dotnet build SunEyeVision.sln
```

## 架构优势

### 1. 清晰的分层职责
| 层级 | 职责 | WPF依赖 | 独立DLL |
|------|------|---------|---------|
| Core | 核心数据模型 | ❌ | ✅ |
| PluginSystem.Base | 插件基础框架 | ❌ | ✅ |
| PluginSystem | 插件管理+UI支持 | ✅ | ✅ |
| Tools | 具体工具实现 | ❌/✅ | ✅ |

### 2. 多人协作支持
```
团队A (基础框架)          团队B (插件开发)        团队C (工具实现)
SunEyeVision.Core     →   SunEyeVision.Tools      ColorConvertTool
SunEyeVision.PluginSystem.Base    →   OCRTool
                          →   TemplateMatchingTool
```

### 3. 代码复用和解耦
- 第三方开发者只需Base层即可开发插件
- Tools项目可以独立版本控制和发布
- PluginSystem专注于UI和管理逻辑

## 注意事项

1. **兼容性**：Base层要保持稳定，避免频繁API变更
2. **文档**：为Base层提供完整的插件开发文档
3. **版本管理**：Base、PluginSystem、Tools要独立版本号
4. **测试**：每层都要有独立的单元测试

## 联系方式
如有问题，请联系开发团队。

---
*重构日期：2026-02-07*
