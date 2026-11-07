using System.Collections.Generic;
using System.Windows.Media;

namespace IMVSMachineLearningClassifierModuControl;

public class CategoryBrushData
{
	private static List<Brush> brushes = new List<Brush>
	{
		Brushes.Purple,
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3754DF")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6C95D")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71CE67")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D25626")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#63CBFA")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7764ED")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#479133")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA336D")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#428CA2")),
		new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F29936"))
	};

	private static int seq = 0;

	public static void Init()
	{
		seq = 0;
	}

	public static Brush GetCategoryBrush()
	{
		if (seq >= 9)
		{
			seq = 0;
		}
		else
		{
			seq++;
		}
		return brushes[seq];
	}

	public static Brush GetCategoryBrush(int index)
	{
		int index2 = index % 9;
		return brushes[index2];
	}
}
