# SunEyeVision 开发规范总览

本文档提供了 SunEyeVision 项目的所有开发规范和编码规则的索引和快速访问入口。

## 📋 规则系统

SunEyeVision 项目使用 CodeBuddy 规则系统来管理和执行开发规范。所有规则文件存储在 `.codebuddy/rules/` 目录下。

### 🔗 规则系统入口
**详细规则**: [`.codebuddy/rules/README.md`](../.codebuddy/rules/README.md)

### 📌 当前活跃规则

#### 1. 日志系统使用规范
- **文件**: `.codebuddy/rules/日志系统使用规范.mdc`
- **状态**: ✅ 总是应用
- **核心要求**:
  - 禁止使用 `Debug.WriteLine()`
  - 禁止使用 `Trace.WriteLine()`
  - 禁止使用 `Console.WriteLine()`
  - 所有日志通过项目日志系统输出

#### 2. 属性更改通知统一规范
- **文件**: `.codebuddy/rules/PropertyNotificationStandard.mdc`
- **状态**: ✅ 总是应用
- **核心要求**:
  - 所有属性通知类继承 ObservableObject
  - 使用 SetProperty 方法
  - 禁止重复实现 INotifyPropertyChanged

---

## 📚 开发文档分类

### 架构设计
- [框架设计文档](./框架设计文档/) - 项目整体架构和设计决策

### 迁移指南
- [属性更改通知迁移总结](./property_notification_migration_summary.md) - ObservableObject 迁移详细记录
- [ROI Editor 迁移指南](./ROIEditor_Migration_Guide.md) - ROI 编辑器迁移详细文档

### 开发过程
- [开发过程方案文档](./开发过程方案文档/) - 开发流程和方案设计

---

## 🎯 快速访问

### 按角色分类

#### 前端开发者 (UI/WPF)
- 🔗 [属性更改通知统一规范](../.codebuddy/rules/PropertyNotificationStandard.mdc)
- 🔗 [日志系统使用规范](../.codebuddy/rules/日志系统使用规范.mdc)
- 🔗 [ROI Editor 迁移指南](./ROIEditor_Migration_Guide.md)

#### 后端开发者 (Core/SDK)
- 🔗 [属性更改通知统一规范](../.codebuddy/rules/PropertyNotificationStandard.mdc)
- 🔗 [日志系统使用规范](../.codebuddy/rules/日志系统使用规范.mdc)
- 🔗 [框架设计文档](./框架设计文档/)

#### 插件开发者
- 🔗 [属性更改通知统一规范](../.codebuddy/rules/PropertyNotificationStandard.mdc)
- 🔗 [框架设计文档](./框架设计文档/)

---

## 📊 规则执行统计

| 规则类型 | 数量 | 状态 |
|---------|------|------|
| 总是应用 (Always) | 2 | ✅ 已启用 |
| 手动应用 (Manual) | 0 | - |
| 请求时应用 (Requested) | 0 | - |
| **总计** | **2** | **100% 启用** |

---

## 🚀 新手入门

### 第一步：阅读规则
1. 打开 [`.codebuddy/rules/README.md`](../.codebuddy/rules/README.md)
2. 阅读所有活跃规则
3. 理解正确和错误的代码示例

### 第二步：查看迁移文档
1. 阅读相关的迁移指南
2. 了解历史决策和优化过程
3. 参考已完成的最佳实践

### 第三步：开始开发
1. 在开发过程中参考规范
2. 代码审查时对照规则
3. 遇到问题查阅相关文档

---

## 🔍 如何查找规范

### 按问题类型查找

**问题：** 如何输出日志？  
→ 查看 [日志系统使用规范](../.codebuddy/rules/日志系统使用规范.mdc)

**问题：** 如何实现属性通知？  
→ 查看 [属性更改通知统一规范](../.codebuddy/rules/PropertyNotificationStandard.mdc)

**问题：** 如何迁移到新架构？  
→ 查看 [迁移指南](./property_notification_migration_summary.md)

---

## 📝 规则生命周期

### 创建规则
1. 识别需要规范的重复问题
2. 使用 `create_rule` 工具创建规则
3. 更新 [`.codebuddy/rules/README.md`](../.codebuddy/rules/README.md)
4. 更新本文档

### 更新规则
1. 审阅现有规则
2. 使用 `update_rule` 工具更新内容
3. 通知团队成员规则变更

### 废弃规则
1. 在规则文件中设置 `enabled: false`
2. 添加废弃原因和替代方案
3. 更新本文档统计信息

---

## 🤝 贡献规范

### 提出新规则
如果发现需要规范的问题：
1. 在团队会议上讨论
2. 起草规则内容
3. 使用 CodeBuddy 工具创建
4. 通知团队成员

### 改进现有规则
如果发现规则有问题：
1. 收集具体问题案例
2. 提出改进方案
3. 更新规则文件
4. 同步更新文档

---

## 📞 获取帮助

- **规则问题**: 查看 [`.codebuddy/rules/README.md`](../.codebuddy/rules/README.md)
- **架构问题**: 查看 [框架设计文档](./框架设计文档/)
- **迁移问题**: 查看 [迁移指南](./property_notification_migration_summary.md)
- **其他问题**: 联系项目维护者

---

## 📅 更新日志

| 日期 | 更新内容 | 作者 |
|------|---------|------|
| 2026-03-09 | 创建开发规范总览文档，整合规则系统 | System |
| 2026-03-09 | 添加属性更改通知统一规范 | System |
| 2026-03-08 | 添加日志系统使用规范 | System |

---

**最后更新**: 2026-03-09  
**文档版本**: 1.0.0  
**维护者**: SunEyeVision Team
