using System.Linq;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SetDisplayObjectPropertyOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetDisplayObjectPropertyOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
	}

	public void SetDisplayObjectProperty(IVmModule node, string objectName, string propertyName, object value)
	{
		if (node != null && VmSolution.Instance[(node as IVmFullPath).FullPath + ".[Display]"] is ParamRoot paramRoot)
		{
			paramRoot.GetDisplayList().OfType<Object>().Single((Object p) => p.NameA == objectName)?.SetPropertyValueByName(propertyName + "A", value);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
