using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class CompensationPointF : PointF
{
	private object Compensation = new object();

	public object CompensationA
	{
		get
		{
			return Compensation;
		}
		set
		{
			Compensation = value;
			OnPropertyChanged("CompensationA");
		}
	}

	public override int GetParamUnit()
	{
		CompensationA = Operation.GetPropertyValueByName(Name);
		NameValueItem nameValueItem = null;
		if (Floats != null && Floats.Count > 1)
		{
			for (int i = 0; i < 3; i++)
			{
				nameValueItem = Floats[i] as NameValueItem;
				if (string.IsNullOrEmpty(nameValueItem.NameA))
				{
					switch (i)
					{
					case 0:
						nameValueItem.NameA = NameA + ".X";
						break;
					case 1:
						nameValueItem.NameA = NameA + ".Y";
						break;
					case 2:
						nameValueItem.NameA = NameA + ".R";
						break;
					}
				}
				if (nameValueItem != null)
				{
					int paramUnit = nameValueItem.GetParamUnit();
					if (paramUnit == -1)
					{
						SetChildState(nameValueItem.MyItemState);
						return paramUnit;
					}
				}
			}
		}
		return 0;
	}

	private void SetChildState(ItemState itemState)
	{
		if (itemState != ItemState.Normal)
		{
			MyItemState = itemState;
		}
	}
}
