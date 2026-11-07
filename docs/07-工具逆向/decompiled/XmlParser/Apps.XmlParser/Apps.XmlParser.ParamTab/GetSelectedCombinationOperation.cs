using System;
using Apps.Localization;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetSelectedCombinationOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetSelectedCombinationOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		int num = 0;
		if (parentNode is ButtonSelecter)
		{
			num = (parentNode as ButtonSelecter).Type;
		}
		string text = "";
		if (ExtraParams.Length == 0)
		{
			return;
		}
		text = ExtraParams[0];
		if (parentNode == null)
		{
			return;
		}
		VmModule vmModule = null;
		object topBaseNode = parentNode.GetTopBaseNode();
		if (topBaseNode is AlgorithmTabRoot algorithmTabRoot)
		{
			vmModule = algorithmTabRoot.Owner as VmModule;
		}
		if (vmModule == null)
		{
		}
		if (vmModule == null)
		{
			return;
		}
		try
		{
			VmIO vmIO = null;
			if (num == 0)
			{
				vmIO = vmModule.GetInputIOWithLeafByName(text);
				if (vmIO == null && topBaseNode is SubAlgorithmTabRoot subAlgorithmTabRoot)
				{
					vmIO = vmModule.GetInputIOWithLeafByName(AppLocalizationService.UnDoTranslate(text + "_" + subAlgorithmTabRoot.HashCode));
				}
				if (vmIO == null)
				{
					vmIO = vmModule.GetOutputIOWithLeafByName(text);
				}
			}
			if (parentNode is ButtonSelecter buttonSelecter)
			{
				buttonSelecter.ReadOnlyA = false;
			}
			string subscriptionInfo = vmIO.SubscriptionInfo;
			if (string.IsNullOrEmpty(subscriptionInfo))
			{
				subscriptionInfo = vmIO.GetSubscriptionInfo();
				if (!string.IsNullOrEmpty(subscriptionInfo) && parentNode is ButtonSelecter buttonSelecter2)
				{
					buttonSelecter2.ReadOnlyA = true;
				}
			}
			parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", subscriptionInfo);
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
