using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 多边形（接口传入）
/// </summary>
public struct PolygonIn
{
	public int PolygonPointNum;

	public IntPtr PolygonPointListPtr;

	public float Opacity;

	public string Color;

	public string FillColor;

	public float StrokeThickness;
}
