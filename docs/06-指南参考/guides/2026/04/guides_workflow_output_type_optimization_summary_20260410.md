# 工作流输出类型优化方案 - 实施总结

## 📋 实施日期
2026-04-09

## 🎯 优化目标

优化数据源查询性能，提供类型分组功能，提升用户体验。

### 核心问题
1. **用户体验**：数据源列表扁平显示，缺少类型分组
2. **全量刷新**：节点变化时需要全量刷新数据源

### 优化方案
1. **类型分组显示**：按图像、形状、数值、文本、列表、其他分类
2. **增量更新机制**：连接建立/删除时增量更新上游节点池

## 📁 新增文件

### 1. TypeCategoryMapper.cs
**路径**：`src/Plugin.SDK/Execution/Parameters/TypeCategoryMapper.cs`

**功能**：
- `OutputTypeCategory` 枚举：定义6种数据类型分类
- `TypeCategoryMapper` 静态类：类型到分类的映射

**分类逻辑**：
- **Image**: 图像数据（OpenCvSharp.Mat）
- **Shape**: 几何形状（Point, Rect, Circle, Line, Polygon）
- **Numeric**: 数值类型（int, double, float, bool）
- **Text**: 文本类型（string, char）
- **List**: 列表类型（List, Array, ObservableCollection）
- **Other**: 其他类型（自定义类型、复合类型）

**自定义扩展**：
- `RegisterCustomMapping()`：注册自定义类型映射
- `UnregisterCustomMapping()`：移除自定义映射
- `ClearCustomMappings()`：清除所有自定义映射

**UI 辅助**：
- `GetCategoryDisplayName()`：获取分类显示名称
- `GetCategoryIcon()`：获取分类图标

### 2. GroupedDataSources.cs
**路径**：`src/Plugin.SDK/Execution/Parameters/GroupedDataSources.cs`

**功能**：
- 按输出类型分类存储数据源
- 提供便捷的 UI 显示接口

**分组结构**：
```csharp
public class GroupedDataSources
{
    public List<AvailableDataSource> ImageSources { get; set; }
    public List<AvailableDataSource> ShapeSources { get; set; }
    public List<AvailableDataSource> NumericSources { get; set; }
    public List<AvailableDataSource> TextSources { get; set; }
    public List<AvailableDataSource> ListSources { get; set; }
    public List<AvailableDataSource> OtherSources { get; set; }
}
```

**核心方法**：
- `AddDataSource()`：添加数据源到对应分组
- `GetSourcesByCategory()`：获取指定分类的数据源
- `GetStatistics()`：获取分组统计信息
- `Clear()`：清空所有分组

## ✏️ 修改文件

### 1. WorkflowNode.cs
**路径**：`src/Workflow/WorkflowNode.cs`

**修改内容**：
- 新增 `UpstreamNodesByOutputType` 属性
  - 类型：`Dictionary<Type, List<string>>`
  - 说明：存储上游节点ID列表，按输出类型分组
  - 访问级别：internal（UI 层专用，不序列化）

- 新增 `AddUpstreamNode()` 方法
  - 功能：添加上游节点（增量更新）
  - 参数：
    - `upstreamNodeId`：上游节点ID
    - `outputTypes`：上游节点的输出类型列表

- 新增 `RemoveUpstreamNode()` 方法
  - 功能：移除上游节点（增量更新）
  - 参数：
    - `upstreamNodeId`：上游节点ID
    - `outputTypes`：上游节点的输出类型列表

- 新增 `ClearUpstreamNodes()` 方法
  - 功能：清空上游节点池

### 2. Workflow.cs
**路径**：`src/Workflow/Workflow.cs`

**修改内容**：
- 修改 `AddConnection()` 方法
  - 添加连接增量更新逻辑
  - 调用 `UpdateUpstreamPoolOnConnectionAdded()` 更新目标节点的上游节点池

- 修改 `RemoveConnection()` 方法
  - 添加连接增量更新逻辑
  - 调用 `UpdateUpstreamPoolOnConnectionRemoved()` 更新目标节点的上游节点池

- 新增 `UpdateUpstreamPoolOnConnectionAdded()` 方法（private）
  - 功能：连接建立时更新上游节点池（增量更新）
  - 实现逻辑：
    1. 获取源节点和目标节点
    2. 获取源节点的输出类型
    3. 调用目标节点的 `AddUpstreamNode()` 方法

- 新增 `UpdateUpstreamPoolOnConnectionRemoved()` 方法（private）
  - 功能：连接删除时更新上游节点池（增量更新）
  - 实现逻辑：
    1. 获取源节点和目标节点
    2. 获取源节点的输出类型
    3. 调用目标节点的 `RemoveUpstreamNode()` 方法

### 3. IDataSourceQueryService.cs
**路径**：`src/Plugin.SDK/Execution/Parameters/IDataSourceQueryService.cs`

**修改内容**：
- 新增 `GetAvailableDataSourcesGrouped()` 接口方法
  - 功能：获取分组可用数据源（按输出类型分类）
  - 参数：
    - `nodeId`：当前节点ID
    - `targetType`：目标参数类型（可选，用于类型过滤）
  - 返回值：`GroupedDataSources`（分组数据源容器）

### 4. DataSourceQueryService.cs
**路径**：`src/Plugin.SDK/Execution/Parameters/DataSourceQueryService.cs`

**修改内容**：
- 新增 `GetAvailableDataSourcesGrouped()` 方法实现
  - 功能：获取分组可用数据源（按输出类型分类）
  - 实现逻辑：
    1. 使用 `GetParentNodes()` 获取上游节点
    2. 遍历上游节点，获取输出属性
    3. 类型过滤（如果 `targetType` 不为 null）
    4. 按类型分类添加到 `GroupedDataSources`
  - 日志输出：
    - 记录查询节点ID和目标类型
    - 记录遍历的父节点数量
    - 记录每个父节点的输出属性数量
    - 记录分组统计信息

## 🚀 核心优化

### 用户体验优化
- **类型分组**：按数据大类分组显示
  - 图像、形状、数值、文本、列表、其他
  - 符合视觉软件行业标准
- **分组统计**：提供各分组的数量统计
  - 快速了解数据源分布
  - 方便用户选择

### 架构优化
- **增量更新**：避免全量刷新
  - 连接建立/删除时立即更新上游节点池
  - 无需全量遍历连接
- **分层清晰**：Workflow 层维护连接关系，Plugin.SDK 层提供查询服务
- **解耦设计**：`DataSourceQueryService` 不直接依赖 `Workflow`，通过接口注入

## 📊 数据流图

```
┌─────────────────┐
│  Workflow 层   │
│                 │
│  WorkflowNode   │ ←── UpstreamNodesByOutputType (按输出类型分组)
│                 │
│  Workflow       │ ←── AddConnection/RemoveConnection (增量更新)
└────────┬────────┘
         │
         │ 连接事件
         ▼
┌─────────────────┐
│  Plugin.SDK 层  │
│                 │
│  DataSource     │ ←── GetAvailableDataSourcesGrouped()
│  QueryService   │     使用 GetParentNodes() 获取上游节点
│                 │     并按类型分类
│  TypeCategory    │ ←── 类型分类映射器
│  Mapper         │     提供6种分类
└─────────────────┘
```

## 📖 待完成工作

### 高优先级
1. **UI 集成**
   - 修改 `PropertyPanelViewModel` 使用分组数据源
   - 添加分组显示的 UI 控件（分组标签页）

2. **性能优化**
   - 使用 `WorkflowNode.UpstreamNodesByOutputType` 替代 `GetParentNodes()`
   - 需要在 `DataSourceQueryService` 中添加接口来访问节点属性

### 中优先级
1. **单元测试**
   - 测试 `TypeCategoryMapper` 的类型分类
   - 测试 `GroupedDataSources` 的添加/获取/清空
   - 测试 `WorkflowNode` 的上游节点池更新
   - 测试 `Workflow` 的连接增量更新

2. **性能测试**
   - 测试大规模工作流的性能表现

### 低优先级
1. **文档更新**
   - 更新 API 文档
   - 添加使用示例

2. **代码审查**
   - 代码质量检查
   - 架构合理性审查

## 🐛 已知问题

### XAML 编译错误（预先存在）
以下错误在本次实施前就存在，不是本次修改导致的：

1. **ToolDebugControlBase 不存在**
   - 文件：`tools/SunEyeVision.Tool.Threshold/Views/ThresholdToolDebugControl.xaml`
   - 错误：`MC3074` - XML 命名空间中不存在标记"ToolDebugControlBase"
   - 状态：待解决

2. **ImageControl 不存在**
   - 文件：`src/UI/Views/Windows/MainWindow.xaml`
   - 错误：`MC3074` - XML 命名空间中不存在标记"ImageControl"
   - 状态：待解决

## 🔧 技术细节

### TypeCategoryMapper
**预定义类型集合**：
- `ImageTypes`：Mat
- `ShapeTypes`：Point, Rect, Size, RotatedRect
- `NumericTypes`：int, long, short, byte, float, double, decimal, bool
- `TextTypes`：string, char

**自定义映射**：
- `CustomMappings`：字典，存储用户自定义的类型映射
- 支持运行时扩展

**列表类型检测**：
- 检查数组类型（`type.IsArray`）
- 检查泛型集合（`List<>`, `IList<>`, `ICollection<>`, `IEnumerable<>`）
- 检查非泛型集合接口（`IList`, `ICollection`, `IEnumerable`）

### WorkflowNode.UpstreamNodesByOutputType
**数据结构**：
```csharp
Dictionary<Type, List<string>> UpstreamNodesByOutputType
```

**维护方式**：
- 连接建立时：调用 `AddUpstreamNode()`
- 连接删除时：调用 `RemoveUpstreamNode()`
- 节点删除时：调用 `ClearUpstreamNodes()`

## 📚 参考资料

- [rule-002: 命名规范](../.codebuddy/rules/01-coding-standards/naming-conventions.mdc)
- [rule-003: 日志系统使用规范](../.codebuddy/rules/01-coding-standards/logging-system.mdc)
- [rule-010: 方案系统实现规范](../.codebuddy/rules/02-development-process/solution-system-implementation.mdc)

## 📝 变更历史

| 日期 | 版本 | 变更内容 | 作者 |
|------|------|----------|------|
| 2026-04-09 | 1.1 | 修正架构问题，删除 GlobalOutputRegistry，保留其他组件 | Team |
| 2026-04-09 | 1.0 | 初始版本，完成核心组件实施 | Team |
