using System.Windows.Media;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 监视模式树形节点 — 根节点=变量名(类型)，叶节点=值
    /// </summary>
    public class MonitorNode
    {
        public string Name { get; set; } = string.Empty;
        public string TypeDisplay { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = "无结果";
        public Brush ValueBrush { get; set; } = Brushes.Gray;
    }
}
