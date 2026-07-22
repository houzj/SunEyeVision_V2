# WPF Designer 扩展安装指南

## 🚀 方法一：通过扩展面板手动安装（推荐）

### 步骤：

1. **打开扩展面板**
   - 在 VSCode 中按 `Ctrl+Shift+X`
   - 或点击左侧活动栏的扩展图标 📦

2. **搜索扩展**
   在搜索框中尝试以下关键词（依次尝试）：
   ```
   WPF Designer
   WPF Preview
   XAML Preview
   ```

3. **安装扩展**
   找到合适的扩展后，点击 "Install" 按钮

---

## 🔍 推荐的扩展列表

### 选项1：WPF Preview
- **搜索关键词**：`WPF Preview`
- **发布者**：EtherealCode
- **特点**：基础的WPF预览功能

### 选项2：XAML Styler
- **搜索关键词**：`XAML Styler`
- **发布者**：TimHeuer
- **特点**：代码格式化，使XAML更易读

### 选项3：.NET MAUI Preview
- **搜索关键词**：`XAML Preview for .NET MAUI`
- **发布者**：Microsoft
- **特点**：微软官方，对WPF有部分支持

---

## 💡 如果没有找到合适的WPF扩展

VSCode 对 WPF 的预览支持确实不如 Visual Studio 完整。以下是替代方案：

### 方案A：使用 Visual Studio 查看设计器
如果您有 Visual Studio：
1. 用 Visual Studio 打开项目
2. 双击 `MainWindow.xaml`
3. 查看设计器预览

### 方案B：直接运行应用程序（最有效）
```bash
# 在项目根目录运行
cd d:\MyWork\SunEyeVision\SunEyeVision
run_release.bat
```

这是查看窗口实际效果的最佳方法！

### 方案C：使用浏览器查看XAML结构
虽然不是视觉预览，但可以看到布局结构：
1. 在VSCode中打开 `MainWindow.xaml`
2. 使用折叠功能查看结构
3. 参考我之前提供的布局图示

---

## 📝 已创建的安装脚本

我已为您创建了以下文件：

### 1. PowerShell 脚本
```
d:\MyWork\SunEyeVision\SunEyeVision\install-wpf-designer.ps1
```

**使用方法**：
1. 右键点击该文件
2. 选择 "使用 PowerShell 运行"

### 2. 批处理脚本
```
d:\MyWork\SunEyeVision\SunEyeVision\install-wpf-designer.bat
```

**使用方法**：
- 双击运行

---

## 🎯 快速查看窗口效果

**现在就可以看到实际效果：**

```bash
# 方式1：运行批处理文件
run_release.bat

# 方式2：使用 dotnet 命令
dotnet run --project SunEyeVision.UI\SunEyeVision.UI.csproj --configuration Release
```

---

## 🆘 需要帮助？

如果安装遇到问题，请告诉我：

1. 扩展面板搜索时看到了哪些扩展
2. 安装过程中是否有错误信息
3. 您使用的 VSCode 版本

我会为您提供更针对性的帮助！

---

## 📌 窗口布局参考

根据您的 `MainWindow.xaml` 文件，窗口包含：

- **菜单栏**：文件、编辑、视图、运行、工具、插件、帮助
- **工具栏**：新建、打开、保存、运行、连续运行、工作流选择
- **左侧工具箱**：可折叠的算法节点列表（260px）
- **中间工作区**：多Tab页工作流画布（10000×10000）
- **右侧面板**：图像显示（500px）+ 属性面板（自适应）
- **状态栏**：状态、相机、流程、CPU、FPS

运行应用程序可以看到完整的交互效果！
