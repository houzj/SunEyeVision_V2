using System.Collections.ObjectModel;

namespace HikPcUI.ImageView;

public interface IRoiOwner
{
	ObservableCollection<object> RoiList { get; set; }

	bool IsSelectMulRoiEnable { get; set; }
}
