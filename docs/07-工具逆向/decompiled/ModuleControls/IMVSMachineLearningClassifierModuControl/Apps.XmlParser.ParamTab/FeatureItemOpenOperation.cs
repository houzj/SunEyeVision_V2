using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class FeatureItemOpenOperation : IOperation
{
	private string textureStr = "HogFeature;LbpFeature;GlcmFeature;EdgeFeature;PolarFeature";

	private string grayStr = "GrayRangeFeature;GrayMeanFeature;GrayDeviationFeature;GrayEntropyFeature;GrayAnisotropyFeature;GrayProjHorFeature;GrayProjVertFeature;GrayHis";

	public string TargetName = "";

	private object TargetObj = null;

	private bool _Working = false;

	private bool isFirstFlag = true;

	private string[] textureArg => textureStr.Split(';');

	private string[] grayArg => grayStr.Split(';');

	public bool OriginalValue { get; set; }

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public FeatureItemOpenOperation(string TName)
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
		}
	}

	public void ReOperate()
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		if (parentNode == null)
		{
			return;
		}
		object topBaseNode = parentNode.GetTopBaseNode();
		if (TargetName == "TextureFeature" && topBaseNode is AlgorithmTabRoot algorithmTabRoot)
		{
			NameValueItem nameValueItem = null;
			string[] array = textureArg;
			foreach (string name in array)
			{
				NameValueItem nameValueItem2 = algorithmTabRoot.FindBaseNodeByName(name) as NameValueItem;
				if (nameValueItem2.ComName == parentNode.ComName)
				{
					nameValueItem = nameValueItem2;
				}
				else if (nameValueItem2.Value == "True")
				{
					parentNode.SetParamUnitByZmq(null, parentNode.Value);
					return;
				}
			}
			if (nameValueItem != null)
			{
				if (nameValueItem.Value == "True")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainParamOpenError"));
				}
				nameValueItem.Value = "True";
			}
		}
		if (TargetName == "GrayFeature" && topBaseNode is AlgorithmTabRoot algorithmTabRoot2)
		{
			NameValueItem nameValueItem3 = null;
			string[] array2 = grayArg;
			foreach (string name2 in array2)
			{
				NameValueItem nameValueItem4 = algorithmTabRoot2.FindBaseNodeByName(name2) as NameValueItem;
				if (nameValueItem4.ComName == parentNode.ComName)
				{
					nameValueItem3 = nameValueItem4;
				}
				else if (nameValueItem4.Value == "True")
				{
					parentNode.SetParamUnitByZmq(null, parentNode.Value);
					return;
				}
			}
			if (nameValueItem3 != null)
			{
				if (nameValueItem3.Value == "True")
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MLClassTrainParamOpenError"));
				}
				nameValueItem3.Value = "True";
			}
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
