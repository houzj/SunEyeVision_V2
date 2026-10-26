using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 折线（接口传入）
/// </summary>
public struct PolylineIn
{
	public int PolylinePointNum;

	public IntPtr PolylinePointListPtr;

	public float Opacity;

	public string Color;

	public string FillColor;

	public float StrokeThickness;
}
