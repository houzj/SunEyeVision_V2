using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IMultiText : IShape, IShapeBase
{
	List<IText> Texts { get; }
}
