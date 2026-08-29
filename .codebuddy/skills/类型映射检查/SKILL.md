---
name: 类型映射检查
description: 类型映射合规检查技能。用于检查 GlobalVariableTypeMapper 类型映射是否正确，确保未映射类型直接报错而非静默回退。触发场景：类型映射修改、类型适配检查、全局变量绑定调试。
---

# 类型映射合规检查技能

## 触发场景

- 修改 `GlobalVariableTypeMapper.cs` 中的类型映射
- 修改 `App.xaml.cs` 中 `OnGlobalVariableSelectionRequested` 的逻辑
- 修改 `ParameterResolver.cs` 中的类型解析逻辑
- 检查全局变量绑定的 DataType 映射是否正确
- 添加新的支持类型到全局变量系统

## 检查流程

### 第1步：确认映射表对称性

检查 `GlobalVariableTypeMapper.cs` 中 `TypeToString` 和 `ChineseNames` 是否完全对称：

```
read_file path="src/Workflow/GlobalVariableTypeMapper.cs"
```

验证清单：
- `TypeToString` 中的每一个类型字符串在 `ChineseNames` 中都有对应的中文名
- `ChineseNames` 覆盖全部 13 种 `GlobalVariableValueConverter.SupportedTypes` 常量
- 每种 `Type` 映射对应的 GlobalVariable 类型字符串在 `GlobalVariableValueConverter.SupportedTypes` 中确实存在

### 第2步：检查无兜底回退

检查 `App.xaml.cs` 中 `OnGlobalVariableSelectionRequested`：

```
search_content pattern="OnGlobalVariableSelectionRequested" path="src/UI/App.xaml.cs"
```

必须满足：
- `ToGlobalVarType` 返回 null 且 `DataType != null` 时，**必须**有弹窗报错或抛异常
- **禁止** `filterType == null` 时静默传给 `ShowSelect` 显示全部
- 报错信息必须包含：不支持的类型名称 + 提示适配通用类型

### 第3步：检查运行时绑定解析

检查 `ParameterResolver.cs` 中 `ResolveDynamic` 方法：

```
search_content pattern="GetResultItems" path="src/Plugin.SDK/Execution/Parameters/ParameterResolver.cs"
```

必须满足：
- `GenericToolResults` 已覆盖 `GetResultItems()`
- 反射查找失败后 fallback 到 `GetResultItems()` 查找
- **禁止** `try-catch` 吞异常

### 第4步：检查中文显示

检查 `GlobalVariableManagerDialog.xaml` 的类型 ComboBox：

```
search_content pattern="TypeOptions|TypeComboBox" path="src/UI/Views/Windows/GlobalVariableManagerDialog.xaml"
```

必须满足：
- 使用 `TypeOptions` 绑定（中文显示名 + 英文值）
- **禁止** 硬编码英文类型列表（`<x:Array x:Key="GlobalVariableTypes">`）
- `DisplayMemberPath` 为 `"DisplayName"`（中文）
- `SelectedValuePath` 为 `"Value"`（英文值）
- `SelectedValue` 绑定到 `Type` 属性

## 约束

- **遵循 rule-008 零兼容原则**：删除旧的英文类型列表，不使用兼容层
- **遵循 rule-008 报错优先原则**：未映射类型直接报错，不静默回退
- **遵循 rule-004 方案设计要求**：分析完整映射链，不只点对点修复
- **遵循 rule-015 禁止兜底回退原则**：不得有任何形式的静默降级
