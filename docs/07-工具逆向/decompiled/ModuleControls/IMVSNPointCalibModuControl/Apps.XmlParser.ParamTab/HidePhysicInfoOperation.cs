using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class HidePhysicInfoOperation : IOperation
{
	public string TargetName = "";

	private Category TargetObj = null;

	private bool _Working = false;

	public Setter Executor { get; set; }

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public HidePhysicInfoOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
		OriginalValue = true;
		GetTargetObj();
	}

	private void GetTargetObj()
	{
		if (string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		AlgorithmTabRoot algorithmTabRoot = Executor.GetTopBaseNode() as AlgorithmTabRoot;
		TargetObj = ((algorithmTabRoot != null) ? (from a in algorithmTabRoot.Tabs.OfType<Tab>()
			select a.CategorysA.OfType<Category>().FirstOrDefault((Category x) => x.Name == TargetName)).FirstOrDefault() : null);
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		bool visibility = GetVisibility();
		if (TargetObj != null)
		{
			TargetObj.CustomVisibleA = visibility;
			_Working = true;
		}
	}

	private bool GetVisibility()
	{
		AlgorithmTabRoot algorithmTabRoot = Executor.GetTopBaseNode() as AlgorithmTabRoot;
		if (!(algorithmTabRoot?.FindBaseNodeByName("CalibPointGet") is Enumeration { CurValueA: 0 }))
		{
			return false;
		}
		RadioSelecter radioSelecter = algorithmTabRoot?.FindBaseNodeByName("CalibPointInput") as RadioSelecter;
		bool flag = radioSelecter.CurValueA == 0;
		if (string.IsNullOrEmpty((algorithmTabRoot?.FindBaseNodeByName("WorldRotateAngle") is ButtonSelecter buttonSelecter) ? buttonSelecter.CurValueA : null))
		{
			if (flag)
			{
				if (string.IsNullOrEmpty((algorithmTabRoot?.FindBaseNodeByName("PhyPoint") is ButtonSelecter buttonSelecter2) ? buttonSelecter2.CurValueA : null))
				{
					return true;
				}
			}
			else
			{
				ButtonSelecter buttonSelecter3 = algorithmTabRoot?.FindBaseNodeByName("PhyPointX") as ButtonSelecter;
				ButtonSelecter buttonSelecter4 = algorithmTabRoot?.FindBaseNodeByName("PhyPointY") as ButtonSelecter;
				if (string.IsNullOrEmpty(buttonSelecter3?.CurValueA) && string.IsNullOrEmpty(buttonSelecter4?.CurValueA))
				{
					return true;
				}
			}
		}
		return false;
	}

	public void ReOperate()
	{
		TargetObj.CustomVisibleA = OriginalValue;
		_Working = false;
	}
}
