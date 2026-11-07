using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class PointFLimit : NameValueItem
{
	public double IncValue = 1.0;

	public int DecimalDigits = 3;

	private object DataContext = new object();

	public double IncValueA
	{
		get
		{
			return IncValue;
		}
		set
		{
			IncValue = value;
			OnPropertyChanged("IncValueA");
		}
	}

	public int DecimalDigitsA
	{
		get
		{
			return DecimalDigits;
		}
		set
		{
			DecimalDigits = value;
			OnPropertyChanged("DecimalDigitsA");
		}
	}

	public object DataContextA
	{
		get
		{
			return DataContext;
		}
		set
		{
			DataContext = value;
			OnPropertyChanged("DataContextA");
		}
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	public override int GetParamUnit()
	{
		DataContextA = Operation.GetPropertyValueByName(Name);
		return 0;
	}
}
