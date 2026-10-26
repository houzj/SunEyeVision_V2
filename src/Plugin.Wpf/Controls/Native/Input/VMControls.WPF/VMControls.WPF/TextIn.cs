using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 文本（接口传入）
/// </summary>
public struct TextIn
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
	public byte[] Content;

	public float PositionX;

	public float PositionY;

	public float Width;

	public float Height;

	public int FontSize;

	public float Opacity;

	public string Color;

	public string FillColor;

	public float StrokeThickness;
}
