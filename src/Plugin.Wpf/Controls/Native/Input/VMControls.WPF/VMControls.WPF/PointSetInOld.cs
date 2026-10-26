using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 点集（接口传入）兼容
/// </summary>
public struct PointSetInOld
{
	public int PointNum;

	public IntPtr PointListPtr;

	public float Opacity;

	public int Color;

	public int FillColor;

	public float StrokeThickness;
}
