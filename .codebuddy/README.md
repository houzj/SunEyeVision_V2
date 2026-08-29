# SunEyeVision CodeBuddy 配置

## 规则系统

详见 [rules/README.md](rules/README.md)

### 核心约束（alwaysApply）

- rule-001: 必须继承 ObservableObject，使用 SetProperty
- rule-002: PascalCase 命名，禁止缩写
- rule-003: 使用 LogInfo/LogError，禁止 Debug.WriteLine
- rule-004: 方案须分析完整上下文，禁止点对点修复
- rule-008: 原型期直接删旧代码，禁止 [Obsolete]
- rule-010: 用 System.Text.Json+[JsonPolymorphic]，禁止 Newtonsoft.Json
- rule-012: UI 层保持 Dictionary+ParametersTypeName
- rule-013: 优先 replace_in_file，中文用 PowerShell+UTF8

### 手动应用规则

- rule-005: 实施方案审批流程
- rule-006: 文档管理政策

## 技能系统

| 技能 | 用途 | 触发场景 |
|------|------|---------|
| 遗留代码清理 | 遗留代码清理 | Recipe→Solution迁移残留、删除孤立类、重命名过时概念 |
| 视觉工具构建 | 视觉工具构建 | 创建新工具、工具三件套(Tool+Parameters+Results)、调试控件 |
| WPF视觉开发 | WPF 视觉开发 | 画布编辑器、节点渲染、连接绘制、ROI编辑、图像预览、参数绑定 |

## 文件结构

```
.codebuddy/
├── README.md           # 本文件
├── config/             # 配置文件
├── rules/              # 规则文件
│   ├── 01-coding-standards/
│   ├── 02-development-process/
│   ├── 04-workflow-guidance/
│   └── templates/      # 方案设计模板
├── skills/             # 技能文件
└── plans/              # 方案记录
```
