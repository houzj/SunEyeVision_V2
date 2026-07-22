# WorkflowExecutionEngine 使用指南

## 概述

`WorkflowExecutionEngine` 是扩展的工作流执行引擎，支持工作流控制节点（子程序节点、条件节点）的执行，并提供异步执行、暂停/恢复、状态追踪等高级功能。

## 主要特性

### 1. 支持多种节点类型
- **AlgorithmNode**: 算法节点，执行图像处理算法
- **SubroutineNode**: 子程序节点，支持循环和参数映射
- **ConditionNode**: 条件节点，支持条件判断和分支

### 2. 执行控制
- 同步执行: `ExecuteWorkflow()`
- 异步执行: `ExecuteWorkflowAsync()`
- 暂停/恢复: `PauseExecution()`, `ResumeExecution()`
- 停止执行: `StopExecution()`

### 3. 状态追踪
- 执行状态: `CurrentState`
- 当前节点: `CurrentNodeId`
- 执行上下文: `CurrentContext`
- 执行统计: `GetExecutionStatistics()`

### 4. 事件通知
- 进度变化: `ProgressChanged`
- 节点状态变化: `NodeStatusChanged`
- 执行完成: `ExecutionCompleted`

## 快速开始

### 1. 创建引擎套件

```csharp
using SunEyeVision.Workflow;
using SunEyeVision.Interfaces;
using SunEyeVision.PluginSystem;

// 创建Logger
ILogger logger = new ConsoleLogger();

// 创建完整的引擎套件
var (workflowEngine, executionEngine, pluginManager) = 
    WorkflowEngineFactory.CreateEngineSuite(logger);
```

### 2. 创建工作流并添加节点

```csharp
// 创建工作流
var workflow = workflowEngine.CreateWorkflow("workflow-001", "示例工作流");

// 添加算法节点
var imageProcessor = new YourImageProcessor();
var algorithmNode = new AlgorithmNode("node-001", "灰度转换", imageProcessor);
workflow.AddNode(algorithmNode);

// 添加子程序节点（如果已创建子工作流）
var subroutineNode = new SubroutineNode
{
    Id = "node-002",
    Name = "子程序调用",
    SubroutineId = "subroutine-workflow-001",
    SubroutineName = "图像预处理",
    IsLoop = true,
    LoopType = LoopType.FixedCount,
    MaxIterations = 5
};
workflow.AddNode(subroutineNode);

// 连接节点
workflow.ConnectNodes("node-001", "node-002");
```

### 3. 执行工作流

#### 同步执行

```csharp
// 加载输入图像
var inputImage = new Mat("input.jpg");

// 执行工作流
var result = executionEngine.ExecuteWorkflow("workflow-001", inputImage);

if (result.Success)
{
    Console.WriteLine($"执行成功，耗时: {result.ExecutionTime}ms");
    
    // 获取输出
    if (result.Outputs != null && result.Outputs.ContainsKey("FinalResult"))
    {
        var outputImage = result.Outputs["FinalResult"] as Mat;
        // 使用输出图像...
    }
}
else
{
    Console.WriteLine($"执行失败: {result.Errors.FirstOrDefault()?.Message}");
}
```

#### 异步执行

```csharp
// 订阅事件
executionEngine.NodeStatusChanged += (sender, status) =>
{
    Console.WriteLine($"节点 {status.NodeId} 状态: {status.Status}");
};

executionEngine.ExecutionCompleted += (sender, result) =>
{
    Console.WriteLine($"执行完成: {result.Success}");
};

// 异步执行
var result = await executionEngine.ExecuteWorkflowAsync("workflow-001", inputImage);
```

### 4. 执行控制

```csharp
// 暂停执行
executionEngine.PauseExecution();

// 恢复执行
executionEngine.ResumeExecution();

// 停止执行
executionEngine.StopExecution();
```

### 5. 状态查询

```csharp
// 查询当前状态
Console.WriteLine($"执行状态: {executionEngine.CurrentState}");
Console.WriteLine($"当前节点: {executionEngine.CurrentNodeId}");

// 获取执行统计
var stats = executionEngine.GetExecutionStatistics();
Console.WriteLine($"总节点数: {stats.TotalNodes}");
Console.WriteLine($"成功节点: {stats.SuccessNodes}");
Console.WriteLine($"失败节点: {stats.FailedNodes}");
Console.WriteLine($"总耗时: {stats.TotalExecutionTime}ms");
```

## 工作流控制节点使用

### 子程序节点

```csharp
// 创建子程序节点
var subroutineNode = new SubroutineNode
{
    Id = "sub-node-001",
    Name = "循环处理",
    SubroutineId = "preprocessing-workflow",
    SubroutineName = "图像预处理",
    
    // 循环设置
    IsLoop = true,
    LoopType = LoopType.FixedCount,
    MaxIterations = 10,
    
    // 参数映射
    InputMappings = new List<ParameterMapping>
    {
        new ParameterMapping
        {
            ExternalPortId = "input",
            InternalPortId = "preprocess-input",
            MappingName = "InputImage"
        }
    },
    
    // 并行执行
    EnableParallel = false
};

// 添加参数映射
subroutineNode.AddInputMapping("external-output", "internal-input", "MappedImage");
subroutineNode.AddOutputMapping("internal-output", "external-result", "Result");
```

### 条件节点

```csharp
// 创建条件节点
var conditionNode = new ConditionNode
{
    Id = "cond-node-001",
    Name = "质量检查",
    ConditionExpression = "QualityScore >= 0.8",
    ConditionType = ConditionType.Expression,
    
    // 输出值
    TrueValue = true,
    FalseValue = false
};

// 或者使用简单条件
var simpleConditionNode = new ConditionNode
{
    Id = "cond-node-002",
    Name = "尺寸检查",
    ConditionType = ConditionType.SimpleComparison,
    LeftOperand = "ImageWidth",
    Operator = ">",
    RightOperand = "1920"
};
```

## 完整示例

### 示例1: 基础工作流执行

```csharp
public class BasicWorkflowExample
{
    public void Run()
    {
        // 1. 创建引擎
        var logger = new ConsoleLogger();
        var (workflowEngine, executionEngine, _) = 
            WorkflowEngineFactory.CreateEngineSuite(logger);

        // 2. 创建工作流
        var workflow = workflowEngine.CreateWorkflow("basic-001", "基础示例");

        // 3. 添加算法节点
        var node1 = new AlgorithmNode("node-1", "灰度转换", new GrayscaleProcessor());
        var node2 = new AlgorithmNode("node-2", "高斯模糊", new GaussianBlurProcessor());
        var node3 = new AlgorithmNode("node-3", "边缘检测", new EdgeDetectionProcessor());

        workflow.AddNode(node1);
        workflow.AddNode(node2);
        workflow.AddNode(node3);

        // 4. 连接节点
        workflow.ConnectNodes("node-1", "node-2");
        workflow.ConnectNodes("node-2", "node-3");

        // 5. 执行工作流
        var inputImage = new Mat("input.jpg");
        var result = executionEngine.ExecuteWorkflow("basic-001", inputImage);

        // 6. 处理结果
        if (result.Success)
        {
            Console.WriteLine("执行成功!");
            var output = result.Outputs["FinalResult"] as Mat;
            output.Save("output.jpg");
        }
        else
        {
            Console.WriteLine($"执行失败: {result.Errors.FirstOrDefault()?.Message}");
        }
    }
}
```

### 示例2: 带子程序的工作流

```csharp
public class SubroutineWorkflowExample
{
    public async Task Run()
    {
        // 1. 创建引擎
        var logger = new ConsoleLogger();
        var (workflowEngine, executionEngine, _) = 
            WorkflowEngineFactory.CreateEngineSuite(logger);

        // 2. 创建子程序工作流
        var subWorkflow = workflowEngine.CreateWorkflow("sub-001", "图像预处理");
        
        var preprocessNode1 = new AlgorithmNode("sub-node-1", "去噪", new DenoiseProcessor());
        var preprocessNode2 = new AlgorithmNode("sub-node-2", "锐化", new SharpenProcessor());
        
        subWorkflow.AddNode(preprocessNode1);
        subWorkflow.AddNode(preprocessNode2);
        subWorkflow.ConnectNodes("sub-node-1", "sub-node-2");

        // 3. 创建主工作流
        var mainWorkflow = workflowEngine.CreateWorkflow("main-001", "主工作流");
        
        var mainNode1 = new AlgorithmNode("main-node-1", "加载图像", new ImageLoader());
        var subroutineNode = new SubroutineNode
        {
            Id = "main-node-2",
            Name = "预处理循环",
            SubroutineId = "sub-001",
            SubroutineName = "图像预处理",
            IsLoop = true,
            LoopType = LoopType.FixedCount,
            MaxIterations = 3
        };
        var mainNode3 = new AlgorithmNode("main-node-3", "特征提取", new FeatureExtractor());
        
        mainWorkflow.AddNode(mainNode1);
        mainWorkflow.AddNode(subroutineNode);
        mainWorkflow.AddNode(mainNode3);
        
        mainWorkflow.ConnectNodes("main-node-1", "main-node-2");
        mainWorkflow.ConnectNodes("main-node-2", "main-node-3");

        // 4. 订阅事件
        executionEngine.NodeStatusChanged += (s, e) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 节点 {e.NodeId}: {e.Status}");
        };

        // 5. 异步执行
        var inputImage = new Mat("input.jpg");
        var result = await executionEngine.ExecuteWorkflowAsync("main-001", inputImage);

        // 6. 处理结果
        if (result.Success)
        {
            Console.WriteLine($"执行成功，耗时: {result.ExecutionTime:F2}ms");
            
            // 查看统计信息
            var stats = executionEngine.GetExecutionStatistics();
            Console.WriteLine($"成功节点: {stats.SuccessNodes}/{stats.TotalNodes}");
        }
    }
}
```

### 示例3: 带条件分支的工作流

```csharp
public class ConditionalWorkflowExample
{
    public void Run()
    {
        // 1. 创建引擎
        var logger = new ConsoleLogger();
        var (workflowEngine, executionEngine, _) = 
            WorkflowEngineFactory.CreateEngineSuite(logger);

        // 2. 创建工作流
        var workflow = workflowEngine.CreateWorkflow("conditional-001", "条件分支示例");

        // 3. 添加节点
        var node1 = new AlgorithmNode("node-1", "质量评估", new QualityAssessor());
        
        var conditionNode = new ConditionNode
        {
            Id = "node-2",
            Name = "质量判断",
            ConditionType = ConditionType.Expression,
            ConditionExpression = "QualityScore >= 0.85"
        };

        var node3 = new AlgorithmNode("node-3", "高质量处理", new HighQualityProcessor());
        var node4 = new AlgorithmNode("node-4", "低质量处理", new LowQualityProcessor());
        var node5 = new AlgorithmNode("node-5", "后处理", new PostProcessor());

        workflow.AddNode(node1);
        workflow.AddNode(conditionNode);
        workflow.AddNode(node3);
        workflow.AddNode(node4);
        workflow.AddNode(node5);

        // 4. 连接节点
        workflow.ConnectNodes("node-1", "node-2");
        workflow.ConnectNodes("node-2", "node-3");
        workflow.ConnectNodes("node-2", "node-4");
        workflow.ConnectNodes("node-3", "node-5");
        workflow.ConnectNodes("node-4", "node-5");

        // 5. 执行工作流
        var inputImage = new Mat("input.jpg");
        var result = executionEngine.ExecuteWorkflow("conditional-001", inputImage);

        // 6. 处理结果
        if (result.Success)
        {
            Console.WriteLine("执行成功!");
            
            // 检查条件节点结果
            if (result.NodeResults != null && result.NodeResults.ContainsKey("node-2"))
            {
                var condResult = result.NodeResults["node-2"];
                if (condResult.Outputs != null && condResult.Outputs.ContainsKey("ConditionResult"))
                {
                    var conditionResult = (bool)condResult.Outputs["ConditionResult"];
                    Console.WriteLine($"条件判断结果: {conditionResult}");
                }
            }
        }
    }
}
```

## 高级功能

### 1. 自定义进度报告

```csharp
executionEngine.ProgressChanged += (sender, progress) =>
{
    Console.WriteLine($"进度: {progress.Progress:P0}");
    Console.WriteLine($"消息: {progress.Message}");
    Console.WriteLine($"当前节点: {progress.CurrentNodeId}");
};
```

### 2. 执行上下文访问

```csharp
// 在执行过程中访问上下文
var context = executionEngine.CurrentContext;

// 设置自定义变量
context.SetVariable("CustomValue", 123);

// 获取变量
if (context.HasVariable("CustomValue"))
{
    var value = context.GetVariable("CustomValue");
}

// 查看调用栈
var callInfo = context.GetCurrentCallInfo();
Console.WriteLine($"调用深度: {context.GetCurrentCallDepth()}");
```

### 3. 调试模式

```csharp
// 启用调试模式
executionEngine.CurrentContext.IsDebugMode = true;

// 查看详细日志
executionEngine.CurrentContext.EnableProfiling = true;

// 获取执行路径
var path = executionEngine.CurrentContext.ExecutionPath;
```

## 最佳实践

1. **错误处理**: 始终检查ExecutionResult的Success属性
2. **资源清理**: 使用using语句管理Mat资源
3. **异步优先**: 对于长时间运行的工作流，使用异步执行
4. **事件订阅**: 适时订阅和取消订阅事件，避免内存泄漏
5. **性能监控**: 使用GetExecutionStatistics监控执行性能
6. **循环优化**: 避免无限循环，设置合理的最大迭代次数

## 故障排除

### 问题1: 插件未加载
```
错误: 未找到工作流控制插件
```
解决: 确保使用WorkflowEngineFactory创建引擎，它会自动注册插件

### 问题2: 循环依赖
```
错误: 检测到循环依赖
```
解决: 检查节点连接关系，移除循环

### 问题3: 子程序不存在
```
错误: 子工作流不存在
```
解决: 确保子程序Id对应的子工作流已创建

## API参考

详见各类的XML文档注释:
- WorkflowExecutionEngine
- WorkflowEngineFactory
- SubroutineNode
- ConditionNode
- WorkflowContext
- ExecutionResult
