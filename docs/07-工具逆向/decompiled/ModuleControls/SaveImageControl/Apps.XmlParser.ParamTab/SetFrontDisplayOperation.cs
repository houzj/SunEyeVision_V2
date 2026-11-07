using System.Linq;
using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class SetFrontDisplayOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetFrontDisplayOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Params == null || Params.Count() < 2)
		{
			return;
		}
		ButtonSelecter_DisplayObject buttonSelecter_DisplayObject = Params[0] as ButtonSelecter_DisplayObject;
		string text = Params[1] as string;
		if (buttonSelecter_DisplayObject.GetTopBaseNode() is BaseNode baseNode && (baseNode as IOwner).Owner != null)
		{
			VmModule vmModule = (baseNode as IOwner).Owner as VmModule;
			if (!string.IsNullOrEmpty(buttonSelecter_DisplayObject.OldCurValue))
			{
			}
			if (string.IsNullOrEmpty(buttonSelecter_DisplayObject.CurValueA))
			{
			}
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
