namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IOutlinePoint : IShape, IShapeBase
{
	byte[] OutlineData { get; }

	string NgColor { get; }
}
