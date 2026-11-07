using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class RoiDisplayOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public RoiDisplayOperation(string TName)
	{
		TargetName = TName;
	}

	public void ReOperate()
	{
		_Working = false;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		Ioc.Get<IModuleResultView>()?.SetRoiVisible();
	}

	public void GetOriginalValue()
	{
		OriginalValue = true;
	}
}
