using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.UI.Shell.Windows;
public partial class RootWorkflowConfigurationWindow
{
 private long _imageEpoch;
 private bool _closedImages;
 private void ImagePortChanged(object sender, SelectionChangedEventArgs args) { }
 private void RefreshImagePreview()
 {
  if (_closedImages) return;
  ImagePorts.ItemsSource = null;
  NoImage.Text = "正式结果读取尚未接入";
  NoImage.Visibility = Visibility.Visible;
 }
 private void ClearImagePreview() { ++_imageEpoch; ImagePorts.ItemsSource = null; }
 private void DisposeImagePreview() { _closedImages = true; ClearImagePreview(); }
 private static void RunImageCleanup(IEnumerable<Action> actions)
 {
  var errors = new List<Exception>();
  foreach (var action in actions) try { action(); } catch (Exception error) { errors.Add(error); }
  if (errors.Count > 0) throw new AggregateException("根配置图像清理失败。", errors);
 }
}
