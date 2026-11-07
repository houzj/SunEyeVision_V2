using System;

namespace VM.Controls.PropertyGrid.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CategoryOrderAttribute : Attribute
{
	public int Order { get; set; }

	public virtual string Category => CategoryValue;

	public string CategoryValue { get; private set; }

	public CategoryOrderAttribute()
	{
	}

	public CategoryOrderAttribute(string categoryName, int order)
		: this()
	{
		CategoryValue = categoryName;
		Order = order;
	}
}
