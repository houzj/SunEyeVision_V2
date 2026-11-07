using System;
using Apps.Interface;
using VM.Core;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class SetFindModeOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public SetFindModeOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		IOwner owner = null;
		if (parentNode != null)
		{
			owner = parentNode.GetTopBaseNode() as AlgorithmTabRoot;
		}
		if (Executor == null || Executor.TargetObj == null)
		{
			return;
		}
		try
		{
			Integer integer = Params[0] as Integer;
			Enumeration enumeration = Executor.TargetObj as Enumeration;
			if (integer.CurValueA != 1)
			{
				enumeration.CurValueA = 1;
				string comName = enumeration.ComName;
				enumeration.SetPropertyValueByName(enumeration.NameA, enumeration.CurValueA);
				if (owner != null && owner.Owner != null)
				{
					VmModule vmModule = owner.Owner as VmModule;
					((IUserStringData)vmModule.Params)[comName] = "1";
				}
				enumeration.EnableA = false;
			}
			else
			{
				enumeration.EnableA = true;
			}
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
