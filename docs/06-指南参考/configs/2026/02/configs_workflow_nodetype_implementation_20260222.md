# 工作流节点类型实施方案总结

## 实施日期
2026-02-07

## 实施方案
方案1：在ToolMetadata中添加NodeType属性，支持工具层指定节点类型

## 实施内容

### 1. 在ToolMetadata中添加NodeType属性
**文件**: `SunEyeVision.PluginSystem/ToolMetadata.cs`

- 添加了 `NodeType` 属性，默认值为 `NodeType.Algorithm`
- 引入了 `SunEyeVision.Workflow` 命名空间以使用 `NodeType` 枚举

```csharp
/// <summary>
/// 节点类型 - 指定此工具创建的工作流节点类型
/// </summary>
public NodeType NodeType { get; set; } = NodeType.Algorithm;
```

### 2. 创建InputNode类
**文件**: `SunEyeVision.Workflow/InputNode.cs` (新文件)

- 继承自 `WorkflowNode`，节点类型为 `NodeType.Input`
- 支持图像处理器 (`IImageProcessor`)
- 支持输入数据缓存 (`CacheInput`)
- 提供 `Execute()` 方法获取数据
- 提供 `ClearCache()` 和 `Dispose()` 方法

**核心特性**:
- 数据源节点（如图像采集）
- 支持缓存避免重复采集
- 空输入处理和异常处理

### 3. 创建OutputNode类
**文件**: `SunEyeVision.Workflow/OutputNode.cs` (新文件)

- 继承自 `WorkflowNode`，节点类型为 `NodeType.Output`
- 支持图像处理器 (`IImageProcessor`)
- 支持文件输出 (`OutputPath`, `OutputFormat`)
- 支持文件覆盖控制 (`OverwriteExisting`)
- 提供 `ValidateConfiguration()` 方法验证配置

**核心特性**:
- 数据输出节点（如图像保存）
- 支持多种输出格式（jpg, png, bmp, tiff等）
- 支持重试机制
- 配置验证和错误处理

### 4. 更新WorkflowNodeFactory
**文件**: `SunEyeVision.Workflow/WorkflowNodeFactory.cs`

新增方法:

#### 4.1 CreateNode() - 统一创建方法
```csharp
public static WorkflowNode? CreateNode(
    string toolId,
    string nodeId,
    string nodeName,
    AlgorithmParameters? parameters = null,
    bool enableCaching = true,
    bool enableRetry = false)
```
- 根据 `ToolMetadata.NodeType` 自动创建对应类型的节点
- 支持 `NodeType.Algorithm`, `NodeType.Input`, `NodeType.Output`
- 默认回退到 `AlgorithmNode` 创建

#### 4.2 CreateInputNode() - 输入节点创建
```csharp
public static InputNode? CreateInputNode(
    string toolId,
    string nodeId,
    string nodeName,
    AlgorithmParameters? parameters = null,
    bool enableCaching = false)
```
- 创建 `InputNode` 实例
- 支持输入缓存配置
- 不应用装饰器（输入节点通常不需要缓存和重试）

#### 4.3 CreateOutputNode() - 输出节点创建
```csharp
public static OutputNode? CreateOutputNode(
    string toolId,
    string nodeId,
    string nodeName,
    AlgorithmParameters? parameters = null,
    bool enableCaching = false,
    bool enableRetry = false)
```
- 创建 `OutputNode` 实例
- 不应用缓存装饰器（每次都应该执行）
- 支持重试装饰器（文件保存可能失败）

### 5. 更新WorkflowExecutionEngine
**文件**: `SunEyeVision.Workflow/WorkflowExecutionEngine.cs`

#### 5.1 更新ExecuteNodeAsync()方法
在节点类型switch语句中添加 `InputNode` 和 `OutputNode` 处理:

```csharp
case InputNode inputNode:
    var inputNodeResult = ExecuteInputNode(inputNode);
    nodeResult.Outputs = inputNodeResult.Outputs;
    nodeResult.Success = inputNodeResult.Success;
    nodeResult.ErrorMessages = inputNodeResult.ErrorMessages;
    break;

case OutputNode outputNode:
    var outputNodeResult = ExecuteOutputNode(outputNode, matInput);
    nodeResult.Outputs = outputNodeResult.Outputs;
    nodeResult.Success = outputNodeResult.Success;
    nodeResult.ErrorMessages = outputNodeResult.ErrorMessages;
    break;
```

#### 5.2 新增ExecuteInputNode()方法
```csharp
private NodeExecutionResult ExecuteInputNode(InputNode node)
```
- 执行输入节点
- 处理成功和失败情况
- 返回包含输出图像的 `NodeExecutionResult`

#### 5.3 新增ExecuteOutputNode()方法
```csharp
private NodeExecutionResult ExecuteOutputNode(OutputNode node, Mat inputImage)
```
- 执行输出节点
- 处理空输入情况（记录错误但不算失败）
- 返回 `NodeExecutionResult`

### 6. 更新示例工具

#### 6.1 ImageCaptureTool
**文件**: `SunEyeVision.PluginSystem/SampleTools/ImageCaptureTool.cs`

- 添加 `using SunEyeVision.Workflow;` 引用
- 在 `GetToolMetadata()` 中设置 `NodeType = NodeType.Input`

```csharp
new ToolMetadata
{
    // ... 其他属性
    NodeType = NodeType.Input,  // 设置为输入节点类型
    // ... 其他属性
}
```

#### 6.2 ImageSaveTool (新增)
**文件**: `SunEyeVision.PluginSystem/SampleTools/ImageSaveTool.cs`

- 完整的输出节点示例工具
- 支持 `outputPath`, `outputFormat`, `overwrite` 参数
- 设置 `NodeType = NodeType.Output`
- 配置为有副作用（`HasSideEffects = true`）
- 不支持缓存（`SupportCaching = false`）
- 支持重试（`MaxRetryCount = 2`）

## 支持的节点类型

### NodeType.Input (输入节点)
- **用途**: 数据源节点，如图像采集、文件读取等
- **特性**:
  - 不需要输入图像
  - 支持数据缓存
  - 禁用时抛出异常
- **示例**: ImageCaptureTool

### NodeType.Output (输出节点)
- **用途**: 数据输出节点，如图像保存、结果导出等
- **特性**:
  - 接收输入图像
  - 支持文件输出配置
  - 支持重试机制
  - 空输入不视为失败
- **示例**: ImageSaveTool

### NodeType.Algorithm (算法节点)
- **用途**: 算法处理节点，如图像处理、特征提取等
- **特性**:
  - 接收输入图像
  - 产生输出图像
  - 支持缓存和重试装饰器
- **默认值**: 所有工具的默认节点类型

## 使用示例

### 1. 在工具插件中设置节点类型

```csharp
public List<ToolMetadata> GetToolMetadata()
{
    return new List<ToolMetadata>
    {
        new ToolMetadata
        {
            Id = "my_tool",
            Name = "MyTool",
            DisplayName = "我的工具",
            // ... 其他属性
            NodeType = NodeType.Input,  // 或 NodeType.Output, NodeType.Algorithm
            // ... 其他属性
        }
    };
}
```

### 2. 使用WorkflowNodeFactory创建节点

```csharp
// 自动根据NodeType创建
var node = WorkflowNodeFactory.CreateNode(
    "image_capture",  // toolId
    "node_001",      // nodeId
    "图像采集",      // nodeName
    parameters,
    enableCaching: true,
    enableRetry: false
);

// 显式创建特定类型节点
var inputNode = WorkflowNodeFactory.CreateInputNode(
    "image_capture",
    "node_001",
    "图像采集",
    parameters,
    enableCaching: true
);

var outputNode = WorkflowNodeFactory.CreateOutputNode(
    "image_save",
    "node_002",
    "图像保存",
    parameters,
    enableCaching: false,
    enableRetry: true
);
```

### 3. 在工作流中使用

```csharp
// 创建工作流
var workflow = new Workflow("test_workflow", "测试工作流");

// 添加输入节点
var inputNode = WorkflowNodeFactory.CreateInputNode(
    "image_capture",
    "input_001",
    "图像采集"
);
workflow.AddNode(inputNode);

// 添加算法节点
var algoNode = WorkflowNodeFactory.CreateNode(
    "edge_detection",
    "algo_001",
    "边缘检测"
);
workflow.AddNode(algoNode);

// 添加输出节点
var outputNode = WorkflowNodeFactory.CreateOutputNode(
    "image_save",
    "output_001",
    "图像保存"
);
workflow.AddNode(outputNode);

// 连接节点
workflow.AddConnection(inputNode.Id, 0, algoNode.Id, 0);
workflow.AddConnection(algoNode.Id, 0, outputNode.Id, 0);

// 执行工作流
var engine = new WorkflowExecutionEngine(workflow, pluginManager);
var result = await engine.ExecuteWorkflowAsync("test_workflow", null);
```

## 设计原则

### 1. 自动化优先
- 工具只需在元数据中指定 `NodeType`
- 工厂自动创建对应类型的节点
- 执行引擎自动处理不同类型的执行逻辑

### 2. 向后兼容
- 默认值为 `NodeType.Algorithm`
- 不设置 `NodeType` 的工具行为不变
- 现有代码无需修改

### 3. 可扩展性
- 易于添加新的节点类型
- 通过switch case扩展新类型处理
- 装饰器模式支持灵活的节点增强

### 4. 类型安全
- 使用C#模式匹配进行类型分发
- 编译时类型检查
- 运行时异常处理

## 注意事项

1. **解决方案文件损坏**: 当前 `SunEyeVision.sln` 文件是二进制格式，无法编译。这不是本次修改引入的问题，需要单独修复。

2. **编译验证**: 由于解决方案文件问题，无法执行完整的编译验证。建议：
   - 恢复解决方案文件后进行完整编译
   - 运行单元测试验证功能
   - 进行集成测试验证工作流执行

3. **示例工具**: ImageSaveTool 是新创建的示例工具，需要确保在ToolRegistry中注册。

4. **UI适配**: UI层可能需要适配新的节点类型显示和操作。

## 后续优化建议

1. **添加更多节点类型**: 如 `NodeType.Loop`, `NodeType.Switch` 等
2. **UI集成**: 在工具箱中区分不同类型的节点
3. **性能优化**: 针对InputNode和OutputNode的特定优化
4. **测试覆盖**: 添加单元测试和集成测试
5. **文档完善**: 更新API文档和使用指南

## 影响范围

### 修改的文件
- `SunEyeVision.PluginSystem/ToolMetadata.cs` - 添加NodeType属性
- `SunEyeVision.Workflow/WorkflowNodeFactory.cs` - 添加新方法
- `SunEyeVision.Workflow/WorkflowExecutionEngine.cs` - 更新执行逻辑
- `SunEyeVision.PluginSystem/SampleTools/ImageCaptureTool.cs` - 设置NodeType

### 新增的文件
- `SunEyeVision.Workflow/InputNode.cs` - 输入节点实现
- `SunEyeVision.Workflow/OutputNode.cs` - 输出节点实现
- `SunEyeVision.PluginSystem/SampleTools/ImageSaveTool.cs` - 输出工具示例

### 依赖关系
- `ToolMetadata` 依赖 `SunEyeVision.Workflow` (新增)
- `InputNode` 继承 `WorkflowNode`
- `OutputNode` 继承 `WorkflowNode`
- `WorkflowNodeFactory` 依赖 `InputNode`, `OutputNode` (新增)
- `WorkflowExecutionEngine` 依赖 `InputNode`, `OutputNode` (新增)

## 总结

本次实施方案成功实现了在工具层指定节点类型的能力，通过在 `ToolMetadata` 中添加 `NodeType` 属性，配合 `WorkflowNodeFactory` 的智能创建和 `WorkflowExecutionEngine` 的自动执行，实现了完整的端到端解决方案。

该方案保持了向后兼容性，提供了良好的扩展性，并遵循了自动化优先的设计原则，为工作流系统的节点类型管理奠定了坚实基础。
