using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

/// <summary>
/// 连接端子信息,被用于路径计算,替代完整的连接器
/// </summary>
internal class ConnectorInfo
{
	/// <summary>
	/// Item的左边
	/// </summary>
	public double DiagramItemLeft { get; set; }

	/// <summary>
	/// Item的上边
	/// </summary>
	public double DiagramItemTop { get; set; }

	/// <summary>
	/// Item的尺寸
	/// </summary>
	public Size DiagramItemSize { get; set; }

	/// <summary>
	/// Item的位置
	/// </summary>
	public Point Position { get; set; }

	/// <summary>
	/// Item的连接方位
	/// </summary>
	public ConnectorOrientation Orientation { get; set; }
}
