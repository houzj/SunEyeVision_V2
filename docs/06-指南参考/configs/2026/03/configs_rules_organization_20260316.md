# 项目规则组织结构说明

## 📁 规则文件组织

SunEyeVision 项目的规则系统已经整理到统一路径下，采用清晰的结构：

```
SunEyeVision/
├── .codebuddy/
│   └── rules/                              ← 规则文件主目录
│       ├── README.md                        ← 规则索引文件
│       ├── 日志系统使用规范.mdc            ← 规则1: 日志系统规范
│       └── PropertyNotificationStandard.mdc  ← 规则2: 属性更改通知规范
├── docs/
│   └── CODING_STANDARDS.md                 ← 开发规范总览
│   ├── property_notification_migration_summary.md  ← 迁移文档
│   ├── ROIEditor_Migration_Guide.md        ← ROI编辑器迁移指南
│   ├── 框架设计文档/                       ← 架构设计文档
│   └── 开发过程方案文档/                   ← 开发流程文档
```

## 🎯 统一路径的优势

### 1. 集中管理
所有规则文件统一存放在 `.codebuddy/rules/` 目录下：
- ✅ 易于查找和维护
- ✅ 方便批量操作
- ✅ 避免规则文件散落各处

### 2. 清晰的索引
创建 `README.md` 索引文件：
- ✅ 列出所有规则
- ✅ 提供快速访问链接
- ✅ 显示规则状态和统计

### 3. 文档集成
在 `docs/` 目录下创建 `CODING_STANDARDS.md`：
- ✅ 规则系统的入口文档
- ✅ 按角色分类的快速访问
- ✅ 规则执行统计
- ✅ 新手入门指南

## 📋 规则文件命名规范

### 命名规则
- **英文规则**: `RuleName.mdc` (PascalCase)
- **中文规则**: `规则名称.mdc`

### 示例
- ✅ `PropertyNotificationStandard.mdc` (推荐英文)
- ✅ `日志系统使用规范.mdc` (推荐中文)
- ❌ `property-notification-standard.mdc` (不符合规范)
- ❌ `LOG_STANDARD.MDC` (不符合规范)

## 🔗 文档关系图

```
开发规范总览 (CODING_STANDARDS.md)
    │
    ├── 规则系统入口 (.codebuddy/rules/README.md)
    │       │
    │       ├── 规则1: 日志系统使用规范
    │       └── 规则2: 属性更改通知统一规范
    │
    ├── 迁移文档
    │   ├── 属性更改通知迁移总结
    │   └── ROI编辑器迁移指南
    │
    ├── 架构设计文档
    └── 开发过程方案文档
```

## 📊 当前规则统计

| 规则名称 | 文件 | 类型 | 状态 | 创建日期 |
|---------|------|------|------|----------|
| 日志系统使用规范 | `日志系统使用规范.mdc` | Always | ✅ 启用 | 2026-03-08 |
| 属性更改通知统一规范 | `PropertyNotificationStandard.mdc` | Always | ✅ 启用 | 2026-03-09 |
| **总计** | **2** | **2** | **100%** | - |

## 🔄 规则生命周期管理

### 创建流程
1. 识别需要规范的重复问题
2. 起草规则内容（包含正确/错误示例）
3. 使用 `create_rule` 工具创建规则文件
4. 更新 `.codebuddy/rules/README.md` 索引
5. 更新 `docs/CODING_STANDARDS.md` 总览
6. 通知团队成员

### 更新流程
1. 审阅现有规则
2. 收集改进建议
3. 使用 `update_rule` 工具更新内容
4. 同步更新索引文件
5. 通知团队成员变更

### 废弃流程
1. 确认规则不再适用
2. 在规则文件中设置 `enabled: false`
3. 添加废弃原因和替代方案
4. 更新索引文件标记为已废弃
5. 通知团队成员

## 🚀 快速导航

### 新手入口
1. 📖 阅读 [开发规范总览](./CODING_STANDARDS.md)
2. 📋 查看 [规则索引](../.codebuddy/rules/README.md)
3. 🔍 学习具体规则
4. 🚀 开始开发

### 开发者入口
1. 📋 [规则索引](../.codebuddy/rules/README.md) - 快速查找规则
2. 📖 [开发规范总览](./CODING_STANDARDS.md) - 全面了解规范
3. 📚 [迁移文档](./property_notification_migration_summary.md) - 历史参考

### 维护者入口
1. 📊 规则执行统计
2. 📝 规则生命周期管理
3. 🤝 贡献指南

## ✅ 整理成果

### 规则文件
- ✅ 统一存储在 `.codebuddy/rules/`
- ✅ 命名规范统一
- ✅ 状态清晰可见

### 文档结构
- ✅ 创建规则索引 `README.md`
- ✅ 创建开发规范总览 `CODING_STANDARDS.md`
- ✅ 文档层次清晰

### 访问便利性
- ✅ 多个入口点
- ✅ 按角色分类
- ✅ 快速导航链接

## 📝 维护建议

### 日常维护
1. 定期检查规则文件的有效性
2. 更新规则执行统计
3. 收集团队反馈

### 版本更新
1. 记录规则变更历史
2. 更新文档版本号
3. 同步更新所有引用

### 质量保证
1. 规则内容清晰易懂
2. 示例代码准确无误
3. 文档链接有效可访问

---

**整理日期**: 2026-03-09
**整理人**: System
**版本**: 1.0.0
