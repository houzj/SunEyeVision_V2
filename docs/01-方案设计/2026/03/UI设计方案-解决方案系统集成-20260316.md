# 解决方案系统 - UI设计方案

> **版本**: 1.0  
> **创建日期**: 2026-03-12  
> **适用范围**: SunEyeVision 解决方案系统集成

---

## 目录

1. [概述](#1-概述)
2. [启动流程设计](#2-启动流程设计)
3. [启动配置界面](#3-启动配置界面)
4. [主界面集成](#4-主界面集成)
5. [菜单栏优化](#5-菜单栏优化)
6. [快捷键定义](#6-快捷键定义)
7. [UI交互流程](#7-ui交互流程)

---

## 1. 概述

### 1.1 设计目标

- **用户友好**：启动时进入配置界面，引导用户完成初始化
- **灵活配置**：支持跳过配置，快速启动
- **便捷切换**：主界面可随时打开配置界面，切换项目/配方
- **快捷操作**：菜单栏提供快捷访问常用功能

### 1.2 核心概念

```
启动配置界面（SolutionConfigurationDialog）
    ├── 项目管理
    │   ├── 新建项目
    │   ├── 打开项目
    │   └── 删除项目
    │
    ├── 配方管理
    │   ├── 添加配方
    │   ├── 复制配方
    │   └── 删除配方
    │
    └── 快速启动
        ├── 跳过配置
        └── 使用上次配置
```

---

## 2. 启动流程设计

### 2.1 启动决策树

```
应用启动
    │
    ├─ 检查 RuntimeConfig
    │   ├─ 有最近项目？
    │   │   ├─ 是 → 显示启动配置界面
    │   │   │        ├── 默认选中最近项目
    │   │   │        ├── 显示配方列表
    │   │   │        └─ [启动] / [跳过] 按钮
    │   │   │
    │   │   └─ 否 → 显示启动配置界面
    │   │            ├── 显示项目列表
    │   │            ├── [新建项目] / [打开项目] 按钮
    │   │            └─ [启动] / [跳过] 按钮
    │   │
    └─ 用户选择
        ├─ [启动] → 加载项目和配方 → 进入主界面
        └─ [跳过] → 直接进入主界面（无加载）
```

### 2.2 配置文件检查

```csharp
public class StartupDecisionService
{
    private readonly RuntimeConfig _runtimeConfig;

    public StartupDecision GetStartupDecision()
    {
        if (string.IsNullOrEmpty(_runtimeConfig.CurrentProject))
        {
            // 没有最近使用的项目
            return StartupDecision.ShowConfigurationWithEmptyState;
        }

        if (_runtimeConfig.HasRecentProjects)
        {
            // 有最近使用的项目
            return StartupDecision.ShowConfigurationWithRecentProject;
        }

        // 默认情况
        return StartupDecision.ShowConfiguration;
    }
}

public enum StartupDecision
{
    ShowConfigurationWithEmptyState,    // 显示配置界面（空状态）
    ShowConfigurationWithRecentProject,  // 显示配置界面（有最近项目）
    ShowConfiguration                    // 显示配置界面
}
```

---

## 3. 启动配置界面

### 3.1 界面布局

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    太阳眼视觉 - 解决方案配置                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌────────────────────────────────────┬──────────────────────────────────┐  │
│  │  📁 项目列表                    │  📋 配方列表                    │  │
│  │  ─────────────────────────────    │  ─────────────────────────      │  │
│  │  [+ 新建项目] [打开项目]         │  [+ 添加配方] [复制配方]         │  │
│  │  ─────────────────────────────    │  ─────────────────────────      │  │
│  │                                  │                                  │  │
│  │  📄 产品A100                    │  📄 标准配方                     │  │
│  │     手机壳检测                    │     产线1                        │  │
│  │     修改: 2026-03-12            │     修改: 2026-03-12            │  │
│  │                                  │                                  │  │
│  │  📄 产品B200                    │  📄 产线2配方                    │  │
│  │     电路板检测                    │     产线2                        │  │
│  │     修改: 2026-03-11            │     修改: 2026-03-10            │  │
│  │                                  │                                  │  │
│  │  📄 产品C300                    │                                  │  │
│  │     连接器检测                    │                                  │  │
│  │     修改: 2026-03-10            │                                  │  │
│  │                                  │                                  │  │
│  └────────────────────────────────────┴──────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │  项目详情                                                              │  │
│  │  ─────────────────────────────────────────────────────────────────      │  │
│  │  项目名称: 产品A100                                                    │  │
│  │  产品编码: A100-001                                                   │  │
│  │  描述: 手机壳外观缺陷检测                                               │  │
│  │  程序版本: v1.1.0                                                     │  │
│  │  配方数量: 2                                                          │  │
│  │  创建时间: 2026-03-10 10:00:00                                        │  │
│  │  修改时间: 2026-03-12 15:30:00                                        │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │  配方详情                                                              │  │
│  │  ─────────────────────────────────────────────────────────────────      │  │
│  │  配方名称: 标准配方                                                    │  │
│  │  设备名称: 1号检测站                                                  │  │
│  │  设备类型: 相机+光源                                                   │  │
│  │  参数数量: 15                                                         │  │
│  │  全局变量: 5                                                          │  │
│  │  修改时间: 2026-03-12 15:30:00                                        │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │  ☑️ 启动时跳过配置界面                                              │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│           [跳过配置]        [删除项目]        [启动]                         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 界面组件

#### 3.2.1 项目列表（左侧）

```xaml
<ListBox Name="ProjectListBox" 
         SelectionMode="Single"
         ItemsSource="{Binding Projects}"
         SelectedItem="{Binding SelectedProject}"
         DisplayMemberPath="Name"
         Margin="10">
    
    <!-- 项目项模板 -->
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="5">
                <TextBlock Text="{Binding Name}" 
                           FontWeight="Bold" 
                           FontSize="14"/>
                <TextBlock Text="{Binding Description}" 
                           Foreground="Gray"
                           FontSize="12"
                           TextWrapping="Wrap"/>
                <TextBlock Text="{Binding ModifiedTime, StringFormat='修改: {0:yyyy-MM-dd}'}"
                           Foreground="DarkGray"
                           FontSize="10"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

#### 3.2.2 配方列表（右侧）

```xaml
<ListBox Name="RecipeListBox"
         SelectionMode="Single"
         ItemsSource="{Binding SelectedProject.Recipes}"
         SelectedItem="{Binding SelectedRecipe}"
         DisplayMemberPath="Name"
         Margin="10">
    
    <!-- 配方项模板 -->
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="5">
                <TextBlock Text="{Binding Name}"
                           FontWeight="Bold"
                           FontSize="14"/>
                <TextBlock Text="{Binding Device.DeviceName}"
                           Foreground="Gray"
                           FontSize="12"/>
                <TextBlock Text="{Binding ModifiedTime, StringFormat='修改: {0:yyyy-MM-dd}'}"
                           Foreground="DarkGray"
                           FontSize="10"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

#### 3.2.3 项目详情（左下）

```xaml
<GroupBox Header="项目详情" Margin="10,5">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="100"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        
        <TextBlock Grid.Row="0" Grid.Column="0" Text="项目名称:" Margin="2"/>
        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding SelectedProject.Name}" Margin="2"/>
        
        <TextBlock Grid.Row="1" Grid.Column="0" Text="产品编码:" Margin="2"/>
        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding SelectedProject.ProductCode}" Margin="2"/>
        
        <TextBlock Grid.Row="2" Grid.Column="0" Text="描述:" Margin="2"/>
        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SelectedProject.Description}" 
                   TextWrapping="Wrap" Margin="2"/>
        
        <TextBlock Grid.Row="3" Grid.Column="0" Text="程序版本:" Margin="2"/>
        <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding SelectedProject.Program.Version}" Margin="2"/>
        
        <TextBlock Grid.Row="4" Grid.Column="0" Text="配方数量:" Margin="2"/>
        <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding SelectedProject.Recipes.Count}" Margin="2"/>
        
        <TextBlock Grid.Row="5" Grid.Column="0" Text="修改时间:" Margin="2"/>
        <TextBlock Grid.Row="5" Grid.Column="1" Text="{Binding SelectedProject.ModifiedTime, StringFormat='yyyy-MM-dd HH:mm:ss'}" Margin="2"/>
    </Grid>
</GroupBox>
```

#### 3.2.4 配方详情（右下）

```xaml
<GroupBox Header="配方详情" Margin="10,5">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="100"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        
        <TextBlock Grid.Row="0" Grid.Column="0" Text="配方名称:" Margin="2"/>
        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding SelectedRecipe.Name}" Margin="2"/>
        
        <TextBlock Grid.Row="1" Grid.Column="0" Text="设备名称:" Margin="2"/>
        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding SelectedRecipe.Device.DeviceName}" Margin="2"/>
        
        <TextBlock Grid.Row="2" Grid.Column="0" Text="设备类型:" Margin="2"/>
        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SelectedRecipe.Device.DeviceType}" Margin="2"/>
        
        <TextBlock Grid.Row="3" Grid.Column="0" Text="参数数量:" Margin="2"/>
        <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding SelectedRecipe.NodeParams.Count}" Margin="2"/>
        
        <TextBlock Grid.Row="4" Grid.Column="0" Text="全局变量:" Margin="2"/>
        <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding SelectedRecipe.GlobalVariables.Count}" Margin="2"/>
        
        <TextBlock Grid.Row="5" Grid.Column="0" Text="修改时间:" Margin="2"/>
        <TextBlock Grid.Row="5" Grid.Column="1" Text="{Binding SelectedRecipe.ModifiedTime, StringFormat='yyyy-MM-dd HH:mm:ss'}" Margin="2"/>
    </Grid>
</GroupBox>
```

### 3.3 ViewModel设计

```csharp
public class SolutionConfigurationDialogViewModel : ViewModelBase
{
    private readonly ProjectManager _projectManager;
    private Project? _selectedProject;
    private InspectionRecipe? _selectedRecipe;
    private bool _skipStartupConfig = false;

    public ObservableCollection<Project> Projects { get; }
    
    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                // 项目切换时，选中第一个配方
                SelectedRecipe = value?.Recipes.FirstOrDefault();
            }
        }
    }
    
    public InspectionRecipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set => SetProperty(ref _selectedRecipe, value);
    }
    
    public bool SkipStartupConfig
    {
        get => _skipStartupConfig;
        set => SetProperty(ref _skipStartupConfig, value);
    }
    
    // 命令
    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }
    public ICommand AddRecipeCommand { get; }
    public ICommand DuplicateRecipeCommand { get; }
    public ICommand DeleteRecipeCommand { get; }
    public ICommand SkipConfigurationCommand { get; }
    public ICommand LaunchCommand { get; }
}
```

---

## 4. 主界面集成

### 4.1 主界面调整

当前主界面布局保持不变，新增：

1. **顶部菜单栏**：新增"解决方案"菜单
2. **工具栏**：新增解决方案快捷按钮
3. **状态栏**：显示当前项目/配方信息

### 4.2 菜单栏新增

```xml
<!-- 新增：解决方案菜单 -->
<MenuItem Header="解决方案(_S)">
    <MenuItem Header="项目配置(_C)..." 
              InputGestureText="Ctrl+Shift+C" 
              Command="{Binding OpenSolutionConfigurationCommand}"/>
    <Separator/>
    <MenuItem Header="切换项目(_P)" 
              InputGestureText="Ctrl+Alt+P">
        <MenuItem Header="{Binding CurrentProject.Name, FallbackValue='无'}"
                  Command="{Binding SwitchProjectCommand}"/>
        <Separator/>
        <MenuItem Header="最近使用的项目">
            <!-- 动态生成 -->
        </MenuItem>
    </MenuItem>
    <MenuItem Header="切换配方(_R)" 
              InputGestureText="Ctrl+Alt+R">
        <MenuItem Header="{Binding CurrentRecipe.Name, FallbackValue='无'}"
                  Command="{Binding SwitchRecipeCommand}"/>
        <Separator/>
        <MenuItem Header="最近使用的配方">
            <!-- 动态生成 -->
        </MenuItem>
    </MenuItem>
    <Separator/>
    <MenuItem Header="保存当前项目(_S)" 
              InputGestureText="Ctrl+Shift+S" 
              Command="{Binding SaveCurrentProjectCommand}"/>
    <MenuItem Header="导出项目(_E)..." 
              Command="{Binding ExportProjectCommand}"/>
    <MenuItem Header="导入项目(_I)..." 
              Command="{Binding ImportProjectCommand}"/>
</MenuItem>
```

### 4.3 工具栏新增

```xml
<!-- 新增：解决方案工具栏按钮 -->
<Button ToolTip="项目配置 (Ctrl+Shift+C)" 
        Padding="8,4" 
        Margin="2" 
        Style="{StaticResource SecondaryButton}"
        Command="{Binding OpenSolutionConfigurationCommand}">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="⚙️" FontSize="16" VerticalAlignment="Center"/>
        <TextBlock Text="项目配置" Margin="5,0,0,0" VerticalAlignment="Center"/>
    </StackPanel>
</Button>

<Separator/>

<!-- 当前项目/配方显示 -->
<StackPanel Orientation="Horizontal" Margin="8,0">
    <TextBlock Text="📁" FontSize="16" VerticalAlignment="Center" Margin="0,0,5,0"/>
    <TextBlock Text="{Binding CurrentProject.Name, FallbackValue='无项目'}" 
               VerticalAlignment="Center"
               FontWeight="SemiBold"/>
    <TextBlock Text=" / " 
               VerticalAlignment="Center"
               Foreground="Gray"/>
    <TextBlock Text="{Binding CurrentRecipe.Name, FallbackValue='无配方'}" 
               VerticalAlignment="Center"
               Foreground="DarkBlue"/>
</StackPanel>
```

### 4.4 状态栏新增

```xml
<!-- 新增：状态栏显示 -->
<StatusBarItem>
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="项目: "/>
        <TextBlock Text="{Binding CurrentProject.Name, FallbackValue='无'}" 
                   FontWeight="Bold"/>
        <TextBlock Text="  配方: " Margin="10,0,0,0"/>
        <TextBlock Text="{Binding CurrentRecipe.Name, FallbackValue='无'}" 
                   FontWeight="Bold"/>
        <TextBlock Text="  设备: " Margin="10,0,0,0"/>
        <TextBlock Text="{Binding CurrentRecipe.Device.DeviceName, FallbackValue='无'}"/>
    </StackPanel>
</StatusBarItem>
```

---

## 5. 菜单栏优化

### 5.1 文件菜单调整

```xml
<MenuItem Header="文件(_F)">
    <!-- 新增 -->
    <MenuItem Header="新建项目(_N)..." 
              InputGestureText="Ctrl+N" 
              Command="{Binding NewProjectCommand}"/>
    <MenuItem Header="打开项目(_O)..." 
              InputGestureText="Ctrl+O" 
              Command="{Binding OpenProjectCommand}"/>
    
    <!-- 保留（改为配方） -->
    <MenuItem Header="新建配方(_R)..." 
              InputGestureText="Ctrl+Shift+N" 
              Command="{Binding NewRecipeCommand}"/>
    <MenuItem Header="保存配方(_S)" 
              InputGestureText="Ctrl+S" 
              Command="{Binding SaveRecipeCommand}"/>
    <MenuItem Header="另存配方为(_A)..." 
              InputGestureText="Ctrl+Shift+S" 
              Command="{Binding SaveRecipeAsCommand}"/>
    
    <Separator/>
    
    <!-- 移除 -->
    <!-- <MenuItem Header="导入(_I)" InputGestureText="Ctrl+I"/> -->
    <!-- <MenuItem Header="导出(_E)" InputGestureText="Ctrl+E"/> -->
    
    <!-- 新增 -->
    <MenuItem Header="导入项目(_I)..." 
              Command="{Binding ImportProjectCommand}"/>
    <MenuItem Header="导出项目(_E)..." 
              Command="{Binding ExportProjectCommand}"/>
    
    <Separator/>
    
    <MenuItem Header="最近项目(_R)">
        <!-- 动态生成 -->
    </MenuItem>
    
    <Separator/>
    <MenuItem Header="退出(_X)" InputGestureText="Alt+F4"/>
</MenuItem>
```

### 5.2 视图菜单调整

```xml
<MenuItem Header="视图(_V)">
    <!-- 新增 -->
    <MenuItem Header="项目浏览器(_P)" 
              InputGestureText="Ctrl+Shift+P" 
              Command="{Binding ToggleProjectExplorerCommand}"
              IsChecked="{Binding IsProjectExplorerVisible}"/>
    <MenuItem Header="配方管理(_R)" 
              InputGestureText="Ctrl+Shift+R" 
              Command="{Binding ToggleRecipeManagerCommand}"
              IsChecked="{Binding IsRecipeManagerVisible}"/>
    
    <Separator/>
    
    <!-- 保留 -->
    <MenuItem Header="工具箱(_T)" 
              InputGestureText="Ctrl+T" 
              Command="{Binding ToggleToolboxCommand}"
              IsChecked="{Binding IsToolboxVisible}"/>
    <MenuItem Header="属性面板(_P)" 
              InputGestureText="Ctrl+P" 
              Command="{Binding TogglePropertyPanelCommand}"
              IsChecked="{Binding IsPropertyPanelVisible}"/>
    <MenuItem Header="日志窗口(_L)" 
              InputGestureText="Ctrl+L" 
              Command="{Binding ToggleLogPanelCommand}"
              IsChecked="{Binding IsLogPanelVisible}"/>
    <MenuItem Header="设备管理(_D)" 
              InputGestureText="Ctrl+D" 
              Command="{Binding ToggleDevicePanelCommand}"
              IsChecked="{Binding IsDevicePanelVisible}"/>
    
    <Separator/>
    
    <!-- 保留 -->
    <MenuItem Header="结果预览(_R)"/>
    <MenuItem Header="系统信息(_I)"/>
    
    <Separator/>
    
    <!-- 保留 -->
    <MenuItem Header="显示最大外接矩形(_B)" InputGestureText="Ctrl+B" 
              Command="{Binding ToggleBoundingRectangleCommand}"/>
    <MenuItem Header="显示路径拐点(_P)" InputGestureText="Ctrl+P" 
              Command="{Binding TogglePathPointsCommand}"/>
    
    <Separator/>
    <MenuItem Header="全屏(_F)" InputGestureText="F11" Click="ToggleFullscreen_Click"/>
    <MenuItem Header="重置布局(_R)" Command="{Binding ResetLayoutCommand}"/>
</MenuItem>
```

### 5.3 运行菜单优化

```xml
<MenuItem Header="运行(_R)">
    <!-- 保留 -->
    <MenuItem Header="运行工作流(_R)" 
              InputGestureText="F5" 
              Command="{Binding RunWorkflowCommand}"/>
    <MenuItem Header="停止工作流(_S)" 
              InputGestureText="Shift+F5" 
              Command="{Binding StopWorkflowCommand}"/>
    
    <!-- 新增 -->
    <MenuItem Header="运行配方(_E)" 
              InputGestureText="Ctrl+F5" 
              Command="{Binding RunRecipeCommand}"
              ToolTip="使用当前配方执行完整工作流"/>
    
    <Separator/>
    
    <!-- 保留 -->
    <MenuItem Header="暂停(_P)" InputGestureText="Ctrl+Break"/>
    
    <Separator/>
    
    <!-- 保留 -->
    <MenuItem Header="调试模式(_D)" InputGestureText="Ctrl+Shift+D"/>
    <MenuItem Header="单步执行(_S)" InputGestureText="F10"/>
    <MenuItem Header="断点(_B)" InputGestureText="F9"/>
</MenuItem>
```

---

## 6. 快捷键定义

### 6.1 解决方案相关快捷键

| 功能 | 快捷键 | 说明 |
|------|--------|------|
| 新建项目 | Ctrl+N | 创建新项目 |
| 打开项目 | Ctrl+O | 打开现有项目 |
| 项目配置 | Ctrl+Shift+C | 打开配置界面 |
| 切换项目 | Ctrl+Alt+P | 快速切换项目 |
| 切换配方 | Ctrl+Alt+R | 快速切换配方 |
| 保存项目 | Ctrl+Shift+S | 保存当前项目 |
| 新建配方 | Ctrl+Shift+N | 创建新配方 |
| 保存配方 | Ctrl+S | 保存当前配方 |
| 运行配方 | Ctrl+F5 | 执行配方 |

### 6.2 视图相关快捷键

| 功能 | 快捷键 | 说明 |
|------|--------|------|
| 项目浏览器 | Ctrl+Shift+P | 显示/隐藏项目浏览器 |
| 配方管理 | Ctrl+Shift+R | 显示/隐藏配方管理 |
| 工具箱 | Ctrl+T | 显示/隐藏工具箱 |
| 属性面板 | Ctrl+P | 显示/隐藏属性面板 |
| 日志窗口 | Ctrl+L | 显示/隐藏日志窗口 |
| 设备管理 | Ctrl+D | 显示/隐藏设备管理 |

### 6.3 工作流相关快捷键

| 功能 | 快捷键 | 说明 |
|------|--------|------|
| 运行工作流 | F5 | 执行当前工作流 |
| 停止工作流 | Shift+F5 | 停止执行 |
| 调试模式 | Ctrl+Shift+D | 进入调试模式 |
| 单步执行 | F10 | 单步执行节点 |
| 断点 | F9 | 设置/清除断点 |

---

## 7. UI交互流程

### 7.1 首次启动流程

```
1. 应用启动
   ↓
2. 检查 RuntimeConfig（无最近项目）
   ↓
3. 显示启动配置界面
   ├─ 项目列表：空
   ├─ 配方列表：空
   ├─ 显示提示："没有项目，请新建或打开项目"
   └─ 按钮状态：
      ├─ [新建项目] - 启用
      ├─ [打开项目] - 启用
      ├─ [启动] - 禁用
      └─ [跳过] - 启用
   ↓
4. 用户选择：
   ├─ [新建项目] → 弹出新建项目对话框 → 填写项目信息 → 创建项目
   ├─ [打开项目] → 文件对话框 → 选择项目文件 → 加载项目
   └─ [跳过] → 直接进入主界面（无加载）
   ↓
5. 如果创建/打开项目：
   ├─ 项目列表显示项目
   ├─ 配方列表显示默认配方
   ├─ [启动] - 启用
   ↓
6. 用户点击 [启动]
   ├─ 加载项目到 ProjectManager
   ├─ 设置当前项目和配方
   ├─ 创建 RunContext
   └─ 进入主界面
```

### 7.2 日常启动流程（有最近项目）

```
1. 应用启动
   ↓
2. 检查 RuntimeConfig（有最近项目）
   ↓
3. 显示启动配置界面
   ├─ 项目列表：显示所有项目，选中最近项目
   ├─ 配方列表：显示当前项目的配方，选中最近配方
   ├─ 项目详情：显示选中项目信息
   ├─ 配方详情：显示选中配方信息
   └─ 按钮状态：
      ├─ [新建项目] - 启用
      ├─ [打开项目] - 启用
      ├─ [启动] - 启用
      └─ [跳过] - 启用
      └─ ☑️ 启动时跳过配置界面
   ↓
4. 用户选择：
   ├─ 选择其他项目 → 配方列表更新
   ├─ 选择其他配方 → 配方详情更新
   ├─ [新建项目] → 创建新项目
   ├─ [打开项目] → 打开现有项目
   ├─ [启动] → 加载项目/配方，进入主界面
   ├─ [跳过] → 直接进入主界面
   └─ ☑️ 勾选"跳过配置" → 下次启动直接进入主界面
   ↓
5. 进入主界面后：
   ├─ 菜单栏显示：项目/配方名称
   ├─ 状态栏显示：项目/配方/设备信息
   ├─ 工作流编辑器加载程序节点
   └─ 节点参数加载配方参数
```

### 7.3 主界面切换项目/配方流程

```
主界面
    │
    ├─ 方式1：菜单栏切换
    │   ├─ 解决方案 → 切换项目 → 选择项目
    │   └─ 解决方案 → 切换配方 → 选择配方
    │
    ├─ 方式2：工具栏切换
    │   └─ 点击"项目配置"按钮 → 打开配置界面 → 选择项目/配方 → [启动]
    │
    └─ 方式3：快捷键切换
        ├─ Ctrl+Alt+P → 项目列表弹出 → 选择项目
        └─ Ctrl+Alt+R → 配方列表弹出 → 选择配方
    │
    ↓
加载流程：
1. 保存当前工作流修改（如果有）
2. 加载新项目/配方
3. 更新工作流编辑器（节点、连接）
4. 更新节点参数面板（配方参数）
5. 更新状态栏信息
6. 记录到 RuntimeConfig（最近使用）
```

### 7.4 配方切换流程（< 1秒）

```
当前配方：标准配方
    │
用户操作：切换到产线2配方
    │
    ↓
1. 调用 ProjectManager.SetCurrentRecipe("产线2配方")
    ↓
2. 更新 RuntimeConfig：
   - CurrentRecipe = "产线2配方"
   - 添加到 RecentRecipes
    ↓
3. 通知 UI 更新：
   - 状态栏：配方名称更新
   - 节点参数面板：重新加载节点参数
   - 工作流编辑器：参数值更新（触发节点刷新）
    ↓
4. 完成切换（< 1秒）
    │
    ↓
可执行：按 F5 执行新配方
```

---

## 8. 文件结构

```
src/
├── UI/
│   ├── Views/
│   │   ├── Windows/
│   │   │   ├── SolutionConfigurationDialog.xaml    ⭐ 新建
│   │   │   ├── SolutionConfigurationDialog.xaml.cs ⭐ 新建
│   │   │   ├── NewProjectDialog.xaml              ⭐ 新建
│   │   │   ├── NewProjectDialog.xaml.cs          ⭐ 新建
│   │   │   ├── NewRecipeDialog.xaml               ⭐ 新建
│   │   │   └── NewRecipeDialog.xaml.cs           ⭐ 新建
│   │   │
│   │   └── Controls/
│   │       ├── ProjectExplorer/
│   │       │   ├── ProjectExplorerView.xaml       ⭐ 新建
│   │       │   └── ProjectExplorerView.xaml.cs   ⭐ 新建
│   │       │
│   │       ├── RecipeManager/
│   │       │   ├── RecipeManagerView.xaml        ⭐ 新建
│   │       │   └── RecipeManagerView.xaml.cs    ⭐ 新建
│   │       │
│   │       └── RecipeSwitcher/
│   │           ├── RecipeSwitcherView.xaml      ⭐ 新建
│   │           └── RecipeSwitcherView.xaml.cs  ⭐ 新建
│   │
│   └── ViewModels/
│       ├── SolutionConfigurationDialogViewModel.cs  ⭐ 新建
│       ├── NewProjectDialogViewModel.cs            ⭐ 新建
│       ├── NewRecipeDialogViewModel.cs             ⭐ 新建
│       ├── ProjectExplorerViewModel.cs            ⭐ 新建
│       ├── RecipeManagerViewModel.cs              ⭐ 新建
│       ├── RecipeSwitcherViewModel.cs            ⭐ 新建
│       └── MainWindowViewModel.cs                ✅ 更新
│
└── Workflow/
    └── StartupDecisionService.cs  ⭐ 新建
```

---

## 9. 实施计划

### 阶段一：启动配置界面（1-2天）

1. 创建 `SolutionConfigurationDialog` 界面
2. 创建 `SolutionConfigurationDialogViewModel`
3. 创建 `StartupDecisionService`
4. 集成到 App.xaml.cs 启动流程

### 阶段二：主界面集成（1天）

5. 更新 MainWindow.xaml 菜单栏
6. 更新 MainWindowViewModel
7. 添加项目/配方切换命令

### 阶段三：辅助对话框（1天）

8. 创建 `NewProjectDialog`
9. 创建 `NewRecipeDialog`

### 阶段四：项目浏览器和配方管理（1-2天）

10. 创建 `ProjectExplorerView`
11. 创建 `RecipeManagerView`
12. 集成到主界面侧边栏

### 阶段五：测试和优化（1天）

13. 测试启动流程
14. 测试切换流程
15. 性能优化
16. UI微调

---

## 10. 总结

### 核心特性

- ✅ **启动配置界面**：首次启动或有最近项目时显示
- ✅ **跳过配置**：用户可选择跳过，快速启动
- ✅ **项目/配方切换**：主界面可随时切换
- ✅ **菜单栏优化**：新增解决方案菜单
- ✅ **快捷键支持**：全面的快捷键定义
- ✅ **状态栏显示**：实时显示项目/配方/设备信息

### 用户体验

- **首次使用**：引导配置，流程清晰
- **日常使用**：快速启动，< 1秒切换配方
- **切换灵活**：多种方式切换项目/配方
- **操作便捷**：菜单栏、工具栏、快捷键全覆盖

---

*本文档由 SunEyeVision 开发团队维护*
