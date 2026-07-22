# 解决方案配置对话框优化方案

**日期**：2026-03-23  
**版本**：1.0  
**状态**：待实施

---

## 📋 问题总结

### 问题1：默认解决方案标记持久化失败

**现象**：设置默认解决方案后关闭窗口重新打开，默认标记（📌）消失

**根本原因**：
```csharp
// ❌ 修改克隆对象（临时对象），不影响 Registry 内部原始对象
var allMetadata = _registry.GetAll();  // 返回克隆对象
foreach (var metadata in allMetadata)
{
    metadata.IsDefault = (metadata.Id == defaultId);  // 只修改了克隆对象
}
```

**影响**：
- 用户设置的默认解决方案标记无法持久化
- 每次关闭窗口后，标记都会消失

---

### 问题2：SkipStartupConfig 同步和持久化失败

**现象**：勾选/取消勾选"启动时跳过配置界面"checkbox后，settings.json 中的值未更新

**根本原因**：

1. **UI同步问题**：`SetPreference()` 方法没有触发 `PropertyChanged` 事件
   ```csharp
   public void SetPreference(string key, object? value)
   {
       UserPreferences[key] = value;  // ❌ 直接修改字典，没有属性通知！
       _logger.Log(LogLevel.Info, $"设置偏好: {key} = {value}", "SolutionSettings");
   }
   ```

2. **持久化时机问题**：勾选后直接关闭或点击启动，设置不会保存到文件
   - 用户勾选 checkbox
   - 关闭窗口或点击启动
   - ❌ settings.json 未更新

**影响**：
- UI 层无法感知属性变化（checkbox 状态不更新）
- 用户设置的偏好无法持久化到文件

---

### 问题3：频繁调用 SaveSettings()

**现象**：15处调用 `SaveSettings()`，导致不必要的磁盘IO

**调用统计**：
| 序号 | 操作 | 调用位置 | 是否必要 |
|------|------|----------|----------|
| 1 | CreateNewSolution() | 第218行 | ✅ 必要 |
| 2 | OpenSolution() | 第317行 | ❌ 不必要 |
| 3 | SetCurrentSolutionSilently() | 第379行 | ❌ 不必要 |
| 4 | SaveSolution() | 第458行 | ❌ 不必要 |
| 5 | SaveAsSolution() | 第518行 | ❌ 不必要 |
| 6 | CloseSolution() | 第561行 | ❌ 不必要 |
| 7 | DeleteSolution() | 第638行 | ✅ 必要 |
| 8 | RenameSolutionFile() | 第748行 | ❌ 不必要 |
| 9 | RegisterKnownSolution() | 第832行 | ❌ 不必要 |
| 10 | UpdateMetadata() | 第883行 | ❌ 不必要 |
| 11 | SetDefaultSolution() | 第987, 997行 | ✅ 必要 |
| 12 | SetCurrentSolution() | 第1414行 | ❌ 不必要 |

**影响**：
- 性能问题：同一个 settings.json 被频繁写入
- 代码冗余：每个方法最后都要加 `SaveSettings()`
- 违反单一职责原则

---

## 🎨 设计方案

### 设计原则

1. **不考虑兼容性**（rule-008）：直接删除旧代码，不保留旧路径
2. **统一关闭逻辑**：所有关闭路径都在 OnClosing 中统一保存
3. **延迟批量保存**：使用脏标记机制，减少磁盘IO
4. **关键操作强制保存**：破坏性操作（删除、新建）立即保存
5. **属性通知统一**（rule-001）：修复 SetPreference 的 PropertyChanged 问题

### 操作分类

**需要立即保存的操作（强制保存）**：

| 操作 | 原因 |
|------|------|
| DeleteSolution() | 破坏性操作，防止误删后恢复困难 |
| CreateNewSolution() | 新建操作，用户期望立即生效 |
| SetDefaultSolution() | 设置默认，用户期望立即生效 |

**可以延迟保存的操作（标记为脏）**：

| 操作 | 原因 |
|------|------|
| OpenSolution() | 用户可能只是查看 |
| SaveSolution() | 文件已保存，settings 可延迟 |
| SaveAsSolution() | 可延迟保存 |
| UpdateMetadata() | 只是更新元数据 |
| RenameSolutionFile() | 可延迟保存 |
| RegisterKnownSolution() | 可延迟保存 |
| SetCurrentSolution() | 可延迟保存 |
| CloseSolution() | 可延迟到窗口关闭 |
| 勾选 SkipStartupConfig | 可延迟到窗口关闭 |

---

## 🔧 实施方案

### 修改1：SolutionRegistry.cs - 添加批量更新方法

**位置**：`src/Workflow/SolutionRegistry.cs` 第281行之后

```csharp
/// <summary>
/// 批量更新所有元数据的 IsDefault 标志
/// </summary>
/// <param name="defaultId">默认解决方案ID</param>
/// <remarks>
/// 设计说明：
/// 1. 直接修改内部字典中的原始对象，而不是修改克隆对象
/// 2. 克隆对象是临时的，修改克隆对象没有意义
/// 3. 下次调用 GetAll() 时，会从已更新的原始对象克隆
/// </remarks>
public void UpdateAllIsDefaultFlags(string defaultId)
{
    _lock.EnterWriteLock();
    try
    {
        int updatedCount = 0;

        // 遍历内部字典中的原始对象
        foreach (var metadata in _metadataMap.Values)
        {
            var newIsDefault = (metadata.Id == defaultId);

            // 记录更新的数量
            if (metadata.IsDefault != newIsDefault)
            {
                updatedCount++;
            }

            // 直接修改原始对象的 IsDefault 属性
            metadata.IsDefault = newIsDefault;
        }

        _logger.Log(LogLevel.Info,
            $"批量更新元数据 IsDefault 标志完成: 默认ID={defaultId}, 更新数量={updatedCount}",
            "SolutionRegistry");
    }
    finally
    {
        _lock.ExitWriteLock();
    }
}
```

---

### 修改2：SolutionManager.cs - 添加脏标记机制

**位置1**：`src/Workflow/SolutionManager.cs` 字段区域（建议在构造函数之前）

```csharp
private bool _isSettingsDirty = false;  // 脏标记：设置是否需要保存
```

**位置2**：`src/Workflow/SolutionManager.cs` 第1446行之后（在 SaveSettings() 方法之前）

```csharp
/// <summary>
/// 标记设置为脏（需要保存）
/// </summary>
private void MarkSettingsDirty()
{
    if (!_isSettingsDirty)
    {
        _logger.Log(LogLevel.Info, "标记设置为脏（需要保存）", "SolutionManager");
        _isSettingsDirty = true;
    }
}

/// <summary>
/// 保存设置（带脏标记检查）
/// </summary>
private void SaveSettings()
{
    if (!_isSettingsDirty)
    {
        _logger.Log(LogLevel.Info, "设置未变更，跳过保存", "SolutionManager");
        return;
    }

    _logger.Log(LogLevel.Info, $"开始保存设置文件: {_configFilePath}", "SolutionManager");
    _settings.Save(_configFilePath);
    _isSettingsDirty = false;
    _logger.Log(LogLevel.Success, "设置保存完成", "SolutionManager");
}

/// <summary>
/// 立即保存设置（强制保存，忽略脏标记）
/// </summary>
private void ForceSaveSettings()
{
    _logger.Log(LogLevel.Info, $"强制保存设置文件: {_configFilePath}", "SolutionManager");
    _settings.Save(_configFilePath);
    _isSettingsDirty = false;
    _logger.Log(LogLevel.Success, "设置强制保存完成", "SolutionManager");
}

/// <summary>
/// 保存用户设置（公开接口，用于窗口关闭时调用）
/// </summary>
public void SaveUserSettings()
{
    if (_isSettingsDirty)
    {
        SaveSettings();
    }
    else
    {
        _logger.Log(LogLevel.Info, "设置未变更，无需保存", "SolutionManager");
    }
}
```

---

### 修改3：SolutionManager.cs - 修改 UpdateMetadataIsDefaultFlags

**位置**：`src/Workflow/SolutionManager.cs` 第1021-1037行

```csharp
/// <summary>
/// 更新所有元数据的 IsDefault 标志
/// </summary>
/// <remarks>
/// 修复说明（2026-03-23）：
/// 修复前：调用 GetAll() 获取克隆对象并修改，导致修改无效
/// 修复后：调用 UpdateAllIsDefaultFlags() 直接修改 Registry 内部原始对象
/// </remarks>
private void UpdateMetadataIsDefaultFlags()
{
    var defaultId = _settings.DefaultSolutionId;

    // ✅ 修复：调用 Registry 的批量更新方法
    // 直接修改 Registry 内部字典中的原始对象
    _registry.UpdateAllIsDefaultFlags(defaultId);
}
```

---

### 修改4：SolutionManager.cs - 替换 SaveSettings() 调用

**需要替换的位置及修改方式**：

| 行号 | 方法 | 原调用 | 修改后 |
|------|------|--------|--------|
| 218 | CreateNewSolution() | `SaveSettings()` | `ForceSaveSettings()` |
| 317 | OpenSolution() | `SaveSettings()` | `MarkSettingsDirty()` |
| 379 | SetCurrentSolutionSilently() | `SaveSettings()` | `MarkSettingsDirty()` |
| 458 | SaveSolution() | `SaveSettings()` | `MarkSettingsDirty()` |
| 518 | SaveAsSolution() | `SaveSettings()` | `MarkSettingsDirty()` |
| 561 | CloseSolution() | `SaveSettings()` | `MarkSettingsDirty()` |
| 638 | DeleteSolution() | `SaveSettings()` | `ForceSaveSettings()` |
| 748 | RenameSolutionFile() | `SaveSettings()` | `MarkSettingsDirty()` |
| 832 | RegisterKnownSolution() | `SaveSettings()` | `MarkSettingsDirty()` |
| 883 | UpdateMetadata() | `SaveSettings()` | `MarkSettingsDirty()` |
| 987, 997 | SetDefaultSolution() | `SaveSettings()` | `ForceSaveSettings()` |
| 1414 | SetCurrentSolution() | `SaveSettings()` | `MarkSettingsDirty()` |

**示例修改**：

```csharp
// ❌ 修改前（第317行）
public Solution? OpenSolution(string filePath)
{
    // ...
    _settings.AddKnownSolution(metadata);
    _settings.CurrentSolutionId = metadata.Id;
    SaveSettings();  // ❌ 立即保存
    // ...
}

// ✅ 修改后
public Solution? OpenSolution(string filePath)
{
    // ...
    _settings.AddKnownSolution(metadata);
    _settings.CurrentSolutionId = metadata.Id;
    MarkSettingsDirty();  // ✅ 标记为脏
    // ...
}
```

---

### 修改5：SolutionSettings.cs - 修复属性通知

**位置**：`src/Workflow/SolutionSettings.cs` 第371-386行

```csharp
/// <summary>
/// 设置用户偏好设置
/// </summary>
/// <param name="key">键</param>
/// <param name="value">值</param>
public void SetPreference(string key, object? value)
{
    if (string.IsNullOrEmpty(key))
    {
        _logger.Log(LogLevel.Warning, "设置偏好设置失败：键为空", "SolutionSettings");
        return;
    }

    UserPreferences[key] = value;
    _logger.Log(LogLevel.Info, $"设置偏好: {key} = {value}", "SolutionSettings");
    
    // ✅ 新增：映射并触发属性通知
    // 将偏好键映射到对应的属性名
    string propertyName = key switch
    {
        "SkipStartupConfig" => nameof(SkipStartupConfig),
        _ => key  // 其他键直接使用原名称（如果有对应的属性）
    };
    
    // 触发属性变更通知
    OnPropertyChanged(propertyName);
}
```

---

### 修改6：SolutionConfigurationDialogViewModel.cs - 添加保存设置方法

**位置**：`src/UI/ViewModels/SolutionConfigurationDialogViewModel.cs` 第128行之后

```csharp
/// <summary>
/// 保存设置
/// </summary>
public void SaveSettings()
{
    try
    {
        // ✅ 调用 SolutionManager 的公开方法
        _solutionManager.SaveUserSettings();
        LogInfo("设置已保存到文件");
    }
    catch (Exception ex)
    {
        LogError($"保存设置失败: {ex.Message}");
    }
}
```

---

### 修改7：SolutionConfigurationDialog.xaml.cs - 统一关闭逻辑

**位置**：`src/UI/Views/Windows/SolutionConfigurationDialog.xaml.cs` 第40-68行

```csharp
/// <summary>
/// 窗口关闭时
/// </summary>
protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
{
    // ✅ 统一保存设置（包括 SkipStartupConfig）
    // 无论通过什么方式关闭，都统一保存设置
    _viewModel.SaveSettings();

    // 如果已启动（双击或点击启动按钮），直接关闭
    if (_viewModel.IsLaunched)
    {
        base.OnClosing(e);
        return;
    }

    // 保存当前解决方案（确保修改的名称和描述被保存）
    _viewModel.SaveCurrentSolution();

    // 如果用户没有点击启动或跳过，确认是否关闭
    var result = MessageBox.Show(
        "确定要关闭配置界面吗？",
        "确认",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result == MessageBoxResult.No)
    {
        e.Cancel = true;
    }

    base.OnClosing(e);
}
```

---

## 📊 修改清单

| 文件 | 修改内容 | 新增行数 | 修改行数 |
|------|----------|----------|----------|
| `src/Workflow/SolutionRegistry.cs` | 添加 `UpdateAllIsDefaultFlags()` 方法 | +40 | 0 |
| `src/Workflow/SolutionManager.cs` | 添加脏标记机制 | +60 | 0 |
| `src/Workflow/SolutionManager.cs` | 修改 `UpdateMetadataIsDefaultFlags()` 方法 | +5 | -10 |
| `src/Workflow/SolutionManager.cs` | 替换 12 处 `SaveSettings()` 为 `MarkSettingsDirty()`/`ForceSaveSettings()` | 0 | 12 |
| `src/Workflow/SolutionSettings.cs` | 修改 `SetPreference()` 方法 | +8 | -2 |
| `src/UI/ViewModels/SolutionConfigurationDialogViewModel.cs` | 添加 `SaveSettings()` 方法 | +10 | 0 |
| `src/UI/Views/Windows/SolutionConfigurationDialog.xaml.cs` | 修改 `OnClosing()` 方法 | +1 | -2 |

**总计**：修改 5 个文件，约 124 行代码

---

## 🎯 预期效果

### 效果1：默认解决方案标记持久化

**修复前**：
```
设置默认 → 📌 显示 → 关闭 → 重新打开 → ❌ 标记消失
```

**修复后**：
```
设置默认 → 📌 显示 → 关闭 → 重新打开 → ✅ 标记仍然显示
```

---

### 效果2：SkipStartupConfig 同步和持久化

**修复前**：
```
勾选 checkbox → ❌ UI 不更新 → 关闭 → ❌ settings.json 未更新
```

**修复后**：
```
勾选 checkbox → ✅ UI 立即更新 → 关闭 → ✅ settings.json 已更新
```

---

### 效果3：性能优化

**修复前**：
```
用户操作流程：
1. 打开解决方案 → SaveSettings() → 写入文件（第1次）
2. 更新元数据 → SaveSettings() → 写入文件（第2次）
3. 关闭窗口   → SaveSettings() → 写入文件（第3次）

总计：3次文件写入
```

**修复后**：
```
用户操作流程：
1. 打开解决方案 → MarkSettingsDirty() → 标记为脏
2. 更新元数据 → MarkSettingsDirty() → 标记为脏
3. 关闭窗口   → SaveUserSettings() → 写入文件（第1次）

总计：1次文件写入（减少67%的IO操作）
```

---

## 📁 文件清单

修改的文件列表：

1. `src/Workflow/SolutionRegistry.cs`
2. `src/Workflow/SolutionManager.cs`
3. `src/Workflow/SolutionSettings.cs`
4. `src/UI/ViewModels/SolutionConfigurationDialogViewModel.cs`
5. `src/UI/Views/Windows/SolutionConfigurationDialog.xaml.cs`

---

## 🔄 实施顺序

建议按以下顺序实施：

1. **第一步**：修改 `SolutionRegistry.cs` 和 `SolutionManager.cs` 的默认标记问题（问题1）
   - 添加 `UpdateAllIsDefaultFlags()` 方法
   - 修改 `UpdateMetadataIsDefaultFlags()` 方法

2. **第二步**：添加脏标记机制并替换调用点（问题3）
   - 添加脏标记相关方法
   - 替换 12 处 `SaveSettings()` 调用

3. **第三步**：修复 `SolutionSettings.cs` 的属性通知问题（问题2的UI同步）
   - 修改 `SetPreference()` 方法

4. **第四步**：修改窗口关闭逻辑（问题2的持久化）
   - 在 ViewModel 中添加 `SaveSettings()` 方法
   - 修改 `OnClosing()` 方法

---

## 📚 参考资料

- [rule-001: 属性更改通知统一规范](./01-coding-standards/property-notification.mdc)
- [rule-003: 日志系统使用规范](./01-coding-standards/logging-system.mdc)
- [rule-008: 原型设计期代码纯净原则](./prototype-design-clean-code.mdc)

---

## 📝 变更历史

| 日期 | 版本 | 变更内容 | 作者 |
|------|------|----------|------|
| 2026-03-23 | 1.0 | 初始版本 | Team |
