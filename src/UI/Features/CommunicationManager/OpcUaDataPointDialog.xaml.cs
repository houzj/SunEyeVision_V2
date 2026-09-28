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
        PreviewBox.Text = ushort.TryParse(NamespaceBox.Text, out var ns) && TagBox.Text.Length > 0
            ? $"ns={ns};s={TagBox.Text}" : string.Empty;
        if (ErrorText != null) ErrorText.Text = string.Empty;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!ushort.TryParse(NamespaceBox.Text, out var ns))
        {
            ErrorText.Text = "命名空间索引须为 0–65535 的整数。";
            return;
        }
        if (string.IsNullOrWhiteSpace(TagBox.Text))
        {
            ErrorText.Text = "请输入完整标签名称。";
            return;
        }
        if (TagBox.Text != TagBox.Text.Trim() && MessageBox.Show(this,
                "标签名称首尾含空格。空格将原样用于 NodeId，确定继续吗？", "确认标签名称",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        Result = new OpcUaDataPoint { Id = _editingId ?? Guid.NewGuid().ToString(), NamespaceIndex = ns, TagName = TagBox.Text };
        DialogResult = true;
    }
}
