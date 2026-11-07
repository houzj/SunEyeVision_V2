using System.Linq;

namespace Apps.XmlParser.ParamTab;

public class HidePhysicInfoOperation : IOperation
{
	public string TargetName = "";

	private Category TargetObj;

	private bool _Working;

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
		bool flag = (algorithmTabRoot?.FindBaseNodeByName("CalibPointInput") as RadioSelecter).CurValueA == 0;
		if (string.IsNullOrEmpty((algorithmTabRoot?.FindBaseNodeByName("RotateAngle") as ButtonSelecter)?.CurValueA))
		{
			if (flag)
			{
				if (string.IsNullOrEmpty((algorithmTabRoot?.FindBaseNodeByName("PhyPoint") as ButtonSelecter)?.CurValueA))
				{
					return true;
				}
			}
			else
			{
				ButtonSelecter buttonSelecter = algorithmTabRoot?.FindBaseNodeByName("MovePhysicalPointX") as ButtonSelecter;
				ButtonSelecter buttonSelecter2 = algorithmTabRoot?.FindBaseNodeByName("MovePhysicalPointY") as ButtonSelecter;
				if (string.IsNullOrEmpty(buttonSelecter?.CurValueA) && string.IsNullOrEmpty(buttonSelecter2?.CurValueA))
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
