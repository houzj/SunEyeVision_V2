using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IMultiText : IShape, IShapeBase
{
	List<IText> Texts { get; }
}
