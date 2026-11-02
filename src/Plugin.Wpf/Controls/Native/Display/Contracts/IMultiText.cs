using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IMultiText : IShape, IShapeBase
{
	List<IText> Texts { get; }
}
