using System.Collections.ObjectModel;
using HikPcUI.ImageView;
using VM.ModuleControl;

namespace FewShotCommonControl;

public class RegisterImageEntity : ImageEntity
{
	private ObservableCollection<RoiParameter> _roiList = null;

	public ObservableCollection<RoiParameter> RoiList
	{
		get
		{
			if (_roiList == null)
			{
				_roiList = new ObservableCollection<RoiParameter>();
			}
			return _roiList;
		}
	}
}
