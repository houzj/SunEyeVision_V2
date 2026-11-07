using System.Collections.ObjectModel;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.XmlData;
using VM.Core;

namespace DynamicObjectConfigControl;

public interface IRunParam
{
	ObservableCollection<DynamicNameValueItemOfGroupCategory> GroupCollection { get; }

	ObservableCollection<DynamicNameValueItemOfGroup> Collections { get; }

	VmModuleSet Owner { get; }

	string GroupNamesKey { get; }

	string GroupIDsKey { get; }

	string ParamIndexListKey { get; }
}
