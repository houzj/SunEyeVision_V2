using System;
using System.Collections.Generic;

namespace VM.Controls.PropertyGrid.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class NewItemTypesAttribute : Attribute
{
	public IList<Type> Types { get; set; }

	public NewItemTypesAttribute(params Type[] types)
	{
		Types = new List<Type>(types);
	}

	public NewItemTypesAttribute()
	{
	}
}
