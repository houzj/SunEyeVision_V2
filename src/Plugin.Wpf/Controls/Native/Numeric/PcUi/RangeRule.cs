using System;
using System.Globalization;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public class RangeRule : ValidationRule
{
	public NumericControl Container { get; set; }

	public override ValidationResult Validate(object value, CultureInfo cultureInfo)
	{
		if (Container != null)
		{
			double num = 0.0;
			try
			{
				if (((string)value).Length > 0)
				{
					num = double.Parse((string)value);
				}
				if (double.IsNaN(num) || double.IsInfinity(num))
				{
					throw new Exception("not a number");
				}
			}
			catch (Exception ex)
			{
				return new ValidationResult(isValid: false, "Illegal characters or " + ex.Message);
			}
			if (num < Container.Minimum || num > Container.Maximum || (Container.IsNeedCheckExtremumByXML && (num < Container.MinValue || num > Container.MaxValue)))
			{
				return new ValidationResult(isValid: false, "Please enter an age in the range: " + Container.Minimum + " - " + Container.Maximum + ".");
			}
			return new ValidationResult(isValid: true, null);
		}
		return new ValidationResult(isValid: false, "Container is null");
	}
}
