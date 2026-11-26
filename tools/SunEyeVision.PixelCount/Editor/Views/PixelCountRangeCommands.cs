using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Tool.PixelCount.Wpf.Views;

/// <summary>
/// 原厂 <c>PixelModuRangeConfigList</c> 的命令条：只负责增减有效区间数量。
/// 与旧版的关键差别——**不持有草稿**：DataContext 就是区间数量那一行（<see cref="FieldRowModel"/>），
/// 增减经 <see cref="FieldRowModel.StepCommand"/> 走宿主的校验与提交通道。
/// 错误提示由平台行模板按 <see cref="FieldRowModel.HasErrorA"/> 显示，工具不绘制任何行内文案。
/// 区间行本身仍是声明字段（32 组固定槽位），工具层不创建任何输入控件。
/// </summary>
public sealed class PixelCountRangeCommands : UserControl
{
    private readonly Button _delete;

    public PixelCountRangeCommands()
    {
        var add = IconButton("add_normal.png", new Thickness(7, 6, 0, 0));
        add.Click += (_, _) => Step(1);
        _delete = IconButton("delete_normal.png", new Thickness(4, 4, 0, 0));
        _delete.Click += (_, _) => Step(-1);
        var commands = new StackPanel { Orientation = Orientation.Horizontal };
        commands.Children.Add(add);
        commands.Children.Add(_delete);
        Content = new StackPanel { Margin = new Thickness(0, 4, 0, 4), Children = { commands } };
        DataContextChanged += (_, _) => Refresh();
    }

    private FieldRowModel? Model => DataContext as FieldRowModel;

    private void Step(int delta)
    {
        if (Model is not { } model) return;
        model.StepCommand.Execute(delta);
        Refresh();
    }

    private void Refresh()
    {
        if (Model is not { } model)
        {
            _delete.Visibility = Visibility.Collapsed;
            return;
        }
        var count = model.CurValueA switch
        {
            long value => value,
            int value => value,
            _ => 1L
        };
        // 原厂 DelEnable：只剩一条时删除按钮不可用。
        _delete.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 图标命令按钮：外观取自公共层 <c>IconCommandButtonStyle</c>，图标资源同样来自平台；
    /// 工具只决定“用哪个图标、放在哪”，不自绘按钮样式（原厂规则）。
    /// </summary>
    private static Button IconButton(string name, Thickness margin) => new()
    {
        Style = Application.Current?.TryFindResource("IconCommandButtonStyle") as Style,
        Margin = margin,
        Content = new Image
        {
            Stretch = Stretch.None,
            Source = new BitmapImage(new Uri(
                $"/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/ParameterPresentation/resources/{name}",
                UriKind.Relative))
        }
    };
}
