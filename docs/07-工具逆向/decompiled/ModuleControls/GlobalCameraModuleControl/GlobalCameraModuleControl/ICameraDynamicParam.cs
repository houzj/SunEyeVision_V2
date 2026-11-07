using System.Collections.ObjectModel;
using Apps.XmlParser.ParamTab;
using GlobalCameraModuleControl.XmlData;
using VM.Core;

namespace GlobalCameraModuleControl;

public interface ICameraDynamicParam
{
	ObservableCollection<DynamicCameraNameItemOfGroupCategory> GroupCollection { get; }

	ObservableCollection<DynamicCameraNameItemOfGroup> Collections { get; }

	VmModule Owner { get; }
}
