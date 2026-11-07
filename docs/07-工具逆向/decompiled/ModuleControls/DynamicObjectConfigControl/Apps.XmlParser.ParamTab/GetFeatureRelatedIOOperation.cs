using System.Collections.Generic;
using System.Linq;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class GetFeatureRelatedIOOperation : IOperation
{
	public string TargetName = "";

	private bool _Working = false;

	public bool Working => _Working;

	public Setter Executor { get; set; }

	public GetFeatureRelatedIOOperation(string TName)
	{
		TargetName = TName;
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		if (ExtraParams == null)
		{
			return;
		}
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		string name = "";
		if (ExtraParams.Length != 0)
		{
			name = ExtraParams[0];
		}
		if (parentNode == null)
		{
			return;
		}
		Feature feature = parentNode.GetParentNode<Object>()?.GetFeatureByName(name);
		if (feature == null)
		{
			return;
		}
		string value = feature.ValueA;
		IEnumerable<VmIO> enumerable = feature.vmIOs.OfType<VmIO>();
		using (IEnumerator<VmIO> enumerator = enumerable.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				VmIO current = enumerator.Current;
				if (!current.Visible)
				{
					VmIO relateIO = current.RelateIO;
					if (relateIO != null)
					{
						value = ((!(relateIO is VmConstantIO)) ? current.SubscriptionInfo : relateIO.Name);
					}
				}
			}
		}
		parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", value);
	}

	public void ReOperate()
	{
	}

	public void GetOriginalValue()
	{
	}
}
