# 树形结构下拉显示 - 集成示例

## 📋 概述

本文档演示如何在实际工具中集成和使用树形结构下拉显示功能。

## 🎯 核心概念

### 1. 什么是树形结构？

树形结构允许工具将输出属性按逻辑分组显示，例如：

**传统平铺显示**：
```
ActualThresholdUsed
BinaryImage
ProcessingTime
```

**树形结构显示**：
```
阈值工具
  ├─ 结果
  │   ├─ 实际使用的阈值 (ActualThresholdUsed)
  │   └─ 处理时间 (ProcessingTime)
  └─ 图像
      └─ 二值化图像 (BinaryImage)
```

### 2. 如何实现？

通过重写 `GetPropertyTreeName()` 方法，返回包含 `.` 分隔符的树形名称。

## 📝 实施步骤

### 步骤 1: 创建 Results 类

```csharp
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.Threshold.Models
{
    /// <summary>
    /// 阈值工具执行结果
    /// </summary>
    public class ThresholdResults : ToolResults
    {
        /// <summary>
        /// 实际使用的阈值
        /// </summary>
        public double ActualThresholdUsed { get; set; }

        /// <summary>
        /// 二值化后的图像
        /// </summary>
        public Mat? BinaryImage { get; set; }

        /// <summary>
        /// 处理时间（毫秒）
        /// </summary>
        public long ProcessingTime { get; set; }

        /// <summary>
        /// 前景像素数
        /// </summary>
        public int ForegroundPixels { get; set; }

        /// <summary>
        /// 背景像素数
        /// </summary>
        public int BackgroundPixels { get; set; }

        #region 树形结构配置

        /// <summary>
        /// 获取属性的树形显示名称
        /// </summary>
        /// <remarks>
        /// 配置规则：
        /// 1. 使用 `.` 分隔符创建多级树结构
        /// 2. 第一级是节点名称（可选）
        /// 3. 后续级别是分组名称
        /// 4. 最后一级是叶子节点（实际属性）
        /// 
        /// 示例：
        /// - "结果.实际使用的阈值" → 结果 → 实际使用的阈值
        /// - "图像.二值化图像" → 图像 → 二值化图像
        /// </remarks>
        public override string? GetPropertyTreeName(string propertyName)
        {
            return propertyName switch
            {
                nameof(ActualThresholdUsed) => "结果.实际使用的阈值",
                nameof(ProcessingTime) => "结果.处理时间",
                nameof(ForegroundPixels) => "结果.前景像素数",
                nameof(BackgroundPixels) => "结果.背景像素数",
                nameof(BinaryImage) => "图像.二值化图像",
                _ => null  // 使用默认的 DisplayName
            };
        }

        #endregion
    }
}
```

### 步骤 2: 创建工具实现

```csharp
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution;
using SunEyeVision.Tool.Threshold.Models;

namespace SunEyeVision.Tool.Threshold
{
    [Tool(Id = "threshold", Name = "图像阈值", Category = "图像处理")]
    public class ThresholdTool : IToolPlugin<ThresholdParameters, ThresholdResults>
    {
        public ThresholdResults Execute(ThresholdParameters parameters, ExecutionContext context)
        {
            var startTime = DateTime.Now;

            // 执行阈值处理
            var results = new ThresholdResults
            {
                ActualThresholdUsed = parameters.Threshold,
                BinaryImage = ExecuteThreshold(parameters.InputImage, parameters.Threshold)
            };

            // 计算统计信息
            CalculateStatistics(results.BinaryImage, results);

            // 记录处理时间
            results.ProcessingTime = (long)(DateTime.Now - startTime).TotalMilliseconds;

            return results;
        }

        private Mat ExecuteThreshold(Mat? inputImage, double threshold)
        {
            if (inputImage == null)
                throw new ArgumentNullException(nameof(inputImage));

            // 转换为灰度图像
            var grayImage = new Mat();
            Cv2.CvtColor(inputImage, grayImage, ColorConversionCodes.BGR2GRAY);

            // 应用阈值
            var binaryImage = new Mat();
            Cv2.Threshold(grayImage, binaryImage, threshold, 255, ThresholdTypes.Binary);

            grayImage.Dispose();
            return binaryImage;
        }

        private void CalculateStatistics(Mat? binaryImage, ThresholdResults results)
        {
            if (binaryImage == null)
                return;

            // 计算前景和背景像素数
            var foregroundPixels = Cv2.CountNonZero(binaryImage);
            var totalPixels = binaryImage.Rows * binaryImage.Cols;

            results.ForegroundPixels = foregroundPixels;
            results.BackgroundPixels = totalPixels - foregroundPixels;
        }
    }
}
```

### 步骤 3: 配置参数类

```csharp
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.Threshold.Models
{
    /// <summary>
    /// 阈值工具参数
    /// </summary>
    public class ThresholdParameters : ToolParameters
    {
        /// <summary>
        /// 输入图像
        /// </summary>
        [ParameterDescription(Name = "输入图像", Description = "需要处理的图像")]
        public Mat? InputImage { get; set; }

        /// <summary>
        /// 阈值
        /// </summary>
        [ParameterDescription(Name = "阈值", Description = "图像二值化的阈值（0-255）")]
        [ParameterRange(Min = 0, Max = 255)]
        public double Threshold { get; set; } = 128;
    }
}
```

## 📊 显示效果对比

### 传统平铺显示

```xml
<!-- 下拉列表显示 -->
<ComboBox>
    <ComboBoxItem>ActualThresholdUsed</ComboBoxItem>
    <ComboBoxItem>BinaryImage</ComboBoxItem>
    <ComboBoxItem>ProcessingTime</ComboBoxItem>
    <ComboBoxItem>ForegroundPixels</ComboBoxItem>
    <ComboBoxItem>BackgroundPixels</ComboBoxItem>
</ComboBox>
```

### 树形结构显示

```xml
<!-- 树形下拉列表显示 -->
<TreeView>
    <TreeViewItem Header="阈值工具" IsExpanded="True">
        <TreeViewItem Header="结果" IsExpanded="True">
            <TreeViewItem Header="实际使用的阈值" />
            <TreeViewItem Header="处理时间" />
            <TreeViewItem Header="前景像素数" />
            <TreeViewItem Header="背景像素数" />
        </TreeViewItem>
        <TreeViewItem Header="图像" IsExpanded="True">
            <TreeViewItem Header="二值化图像" />
        </TreeViewItem>
    </TreeViewItem>
</TreeView>
```

### 下拉显示文本

```
阈值工具 → 结果 → 实际使用的阈值
阈值工具 → 结果 → 处理时间
阈值工具 → 结果 → 前景像素数
阈值工具 → 结果 → 背景像素数
阈值工具 → 图像 → 二值化图像
```

## 🎯 高级用法

### 1. 多级分组

```csharp
public override string? GetPropertyTreeName(string propertyName)
{
    return propertyName switch
    {
        nameof(Result1) => "第一组.子组1.结果1",
        nameof(Result2) => "第一组.子组1.结果2",
        nameof(Result3) => "第一组.子组2.结果3",
        nameof(Result4) => "第二组.子组1.结果4",
        _ => null
    };
}

// 显示：
// 第一组
//   ├─ 子组1
//   │   ├─ 结果1
//   │   └─ 结果2
//   └─ 子组2
//       └─ 结果3
// 第二组
//   └─ 子组1
//       └─ 结果4
```

### 2. 隐藏某些属性

```csharp
public override string? GetPropertyTreeName(string propertyName)
{
    return propertyName switch
    {
        nameof(InternalProperty) => "",  // 返回空字符串：不显示
        nameof(DebugProperty) => "",     // 返回空字符串：不显示
        nameof(UserProperty) => "用户可见.重要属性",
        _ => null
    };
}
```

### 3. 动态树形结构

```csharp
public override string? GetPropertyTreeName(string propertyName)
{
    // 根据属性类型动态生成树形名称
    var propertyType = GetType().GetProperty(propertyName)?.PropertyType;

    if (propertyType == typeof(Mat))
    {
        return "图像." + GetDisplayName(propertyName);
    }
    else if (propertyType == typeof(double) || propertyType == typeof(int))
    {
        return "数值." + GetDisplayName(propertyName);
    }
    else if (propertyType == typeof(string))
    {
        return "文本." + GetDisplayName(propertyName);
    }

    return null;
}
```

## 🚀 性能说明

### 编译器已自动优化

C# 编译器会自动优化 `switch` 表达式为高效的跳转表或哈希表查找，性能已足够好，无需额外缓存。

**性能对比：**

| 方案 | 时间复杂度 | 内存开销 | 代码行数 |
|------|-----------|---------|---------|
| switch 表达式 | O(1) | 无 | 4 行 |
| 静态缓存 | O(1) | 字典 | 7 行 |

**推荐写法：**

```csharp
// ✅ 推荐写法：简洁且高效
public override string? GetPropertyTreeName(string propertyName)
{
    return propertyName switch
    {
        nameof(OutputImage) => "结果.输出图像",
        nameof(ThresholdUsed) => "结果.实际使用的阈值",
        _ => null
    };
}
```

### 何时考虑缓存

**仅当 `GetPropertyTreeName` 内部涉及以下场景时，才考虑使用静态缓存：**

1. **复杂计算**：如正则匹配、数据库查询、网络请求等
2. **动态生成映射**：如从外部配置文件加载映射关系
3. **反射操作**：如通过反射获取属性特性信息

**当前实现（简单的 switch 表达式）：无需缓存**

## 🔍 常见问题

### Q1: 树形结构会影响性能吗？

**A**: 不会。`GetPropertyTreeName()` 只在属性提取时调用一次，不是每次显示都调用。性能开销可以忽略不计。

### Q2: 可以同时使用平铺和树形显示吗？

**A**: 可以。返回 `null` 的属性会使用平铺显示，返回树形名称的属性会使用树形显示。

### Q3: 如何测试树形结构是否生效？

**A**:
1. 运行工作流
2. 打开参数绑定对话框
3. 查看数据源下拉列表
4. 确认是否显示树形结构（有层级关系的 `→` 分隔符）

### Q4: 树形结构支持多少级？

**A**: 理论上支持任意级，但建议不超过 3 级。过多的层级会影响用户体验。

### Q5: 如何修改树形结构的显示文本？

**A**: 修改 `GetPropertyTreeName()` 返回的字符串即可。例如：`"结果.阈值"` 改为 `"计算结果.实际使用的阈值"`。

## 📚 参考资料

- [插件开发指南](../框架设计文档/插件开发指南.md)
- [参数系统架构](../开发过程方案文档/parameter-system-optimization-implementation-summary.md)
- [ToolResults API](../../src/Plugin.SDK/Execution/Results/ToolResults.cs)
- [TreeNodeData API](../../src/Plugin.SDK/Execution/Parameters/TreeNodeData.cs)
