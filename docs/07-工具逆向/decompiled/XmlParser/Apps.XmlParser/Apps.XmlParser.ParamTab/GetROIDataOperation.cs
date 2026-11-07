using System;
using Apps.Interface;
using VM.Core;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class GetROIDataOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetROIDataOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		try
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
			Integer integer = Params[0] as Integer;
			if (owner != null && owner.Owner != null)
			{
				VmModule vmModule = owner.Owner as VmModule;
				((IUserStringData)vmModule.Params)[integer.ComName] = integer.CurValueA.ToString();
				if (Executor.TargetObj is ROISelecter rOISelecter)
				{
					rOISelecter.GetParamUnit();
				}
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
