using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    public sealed class OneBasedIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int index ? index + 1 : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 相机管理器对话框
    /// </summary>
    public partial class CameraManagerDialog : ThemeDialogWindow
    {
        public CameraManagerDialog(CameraManagerViewModel viewModel)
        {
            InitializeComponent();
            
            // 设置 ViewModel
            DataContext = viewModel;
            Closed += OnClosed;
        }

        private async void OnClosed(object? sender, System.EventArgs e)
        {
            Closed -= OnClosed;
            if (DataContext is CameraManagerViewModel viewModel)
                await viewModel.DisposeAsync();
        }

        private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CameraDevice camera })
                return;

            if (e.Key == Key.Enter)
            {
                if (DataContext is CameraManagerViewModel viewModel)
                    viewModel.CommitRename(camera);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (DataContext is CameraManagerViewModel viewModel)
                    viewModel.CommitRename(camera, cancel: true);
                e.Handled = true;
            }
        }

        private void CameraItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }

        private async void CameraConnectionToggle_CheckedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (sender is not ToggleSwitch toggle || toggle.DataContext is not CameraDevice camera)
                return;
            if (camera.IsConnected == e.NewValue)
                return;

            if (DataContext is CameraManagerViewModel manager)
                await manager.SetCameraConnectionAsync(camera, e.NewValue);

            BindingOperations.SetBinding(toggle, ToggleSwitch.IsCheckedProperty, new Binding(nameof(CameraDevice.IsConnected))
            {
                Source = camera,
                Mode = BindingMode.OneWay
            });
        }

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CameraDevice camera } && camera.IsRenaming
                && DataContext is CameraManagerViewModel viewModel)
                viewModel.CommitRename(camera);
        }

        private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true || sender is not TextBox textBox)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void RenameCameraMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu menu }
                || menu.PlacementTarget is not FrameworkElement { DataContext: CameraDevice camera }
                || DataContext is not CameraManagerViewModel viewModel)
                return;

            viewModel.RenameCameraCommand.Execute(camera);
        }

        private void DeleteCameraMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Parent: ContextMenu menu }
                || menu.PlacementTarget is not FrameworkElement { DataContext: CameraDevice camera }
                || DataContext is not CameraManagerViewModel viewModel)
                return;

            if (viewModel.DeleteCameraCommand.CanExecute(camera))
                viewModel.DeleteCameraCommand.Execute(camera);
        }
    }
}
