using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class FeatureOpenOperation : IOperation
{
	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	private bool isFirstFlag = true;

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public FeatureOpenOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
			if (parentNode != null)
			{
				parentNode.SetParamUnitByZmq(null, parentNode.Value);
				_Working = true;
			}
		}
	}

	private void GetTargetObj()
	{
		if (string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode != null)
		{
			object topBaseNode = parentNode.GetTopBaseNode();
			if (topBaseNode is AlgorithmTabRoot algorithmTabRoot)
			{
				TargetObj = algorithmTabRoot.FindBaseNodeByName(TargetName);
			}
		}
	}

	public void ReOperate()
	{
		if (Executor.TargetObj.Value == "False")
		{
			if (Executor != null && Executor.TargetObj != null)
			{
				NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
				if (parentNode == null)
				{
					return;
				}
				if (parentNode.Value == "False")
				{
					parentNode.Value = "True";
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainFeatrueOpenError"));
				}
				else
				{
					parentNode.SetParamUnitByZmq(null, parentNode.Value);
				}
			}
		}
		else
		{
			NameValueItem parentNode2 = Executor.GetParentNode<NameValueItem>();
			if (parentNode2 == null)
			{
				return;
			}
			parentNode2.SetParamUnitByZmq(null, parentNode2.Value);
		}
		_Working = false;
	}

	public void GetOriginalValue()
	{
		SetOriginalValue(init: true);
	}

	private void SetOriginalValue(bool init = false)
	{
		if (Executor != null && Executor.TargetObj != null)
		{
			OriginalValue = !init && Executor.TargetObj.CustomVisibleA;
		}
		else
		{
			OriginalValue = false;
		}
	}
}
