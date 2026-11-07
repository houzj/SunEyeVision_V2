using System.ComponentModel;
using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public interface IOutputTree : IPropertyChanged, INotifyPropertyChanged
{
	bool OutputEnabled { get; set; }

	bool? CheckEnabled { get; set; }

	IOutputTree ParentTree { get; set; }

	void OnPropertyChangedParentTree(string name);
}
