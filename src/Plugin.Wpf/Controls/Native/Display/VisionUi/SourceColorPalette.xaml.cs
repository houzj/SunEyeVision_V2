using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;using System.Windows.Controls;using System.Windows.Media;namespace SunEyeVision.Plugin.Wpf.Native.Display.VisionUi;
public partial class SourceColorPalette:UserControl{public event Action<string>? Selected;public SourceColorPalette(){InitializeComponent();AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{if(e.OriginalSource is Button b&&b.Background is SolidColorBrush c)Selected?.Invoke(c.Color.ToString());}));}}
