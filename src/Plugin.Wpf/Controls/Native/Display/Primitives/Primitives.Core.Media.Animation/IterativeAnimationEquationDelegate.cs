using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Media.Animation;

public delegate T IterativeAnimationEquationDelegate<T>(TimeSpan currentTime, T from, T to, TimeSpan duration);
