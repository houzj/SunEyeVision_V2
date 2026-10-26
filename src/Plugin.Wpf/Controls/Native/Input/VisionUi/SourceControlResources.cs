using System;
using System.Runtime.CompilerServices;using System.Windows;using System.Windows.Threading;
namespace SunEyeVision.Plugin.Wpf.Native.Input.VisionUi;
public static class SourceControlResources{static readonly ConditionalWeakTable<Dispatcher,ResourceDictionary> dictionaries=new();public static ResourceDictionary Current=>dictionaries.GetValue(Dispatcher.CurrentDispatcher,_=>new(){Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Input/VisionUi/SourceControlStyles.xaml",UriKind.Relative)});}
