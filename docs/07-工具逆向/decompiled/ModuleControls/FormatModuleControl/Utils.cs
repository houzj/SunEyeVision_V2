using System;
using System.Windows;
using System.Windows.Media;
using Apps.Json;

internal static class Utils
{
	public static T FindVisualParent<T>(DependencyObject obj) where T : class
	{
		while (obj != null)
		{
			if (!(obj is T result))
			{
				obj = VisualTreeHelper.GetParent(obj);
				continue;
			}
			return result;
		}
		return null;
	}

	public static string ToJson(this object source)
	{
		return JsonConvert.SerializeObject(source);
	}

	public static T DeSerializationFromJson<T>(this string jsonstring) where T : class, new()
	{
		T result = null;
		try
		{
			result = JsonConvert.DeserializeObject(jsonstring, typeof(T)) as T;
			return result;
		}
		catch (Exception)
		{
		}
		return result;
	}
}
