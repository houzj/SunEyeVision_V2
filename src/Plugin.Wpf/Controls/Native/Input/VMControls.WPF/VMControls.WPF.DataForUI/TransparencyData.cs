namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

/// <summary>
/// 透明度数据
/// </summary>
public class TransparencyData
{
	/// <summary>
	/// 透明度所在图层key
	/// </summary>
	public string key { get; set; }

	/// <summary>
	/// 图像透明度
	/// </summary>
	public byte ImageTransparency { get; set; }

	/// <summary>
	/// Z平面出现的顺序
	/// </summary>
	public int ZIndex { get; set; }

	/// <summary>
	/// 是否已被使用
	/// </summary>
	public bool IsUsed { get; set; }
}
