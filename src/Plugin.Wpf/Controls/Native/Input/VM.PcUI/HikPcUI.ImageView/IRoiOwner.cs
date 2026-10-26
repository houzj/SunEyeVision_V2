using System.Collections.ObjectModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ImageView;

public interface IRoiOwner
{
	ObservableCollection<object> RoiList { get; set; }

	bool IsSelectMulRoiEnable { get; set; }
}
