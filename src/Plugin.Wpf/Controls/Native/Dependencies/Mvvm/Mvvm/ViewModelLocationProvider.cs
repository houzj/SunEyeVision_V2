using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm;

/// <summary>
///     The ViewModelLocationProvider class locates the view model for the view that has the AutoWireViewModelChanged
///     attached property set to true.
///     The view model will be located and injected into the view's DataContext. To locate the view, two strategies are
///     used: First the ViewModelLocationProvider
///     will look to see if there is a view model factory registered for that view, if not it will try to infer the view
///     model using a convention based approach.
///     This class also provide methods for registering the view model factories,
///     and also to override the default view model factory and the default view type to view model type resolver.
/// </summary>
public static class ViewModelLocationProvider
{
	/// <summary>
	///     A dictionary that contains all the registered factories for the views.
	/// </summary>
	private static readonly Dictionary<string, Func<object>> Factories = new Dictionary<string, Func<object>>();

	/// <summary>
	///     The default view model factory.
	/// </summary>
	private static Func<Type, object> _defaultViewModelFactory = (Type type) => Activator.CreateInstance(type);

	/// <summary>
	///     Default view type to view model type resolver, assumes the view model is in same assembly as the view type, but in
	///     the "ViewModels" namespace.
	/// </summary>
	private static Func<Type, Type> _defaultViewTypeToViewModelTypeResolver = (Type viewType) =>
	{
		string fullName = viewType.FullName;
		fullName = fullName.Replace(".Views.", ".ViewModels.");
		string fullName2 = viewType.GetTypeInfo().Assembly.FullName;
		return Type.GetType(string.Format(CultureInfo.InvariantCulture, "{0}ViewModel, {1}", new object[2] { fullName, fullName2 }));
	};

	/// <summary>
	///     Sets the default view model factory.
	/// </summary>
	/// <param name="viewModelFactory">The view model factory.</param>
	public static void SetDefaultViewModelFactory(Func<Type, object> viewModelFactory)
	{
		_defaultViewModelFactory = viewModelFactory;
	}

	/// <summary>
	///     Sets the default view type to view model type resolver.
	/// </summary>
	/// <param name="viewTypeToViewModelTypeResolver">The view type to view model type resolver.</param>
	public static void SetDefaultViewTypeToViewModelTypeResolver(Func<Type, Type> viewTypeToViewModelTypeResolver)
	{
		_defaultViewTypeToViewModelTypeResolver = viewTypeToViewModelTypeResolver;
	}

	/// <summary>
	///     Automatically looks up the viewmodel that corresponds to the current view, using two strategies:
	///     It first looks to see if there is a mapping registered for that view, if not it will fallback to the convention
	///     based approach.
	/// </summary>
	/// <param name="view">The dependency object, typically a view.</param>
	public static void AutoWireViewModelChanged(IView view)
	{
		object obj = GetViewModelForView(view);
		if (obj == null)
		{
			Type type = _defaultViewTypeToViewModelTypeResolver(view.GetType());
			if (type == null)
			{
				return;
			}
			obj = _defaultViewModelFactory(type);
		}
		view.DataContext = obj;
	}

	/// <summary>
	///     Gets the view model for the specified view.
	/// </summary>
	/// <param name="view">The view that the view model wants.</param>
	/// <returns>The vie wmodel that corresponds to the view passed as a parameter.</returns>
	private static object GetViewModelForView(IView view)
	{
		if (Factories.ContainsKey(view.GetType().ToString()))
		{
			return Factories[view.GetType().ToString()]();
		}
		return null;
	}

	/// <summary>
	///     Registers the view model factory for the specified view type name.
	/// </summary>
	/// <param name="viewTypeName">The name of the view type.</param>
	/// <param name="factory">The viewmodel factory.</param>
	public static void Register(string viewTypeName, Func<object> factory)
	{
		Factories[viewTypeName] = factory;
	}
}
