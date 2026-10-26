using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 折线（接口传入）兼容
/// </summary>
public struct PolylineInOld
{
	public int PolylinePointNum;

	public IntPtr PolylinePointListPtr;

	public float Opacity;

	public int Color;

	public int FillColor;

	public float StrokeThickness;
}
