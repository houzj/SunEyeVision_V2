using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>一条命令按钮声明：值、本地化标签键和图标 URI 都由消费者提供，控件本身不持有工具语义。</summary>
public sealed record NativeCommandOption(string Value, string LabelKey, string IconUri);

public sealed class NativeCommandSelectedEventArgs(string value) : EventArgs
{
    public string Value { get; } = value;
}

/// <summary>
/// 原厂 <c>CommandSelecter_Frame</c> 的共享控件：两枚命令按钮 + 已统计图像数量 + 清空按钮。
/// 工具只提供命令值、标签键、图标和清空值，不自行拼装按钮。
/// </summary>
public sealed partial class NativeCommandSelector : UserControl
{
    private readonly RadioButton[] _commands;
    private NativeCommandOption[] _options = [];
    private string? _selectedValue;
    private string _countLabelKey = string.Empty;
    private bool _loading;

    public NativeCommandSelector()
    {
        InitializeComponent();
        _commands = new[] { FirstCommand, SecondCommand };
        FirstCommand.Checked += OnCommandChecked;
        SecondCommand.Checked += OnCommandChecked;
        ClearCommand.Checked += OnClearChecked;
        ClearCommand.IsEnabled = false;
        CountValue.Text = "—";
    }

    /// <summary>命令按钮声明，按原厂模板固定两枚。</summary>
    public IReadOnlyList<NativeCommandOption> Options
    {
        get => _options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count != _commands.Length)
                throw new ArgumentException($"命令选择器需要 {_commands.Length} 项命令声明，实际 {value.Count} 项。", nameof(value));
            _options = value.ToArray();
            for (var index = 0; index < _commands.Length; index++) ApplyOption(_commands[index], _options[index]);
        }
    }

    /// <summary>已统计图像数量标题的本地化键。</summary>
    public string? CountLabelKey
    {
        get => _countLabelKey;
        set
        {
            _countLabelKey = value ?? string.Empty;
            CountLabel.Text = string.IsNullOrEmpty(_countLabelKey) ? string.Empty : Localize(_countLabelKey);
        }
    }

    /// <summary>清空按钮提交的值。</summary>
    public string ClearValue { get; set; } = "3";

    public string? SelectedValue
    {
        get => _selectedValue;
        private set => _selectedValue = value;
    }

    public bool IsClearEnabled
    {
        get => ClearCommand.IsEnabled;
        set => ClearCommand.IsEnabled = value;
    }

    public event EventHandler<NativeCommandSelectedEventArgs>? CommandSelected;

    /// <summary>同步命令选中态，不触发 CommandSelected。</summary>
    public void SetSelected(string? value)
    {
        _loading = true;
        try
        {
            SelectedValue = value;
            for (var index = 0; index < _commands.Length; index++)
                _commands[index].IsChecked = value is not null && string.Equals(_options[index].Value, value, StringComparison.Ordinal);
        }
        finally { _loading = false; }
    }

    /// <summary>更新运行数量显示与清空可用状态。</summary>
    public void SetCount(long? count)
    {
        CountValue.Text = (count ?? 0).ToString();
        IsClearEnabled = true;
    }

    private void ApplyOption(RadioButton button, NativeCommandOption option)
    {
        var icon = new Image
        {
            Width = 46,
            Height = 34,
            Stretch = Stretch.None,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 9, 0, 0),
            Source = new BitmapImage(new Uri(option.IconUri, UriKind.Absolute))
        };
        var label = new TextBlock
        {
            Text = Localize(option.LabelKey),
            ToolTip = Localize(option.LabelKey),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            FontSize = 12,
            Margin = new Thickness(0, 1, 0, 1),
            Height = 24
        };
        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(icon);
        content.Children.Add(label);
        button.Content = content;
        button.Tag = option.Value;
        button.GroupName = "NativeCommandSelector";
    }

    private void OnCommandChecked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton { Tag: string value }) return;
        SelectedValue = value;
        CommandSelected?.Invoke(this, new NativeCommandSelectedEventArgs(value));
    }

    private void OnClearChecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        // 清空是瞬时动作：不保留单选态，恢复当前命令的选中标记。
        _loading = true;
        try
        {
            ClearCommand.IsChecked = false;
            SetSelected(SelectedValue);
        }
        finally { _loading = false; }
        CommandSelected?.Invoke(this, new NativeCommandSelectedEventArgs(ClearValue));
    }

    private string Localize(string key) => TryFindResource(key) as string ?? key;
}
