namespace Apps.XmlParser.ParamTab;

public class BridgeItemBase : NameValueItem
{
	public string PropertyName;

	public Setter BrigdgeSetter;

	private object _BridgeObj;

	public string BindingPathA => "BridgeObjA." + PropertyName + "A";

	public Setter BrigdgeSetterA
	{
		get
		{
			return BrigdgeSetter;
		}
		set
		{
			BrigdgeSetter = value;
			OnPropertyChanged("BrigdgeSetterA");
		}
	}

	public object BridgeObjA
	{
		get
		{
			if (_BridgeObj == null && BrigdgeSetter != null)
			{
				if (BrigdgeSetter.Operation == null)
				{
					BrigdgeSetter.CreatOperateInstance();
				}
				if (BrigdgeSetter.Operation != null)
				{
					BrigdgeSetter.Operate(null);
				}
			}
			return _BridgeObj;
		}
		set
		{
			_BridgeObj = value;
		}
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
