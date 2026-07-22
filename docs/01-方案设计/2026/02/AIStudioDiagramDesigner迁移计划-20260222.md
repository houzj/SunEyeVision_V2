# AIStudio.Wpf.DiagramDesigner Boundary 正交连线迁移计划

## 🎯 项目概述

**目标**：从当前简化 Canvas 实现迁移到 AIStudio.Wpf.DiagramDesigner 原生库，**仅使用 Boundary 正交连线模式**

**核心需求**：
- ✅ 替换 Canvas 为 Diagram 控件
- ✅ 使用 Boundary 模式实现正交连线（A* 网格路由）
- ✅ 保留现有节点拖拽和连线创建功能
- ✅ 启用缩放平移功能
- ✅ 实现对齐吸附
- ✅ 性能优化至支持 500 节点

**预计周期**：**4 周**（从原 6 周优化）

---

## 📅 阶段一：核心集成（第1周）

### 1.1 Diagram 控件基础替换

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 替换 WorkflowCanvasControl | 将 Canvas 替换为 `diagram:Diagram` 控件 | 修改后的 XAML | 控件正常加载，显示空白画布 |
| 配置基础属性 | 设置 `AllowDrop`, `CanZoom`, `CanPan`, `SelectionMode` | 配置代码 | 属性生效，基本交互可用 |
| 数据模型适配 | 将 `WorkflowNode` 适配为 `DiagramNode` | 适配器类 | 节点数据正确绑定 |
| 节点集合绑定 | 实现 `ObservableCollection<DiagramNode>` 绑定 | 绑定代码 | 节点增删实时反映到视图 |

**关键配置**：
```xaml
<diagram:Diagram x:Name="WorkflowDiagram"
                 AllowDrop="True"
                 CanZoom="True"
                 CanPan="True"
                 SelectionMode="Multiple"
                 SnapToGrid="True"
                 GridSize="20" />
```

---

### 1.2 节点模板迁移

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 创建节点模板 | 基于 Diagram 控件创建 `WorkflowNodeTemplate` | ResourceDictionary | 节点样式与当前一致 |
| 端口定义 | 在模板中定义 4 个端口 | Port 组件 | 端口位置和方向正确 |
| 选中状态 | 实现节点选中高亮效果 | VisualState | 选中节点有明显视觉反馈 |
| 拖拽手柄 | 添加节点拖拽手柄（可选） | ControlTemplate | 拖拽体验流畅 |

**节点模板结构**：
```xaml
<DataTemplate x:Key="WorkflowNodeTemplate">
    <Border Width="120" Height="60" 
            BorderThickness="2" 
            CornerRadius="4"
            Background="{Binding StyleConfig.BackgroundColor}">
        <!-- 节点内容 -->
        <Grid>
            <TextBlock Text="{Binding Name}" 
                       HorizontalAlignment="Center" 
                       VerticalAlignment="Center"/>
            
            <!-- 端口定义 -->
            <diagram:Port x:Name="TopPort" Position="Top" 
                          VerticalAlignment="Top" 
                          Margin="0,-10,0,0"/>
            <diagram:Port x:Name="BottomPort" Position="Bottom" 
                          VerticalAlignment="Bottom" 
                          Margin="0,0,0,-10"/>
            <diagram:Port x:Name="LeftPort" Position="Left" 
                          HorizontalAlignment="Left" 
                          Margin="-10,0,0,0"/>
            <diagram:Port x:Name="RightPort" Position="Right" 
                          HorizontalAlignment="Right" 
                          Margin="0,0,-10,0"/>
        </Grid>
    </Border>
</DataTemplate>
```

---

### 1.3 Boundary 路径生成器集成

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 创建 Edge 模型 | 实现 `WorkflowEdge` 类包装 Diagram.Edge | C# 类文件 | Edge 模型结构合理 |
| 配置 Boundary 模式 | 为 Diagram 设置 `PathGenerator="Boundary"` | XAML 配置 | 连线使用正交路由 |
| 端口映射 | 实现 WorkflowConnection 到 Diagram.Edge 的映射 | 映射逻辑 | 连线创建正确 |
| EdgeCollection 管理 | 管理连线集合，响应节点变化 | 集合管理代码 | 连线增删改正确 |

**Boundary 配置**：
```xaml
<diagram:Diagram x:Name="WorkflowDiagram">
    <diagram:Diagram.Resources>
        <!-- 配置全局 Boundary 路径生成器 -->
        <Style TargetType="diagram:Edge">
            <Setter Property="PathGenerator" Value="Boundary"/>
            <Setter Property="StrokeThickness" Value="2"/>
            <Setter Property="Stroke" Value="#666666"/>
        </Style>
    </diagram:Diagram.Resources>
</diagram:Diagram>
```

---

### 1.4 拖放功能迁移

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 工具箱拖放 | 实现从工具箱拖拽节点到画布 | 拖放事件处理 | 拖放流程完整 |
| 端口连线拖放 | 实现从端口拖拽创建连线 | 连线创建逻辑 | 连线创建准确 |
| 拖拽位置计算 | 坐标转换（屏幕 → 画布） | 坐标转换方法 | 位置计算准确 |
| 拖拽视觉反馈 | 添加拖拽过程中的视觉提示 | 视觉效果 | 体验流畅自然 |

---

### 1.5 基础测试

| 测试项 | 测试内容 | 通过标准 |
|--------|---------|----------|
| 控件加载 | Diagram 控件正常加载 | 无报错，画布空白 |
| 节点添加 | 拖拽添加 10 个节点 | 所有节点正确显示 |
| 节点移动 | 拖拽移动节点 | 移动流畅，位置准确 |
| 连线创建 | 创建 5 条连线 | 连线使用正交路由，无交叉 |
| 基础性能 | 20 节点 + 15 连线场景 | 响应时间 < 200ms |

---

## 📅 阶段二：交互优化（第2周）

### 2.1 缩放和平移

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 配置缩放范围 | 设置 `MinZoom=0.25`, `MaxZoom=4.0` | 配置代码 | 缩放范围正确 |
| 滚轮缩放 | 实现鼠标滚轮缩放 | 事件处理 | 缩放流畅，以鼠标为中心 |
| 空格拖拽平移 | 实现空格+鼠标拖拽平移 | 事件处理 | 平移流畅 |
| 缩放指示器 | 添加缩放比例显示 UI | XAML 组件 | 缩放比例实时更新 |
| 自适应按钮 | 实现"适应画布"和"100%"按钮 | 功能代码 | 一键适应视图 |

**配置代码**：
```xaml
<diagram:Diagram x:Name="WorkflowDiagram"
                 CanZoom="True"
                 MinZoom="0.25"
                 MaxZoom="4.0"
                 CanPan="True"
                 PanModifierKeys="Space"/>
```

---

### 2.2 对齐和吸附

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 网格吸附 | 启用 `SnapToGrid="True"`，设置网格大小 20px | 配置代码 | 拖拽时自动对齐网格 |
| 节点对齐 | 启用 `SnapToNode="True"` | 配置代码 | 拖拽时显示对齐辅助线 |
| 边缘吸附 | 实现节点边缘自动吸附（距离 ≤5px） | 吸附逻辑 | 节点自动对齐边缘 |
| 居中吸附 | 显示水平和垂直中心线 | 辅助线显示 | 拖拽时显示中心对齐 |
| 对齐工具栏 | 添加"左对齐""上对齐"等按钮 | 工具栏 | 对齐操作正确 |

---

### 2.3 撤销重做系统

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| Command 基类 | 定义 `IWorkflowCommand` 接口 | 接口文件 | 接口设计合理 |
| 节点命令 | 实现 `AddNodeCommand`, `MoveNodeCommand`, `DeleteNodeCommand` | 命令类 | 每个命令可撤销 |
| 连线命令 | 实现 `AddEdgeCommand`, `DeleteEdgeCommand` | 命令类 | 连线操作可撤销 |
| UndoManager | 实现命令栈管理器 | 管理器类 | Undo/Redo 正确 |
| 快捷键绑定 | 绑定 Ctrl+Z / Ctrl+Y | 事件处理 | 快捷键响应正确 |

---

### 2.4 交互测试

| 测试项 | 测试内容 | 通过标准 |
|--------|---------|----------|
| 缩放测试 | 各种缩放级别下操作 | 渲染正常，无卡顿 |
| 平移测试 | 空格拖拽平移 | 平移流畅 |
| 对齐测试 | 网格吸附准确性 | 吸附误差 < 2px |
| 撤销重做测试 | 连续 20 次操作 | 状态完全恢复 |

---

## 📅 阶段三：性能优化（第3周）

### 3.1 虚拟化渲染

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 节点虚拟化 | 启用 `VirtualizingMode="Recycling"` | 配置代码 | 视口外节点不渲染 |
| 连线虚拟化 | 只渲染可见区域内的连线 | 渲染优化 | 大场景下不卡顿 |
| 懒加载 | 滚动到视图时才加载节点 | 加载逻辑 | 首次加载 < 3 秒 |

---

### 3.2 批量更新机制

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| BatchUpdateManager | 实现批量更新管理器 | 管理器类 | 减少刷新次数 |
| 拖拽优化 | 拖拽时延迟更新连线 | 优化代码 | 拖拽流畅度提升 50% |
| 批量操作 | 实现 `SuspendLayout/ResumeLayout` | 扩展方法 | 批量操作性能提升 |

---

### 3.3 Boundary 路径优化

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 网格大小调优 | 根据节点尺寸调整网格大小（20px → 30px） | 配置优化 | 路径更优 |
| A* 权重调优 | 优化 A* 搜索的启发函数 | 算法优化 | 路径计算更快 |
| 路径缓存 | 缓存未变化节点的连线路径 | 缓存机制 | 减少重复计算 |
| 延迟计算 | 只在节点拖拽结束时重新计算路径 | 延迟机制 | 拖拽更流畅 |

---

### 3.4 渲染优化

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 节点模板简化 | 减少 Visual Tree 层级 | 优化模板 | 渲染性能提升 30% |
| GPU 加速 | 启用 `BitmapCacheEffect` | 配置代码 | 帧率稳定 60fps |
| 连线批处理 | 批量更新连线几何 | 批处理代码 | 连线渲染更快 |

---

### 3.5 性能基准测试

| 场景 | 节点数 | 连线数 | 性能指标 | 目标 |
|------|-------|-------|----------|------|
| 小型 | 50 | 30 | 响应时间 | < 100ms |
| 中型 | 200 | 150 | 响应时间 | < 300ms |
| 大型 | 500 | 400 | 响应时间 | < 800ms |
| 超大型 | 1000 | 800 | 响应时间 | < 2000ms |

---

## 📅 阶段四：体验优化（第4周）

### 4.1 节点类型扩展

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 流程节点 | 创建带端口的流程节点模板 | 节点模板 | 样式区分明显 |
| 判断节点 | 创建菱形判断节点模板 | 节点模板 | 形状正确 |
| 开始/结束节点 | 创建圆角矩形开始/结束节点 | 节点模板 | 样式符合规范 |

---

### 4.2 用户体验增强

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 右键菜单 | 添加节点右键菜单 | 菜单资源 | 功能正确 |
| 快捷键 | 完善快捷键映射 | 配置文件 | 响应正确 |
| 搜索功能 | 实现节点搜索和高亮 | 搜索 UI | 结果准确 |
| 导出功能 | 导出为图片、JSON | 导出模块 | 数据完整 |

---

### 4.3 最终测试和文档

| 任务 | 工作内容 | 输出物 | 验收标准 |
|------|---------|--------|----------|
| 完整功能测试 | 端到端功能测试 | 测试报告 | 所有功能正常 |
| 压力测试 | 1000 节点场景 | 性能报告 | 性能达标 |
| 用户验收 | 产品团队验收 | 验收报告 | 认可质量 |
| 文档编写 | API 文档和使用指南 | 文档 | 文档完整 |

---

## 🎯 里程碑和时间线

| 里程碑 | 完成时间 | 交付物 |
|--------|---------|--------|
| M1: 核心集成完成 | 第1周结束 | Diagram 控件 + Boundary 连线 |
| M2: 交互优化完成 | 第2周结束 | 缩放平移、对齐、撤销重做 |
| M3: 性能达标 | 第3周结束 | 支持 500 节点流畅运行 |
| M4: 体验优化完成 | 第4周结束 | 所有功能验收通过 |

---

## ⚠️ 风险和应对

| 风险 | 影响 | 概率 | 应对措施 |
|------|------|------|----------|
| Boundary 路径不符合预期 | 需要调整算法 | 中 | 提前测试，准备自定义路由 |
| 性能不达标 | 用户体验差 | 低 | 预留优化时间，考虑分层加载 |
| 端口映射复杂 | 实现困难 | 中 | 参考官方示例，准备简化方案 |
| 兼容性问题 | 现有功能破坏 | 低 | 充分测试，保持向后兼容 |

---

## ✅ 成功标准

- ✅ Diagram 控件成功替换 Canvas
- ✅ Boundary 正交连线正常工作，支持避障
- ✅ 支持缩放平移、对齐吸附、撤销重做
- ✅ 性能达标：500 节点 + 500 连线流畅运行（60fps）
- ✅ 核心功能稳定无 Bug
- ✅ 单元测试覆盖率 > 70%
- ✅ 用户验收通过

---

## 📊 优化对比

| 项目 | 原计划 | 优化计划 | 改进 |
|------|--------|---------|------|
| **周期** | 6 周 | **4 周** | 减少 33% |
| **路径算法** | 4 种（Smooth/Straight/Boundary/Corner） | **1 种（Boundary）** | 聚焦核心需求 |
| **主题系统** | 包含 | **删除** | 减少不必要工作 |
| **插件系统** | 包含 | **删除** | 简化架构 |
| **连线样式** | 多种样式配置 | **基础样式** | 减少复杂度 |
| **连线动画** | 包含 | **删除** | 简化实现 |
| **重点** | 全面功能 | **性能和用户体验** | 提升质量 |

---

## 📝 附录：关键配置示例

### Boundary 路径配置

```xaml
<diagram:Diagram x:Name="WorkflowDiagram"
                 AllowDrop="True"
                 CanZoom="True"
                 MinZoom="0.25"
                 MaxZoom="4.0"
                 CanPan="True"
                 PanModifierKeys="Space"
                 SelectionMode="Multiple"
                 SnapToGrid="True"
                 GridSize="20"
                 SnapToNode="True"
                 VirtualizingMode="Recycling">
    
    <!-- Boundary 路径生成器配置 -->
    <diagram:Diagram.Resources>
        <Style TargetType="diagram:Edge">
            <Setter Property="PathGenerator" Value="Boundary"/>
            <Setter Property="StrokeThickness" Value="2"/>
            <Setter Property="Stroke" Value="#666666"/>
            <Setter Property="ArrowSize" Value="15"/>
        </Style>
    </diagram:Diagram.Resources>
    
    <!-- 节点模板 -->
    <diagram:Diagram.NodeTemplate>
        <DataTemplate>
            <Border Width="120" Height="60" 
                    BorderThickness="2" 
                    CornerRadius="4"
                    Background="White"
                    BorderBrush="#CCCCCC">
                <Grid>
                    <TextBlock Text="{Binding Name}" 
                               HorizontalAlignment="Center" 
                               VerticalAlignment="Center"
                               FontSize="12"/>
                    
                    <!-- 端口 -->
                    <diagram:Port x:Name="TopPort" Position="Top"/>
                    <diagram:Port x:Name="BottomPort" Position="Bottom"/>
                    <diagram:Port x:Name="LeftPort" Position="Left"/>
                    <diagram:Port x:Name="RightPort" Position="Right"/>
                </Grid>
            </Border>
        </DataTemplate>
    </diagram:Diagram.NodeTemplate>
</diagram:Diagram>
```

---

**文档版本**：v2.0 (Boundary 优化版)  
**创建日期**：2026年2月  
**负责人**：开发团队
