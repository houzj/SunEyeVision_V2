namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IOutlinePoint : IShape, IShapeBase
{
	byte[] OutlineData { get; }

	string NgColor { get; }
}
