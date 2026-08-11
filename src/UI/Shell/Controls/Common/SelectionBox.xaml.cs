using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.UI.Views.Controls.Common
{
    public partial class SelectionBox : UserControl
    {
        private const int HIGH_ZINDEX = 9999;   // 框选时的高 ZIndex
        private const int LOW_ZINDEX = -1;       // 框选完成后的低 ZIndex

        private Point _startPoint;

        public SelectionBox()
        {
            InitializeComponent();
        }

        public void StartSelection(Point startPoint)
        {
            _startPoint = startPoint;

            // ✅ 设置高 ZIndex，确保框选时在最上层
            System.Windows.Controls.Canvas.SetZIndex(this, HIGH_ZINDEX);

            // ✅ 显示外层 UserControl
            this.Visibility = Visibility.Visible;

            // ✅ 显示内部元素
            SelectionBorder.Visibility = Visibility.Visible;

            // ✅ 初始化选择框
            UpdateSelection(startPoint);
        }

        public void UpdateSelection(Point endPoint)
        {
            double x = Math.Min(_startPoint.X, endPoint.X);
            double y = Math.Min(_startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - _startPoint.X);
            double height = Math.Abs(endPoint.Y - _startPoint.Y);

            SelectionBorder.Width = width;
            SelectionBorder.Height = height;
            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
        }

        public Rect GetSelectionRect()
        {
            double x = SelectionBorder.Margin.Left;
            double y = SelectionBorder.Margin.Top;
            return new Rect(x, y, SelectionBorder.Width, SelectionBorder.Height);
        }

        public void SetItemCount(int count)
        {
            ItemCountText.Text = $"项";
            ItemCountText.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void EndSelection()
        {
            // ✅ 优先降低 ZIndex（关键！）
            // 这样命中测试会先遍历到节点，而不是 SelectionBox
            System.Windows.Controls.Canvas.SetZIndex(this, LOW_ZINDEX);

            // ✅ 隐藏外层 UserControl（关键修改）
            // UserControl 不参与命中测试和渲染
            this.Visibility = Visibility.Collapsed;

            // ✅ 隐藏内部元素
            SelectionBorder.Visibility = Visibility.Collapsed;
            ItemCountText.Visibility = Visibility.Collapsed;

            // ✅ 清理状态，避免残留影响下次显示
            SelectionBorder.Width = 0;
            SelectionBorder.Height = 0;
            SelectionBorder.Margin = new Thickness(0, 0, 0, 0);
        }
    }
}
