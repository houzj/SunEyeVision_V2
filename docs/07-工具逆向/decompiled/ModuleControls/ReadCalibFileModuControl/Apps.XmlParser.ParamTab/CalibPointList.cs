using System.Collections.ObjectModel;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class CalibPointList : IOutput
{
	public string DisplayType => "CalibPointList";

	public string NameA { get; set; }

	public ObservableCollection<PointR> PointList { get; set; } = new ObservableCollection<PointR>();

	public void AddOutput(IVmModule vmModule)
	{
	}
}
