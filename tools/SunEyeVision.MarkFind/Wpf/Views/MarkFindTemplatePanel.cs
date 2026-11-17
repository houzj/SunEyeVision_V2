using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Tool.MarkFind.Runtime;

namespace SunEyeVision.Tool.MarkFind.Wpf.Views;

/// <summary>
/// 原件「新建模型 / 修改模型 / 清空 / 导出 / 导入 + 模型预览」面板的迁移
/// （<c>MarkFindConfigPanal.xaml</c>:18–126、<c>MarkFindConfigPanal.xaml.cs</c>:413–474/820–903）。
/// 模板本身由 Runtime 用当前发布图像 + 搜索区域建立，结果写回 <see cref="MarkFindFields.ModelData"/>；
/// 面板不持有第二套模型，也不复制原件加密 .mfxml 文件格式。
/// </summary>
public sealed class MarkFindTemplatePanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly NativeRoiFrameSource? _frames;
    private readonly TextBlock _summary = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    private readonly TextBlock _error = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };

    public MarkFindTemplatePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, NativeRoiFrameSource? frames)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _frames = frames;
        var root = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 6) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("新建模型", CreateTemplate));
        buttons.Children.Add(Button("清空模型", ClearTemplate));
        buttons.Children.Add(Button("导出", ExportTemplate));
        buttons.Children.Add(Button("导入", ImportTemplate));
        root.Children.Add(buttons);
        root.Children.Add(_summary);
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
            _error.Text = "参数页尚未接入调试输入：先对节点执行一次单步调试后再新建模板。";
            return;
        }
        MarkFindSettings settings;
        try { settings = ReadSettings(); }
        catch (Exception failure) { _error.Text = failure.Message; return; }
        NativeRoiInputLease? lease = null;
        try
        {
            lease = _frames.CaptureInputs(new[] { MarkFindSchema.ImageInput, MarkFindSchema.RoiInput });
            if (lease is null || !lease.IsCurrent)
            {
                _error.Text = "请选择与输入一致的已发布图像后再新建模板。";
                return;
            }
            var image = lease.Inputs.Get<IReadOnlyImage>(MarkFindSchema.ImageInput);
            if (image is null)
            {
                _error.Text = "缺少输入图像，无法建立模板。";
                return;
            }
            var roi = lease.Inputs.Contains(MarkFindSchema.RoiInput)
                ? lease.Inputs.Get<IReadOnlyRegion?>(MarkFindSchema.RoiInput)
                : null;
            if (roi is null)
            {
                _error.Text = "请先连接搜索区域 ROI（原件必须框选区域）后再新建模板。";
                return;
            }
            Apply(MarkFindTemplateBuilder.Create(image, roi, settings));
        }
        catch (MarkFindCapabilityException failure) { _error.Text = failure.Message; }
        catch (Exception failure) { _error.Text = "模板创建失败：" + failure.Message; }
        finally { lease?.Dispose(); }
    }

    private void ClearTemplate()
    {
        _error.Text = "";
        Apply("");
    }

    /// <summary>模板参数必须是固定值：模板在编辑期一次性构建，不接受运行期变量来源。</summary>
    private MarkFindSettings ReadSettings()
    {
        var fields = _draft.CaptureFields();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in MarkFindFields.All)
        {
            if (spec.Name == nameof(MarkFindSettings.ModelData)) continue;
            values[spec.Id] = fields.Read(spec.Id) switch
            {
                ConstantSetting constant => constant.Value,
                UnsetSetting => throw new InvalidOperationException("模板参数“" + spec.Label + "”尚未赋值。"),
                _ => throw new InvalidOperationException("模板参数“" + spec.Label + "”必须是固定值，不能绑定引用或表达式。")
            };
        }
        return MarkFindFields.Materialize(new InputValues(MarkFindFields.Schema.Inputs, values));
    }

    // ── 导出 / 导入 ─────────────────────────────────────────────────────

    private void ExportTemplate()
    {
        _error.Text = "";
        var payload = CurrentPayload();
        if (payload.Length == 0)
        {
            _error.Text = "尚未创建模板，无法导出。";
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "标记查找模板(*" + MarkFindTemplate.FileExtension + ")|*" + MarkFindTemplate.FileExtension,
            DefaultExt = MarkFindTemplate.FileExtension,
            FileName = "MarkFindTemplate" + MarkFindTemplate.FileExtension
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, payload, new UTF8Encoding(false));
    }

    private void ImportTemplate()
    {
        _error.Text = "";
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "标记查找模板(*" + MarkFindTemplate.FileExtension + ")|*" + MarkFindTemplate.FileExtension
        };
        if (dialog.ShowDialog() != true) return;
        string payload;
        try { payload = File.ReadAllText(dialog.FileName, Encoding.UTF8); }
        catch (Exception failure) { _error.Text = "读取模板文件失败：" + failure.Message; return; }
        if (!MarkFindTemplate.TryDecode(payload, out _, out var error))
        {
            _error.Text = "模板文件无效：" + error;
            return;
        }
        Apply(payload);
    }

    // ── 状态 ────────────────────────────────────────────────────────────

    private string CurrentPayload()
        => _draft.CaptureFields().Read(MarkFindFields.ModelData) is ConstantSetting { Value: string text } ? text : "";

    private void Apply(string payload)
    {
        try
        {
            _draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
            {
                [MarkFindFields.ModelData] = new ConstantSetting(payload)
            }, candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (ArgumentException failure) { _error.Text = failure.Message; }
        finally { Reload(); }
    }

    private void Reload()
    {
        var payload = CurrentPayload();
        if (payload.Length == 0)
        {
            _summary.Text = "尚未创建模型：连接输入图像与搜索区域后点击“新建模型”。";
            return;
        }
        if (!MarkFindTemplate.TryDecode(payload, out var template, out var error))
        {
            _summary.Text = "模型数据不可用：" + error;
            return;
        }
        _summary.Text = "模型已就绪：" + template!.Describe();
    }
}
