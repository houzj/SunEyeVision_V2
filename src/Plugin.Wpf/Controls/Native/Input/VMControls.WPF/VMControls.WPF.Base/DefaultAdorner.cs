using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

/// <summary>
/// 默认装饰器，为了获取当前缩放系数
/// </summary>
public class DefaultAdorner : Adorner
{
	public GeneralTransform CurTransform { get; internal set; }

	public DefaultAdorner(UIElement adornedElement)
		: base(adornedElement)
	{
	}

	public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
	{
		CurTransform = base.GetDesiredTransform(transform);
		return CurTransform;
	}
}
