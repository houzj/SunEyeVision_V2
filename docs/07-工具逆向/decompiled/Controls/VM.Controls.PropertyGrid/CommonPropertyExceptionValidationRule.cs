using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using VM.Controls.Core.Utilities;

namespace VM.Controls.PropertyGrid;

internal class CommonPropertyExceptionValidationRule : ValidationRule
{
	private TypeConverter _propertyTypeConverter;

	private Type _type;

	internal CommonPropertyExceptionValidationRule(Type type)
	{
		_propertyTypeConverter = TypeDescriptor.GetConverter(type);
		_type = type;
	}

	public override ValidationResult Validate(object value, CultureInfo cultureInfo)
	{
		ValidationResult result = new ValidationResult(isValid: true, null);
		if (GeneralUtilities.CanConvertValue(value, _type))
		{
			try
			{
				_propertyTypeConverter.ConvertFrom(value);
			}
			catch (Exception ex)
			{
				result = new ValidationResult(isValid: false, ex.Message);
			}
		}
		return result;
	}
}
