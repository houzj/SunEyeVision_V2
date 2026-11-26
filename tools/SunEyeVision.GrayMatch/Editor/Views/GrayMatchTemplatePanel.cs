using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Tool.GrayMatch.Runtime;

namespace SunEyeVision.Tool.GrayMatch.Wpf.Views;

/// <summary>
/// 原件「创建 / 载入 / 编辑模板 / 删除所有模板 + 模型列表与预览」面板的迁移
/// （<c>FeatureMatchCommonControl/base/featurematchconfigpanal.xaml</c>、
/// <c>FeatureMatchCommonControl/FeatureMatchConfigPanal.cs</c>）。
/// 原件把灰度模板存成模块字节数据并导出 <c>*.gmvaxml</c>；本工具模板由 Runtime 用当前发布图像 + 搜索区域建立，
/// 结果写回 <see cref="GrayMatchFields.ModelData"/>，面板不持有第二套模型。
/// </summary>
public sealed class GrayMatchTemplatePanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly NativeRoiFrameSource? _frames;
    private readonly TextBlock _summary = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
    private readonly ListBox _modelList = new() { Height = 86, Margin = new Thickness(0, 4, 6, 0) };
    private readonly Button _editButton;

    public GrayMatchTemplatePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, NativeRoiFrameSource? frames)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _frames = frames;
        var root = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 6) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("创建", CreateTemplate));
        buttons.Children.Add(Button("载入", ImportTemplate));
        _editButton = Button("编辑模板", CreateTemplate);
        _editButton.IsEnabled = false;
        buttons.Children.Add(_editButton);
        buttons.Children.Add(Button("删除所有模板", ClearTemplate));
        root.Children.Add(buttons);

        var modelArea = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        modelArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        modelArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_modelList, 0);
        modelArea.Children.Add(_modelList);
        var preview = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(6),
            Child = _summary
        };
        Grid.SetColumn(preview, 1);
        modelArea.Children.Add(preview);
        root.Children.Add(modelArea);

        var fileButtons = new StackPanel { Orientation = Orientation.Horizontal };
        fileButtons.Children.Add(Button("保存模型工程", ExportTemplate));
        fileButtons.Children.Add(Button("载入模型工程", ImportTemplate));
        root.Children.Add(fileButtons);
        root.Children.Add(_error);
        Content = root;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private static Button Button(string caption, Action action)
    {
        var button = new Button
        {
            Content = caption,
            Margin = new Thickness(0, 0, 6, 4),
            Padding = new Thickness(10, 2, 10, 2),
            MinWidth = 76
        };
        button.Click += (_, _) => action();
        return button;
    }

    // ── 新建 / 清空 ─────────────────────────────────────────────────────

    private void CreateTemplate()
    {
        _error.Text = "";
        if (_frames is null)
        {
            _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再新建模型。";
            return;
        }
        GrayMatchSettings settings;
        try { settings = ReadSettings(); }
        catch (Exception failure) { _error.Text = failure.Message; return; }
        NativeRoiInputLease? lease = null;
        try
        {
            lease = _frames.CaptureInputs(new[] { GrayMatchSchema.ImageInput, GrayMatchSchema.RoiInput });
            if (lease is null || !lease.IsCurrent)
            {
                _error.Text = "请选择与输入一致的已发布图像后再新建模型。";
                return;
            }
            var image = lease.Inputs.Get<IReadOnlyImage>(GrayMatchSchema.ImageInput);
            if (image is null)
            {
                _error.Text = "缺少输入图像，无法建立模型。";
                return;
            }
            var roi = lease.Inputs.Contains(GrayMatchSchema.RoiInput)
                ? lease.Inputs.Get<IReadOnlyRegion?>(GrayMatchSchema.RoiInput)
                : null;
            if (roi is null)
            {
                _error.Text = "请先连接搜索区域 ROI（原件必须框选区域）后再新建模型。";
                return;
            }
            Apply(GrayMatchTemplateBuilder.Create(image, roi, settings));
        }
        catch (GrayMatchCapabilityException failure) { _error.Text = failure.Message; }
        catch (Exception failure) { _error.Text = "模型创建失败：" + failure.Message; }
        finally { lease?.Dispose(); }
    }

    private void ClearTemplate()
    {
        _error.Text = "";
        Apply("");
    }

    /// <summary>模板参数必须是固定值：模型在编辑期一次性构建，不接受运行期变量来源。</summary>
    private GrayMatchSettings ReadSettings()
    {
        var fields = _draft.CaptureFields();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in GrayMatchFields.All)
        {
            if (spec.Name == nameof(GrayMatchSettings.ModelData)) continue;
            values[spec.Id] = fields.Read(spec.Id) switch
            {
                ConstantSetting constant => constant.Value,
                UnsetSetting => throw new InvalidOperationException("模板参数“" + spec.Label + "”尚未赋值。"),
                _ => throw new InvalidOperationException("模板参数“" + spec.Label + "”必须是固定值，不能绑定引用或表达式。")
            };
        }
        // 声明与往返由工具基类统一承担（工具不再自建 Schema/Materialize）。
        var tool = new GrayMatchTool();
        return (GrayMatchSettings)tool.MaterializeSettings(new InputValues(tool.SettingsSchema.Inputs, values));
    }

    // ── 导出 / 导入 ─────────────────────────────────────────────────────

    private void ExportTemplate()
    {
        _error.Text = "";
        var payload = CurrentPayload();
        if (payload.Length == 0)
        {
            _error.Text = "尚未创建模型，无法导出。";
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "灰度匹配模型(*" + GrayMatchTemplate.FileExtension + ")|*" + GrayMatchTemplate.FileExtension,
            DefaultExt = GrayMatchTemplate.FileExtension,
            FileName = "GrayMatchTemplate" + GrayMatchTemplate.FileExtension
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, payload, new UTF8Encoding(false));
    }

    private void ImportTemplate()
    {
        _error.Text = "";
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "灰度匹配模型(*" + GrayMatchTemplate.FileExtension + ")|*" + GrayMatchTemplate.FileExtension
        };
        if (dialog.ShowDialog() != true) return;
        string payload;
        try { payload = File.ReadAllText(dialog.FileName, Encoding.UTF8); }
        catch (Exception failure) { _error.Text = "读取模型文件失败：" + failure.Message; return; }
        if (!GrayMatchTemplate.TryDecode(payload, out _, out var error))
        {
            _error.Text = "模型文件无效：" + error;
            return;
        }
        Apply(payload);
    }

    // ── 状态 ────────────────────────────────────────────────────────────

    private string CurrentPayload()
        => _draft.CaptureFields().Read(GrayMatchFields.ModelData) is ConstantSetting { Value: string text } ? text : "";

    private void Apply(string payload)
    {
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [GrayMatchFields.ModelData] = new ConstantSetting(payload)
            }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (ArgumentException failure) { _error.Text = failure.Message; }
        finally { Reload(); }
    }

    private void Reload()
    {
        var payload = CurrentPayload();
        _modelList.Items.Clear();
        if (payload.Length == 0)
        {
            _summary.Text = "暂无模板";
            return;
        }
        _modelList.Items.Add("灰度模板");
        if (!GrayMatchTemplate.TryDecode(payload, out var template, out var error))
        {
            _summary.Text = "模型数据不可用：" + error;
            return;
        }
        _summary.Text = "模型已就绪：" + template!.Describe();
    }
}
