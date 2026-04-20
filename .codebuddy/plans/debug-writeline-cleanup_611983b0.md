---
name: debug-writeline-cleanup
overview: 删除 src/ 中 44 个业务代码文件里的 Debug.WriteLine/Trace.WriteLine/Console.WriteLine 调用（共约490+处），排除6个日志框架自身文件。
todos:
  - id: write-cleanup-script
    content: 编写 PowerShell 脚本批量删除 44 个文件中的 Debug/Trace/Console.WriteLine 调用
    status: completed
  - id: execute-cleanup
    content: 执行清理脚本并统计删除数量
    status: completed
    dependencies:
      - write-cleanup-script
  - id: verify-cleanup
    content: 使用 [subagent:code-explorer] 搜索验证无遗漏，确认清理完整性
    status: completed
    dependencies:
      - execute-cleanup
  - id: build-verify
    content: 执行编译验证确保无破坏性变更
    status: completed
    dependencies:
      - verify-cleanup
---

## 产品概述

清理项目中所有违规的日志输出调用（`Debug.WriteLine`/`Trace.WriteLine`/`Console.WriteLine`），消除输出到 VS 输出窗口的问题，使所有日志统一走项目日志系统。

## 核心功能

- 删除 44 个业务代码文件中约 490+ 处违规调试输出
- 排除 6 个日志框架自身文件（设计如此）
- 排除 3 个工具/测试文件（非生产代码）
- 同时清理不再需要的 `using System.Diagnostics;` / `using System;` 引用
- 清理完成后编译验证

## 清理范围确认

**排除文件（不修改）**:

- 日志框架: `DebugLogWriter.cs`, `ConsoleLogWriter.cs`, `VisionLogger.cs`, `PluginLogger.cs`, `FileLogWriter.cs`
- 工具/测试: `DispatcherCallAnalyzer.cs`, `FixEncoding.cs`, `PngToIcoConverter.cs`

**待清理: 44 个文件, 490+ 处**

## Tech Stack

- C# WPF 项目 (.NET)
- 正则替换方式删除整行调用及关联的空行

## 实现方案

### 核心策略: 使用 PowerShell 脚本批量正则替换

由于涉及 44 个文件、500+ 处修改，逐个使用 replace_in_file 效率极低。采用 PowerShell 脚本进行批量处理：

1. **匹配模式**: 匹配三种调用形式的完整行（含缩进、行尾换行）
2. **删除策略**: 删除调用行 + 紧随的空行（避免残留空行）
3. **引用清理**: 删除后检查是否还有其他 `System.Diagnostics` 的使用，若无则移除 `using System.Diagnostics;`
4. **编码保证**: UTF-8 无 BOM 写入

### 替换规则

```
# 规则1: 删除 Debug.WriteLine 行 (含 System.Diagnostics.Debug 前缀和短名称)
$pattern = '^\s*(?:System\.Diagnostics\.)?Debug\.WriteLine.*?[\r\n]+\s*[\r\n]*'
# 规则2: 删除 Trace.WriteLine 行
$pattern = '^\s*(?:System\.Diagnostics\.)?Trace\.WriteLine.*?[\r\n]+\s*[\r\n]*'
# 规则3: 删除 Console.WriteLine 行
$pattern = '^\s*Console\.WriteLine.*?[\r\n]+\s*[\r\n]*'
# 规则4: 清理孤立的 using System.Diagnostics;
$pattern = '^using System\.Diagnostics;\s*[\r\n]+'
```

### 执行步骤

1. 编写 PowerShell 脚本执行批量替换
2. 对每个目标文件应用替换
3. 统计各文件删除数量
4. 执行编译验证

### 关键注意事项

- 不影响 catch 块中的业务逻辑（只删除 Debug.WriteLine 行）
- 不影响 if/else 分支结构（只删除独立的 WriteLine 调用）
- 使用 UTF-8 无 BOM 编码写入，避免中文乱码

## SubAgent

- **code-explorer**
- Purpose: 验证清理结果，搜索遗漏的 Debug/Trace/Console.WriteLine 调用
- Expected outcome: 输出完整的清理报告，包括：已清理数量、遗漏位置（如有）、需要手动处理的特殊情况