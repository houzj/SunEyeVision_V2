namespace Apps.XmlParser.ParamTab;

public class CommandSetWidth : Command
{
	public new double _MaxWidth = double.MaxValue;

	public double _Width = 120.0;

	public double _MinWidth = 120.0;

	public new double MaxWidthA
	{
		get
		{
			return _MaxWidth;
		}
		set
		{
			_MaxWidth = value;
			OnPropertyChanged("MaxWidthA");
		}
	}

	public double WidthA
	{
		get
		{
			return _Width;
		}
		set
		{
			_Width = value;
			OnPropertyChanged("WidthA");
		}
	}

	public double MinWidthA
	{
		get
		{
			return _MinWidth;
		}
		set
		{
			_MinWidth = value;
			OnPropertyChanged("MinWidthA");
		}
	}
}
