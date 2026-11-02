namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IOutlinePoint : IShape, IShapeBase
{
	byte[] OutlineData { get; }

	string NgColor { get; }
}
