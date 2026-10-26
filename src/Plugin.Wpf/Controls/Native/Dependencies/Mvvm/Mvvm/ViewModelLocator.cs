using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm;

/// <summary>
///     This class defines the attached property and related change handler that calls the ViewModelLocator in Prism.Mvvm.
/// </summary>
public static class ViewModelLocator
{
	/// <summary>
	///     The AutoWireViewModel attached property.
	/// </summary>
	public static DependencyProperty AutoWireViewModelProperty = DependencyProperty.RegisterAttached("AutoWireViewModel", typeof(bool), typeof(ViewModelLocator), new PropertyMetadata(false, AutoWireViewModelChanged));

	private static void AutoWireViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ViewModelLocationProvider.AutoWireViewModelChanged((d as IView) ?? throw new Exception("Your views must implement IView"));
	}

	/// <summary>
	///     Gets the value of the AutoWireViewModel attached property.
	/// </summary>
	/// <param name="obj">The dependency object that has this attached property.</param>
	/// <returns><c>True</c> if view model autowiring is enabled; otherwise, <c>false</c>.</returns>
	public static bool GetAutoWireViewModel(DependencyObject obj)
	{
		if (obj != null)
		{
			return (bool)obj.GetValue(AutoWireViewModelProperty);
		}
		return false;
	}

	/// <summary>
	///     Sets the value of the AutoWireViewModel attached property.
	/// </summary>
	/// <param name="obj">The dependency object that has this attached property.</param>
	/// <param name="value">if set to <c>true</c> the view model wiring will be performed.</param>
	public static void SetAutoWireViewModel(DependencyObject obj, bool value)
	{
		obj?.SetValue(AutoWireViewModelProperty, value);
	}
}
