using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class OpcUaDataPointDialog : ThemeDialogWindow
{
    private readonly string? _editingId;
    public OpcUaDataPoint? Result { get; private set; }

    public OpcUaDataPointDialog() => InitializeComponent();

    public OpcUaDataPointDialog(OpcUaDataPoint point) : this()
    {
        _editingId = point.Id;
        Title = "编辑 OPC UA 数据点";
        ConfirmButton.Content = "保存";
        NamespaceBox.Text = point.NamespaceIndex.ToString();
        TagBox.Text = point.TagName;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PreviewBox == null || NamespaceBox == null || TagBox == null) return;
        PreviewBox.Text = ushort.TryParse(NamespaceBox.Text, out var ns) &&
            OpcUaDataPointInput.TryParse(ns, TagBox.Text, out var point, out _)
                ? point.NodeId : string.Empty;
        if (ErrorText != null) ErrorText.Text = string.Empty;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!ushort.TryParse(NamespaceBox.Text, out var ns))
        {
            ErrorText.Text = "命名空间索引须为 0–65535 的整数。";
            return;
        }
        if (!OpcUaDataPointInput.TryParse(ns, TagBox.Text, out var point, out var error))
        {
            ErrorText.Text = error;
            return;
        }
        if (point.TagName != point.TagName.Trim() && MessageBox.Show(this,
                "标签名称首尾含空格。空格将原样用于 NodeId，确定继续吗？", "确认标签名称",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        point.Id = _editingId ?? Guid.NewGuid().ToString();
        Result = point;
        DialogResult = true;
    }
}

internal static class OpcUaDataPointInput
{
    internal static bool TryParse(ushort namespaceIndex, string? text, out OpcUaDataPoint point, out string error)
    {
        point = new OpcUaDataPoint { NamespaceIndex = namespaceIndex };
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "请输入标签名称或完整的字符串 NodeId。";
            return false;
        }

        string tagName;
        if (text.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = text.IndexOf(";s=", StringComparison.OrdinalIgnoreCase);
            if (separator < 0 || !ushort.TryParse(text.AsSpan(3, separator - 3), out var pastedNamespace))
            {
                error = "完整 NodeId 格式应为 ns=数字;s=标签名称。";
                return false;
            }
            if (pastedNamespace != namespaceIndex)
            {
                error = $"NodeId 中的命名空间索引 {pastedNamespace} 与上方的 {namespaceIndex} 不一致。";
                return false;
            }
            tagName = text[(separator + 3)..];
        }
        else
        {
            tagName = text.StartsWith("s=", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            error = "字符串 NodeId 的标签名称不能为空。";
            return false;
        }
        point.TagName = tagName;
        return true;
    }
}
