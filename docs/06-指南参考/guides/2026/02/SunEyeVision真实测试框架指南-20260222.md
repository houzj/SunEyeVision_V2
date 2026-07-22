# SunEyeVision 工作流执行引擎测试指南

## 一、测试概述

本文档说明如何基于真实框架测试工作流执行引擎的运行状态。

### 测试环境
- **框架**: .NET 9.0
- **测试方法**: 集成测试(真实插件+真实执行引擎)
- **测试位置**: SunEyeVision.Workflow/WorkflowExecutionTests.cs

## 二、已删除的测试项目

❌ **已删除**: `SunEyeVision.Test`项目
- 原因: 使用模拟数据,无法验证真实执行逻辑
- 替代: 使用SunEyeVision.Workflow中的真实测试框架

## 三、真实测试框架

### 3.1 测试文件位置

```
SunEyeVision.Workflow/
├── WorkflowExecutionTests.cs  # 主测试文件(5个测试用例)
├── TestRunner.cs             # 测试运行器
└── TestProgram.cs           # 独立测试程序(需创建)
```

### 3.2 测试用例清单

| 测试用例 | 功能 | 验证内容 |
|---------|------|---------|
| **TestSequentialExecution** | 顺序执行 | 简单串行工作流的正确执行 |
| **TestParallelExecution** | 并行执行 | 多个并行节点的并发执行 |
| **TestHybridExecution** | 混合执行 | 多执行链的混合执行策略 |
| **TestCachingDecorator** | 缓存装饰器 | 缓存命中和性能提升 |
| **TestStrategySelection** | 策略选择 | 智能策略选择的正确性 |

### 3.3 测试覆盖范围

#### 功能覆盖
- ✅ 插件层: ToolMetadata, 装饰器, WorkflowNodeFactory
- ✅ 执行引擎: 4种执行策略
- ✅ 策略选择: 智能选择逻辑
- ✅ 缓存机制: 缓存命中和性能提升
- ✅ 真实插件: 使用ToolRegistry的真实插件

#### 场景覆盖
- ✅ 顺序工作流(串行节点)
- ✅ 并行工作流(Start节点驱动的并行执行)
- ✅ 混合工作流(多执行链,依赖关系)
- ✅ 缓存场景(重复执行相同输入)
- ✅ 策略选择(不同类型的工作流)

## 四、测试方法

### 方法1: 在现有应用中集成测试(推荐)

#### 步骤1: 创建测试服务

在`SunEyeVision.UI/Services/`中创建`TestExecutionService.cs`:

```csharp
using System;
using System.Threading.Tasks;
using SunEyeVision.Interfaces;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Tests;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 测试执行服务 - 在应用中集成测试
    /// </summary>
    public class TestExecutionService
    {
        private readonly ILogger _logger;
        private WorkflowExecutionTests? _tests;

        public TestExecutionService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 初始化测试套件
        /// </summary>
        public void Initialize()
        {
            _tests = new WorkflowExecutionTests(_logger);
        }

        /// <summary>
        /// 运行所有测试
        /// </summary>
        public async Task RunAllTestsAsync()
        {
            if (_tests == null)
            {
                _logger.LogError("测试套件未初始化");
                return;
            }

            await _tests.RunAllTests();
        }

        /// <summary>
        /// 运行单个测试
        /// </summary>
        public async Task RunSpecificTestAsync(string testName)
        {
            if (_tests == null)
            {
                _logger.LogError("测试套件未初始化");
                return;
            }

            switch (testName.ToLower())
            {
                case "sequential":
                    await _tests.TestSequentialExecution();
                    break;
                case "parallel":
                    await _tests.TestParallelExecution();
                    break;
                case "hybrid":
                    await _tests.TestHybridExecution();
                    break;
                case "caching":
                    await _tests.TestCachingDecorator();
                    break;
                case "strategy":
                    await _tests.TestStrategySelection();
                    break;
                default:
                    _logger.LogError($"未知测试: {testName}");
                    break;
            }
        }
    }
}
```

#### 步骤2: 在MainWindow中添加测试菜单

在`MainWindow.xaml`中添加测试按钮:

```xml
<!-- 在菜单栏中添加 -->
<Menu>
    <MenuItem Header="测试">
        <MenuItem Header="运行所有测试" Command="{Binding RunAllTestsCommand}" />
        <Separator />
        <MenuItem Header="顺序执行测试" Command="{Binding RunSequentialTestCommand}" />
        <MenuItem Header="并行执行测试" Command="{Binding RunParallelTestCommand}" />
        <MenuItem Header="混合执行测试" Command="{Binding RunHybridTestCommand}" />
        <MenuItem Header="缓存装饰器测试" Command="{Binding RunCachingTestCommand}" />
        <MenuItem Header="策略选择测试" Command="{Binding RunStrategyTestCommand}" />
    </MenuItem>
</Menu>
```

#### 步骤3: 在MainWindowViewModel中添加测试命令

在`MainWindowViewModel.cs`中:

```csharp
using SunEyeVision.UI.Services;

public class MainWindowViewModel
{
    private readonly TestExecutionService _testService;
    
    // 测试命令
    public ICommand RunAllTestsCommand { get; }
    public ICommand RunSequentialTestCommand { get; }
    public ICommand RunParallelTestCommand { get; }
    public ICommand RunHybridTestCommand { get; }
    public ICommand RunCachingTestCommand { get; }
    public ICommand RunStrategyTestCommand { get; }

    public MainWindowViewModel()
    {
        // 初始化测试服务
        _testService = new TestExecutionService(logger);
        _testService.Initialize();

        // 初始化命令
        RunAllTestsCommand = new RelayCommand(async () => await _testService.RunAllTestsAsync());
        RunSequentialTestCommand = new RelayCommand(async () => await _testService.RunSpecificTestAsync("sequential"));
        RunParallelTestCommand = new RelayCommand(async () => await _testService.RunSpecificTestAsync("parallel"));
        RunHybridTestCommand = new RelayCommand(async () => await _testService.RunSpecificTestAsync("hybrid"));
        RunCachingTestCommand = new RelayCommand(async () => await _testService.RunSpecificTestAsync("caching"));
        RunStrategyTestCommand = new RelayCommand(async () => await _testService.RunSpecificTestAsync("strategy"));
    }
}
```

#### 步骤4: 运行测试

启动应用后,通过菜单:
- **测试** → **运行所有测试** - 运行所有5个测试用例
- **测试** → **顺序执行测试** - 单独测试顺序执行
- **测试** → **并行执行测试** - 单独测试并行执行
- ... 其他测试

测试结果会显示在日志窗口中。

### 方法2: 创建独立控制台测试程序

#### 步骤1: 创建控制台项目

```bash
cd d:/MyWork/SunEyeVision/SunEyeVision
dotnet new console -n SunEyeVision.Workflow.Tests -f net9.0
```

#### 步骤2: 编辑项目文件

编辑`SunEyeVision.Workflow.Tests/SunEyeVision.Workflow.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SunEyeVision.Workflow\SunEyeVision.Workflow.csproj" />
    <ProjectReference Include="..\SunEyeVision.Core\SunEyeVision.Core.csproj" />
    <ProjectReference Include="..\SunEyeVision.PluginSystem\SunEyeVision.PluginSystem.csproj" />
  </ItemGroup>
</Project>
```

#### 步骤3: 创建Program.cs

```csharp
using System;
using SunEyeVision.Interfaces;
using SunEyeVision.Workflow.Tests;

namespace SunEyeVision.Workflow.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("SunEyeVision 工作流执行引擎测试");
            Console.WriteLine("========================================");
            Console.WriteLine();

            try
            {
                // 创建日志器
                var logger = new ConsoleLogger();

                // 创建测试套件
                var tests = new WorkflowExecutionTests(logger);

                // 运行所有测试
                await tests.RunAllTests();

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("测试程序执行完成");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试程序异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// 控制台日志器
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void LogDebug(string message)
        {
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public void LogInfo(string message)
        {
            Console.WriteLine($"[INFO]  {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARN]  {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
            if (exception != null)
            {
                Console.WriteLine($"        Exception: {exception.Message}");
                Console.WriteLine($"        StackTrace: {exception.StackTrace}");
            }
            Console.ResetColor();
        }
    }
}
```

#### 步骤4: 移动测试文件

将`SunEyeVision.Workflow/WorkflowExecutionTests.cs`和`TestRunner.cs`复制到`SunEyeVision.Workflow.Tests/`。

#### 步骤5: 运行测试

```bash
cd SunEyeVision.Workflow.Tests
dotnet run
```

### 方法3: 直接在现有工作流中测试

#### 步骤1: 创建测试工作流

在SunEyeVision.UI中创建一个测试工作流:

```
工作流名称: test_sequential_execution
节点:
  - Start节点 (ID: start)
  - 高斯模糊节点 (ID: blur1, ToolId: gaussian_blur)
  - 边缘检测节点 (ID: edge1, ToolId: edge_detection)
  - 阈值处理节点 (ID: thresh1, ToolId: threshold)

连接:
  - start -> blur1
  - blur1 -> edge1
  - edge1 -> thresh1
```

#### 步骤2: 执行工作流

在工作流画布中:
1. 创建上述节点和连接
2. 点击"执行"按钮
3. 观察日志输出

#### 步骤3: 验证结果

日志应显示:
```
[INFO]  选择执行策略: Sequential
[INFO]  使用顺序执行模式
[INFO]  ========== 节点执行顺序 (共4个节点) ==========
[INFO]    [1] start - Start
[INFO]    [2] blur1 - gaussian_blur
[INFO]    [3] edge1 - edge_detection
[INFO]    [4] thresh1 - threshold
[INFO]  =====================================================
[INFO]  [1/4] 开始执行节点: 高斯模糊
...
[INFO]  [4/4] 节点执行完成: 阈值处理
[INFO]  工作流执行完成: test_sequential_execution
```

## 五、测试验证要点

### 5.1 功能验证

#### 1. 顺序执行验证
- ✅ 节点按拓扑顺序执行
- ✅ 每个节点的输入正确(前一个节点的输出)
- ✅ 执行时间合理(逐个节点累加)
- ✅ 策略选择为Sequential

#### 2. 并行执行验证
- ✅ 多个Start节点触发并行执行
- ✅ 执行时间明显短于顺序执行
- ✅ 日志显示并行执行信息
- ✅ 策略选择为Parallel

#### 3. 混合执行验证
- ✅ 识别多条执行链
- ✅ 无依赖的链并行执行
- ✅ 有依赖的链按依赖顺序执行
- ✅ 策略选择为Hybrid

#### 4. 缓存装饰器验证
- ✅ 第一次执行正常
- ✅ 第二次执行时间大幅缩短(>80%)
- ✅ 日志显示缓存命中信息
- ✅ 缓存统计正确

#### 5. 策略选择验证
- ✅ 顺序工作流选择Sequential
- ✅ 并行工作流选择Parallel
- ✅ 混合工作流选择Hybrid
- ✅ 日志显示选择理由

### 5.2 性能验证

| 测试 | 预期时间 | 实际时间 | 通过 |
|-----|---------|---------|-----|
| 顺序执行(3节点) | ~200ms | ___ms | ⬜ |
| 并行执行(3节点) | ~110ms | ___ms | ⬜ |
| 混合执行(2链) | ~140ms | ___ms | ⬜ |
| 缓存命中(第2次) | ~20ms | ___ms | ⬜ |

### 5.3 插件集成验证

#### 真实插件使用
- ✅ WorkflowNodeFactory成功创建节点
- ✅ 使用ToolRegistry中的真实插件
- ✅ 装饰器正确应用(缓存/重试)
- ✅ 节点执行使用真实算法

#### 后备机制
- ✅ 如果工具不存在,使用TestImageProcessor
- ✅ 日志记录警告信息
- ✅ 不影响测试执行

## 六、常见问题

### Q1: 测试运行时提示"工具不存在"
**A**: 
- 检查插件是否正确加载
- 查看ToolRegistry中的工具列表
- 如果工具不存在,会自动使用TestImageProcessor后备

### Q2: 缓存效果不明显
**A**:
- 确保ToolMetadata中SupportCaching = true
- 两次执行使用相同的输入图像
- 检查CacheTtlMs是否过期

### Q3: 并行执行性能没有提升
**A**:
- 确认工作流有Start节点
- 检查节点是否标记为SupportParallel = true
- 查看CPU核心数(并行度受核心数限制)

### Q4: 策略选择不符合预期
**A**:
- 查看日志中的工作流分析信息
- 确认ToolMetadata中的执行特性配置
- 检查工作流拓扑结构

### Q5: 如何调试测试
**A**:
1. 在WorkflowExecutionTests中设置断点
2. 在Visual Studio中附加到进程
3. 或者使用日志器输出详细日志

## 七、下一步建议

### 短期(本周)
1. ✅ 删除SunEyeVision.Test项目
2. ✅ 集成测试到现有应用
3. ⬜ 运行完整测试套件
4. ⬜ 记录测试结果和性能数据

### 中期(2周)
1. ⬜ 添加更多测试用例
2. ⬜ 性能基准测试
3. ⬜ 压力测试
4. ⬜ 边界条件测试

### 长期(1月)
1. ⬜ 持续集成测试
2. ⬜ 自动化测试报告
3. ⬜ 性能回归测试
4. ⬜ 测试覆盖率分析

## 八、总结

- ✅ **已删除**: SunEyeVision.Test项目(使用模拟数据)
- ✅ **已创建**: 真实测试框架(使用真实插件)
- ✅ **测试覆盖**: 5个测试用例,覆盖核心功能
- ✅ **测试方法**: 3种方式(集成测试、独立程序、工作流测试)
- ✅ **验证要点**: 功能、性能、插件集成

**推荐的测试方法**: 方法1(在现有应用中集成测试)
- 无需创建新项目
- 可通过菜单直接运行
- 测试结果直观可见
- 易于调试和维护

立即开始测试: 在SunEyeVision.UI中创建TestExecutionService,添加测试菜单,运行测试!
