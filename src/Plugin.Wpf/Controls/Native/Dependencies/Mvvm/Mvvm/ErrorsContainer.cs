using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm;

/// <summary>
///     Manages validation errors for an object, notifying when the error state changes.
/// </summary>
/// <typeparam name="T">The type of the error object.</typeparam>
public class ErrorsContainer<T>
{
	private static readonly T[] NoErrors = new T[0];

	protected readonly Action<string> RaiseErrorsChanged;

	protected readonly Dictionary<string, List<T>> ValidationResults;

	/// <summary>
	///     Gets a value indicating whether the object has validation errors.
	/// </summary>
	public bool HasErrors => ValidationResults.Count != 0;

	/// <summary>
	///     Initializes a new instance of the <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.ErrorsContainer`1" /> class.
	/// </summary>
	/// <param name="raiseErrorsChanged">
	///     The action that invoked if when errors are added for an object./&gt;
	///     event.
	/// </param>
	public ErrorsContainer(Action<string> raiseErrorsChanged)
	{
		if (raiseErrorsChanged == null)
		{
			throw new ArgumentNullException("raiseErrorsChanged");
		}
		RaiseErrorsChanged = raiseErrorsChanged;
		ValidationResults = new Dictionary<string, List<T>>();
	}

	/// <summary>
	///     Gets the validation errors for a specified property.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <returns>The validation errors of type <typeparamref name="T" /> for the property.</returns>
	public IEnumerable<T> GetErrors(string propertyName)
	{
		string key = propertyName ?? string.Empty;
		if (ValidationResults.TryGetValue(key, out var value))
		{
			return value;
		}
		return NoErrors;
	}

	/// <summary>
	///     Clears the errors for the property indicated by the property expression.
	/// </summary>
	/// <typeparam name="TProperty">The property type.</typeparam>
	/// <param name="propertyExpression">The expression indicating a property.</param>
	/// <example>
	///     container.ClearErrors(()=&gt;SomeProperty);
	/// </example>
	public void ClearErrors<TProperty>(Expression<Func<TProperty>> propertyExpression)
	{
		string propertyName = PropertySupport.ExtractPropertyName(propertyExpression);
		ClearErrors(propertyName);
	}

	/// <summary>
	///     Clears the errors for a property.
	/// </summary>
	/// <param name="propertyName">The name of th property for which to clear errors.</param>
	/// <example>
	///     container.ClearErrors("SomeProperty");
	/// </example>
	public void ClearErrors(string propertyName)
	{
		SetErrors(propertyName, new List<T>());
	}

	/// <summary>
	///     Sets the validation errors for the specified property.
	/// </summary>
	/// <typeparam name="TProperty">The property type for which to set errors.</typeparam>
	/// <param name="propertyExpression">The <see cref="T:System.Linq.Expressions.Expression" /> indicating the property.</param>
	/// <param name="propertyErrors">The list of errors to set for the property.</param>
	public void SetErrors<TProperty>(Expression<Func<TProperty>> propertyExpression, IEnumerable<T> propertyErrors)
	{
		string propertyName = PropertySupport.ExtractPropertyName(propertyExpression);
		SetErrors(propertyName, propertyErrors);
	}

	/// <summary>
	///     Sets the validation errors for the specified property.
	/// </summary>
	/// <remarks>
	///     If a change is detected then the errors changed event is raised.
	/// </remarks>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="newValidationResults">The new validation errors.</param>
	public void SetErrors(string propertyName, IEnumerable<T> newValidationResults)
	{
		string text = propertyName ?? string.Empty;
		bool flag = ValidationResults.ContainsKey(text);
		T[] array = (newValidationResults as T[]) ?? newValidationResults.ToArray();
		bool flag2 = newValidationResults != null && array.Any();
		if (flag | flag2)
		{
			if (flag2)
			{
				ValidationResults[text] = new List<T>(array);
				RaiseErrorsChanged(text);
			}
			else
			{
				ValidationResults.Remove(text);
				RaiseErrorsChanged(text);
			}
		}
	}
}
