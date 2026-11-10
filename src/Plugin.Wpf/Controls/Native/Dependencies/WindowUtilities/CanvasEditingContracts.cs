using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Service;

/// <summary>
/// 工具箱数据契约：原件在工具箱与图形页之间共享同一份视图数据。
/// </summary>
public interface IToolBoxData
{
    /// <summary>临时存放被拖拽项的标记，用于拖拽期间清除选中态。</summary>
    object? Tag { get; set; }

    /// <summary>工具箱根目录。</summary>
    string DirPath { get; }
}

/// <summary>
/// 工具箱视图数据：单例共享，承载工具项集合与展开命令。
/// </summary>
public sealed class ToolBoxData : IToolBoxData
{
    private static ToolBoxData? _instance;

    private readonly ObservableCollection<object> _items = new();

    private ToolBoxData()
    {
    }

    /// <summary>取得共享实例。</summary>
    public static ToolBoxData GetInstance() => _instance ??= new ToolBoxData();

    public object? Tag { get; set; }

    /// <summary>工具箱根目录，宿主启动时注入。</summary>
    public string DirPath { get; set; } = string.Empty;

    /// <summary>展开或折叠工具箱的命令。</summary>
    public ICommand? ExpandCommand { get; set; }

    /// <summary>附加信息，供宿主存放与工具相关的归属数据。</summary>
    public object? Info { get; set; }

    /// <summary>当前工具箱项集合。</summary>
    public ObservableCollection<object> Items => _items;

    /// <summary>加载完成通知，宿主重载工具项后触发。</summary>
    public event EventHandler? LoadRequested;

    /// <summary>加入一个工具箱项。</summary>
    public void AddNewItem(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    /// <summary>触发重载通知。</summary>
    public void UpdateLoad() => LoadRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// 画布批注数据：批注框的位置、尺寸与显示层级。
/// </summary>
public sealed class VMCommentInfo
{
    /// <summary>批注标识。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>批注文字。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>批注宽。</summary>
    public double Width { get; set; }

    /// <summary>批注高。</summary>
    public double Height { get; set; }

    /// <summary>批注左上角位置，画布坐标。</summary>
    public Point OriginPoint { get; set; }

    /// <summary>显示层级。</summary>
    public int ZIndex { get; set; }
}
