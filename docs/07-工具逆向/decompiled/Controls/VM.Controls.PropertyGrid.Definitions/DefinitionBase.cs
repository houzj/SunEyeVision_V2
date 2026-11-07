using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;
using VM.Controls.Core.Utilities;

namespace VM.Controls.PropertyGrid.Definitions;

public abstract class DefinitionBase : DependencyObject
{
	private bool _isLocked;

	internal bool IsLocked => _isLocked;

	internal void ThrowIfLocked<TMember>(Expression<Func<TMember>> propertyExpression)
	{
		if (DesignerProperties.GetIsInDesignMode(this) || !IsLocked)
		{
			return;
		}
		string propertyOrFieldName = ReflectionHelper.GetPropertyOrFieldName(propertyExpression);
		throw new InvalidOperationException($"Cannot modify {propertyOrFieldName} once the definition has beed added to a collection.");
	}

	internal virtual void Lock()
	{
		if (!_isLocked)
		{
			_isLocked = true;
		}
	}
}
