# 编译错误修复指南

## 当前状态

✅ **SunEyeVision.PluginSystem.Base** - 编译成功（0个错误）

⚠️ **SunEyeVision.PluginSystem** - 还有少量编译错误

## 剩余编译错误

### AutoToolDebugViewModelBase.cs

这些错误主要是因为占位实现与原代码不匹配：

#### 1. AsyncRelayCommand 参数类型不匹配（第147行）
```csharp
// 当前代码
RunCommand = new AsyncRelayCommand(
    async ct => await RunToolAsync(ct),  // 错误：ct 是 object? 类型
    _ => !IsBusy,
    OnExecutionError);
```

**修复方法**：
```csharp
RunCommand = new AsyncRelayCommand(
    async (param, ct) => await RunToolAsync(ct),
    _ => !IsBusy,
    OnExecutionError);
```

#### 2. ParameterValidator.ValidateItems 参数类型不匹配（第357、375行）

**修复方法**：将 `ObservableCollection<ParameterItem>` 转换为 `Dictionary<string, object>`
```csharp
// 当前代码
var errors = _validator.ValidateItems(ParameterItems);

// 修复为
var errors = _validator.ValidateItems(
    ParameterItems.ToDictionary(p => p.Name, p => p.Value ?? ""));
```

#### 3. LoadItemsFromFile 参数不匹配（第509行）

**修复方法**：调整参数类型
```csharp
// 当前代码
var items = _repository.LoadItemsFromFile(filePath, ParameterItems);

// 修复为
var items = _repository.LoadItemsFromFile(filePath);
```

## 快速修复方案

由于 `AutoToolDebugViewModelBase.cs` 是一个复杂的调试ViewModel类，而相关的支持类（ParameterRepository、ParameterValidator等）只是占位实现，有两个解决方案：

### 方案A：完整实现（推荐）
1. 在 `ParameterRepository` 中实现完整的参数持久化逻辑
2. 在 `ParameterValidator` 中实现完整的参数验证逻辑
3. 修复 `AutoToolDebugViewModelBase` 中的所有调用

### 方案B：简化实现（快速编译）
1. 将 `AutoToolDebugViewModelBase.cs` 中有问题的功能临时注释掉
2. 保留核心功能，让项目先编译通过

## 架构验证

当前架构已基本完成：

```
✅ SunEyeVision.Core.dll
✅ SunEyeVision.PluginSystem.Base.dll  (0个错误)
⚠️ SunEyeVision.PluginSystem.dll      (少量错误)
⚠️ SunEyeVision.Tools.dll              (待验证)
```

## 下一步

1. 在Visual Studio中打开项目
2. 使用IntelliSense修复剩余错误
3. 运行完整编译测试
4. 提交代码到git

## 自动化脚本

以下脚本可帮助清理和编译：

```bash
# 清理生成文件
cd SunEyeVision.PluginSystem
rmdir /s /q obj bin
cd ../SunEyeVision.Tools
rmdir /s /q obj bin

# 编译项目
cd ..
dotnet build SunEyeVision.PluginSystem.Base/SunEyeVision.PluginSystem.Base.csproj
dotnet build SunEyeVision.PluginSystem/SunEyeVision.PluginSystem.csproj
dotnet build SunEyeVision.Tools/SunEyeVision.Tools.csproj
```

---
*更新时间：2026-02-07*
