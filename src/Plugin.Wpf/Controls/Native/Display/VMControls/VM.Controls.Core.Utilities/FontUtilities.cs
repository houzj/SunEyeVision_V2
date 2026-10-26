using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal class FontUtilities
{
	internal static IEnumerable<FontFamily> Families
	{
		get
		{
			foreach (FontFamily systemFontFamily in Fonts.SystemFontFamilies)
			{
				try
				{
					_ = systemFontFamily.FamilyNames;
				}
				catch
				{
					continue;
				}
				yield return systemFontFamily;
			}
		}
	}

	internal static IEnumerable<FontWeight> Weights
	{
		get
		{
			yield return FontWeights.Black;
			yield return FontWeights.Bold;
			yield return FontWeights.ExtraBlack;
			yield return FontWeights.ExtraBold;
			yield return FontWeights.ExtraLight;
			yield return FontWeights.Light;
			yield return FontWeights.Medium;
			yield return FontWeights.Normal;
			yield return FontWeights.SemiBold;
			yield return FontWeights.Thin;
		}
	}

	internal static IEnumerable<FontStyle> Styles
	{
		get
		{
			yield return FontStyles.Italic;
			yield return FontStyles.Normal;
		}
	}

	internal static IEnumerable<FontStretch> Stretches
	{
		get
		{
			yield return FontStretches.Condensed;
			yield return FontStretches.Expanded;
			yield return FontStretches.ExtraCondensed;
			yield return FontStretches.ExtraExpanded;
			yield return FontStretches.Normal;
			yield return FontStretches.SemiCondensed;
			yield return FontStretches.SemiExpanded;
			yield return FontStretches.UltraCondensed;
			yield return FontStretches.UltraExpanded;
		}
	}
}
