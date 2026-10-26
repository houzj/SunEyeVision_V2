using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 点集（接口传入）
/// </summary>
public struct PointSetIn
{
	public int PointNum;

	public IntPtr PointListPtr;

	public float Opacity;

	public string Color;

	public string FillColor;

	public float StrokeThickness;
}
