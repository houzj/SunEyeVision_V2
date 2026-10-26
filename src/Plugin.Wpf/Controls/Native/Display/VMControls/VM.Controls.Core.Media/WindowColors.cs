using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Media;

public static class WindowColors
{
	private static Color? _colorizationMode;

	private static bool? _colorizationOpaqueBlend;

	public static Color ColorizationColor
	{
		get
		{
			if (_colorizationMode.HasValue)
			{
				return _colorizationMode.Value;
			}
			try
			{
				_colorizationMode = GetDWMColorValue("ColorizationColor");
			}
			catch
			{
				_colorizationMode = Color.FromArgb(byte.MaxValue, 175, 175, 175);
			}
			return _colorizationMode.Value;
		}
	}

	public static bool ColorizationOpaqueBlend
	{
		get
		{
			if (_colorizationOpaqueBlend.HasValue)
			{
				return _colorizationOpaqueBlend.Value;
			}
			try
			{
				_colorizationOpaqueBlend = GetDWMBoolValue("ColorizationOpaqueBlend");
			}
			catch
			{
				_colorizationOpaqueBlend = false;
			}
			return _colorizationOpaqueBlend.Value;
		}
	}

	private static int GetDWMIntValue(string keyName)
	{
		return (int)Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\DWM", RegistryKeyPermissionCheck.ReadSubTree, RegistryOptions.None).GetValue(keyName);
	}

	private static Color GetDWMColorValue(string keyName)
	{
		byte[] bytes = BitConverter.GetBytes(GetDWMIntValue(keyName));
		return new Color
		{
			B = bytes[0],
			G = bytes[1],
			R = bytes[2],
			A = byte.MaxValue
		};
	}

	private static bool GetDWMBoolValue(string keyName)
	{
		return GetDWMIntValue(keyName) != 0;
	}
}
