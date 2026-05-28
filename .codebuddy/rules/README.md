# 规则文件目录

```
.codebuddy/rules/
├── 01-coding-standards/
│   ├── property-notification.mdc     # rule-001 属性更改通知
│   ├── naming-conventions.mdc        # rule-002 命名规范
│   ├── logging-system.mdc            # rule-003 日志系统
│   ├── file-encoding.mdc             # rule-013 文件编码
│   ├── halcon-operator-usage.mdc     # rule-014 Halcon 算子调用
│   └── no-silent-fallback.mdc        # rule-015 禁止兜底回退
├── 02-development-process/
│   ├── solution-design.mdc           # rule-004 方案设计
│   ├── prototype-design-clean-principle.mdc  # rule-008 开发与代码纯净
│   ├── solution-system-implementation.mdc    # rule-010 方案系统实现
│   └── parameter-system-constraints.mdc      # rule-012 参数系统
├── 04-workflow-guidance/
│   ├── implementation-approval.mdc   # rule-005 实施审批
│   └── documentation-policy.mdc      # rule-006 文档管理
└── templates/
    ├── solution-design-template.md   # 方案设计模板
    └── rules-checklist.md            # 规则检查清单
```

## 核心约束速查

| ID | 规则 | 核心约束 |
|----|------|---------|
| 001 | 属性通知 | 继承 ObservableObject，用 SetProperty；禁止 INotifyPropertyChanged |
| 002 | 命名规范 | PascalCase 类名，_camelCase 字段，UPPER_CASE 常量，Is/Has/Can 布尔 |
| 003 | 日志系统 | ViewModel 用 LogInfo/LogError；禁止 Debug.WriteLine/Console.WriteLine |
| 004 | 方案设计 | 分析完整上下文，复用基础设施，禁止点对点修复 |
| 005 | 实施审批 | 方案须包含概述、影响、风险、步骤、回退方案 |
| 006 | 文档管理 | 确认后保存；禁止随意生成 md |
| 008 | 开发纯净 | YAGNI/KISS/按需设计；禁止 [Obsolete]/条件编译/注释代码 |
| 010 | 方案系统 | System.Text.Json+[JsonPolymorphic]；PascalCase；禁止 Newtonsoft.Json |
| 012 | 参数系统 | UI 层 Dictionary+ParametersTypeName；保持工具注册机制 |
| 013 | 文件编码 | 优先 replace_in_file；中文写入用 PowerShell+UTF8 无 BOM |
| 014 | Halcon 算子 | HOperatorSet 静态方法；禁止实例方法 |
| 015 | 禁止兜底回退 | 未映射/不支持的类型直接报错，禁止静默处理 |
