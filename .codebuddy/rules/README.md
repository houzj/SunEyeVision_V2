---
description: 
alwaysApply: true
enabled: true
updatedAt: 2026-06-29T05:31:40.849Z
provider: 
---

# 规则文件目录

```
.codebuddy/rules/
├── 01-coding-standards/
│   ├── property-notification.mdc     # rule-001 属性更改通知
│   ├── naming-conventions.mdc        # rule-002 命名规范
│   ├── logging-system.mdc            # rule-003 日志系统
│   ├── file-encoding.mdc             # rule-013 文件编码
│   ├── halcon-operator-usage.mdc     # rule-014 Halcon 算子调用
│   ├── no-silent-fallback.mdc        # rule-015 禁止兜底回退
│   ├── data-driven-first.mdc         # rule-016 数据驱动优先
│   ├── gitignore-file-reading.mdc    # rule-017 gitignore 屏蔽目录读取规范
│   ├── skill-priority.mdc            # rule-018 Skill 优先级与选择规范
│   └── version-single-source.mdc     # rule-022 版本号单一真源
├── 02-development-process/
│   ├── solution-design.mdc           # rule-004 方案设计
│   ├── prototype-design-clean-principle.mdc  # rule-008 开发与代码纯净
│   ├── solution-system-implementation.mdc    # rule-010 方案系统实现
│   ├── parameter-system-constraints.mdc      # rule-012 参数系统
│   ├── solution-plain-language.mdc           # rule-019 方案与回复说人话
│   ├── plan-task-classify.mdc                # rule-020 计划任务归类
│   └── ai-collaboration-boundaries.mdc       # rule-021 双 AI 协作写入边界
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
| 016 | 数据驱动优先 | 数据 > 事件 > 程序式；判断链三选一；例外场景需注释 |
| 017 | gitignore 屏蔽目录 | dev/libs/halcon/runtime 等被屏蔽目录必须用 PowerShell 读取 |
| 018 | Skill 优先级 | project > user > plugin；use_skill 前必须扫描 project 级 |
| 019 | 说人话 | 方案与回复用通俗表达，禁止术语堆砌 |
| 020 | 计划任务归类 | 按语义归类提交，不依赖 hook 机械匹配 |
| 021 | 双 AI 协作边界 | 同一目录单写者；禁区（执行链/SDK/主题/插件加载/发布）停止并转交 Codex；任务卡必读必更；代执行需明示 |
| 022 | 版本号单一真源 | 版本只写 `version.json`；禁止硬编码版本号；发布必须走 `Pack-Release.ps1`；方案格式版本与产品版本解耦；界面读 `AppVersionInfo` |