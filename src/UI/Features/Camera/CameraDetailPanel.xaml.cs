using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 相机详情面板控件
    /// </summary>
    public partial class CameraDetailPanel : UserControl
    {
        public CameraDetailPanel()
        {
            InitializeComponent();
        }

        private void OnParameterCategorySelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is CameraDetailViewModel viewModel
                && e.NewValue is CameraParameterGroupViewModel category)
                viewModel.SelectedParameterCategory = category;
        }
    }
}
