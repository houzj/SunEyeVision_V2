using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IMultiPoint : IShape, IShapeBase
{
	ObservableCollection<IOutlinePoint> OutlinePoints { get; }

	ObservableCollection<IMicroPoint> MicroPoints { get; }

	ObservableCollection<IRectangle> Rectangles { get; }
}
