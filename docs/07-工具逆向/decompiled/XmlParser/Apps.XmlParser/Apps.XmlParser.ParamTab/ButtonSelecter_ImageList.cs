namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_ImageList : NameValueItem
{
	public string CurValue;

	private bool selectEnable;

	private int freshImageListFlag;

	public string CurValueA
	{
		get
		{
			if (CurValue == null)
			{
				CurValue = "";
			}
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			CurValueA = value;
		}
	}

	public bool SelectEnable
	{
		get
		{
			return selectEnable;
		}
		set
		{
			if (selectEnable != value)
			{
				selectEnable = value;
				OnPropertyChanged("SelectEnable");
			}
		}
	}

	public int FreshImageListFlag
	{
		get
		{
			return freshImageListFlag;
		}
		set
		{
			if (freshImageListFlag != value)
			{
				freshImageListFlag = value;
				OnPropertyChanged("FreshImageListFlag");
			}
		}
	}
}
