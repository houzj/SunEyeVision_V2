using System.Collections.Generic;
using System.Windows.Controls;
namespace SunEyeVision.Plugin.Wpf.Design.Results.PropertyList;
public partial class PropertyListDesignView : UserControl
{
    public PropertyListDesignView() { InitializeComponent(); Results.ItemsSource = CreateRows(); }
    public static IReadOnlyList<SampleRow> CreateRows() => new[] { new SampleRow("示例图像源", "", 0, new[] { new SampleRow("模块状态", "成功", 1), new SampleRow("耗时（ms）", "24.4129", 1), new SampleRow("图像", "", 1, new[] { new SampleRow("图像宽度", "1280", 2), new SampleRow("图像高度", "960", 2), new SampleRow("图像像素格式", "Gray8", 2) }) }) };
    // These objects are visual samples only. No workflow/result resource model is duplicated.
    public sealed class SampleRow
    {
        public SampleRow(string name, string value, int level, IReadOnlyList<SampleRow>? children = null) { DisplayName = name; DisplayValue = value; Level = level; Children = children ?? System.Array.Empty<SampleRow>(); }
        public string DisplayName { get; }
        public string DisplayValue { get; }
        public int Level { get; }
        public IReadOnlyList<SampleRow> Children { get; }
        public bool IsGroup => Children.Count > 0;
        public bool IsExpanded { get; set; } = true;
        public bool CanBindVariable => false;
        public string? BindingDisplayName => null;
        public string BindingUnavailableReason => "未提供变量绑定服务";
    }
}
