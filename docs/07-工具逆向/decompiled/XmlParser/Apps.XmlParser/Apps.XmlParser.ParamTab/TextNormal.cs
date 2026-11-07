using System;
using System.Linq;
using System.Windows;

namespace Apps.XmlParser.ParamTab;

public class TextNormal : NameValueItem
{
	public double FontSize = 12.0;

	public string Margin;

	public double FontSizeA
	{
		get
		{
			return FontSize;
		}
		set
		{
			FontSize = value;
			OnPropertyChanged("FontSizeA");
		}
	}

	public Thickness MarginA => CovertStringToThickness(Margin);

	private Thickness CovertStringToThickness(string inputString)
	{
		try
		{
			if (string.IsNullOrEmpty(inputString))
			{
				return new Thickness(0.0, 0.0, 0.0, 0.0);
			}
			if (double.TryParse(inputString, out var result))
			{
				return new Thickness(result);
			}
			if (inputString.Contains(",") && inputString.Split(',').Count() == 4)
			{
				string[] array = inputString.Split(',');
				double left = double.Parse(array[0]);
				double top = double.Parse(array[1]);
				double right = double.Parse(array[2]);
				double bottom = double.Parse(array[3]);
				return new Thickness(left, top, right, bottom);
			}
			return new Thickness(0.0, 0.0, 0.0, 0.0);
		}
		catch (Exception)
		{
			return new Thickness(0.0, 0.0, 0.0, 0.0);
		}
	}
}
