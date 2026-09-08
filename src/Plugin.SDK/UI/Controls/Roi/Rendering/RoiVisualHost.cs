using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering
{
    /// <summary>
    /// 区域视觉宿主：以单个 UIElement 承载任意数量的 DrawingVisual（每个区域一个），
    /// 替代"每区域多个 Shape UIElement"的对象树方案（ROI 渲染轻量化）。
    /// 本类不参与布局与命中测试，仅提供 Visual 子树挂载；区域交互走数学命中，不受影响。
    /// </summary>
    public sealed class RoiVisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;

        public RoiVisualHost()
        {
            // 不测量自身（内容由子 DrawingVisual 绝对坐标绘制，宿主定位于画布原点）
            _children = new VisualCollection(this);
        }

        /// <summary>当前承载的区域视觉数。</summary>
        public int Count => _children.Count;

        public void Add(Visual visual) => _children.Add(visual);

        public void Remove(Visual visual)
        {
            if (_children.Contains(visual))
                _children.Remove(visual);
        }

        /// <summary>将指定视觉移到末尾（用于选中区域置顶显示）。</summary>
        public void MoveToTop(Visual visual)
        {
            int count = _children.Count;
            if (count <= 1) return;
            int idx = _children.IndexOf(visual);
            if (idx < 0 || idx == count - 1) return;
            _children.RemoveAt(idx);
            _children.Add(visual);
        }

        public void Clear() => _children.Clear();

        public Visual? GetAt(int index) => index >= 0 && index < _children.Count ? _children[index] : null;

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) => _children[index];
    }
}
