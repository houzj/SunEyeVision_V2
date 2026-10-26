using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ShapeTempConverter : IValueConverter
{
	private static List<Tuple<Type, Type>> _ControlTypeAndInterfaceDic;

	private static List<Tuple<Type, Type>> ControlTypeAndInterfaceDic
	{
		get
		{
			if (_ControlTypeAndInterfaceDic == null)
			{
				_ControlTypeAndInterfaceDic = (from p in typeof(ROIItem).Assembly.GetTypes()
					select new Tuple<Type, Type>(p, p.GetCustomAttribute<ShapeDisplayForInterfaceTypeAttribute>()?.InterfaceType) into p
					where p.Item2 != null
					select p).ToList();
			}
			return _ControlTypeAndInterfaceDic;
		}
	}

	public static Type GetControlTypeByInterfaceType(Type dataType)
	{
		Type interfaceType = dataType.GetInterfaces().SingleOrDefault((Type p) => typeof(IShape).IsAssignableFrom(p) && p != typeof(IShape));
		if (interfaceType != null)
		{
			Type type = ControlTypeAndInterfaceDic.FirstOrDefault((Tuple<Type, Type> p) => p.Item2.Equals(interfaceType))?.Item1;
			if (type != null)
			{
				return type;
			}
			return null;
		}
		return null;
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value != null && !(value.ToString() == "{DisconnectedItem}"))
			{
				Type controlTypeByInterfaceType = GetControlTypeByInterfaceType(value.GetType());
				return Activator.CreateInstance(controlTypeByInterfaceType);
			}
		}
		catch
		{
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
