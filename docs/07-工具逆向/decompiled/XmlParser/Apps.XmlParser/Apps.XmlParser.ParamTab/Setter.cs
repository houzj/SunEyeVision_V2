using System;
using System.Linq;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class Setter : BaseNode
{
	public string TargetName;

	public string OperationName;

	public string OperationParams;

	public string PropertyName;

	public string Value;

	private NameValueItem _TargetObj = null;

	public string TargetNameA
	{
		get
		{
			return TargetName;
		}
		set
		{
			TargetName = value;
		}
	}

	public string OperationNameA
	{
		get
		{
			return OperationName;
		}
		set
		{
			OperationName = value;
		}
	}

	public string OperationParamsA
	{
		get
		{
			return OperationParams;
		}
		set
		{
			OperationParams = value;
		}
	}

	public string PropertyNameA
	{
		get
		{
			return PropertyName;
		}
		set
		{
			PropertyName = value;
		}
	}

	public NameValueItem TargetObj
	{
		get
		{
			return _TargetObj;
		}
		set
		{
			_TargetObj = value;
		}
	}

	public IOperation Operation { get; set; }

	public void Init()
	{
		GetTargetObj();
		if (Operation != null)
		{
			Operation.GetOriginalValue();
		}
	}

	public void CreatOperateInstance()
	{
		OperationManager.CreatInstanceBySetter(this);
	}

	public void Operate(object[] Params, bool needThrowException = false)
	{
		try
		{
			if (Operation != null)
			{
				Operation.Operate(Params ?? new object[0], (OperationParams == null) ? new string[0] : OperationParams.Split(';'));
			}
			if (PropertyName != null && TargetObj != null)
			{
				TargetObj.SetPropertyValueByName(PropertyName + "A", Value);
			}
		}
		catch (Exception ex)
		{
			if (needThrowException)
			{
				throw new Exception(ex.Message);
			}
		}
	}

	private void GetTargetObj()
	{
		if (string.IsNullOrEmpty(TargetName))
		{
			return;
		}
		for (BaseNode baseNode = GetParentNode() as BaseNode; baseNode != null; baseNode = baseNode.GetParentNode() as BaseNode)
		{
			if (baseNode is Category)
			{
				Category category = baseNode as Category;
				TargetObj = category.ItemsA.OfType<NameValueItem>().FirstOrDefault((NameValueItem p) => p.Name == TargetName);
				break;
			}
		}
		if (TargetObj == null)
		{
			TargetObj = (GetTopBaseNode() as AlgorithmTabRoot)?.FindBaseNodeByName(TargetName) as NameValueItem;
		}
	}

	public override string ToString()
	{
		return OperationName;
	}
}
