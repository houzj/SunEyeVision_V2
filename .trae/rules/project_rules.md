# SunEyeVision 项目规则

## 核心约束（始终生效）

### rule-001: 属性更改通知统一规范 [Critical]

- **必须**继承 `ObservableObject` 或其派生类（Plugin.SDK 层直接继承，UI 层继承 `ViewModelBase`）
- **必须**使用 `SetProperty(ref _field, value)` 设置属性
- **禁止**直接实现 `INotifyPropertyChanged`
- **禁止**重复实现 SetProperty 方法（ObservableObject 已提供）
- 用户可见属性带 displayName：`SetProperty(ref _threshold, value, "阈值")`
- 内部属性不带 displayName：`SetProperty(ref _isVisible, value)`
- 例外：仅当需要属性变更批处理或扩展事件时，可直接实现 INotifyPropertyChanged，但**必须**添加注释说明原因

### rule-002: 命名规范 [High]

| 类型 | 格式 | 正确 | 错误 |
|------|------|------|------|
| 类名 | PascalCase 名词 | `WorkflowEngine` | `ProcessWorkflow`, `ImgProc` |
| 接口 | I+PascalCase | `IPluginManager` | `PluginManager` |
| 方法 | PascalCase 动词 | `ProcessImage()` | `processImage()`, `SaveCfg()` |
| 属性 | PascalCase | `UserName`, `IsEnabled` | `userName`, `enabled` |
| 变量 | camelCase | `imageWidth` | `_img_w` |
| 私有字段 | _camelCase | `_threshold` | `threshold`, `m_threshold` |
| 常量 | UPPER_CASE | `MAX_RETRY_COUNT` | `MaxRetryCount` |
| 布尔值 | Is/Has/Can 前缀 | `IsEnabled`, `HasChildren` | `Enabled` |
| 事件 | 过去式 | `Loaded`, `Saved` | `Load`, `Save` |
| 枚举 | PascalCase 单数 | `LogLevel` | `LogLevels`, `NStatus` |
| 命名空间 | PascalCase 点分隔 | `SunEyeVision.UI.ViewModels` | `UI_ViewModels` |

- **禁止**使用缩写（`ImgProc` → `ImageProcessor`），广泛认知的除外（ID, UI, API）

### rule-003: 日志系统使用规范 [High]

**正确用法：**
- ViewModel 层：`LogInfo()`, `LogSuccess()`, `LogWarning()`, `LogError()`, `LogFatal()`
- Service 层：`_logger.Log(LogLevel.Info, "消息", "来源")`

**禁止用法：**
- `System.Diagnostics.Debug.WriteLine()`
- `System.Diagnostics.Trace.WriteLine()`
- `Console.WriteLine()`
- 直接操作 UI 日志显示（如 `AddLogToUI()``

**日志级别：** Info：一般信息 | Success：成功操作 | Warning：警告 | Error：错误 | Fatal：严重错误

**日志信息必须包含足够上下文**：`LogInfo($"开始处理: {imagePath}, 尺寸: {width}x{height}")`，而非 `LogInfo("处理图像")`

### rule-004: 方案设计要求 [High]

- **必须**分析完整上下文（涉及模块、依赖关系、影响范围），**禁止**点对点修复
- **必须**复用现有基础设施（日志系统、事件总线、ObservableObject 等），**禁止**重复造轮子
- **必须**遵循 MVVM 分层架构，UI 层不包含业务逻辑
- **必须**识别技术风险并提供缓解措施
- **必须**提供清晰的实施步骤和验证清单

**方案设计原则：**
1. 全局视角：从整体架构考虑，不只解决当前问题
2. 复用优先：使用项目现有框架和模式
3. 风险控制：识别风险、提供缓解措施和回退方案
4. 上下文发现：动态搜索和分析现有代码，不硬编码假设

### rule-008: 开发与代码纯净原则 [Critical]

**三大核心原则：**

1. **YAGNI** - 只实现当前明确需要的功能，不提前实现未确认需求
2. **KISS** - 选择最简单、最优、最合理的实现方式，参考标准视觉软件设计模式
3. **按需设计** - 基于真实需求设计，在问题出现时再寻找解决方案

**原型期代码纯净：**
- **不考虑向后兼容**，直接删除旧代码
- **禁止**使用 `[Obsolete]` 标记、条件编译（`#if DEBUG`）、注释掉的代码
- **禁止**保留无用的类、方法、注释
- Git 已提供版本历史，无需在代码中保留旧实现

### rule-010: 方案系统实现规范 [Critical]

**序列化：**
- **必须**使用 `System.Text.Json`，**禁止** Newtonsoft.Json
- **必须**使用 `[JsonPolymorphic]` + `[JsonDerivedType]` 支持多态序列化
- **必须**使用 PascalCase 命名（符合视觉软件行业标准，与 C# 属性名一致）
- JSON 必须包含类型信息（`$type` 字段）

**参数存储：**
- Recipe 层**必须**直接存储 `ToolParameters` 实例：`Dictionary<string, ToolParameters>`
- **禁止**在业务逻辑中直接调用 `ToSerializableDictionary`/`LoadFromDictionary`
- Dictionary 转换层方法**必须**为 `internal`/`private` 访问级别

**允许 Dictionary 转换层的特殊场景：**
- 自定义 JsonConverter 内部使用（如 WorkflowJsonConverter）
- 处理特殊类型（OpenCvSharp.Point/Rect/Size 等）

**JSON 配置：** WriteIndented=true, PropertyNamingPolicy=null(PascalCase), DefaultIgnoreCondition=WhenWritingNull, Encoder=UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive=true, IncludeFields=true

### rule-012: 参数系统约束条件 [High]

**约束1: UI 绑定机制**
- UI 层**必须**保持 `Dictionary<string, object>` 存储参数（便于绑定属性面板）
- UI 层**必须**添加 `ParametersTypeName` 属性（用于强类型恢复）
- 参数转换方法**必须**支持根据 ParametersTypeName 创建强类型参数
- 转换失败时**必须**回退到 GenericToolParameters

**约束2: 工具注册机制**
- **必须**保持 ToolRegistry 注册机制不变
- 参数类型提取**必须**使用类型比较：`iface.GetGenericTypeDefinition() == typeof(IToolPlugin<,>)`
- **禁止**使用字符串匹配：`Name.StartsWith("IToolPlugin")`

**约束3: 参数绑定系统**
- ParameterBindingContainer 无需修改，与参数存储方式无关

### rule-013: 文件编码与写入规范 [Critical]

1. **首选** `replace_in_file` 工具（精确匹配上下文，原子操作）
2. **备选** PowerShell + UTF-8 无 BOM：`[System.IO.File]::WriteAllText("路径", $content, [System.Text.UTF8Encoding]::new($false))`
3. **禁止**使用 `write_to_file` 写入包含中文的内容（可能乱码）
4. **禁止**使用 `echo` 命令输出中文

## 手动应用规则

### rule-005: 实施方案审批流程 [High]

| 风险 + 范围 + 紧急度 | 审批方式 |
|---|---|
| 低风险 + 小范围 + 不紧急 | 快速批准（自动） |
| 中风险 + 中范围 + 正常 | 标准审批（人工确认） |
| 高风险 + 大范围 + 不紧急 | 严格审批（多轮评审） |
| 任意 + 关键功能 + 紧急 | 紧急审批（快速通道） |

方案实施请求必须包含：方案概述、影响范围、风险评估、实施步骤、测试计划、回退方案

**禁止：**
- 禁止不请求确认直接实施
- 禁止方案不完整就请求审批

### rule-006: 文档管理政策 [Medium]

- 需要保存文档时**必须**确认后才能保存到本地
- **禁止**随意生成本地 md 文档
- 所有文档必须有明确的用途和目标受众
- 文档命名规范：小写字母+下划线，使用前缀（tutorial_、guide_、reference_、faq_、migration_）

## 方案设计模板

```markdown
## 问题分析
- 用户意图：[理解用户的真实需求]
- 涉及的代码：[文件和方法]
- 相关规则检查：[列出相关 rule-XXX]

## 解决方案
### 方案概述
[简要描述]

### 技术方案
1. [步骤1]：描述 + 遵循的规则
2. [步骤2]：描述 + 遵循的规则

### 实施步骤
1. [步骤1]
2. [步骤2]

### 验证清单
- [ ] 遵循 rule-XXX: [规则名]
- [ ] 遵循 rule-XXX: [规则名]

### 风险控制
- 风险：[描述] → 缓解：[措施]
```

## 优先级体系

| 优先级 | 说明 | 违规后果 |
|--------|------|---------|
| Critical | 关键规则，违反会导致代码审查不通过 | 必须修复 |
| High | 重要规则，有充分理由才能违反 | 需书面解释，技术负责人批准 |
| Medium | 一般性指导原则 | 记录为建议改进项 |
| Low | 最佳实践参考 | 仅作为参考 |

## 架构锚点

| 层 | 关键入口 | 路径 | 说明 |
|---|---|---|---|
| SDK 核心 | `IToolPlugin` | `src/Plugin.SDK/Core/IToolPlugin.cs` | 工具接口，修改影响所有工具 |
| SDK 核心 | `ToolParameters` | `src/Plugin.SDK/Execution/Parameters/ToolParameters.cs` | 参数基类，修改影响序列化 |
| SDK 核心 | `ToolResults` | `src/Plugin.SDK/Execution/Results/ToolResults.cs` | 结果基类，修改影响序列化 |
| SDK 模型 | `ObservableObject` | `src/Plugin.SDK/Models/ObservableObject.cs` | 属性通知基类，修改影响全局 |
| SDK 控件 | `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 调试控件基类，修改影响所有工具 UI |
| SDK 控件 | `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | 图像控件，修改影响画布和调试窗口 |
| 工作流 | `WorkflowNodeBase` | `src/Workflow/WorkflowNode.cs` | 节点模型，修改影响序列化和画布 |
| UI 层 | `ViewModelBase` | `src/UI/ViewModels/ViewModelBase.cs` | VM 基类，修改影响所有 ViewModel |
