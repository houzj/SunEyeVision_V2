using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

/// <summary>
/// 多边形（接口传入）兼容
/// </summary>
public struct PolygonInOld
{
	public int PolygonPointNum;

	public IntPtr PolygonPointListPtr;

	public float Opacity;

	public int Color;

	public int FillColor;

	public float StrokeThickness;
}
