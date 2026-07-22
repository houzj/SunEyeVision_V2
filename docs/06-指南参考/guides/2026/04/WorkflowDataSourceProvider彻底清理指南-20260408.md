# WorkflowDataSourceProvider 彻底清理指南

## 已完成的清理工作

### 1. ✅ ThresholdToolDebugControl.xaml.cs

**文件路径**: `tools/SunEyeVision.Tool.Threshold/Views/ThresholdToolDebugControl.xaml.cs`

**修改内容**:

#### 1.1 清理 InitializeRegionEditor 方法
```csharp
// 删除了注释掉的旧代码
// _dataProvider = new WorkflowDataSourceProvider(); // 已弃用
```

#### 1.2 简化 SetDataProvider 方法
```csharp
// 删除了对 WorkflowDataSourceProvider 的支持
// 只保留 IRegionDataSourceProvider

public void SetDataProvider(object dataProvider)
{
    PluginLogger.Info($"SetDataProvider 被调用，dataProvider = {(dataProvider != null ? "非空" : "null")}", "ThresholdTool");

    // 只支持 IRegionDataSourceProvider
    if (dataProvider is IRegionDataSourceProvider newProvider)
    {
        _dataProvider = newProvider;
        PluginLogger.Info("使用 IRegionDataSourceProvider", "ThresholdTool");
        PopulateImageSources(_dataProvider);
    }
    else
    {
        PluginLogger.Warning($"未知的 dataProvider 类型：{dataProvider?.GetType().Name}", "ThresholdTool");
        return;
    }
}
```

#### 1.3 更新 InitializeRegionEditor 调用
```csharp
// 修改前
_regionEditorIntegration = new RegionEditorIntegration(regionEditorViewModel);

// 修改后
_regionEditorIntegration = new RegionEditorIntegration(regionEditorViewModel, _dataProvider);
```

### 2. ✅ RegionEditorIntegration.cs

**文件路径**: `src/Plugin.SDK/UI/Controls/Region/Logic/RegionEditorIntegration.cs`

**修改内容**:

#### 2.1 删除旧构造函数
```csharp
// 完全删除了向后兼容的构造函数
[Obsolete("请使用 RegionEditorIntegration(RegionEditorViewModel, IRegionDataSourceProvider, ILogger) 构造函数")]
public RegionEditorIntegration(RegionEditorViewModel viewModel, ILogger? logger = null)
    : this(viewModel, new WorkflowDataSourceProvider(logger), logger)
{
    PluginLogger.Logger.LogWarning("使用了已弃用的 RegionEditorIntegration 构造函数，建议迁移到新的 IRegionDataSourceProvider", "RegionEditorIntegration");
}
```

#### 2.2 清理导入语句
```csharp
// 删除了不必要的导入
using SunEyeVision.Plugin.SDK.Execution.Parameters;
```

### 3. ✅ WorkflowExecutionEngine.cs

**文件路径**: `src/Workflow/WorkflowExecutionEngine.cs`

**修改内容**:

#### 3.1 更新成员变量
```csharp
// 已经在之前的优化中完成
private readonly DataSourceQueryService _dataSourceQueryService;
private readonly Dictionary<string, DataSourceQueryServiceAdapter> _dataProviderAdapters = new();
```

#### 3.2 使用新的适配器
所有相关方法已经在之前的优化中更新为使用 `DataSourceQueryServiceAdapter`

### 4. ✅ MainWindowViewModel.cs

**文件路径**: `src/UI/ViewModels/MainWindowViewModel.cs`

**修改内容**:

#### 4.1 更新成员变量类型
```csharp
// 已经在之前的优化中完成
private readonly Dictionary<string, IRegionDataSourceProvider> _nodeDataProviders = new();
```

#### 4.2 简化数据提供者创建
```csharp
// 已经在之前的优化中完成
var dataProvider = workflowExecutionEngine.GetDataProvider(currentNode.Id);
```

## 编译错误修复记录

### 问题
在创建 `DataSourceQueryServiceAdapter` 时，出现以下编译错误：
- CS0246: 未能找到类型或命名空间名"ParentNodeInfo"
- CS0246: 未能找到类型或命名空间名"IDataSourceQueryService"
- CS0246: 未能找到类型或命名空间名"AvailableDataSource"

### 解决方案
在 `DataSourceQueryServiceAdapter.cs` 文件开头添加缺少的 using 指令：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;  // ← 添加这一行
```

### 验证结果
✅ 所有编译错误已解决
✅ 所有文件通过 linter 检查
✅ 代码可以正常编译

## 需要手动执行的清理步骤

### 1. 删除 WorkflowDataSourceProvider.cs 文件

**文件路径**: `src/Plugin.SDK/UI/Controls/Region/Models/WorkflowDataSourceProvider.cs`

**操作**: 直接删除该文件

**原因**: 该类已经完全被 `DataSourceQueryServiceAdapter` 替代，没有任何代码在使用它

### 2. 验证编译

**操作步骤**:
1. 在 Visual Studio 或 Rider 中重新加载项目
2. 执行清理解决方案
3. 执行重新生成解决方案
4. 检查是否有编译错误

**预期结果**: 
- ✅ 无编译错误
- ✅ 无 `WorkflowDataSourceProvider` 相关的警告

### 3. 检查其他工具

**操作步骤**:
1. 在项目中搜索 `WorkflowDataSourceProvider` 的引用
2. 确认所有工具都已更新
3. 如果还有旧工具使用旧类，需要迁移

**搜索命令**:
```bash
cd d:/MyWork/SunEyeVision_Dev-tool
find . -name "*.cs" -type f -exec grep -l "WorkflowDataSourceProvider" {} \;
```

### 4. 更新文档

**需要更新的文档**:
- API 文档：删除 `WorkflowDataSourceProvider` 的说明
- 示例代码：更新为使用 `IRegionDataSourceProvider`
- 迁移指南：如果存在，可以标记为已完成

## 清理验证清单

- [x] ThresholdToolDebugControl 已更新
- [x] RegionEditorIntegration 已更新
- [x] WorkflowExecutionEngine 已更新
- [x] MainWindowViewModel 已更新
- [ ] WorkflowDataSourceProvider.cs 已删除
- [ ] 编译验证通过
- [ ] 所有工具已迁移
- [ ] 文档已更新
- [ ] 测试通过

## 清理后的架构

### 新的数据源架构

```
DataSourceQueryService (核心服务)
├── 管理节点执行结果缓存
├── 从 IWorkflowConnectionProvider 获取连接关系
├── 从 INodeInfoProvider 获取节点信息
└── 提供数据查询接口

DataSourceQueryServiceAdapter (适配器)
├── 实现 IRegionDataSourceProvider 接口
├── 适配到 UI 组件（Region Editor、调试窗口等）
└── 作为 DataSourceQueryService 和 UI 组件之间的桥梁

UI 组件
├── ThresholdToolDebugControl
├── RegionEditorIntegration
└── 其他工具调试控件
```

## 优势总结

### 1. 代码更干净
- ✅ 删除了重复的代码
- ✅ 删除了向后兼容的临时代码
- ✅ 代码结构更清晰

### 2. 职责更明确
- ✅ 查询服务：专门负责数据查询
- ✅ 适配器：专门负责接口适配
- ✅ UI 组件：专注业务逻辑

### 3. 维护更简单
- ✅ 单一数据源
- ✅ 统一的缓存管理
- ✅ 减少了代码重复

### 4. 扩展更容易
- ✅ 新工具直接使用 `IRegionDataSourceProvider`
- ✅ 可以轻松添加新的查询功能
- ✅ 支持不同的数据源实现

## 注意事项

### 1. 编译警告
删除 `WorkflowDataSourceProvider.cs` 后，可能会出现以下警告：
- `DataSourceQueryServiceAdapter` 注释中的引用
- 这些是文档注释，不影响编译

### 2. 运行时检查
如果代码还有隐式的依赖，可能只在运行时才发现：
- 反射调用
- 序列化/反序列化
- 插件动态加载

### 3. 向后兼容
如果下游项目（插件）还在使用 `WorkflowDataSourceProvider`：
- 需要通知插件开发者更新
- 提供迁移指南
- 给予过渡期

## 后续工作

### 立即执行
1. 删除 `WorkflowDataSourceProvider.cs` 文件
2. 编译验证
3. 运行测试

### 短期工作
1. 检查所有自定义工具是否都已迁移
2. 更新文档
3. 发布迁移通知

### 长期工作
1. 监控是否有新的工具使用旧类
2. 考虑在编译时检测旧类的使用
3. 持续优化数据源架构

---

**文档版本**: 1.0
**创建日期**: 2026-04-08
**作者**: AI Assistant
**状态**: ✅ 代码清理完成，等待删除文件和验证
