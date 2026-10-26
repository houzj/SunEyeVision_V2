using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class PropertyChangedExt
{
	public static void Notify<TMember>(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, Expression<Func<TMember>> expression)
	{
		if (sender == null)
		{
			throw new ArgumentNullException("sender");
		}
		if (expression == null)
		{
			throw new ArgumentNullException("expression");
		}
		string propertyName = GetPropertyName((expression.Body as MemberExpression) ?? throw new ArgumentException("The expression must target a property or field.", "expression"), sender.GetType());
		NotifyCore(sender, handler, propertyName);
	}

	public static void Notify(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, string propertyName)
	{
		if (sender == null)
		{
			throw new ArgumentNullException("sender");
		}
		if (propertyName == null)
		{
			throw new ArgumentNullException("propertyName");
		}
		NotifyCore(sender, handler, propertyName);
	}

	private static void NotifyCore(INotifyPropertyChanged sender, PropertyChangedEventHandler handler, string propertyName)
	{
		handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
	}

	internal static bool PropertyChanged(string propertyName, PropertyChangedEventArgs e, bool targetPropertyOnly)
	{
		string propertyName2 = e.PropertyName;
		if (propertyName2 == propertyName)
		{
			return true;
		}
		if (!targetPropertyOnly)
		{
			return string.IsNullOrEmpty(propertyName2);
		}
		return false;
	}

	internal static bool PropertyChanged<TOwner, TMember>(Expression<Func<TMember>> expression, PropertyChangedEventArgs e, bool targetPropertyOnly)
	{
		return PropertyChanged((expression.Body as MemberExpression) ?? throw new ArgumentException("The expression must target a property or field.", "expression"), typeof(TOwner), e, targetPropertyOnly);
	}

	internal static bool PropertyChanged<TOwner, TMember>(Expression<Func<TOwner, TMember>> expression, PropertyChangedEventArgs e, bool targetPropertyOnly)
	{
		return PropertyChanged((expression.Body as MemberExpression) ?? throw new ArgumentException("The expression must target a property or field.", "expression"), typeof(TOwner), e, targetPropertyOnly);
	}

	private static bool PropertyChanged(MemberExpression expression, Type ownerType, PropertyChangedEventArgs e, bool targetPropertyOnly)
	{
		return PropertyChanged(GetPropertyName(expression, ownerType), e, targetPropertyOnly);
	}

	private static string GetPropertyName(MemberExpression expression, Type ownerType)
	{
		if (!expression.Expression.Type.IsAssignableFrom(ownerType))
		{
			throw new ArgumentException("The expression must target a property or field on the appropriate owner.", "expression");
		}
		return ReflectionHelper.GetPropertyOrFieldName(expression);
	}
}
