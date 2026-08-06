# RegionEditorControl 改造完成说明

## 改造要点

### 1. 架构变更
- ✅ **移除ImageControl**：不再包含图像显示区域，改为引用主界面的ImageControl
- ✅ **垂直布局**：改为垂直堆叠布局，适合侧边栏面板
- ✅ **职责分离**：RegionEditorControl只负责UI编辑，不负责数据持久化

### 2. 新增面板
- **DrawingParameterPanel**: 绘制参数面板（占位符）
- **RegionSubscribePanel**: 区域订阅面板（占位符）
- **ParameterPanel**: 已有，参数订阅面板

### 3. 布局结构
```
垂直堆叠布局
├── 输入类型选择（绘制/订阅）
├── 图形类型选择（矩形/圆形/旋转矩形/直线）
├── 输入方式选择（仅订阅模式：按区域/按参数）
├── 参数面板（根据模式动态显示）
│   ├── 绘制参数面板
│   ├── 区域订阅面板
│   └── 参数订阅面板
├── 区域列表
└── 选中区域详细信息
```

### 4. 核心API

#### RegionEditorControl 新增方法

```csharp
/// <summary>
/// 设置主界面ImageControl引用
/// </summary>
public void SetMainImageControl(ImageControl imageControl)

/// <summary>
/// 清理主界面覆盖层
/// </summary>
public void ClearMainOverlay()

/// <summary>
/// 区域数据变更事件
/// </summary>
public event EventHandler<RegionData>? RegionDataChanged;
```

## 集成示例

### 在ToolNode中使用

```csharp
public class RegionEditorToolNode : IToolNode
{
    private RegionEditorControl? _regionEditorControl;
    private ImageControl? _mainImageControl;
    private List<RegionData> _regions = new();

    /// <summary>
    /// 双击节点打开调试窗口
    /// </summary>
    public void OpenDebugWindow(Window owner, ImageControl mainImageControl)
    {
        _mainImageControl = mainImageControl;
        
        var debugWindow = new Window
        {
            Title = "区域编辑器",
            Width = 340,
            Height = 600,
            Owner = owner,
            Content = CreateRegionEditor()
        };

        debugWindow.Closed += OnDebugWindowClosed;
        debugWindow.Show();
    }

    private RegionEditorControl CreateRegionEditor()
    {
        _regionEditorControl = new RegionEditorControl();
        
        // 设置主界面ImageControl引用
        _regionEditorControl.SetMainImageControl(_mainImageControl);
        
        // 加载已有区域数据
        _regionEditorControl.SetRegions(_regions);
        
        // 订阅区域变更事件
        _regionEditorControl.RegionDataChanged += OnRegionDataChanged;
        
        return _regionEditorControl;
    }

    private void OnRegionDataChanged(object? sender, RegionData region)
    {
        // 实时保存区域数据
        SaveRegions();
    }

    private void OnDebugWindowClosed(object? sender, EventArgs e)
    {
        if (_regionEditorControl != null)
        {
            // 取消订阅事件
            _regionEditorControl.RegionDataChanged -= OnRegionDataChanged;
            
            // 清理主界面覆盖层
            _regionEditorControl.ClearMainOverlay();
            
            // 获取编辑后的区域数据
            _regions = _regionEditorControl.GetRegions().ToList();
            
            // 保存数据
            SaveRegions();
            
            _regionEditorControl = null;
        }
    }

    private void SaveRegions()
    {
        // ToolNode负责数据持久化
        // 例如：保存到文件、数据库或工作流配置
    }
}
```

### 在主窗口中集成

```csharp
public class MainWindowViewModel
{
    public ImageControl MainImageControl { get; }

    public void OpenRegionEditor()
    {
        var toolNode = new RegionEditorToolNode();
        toolNode.OpenDebugWindow(Application.Current.MainWindow, MainImageControl);
    }
}
```

## ViewModel新增属性

### 模式相关属性

```csharp
// 模式切换
public bool IsDrawingMode { get; set; }
public bool IsSubscribeMode { get; set; }
public bool IsSubscribeByRegion { get; set; }
public bool IsSubscribeByParameter { get; set; }

// 图形类型选择
public bool IsRectangleSelected { get; set; }
public bool IsCircleSelected { get; set; }
public bool IsRotatedRectangleSelected { get; set; }
public bool IsLineSelected { get; set; }

// 当前状态
public ShapeType CurrentShapeType { get; set; }
public bool ShowShapeTypeSelector { get; }
```

## 后续工作

### 待完善的面板
1. **DrawingParameterPanel**: 需要完善绘制参数编辑功能
   - 中心坐标编辑
   - 宽高编辑
   - 角度编辑（旋转矩形）
   - 半径编辑（圆形）

2. **RegionSubscribePanel**: 需要完善节点选择功能
   - 节点浏览器
   - 输出端口选择
   - 实时预览

3. **ParameterPanel**: 已有基础实现，需要测试

### 测试要点
1. ✅ 编译通过
2. ⏳ 运行时测试
   - ImageControl引用传递
   - 覆盖层渲染
   - 绘制功能
   - 选择/拖动功能
   - 模式切换
3. ⏳ 数据持久化测试
   - 区域保存
   - 区域加载
   - 变更事件触发

## 编译状态

✅ **编译通过**，无错误，仅有少量代码风格提示（HINT级别）

## 注意事项

1. **实时编辑模式**：所有编辑操作立即生效，关闭窗口时自动保存
2. **职责分离**：RegionEditorControl不负责保存，由ToolNode负责
3. **资源清理**：窗口关闭时必须调用ClearMainOverlay()清理主界面覆盖层
4. **ImageControl引用**：必须通过SetMainImageControl()设置引用才能进行绘制操作
