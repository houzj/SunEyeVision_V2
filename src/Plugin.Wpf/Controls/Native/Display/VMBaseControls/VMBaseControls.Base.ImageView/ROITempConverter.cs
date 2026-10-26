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
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ROITempConverter : IValueConverter
{
	private static List<Tuple<Type, Type>> _ControlTypeAndInterfaceDic;

	private static List<Tuple<Type, Type>> ControlTypeAndInterfaceDic
	{
		get
		{
			if (_ControlTypeAndInterfaceDic == null)
			{
				_ControlTypeAndInterfaceDic = (from p in typeof(ROIItem).Assembly.GetTypes()
					select new Tuple<Type, Type>(p, p.GetCustomAttribute<ROIDisplayForInterfaceTypeAttribute>()?.InterfaceType) into p
					where p.Item2 != null
					select p).ToList();
			}
			return _ControlTypeAndInterfaceDic;
		}
	}

	public static Type GetControlTypeByInterfaceType(Type dataType)
	{
		Type interfaceType = null;
		IEnumerable<Type> enumerable = from p in dataType.GetInterfaces()
			where typeof(IROI).IsAssignableFrom(p) && p != typeof(IROI)
			select p;
		int num = enumerable.Count();
		if (num == 1)
		{
			interfaceType = enumerable.First();
		}
		else if (num > 1)
		{
			int num2 = enumerable.Max((Type p) => (from pt in p.GetInterfaces()
				where typeof(IROI).IsAssignableFrom(pt)
				select pt).Count());
			foreach (Type item in enumerable)
			{
				int num3 = (from p in item.GetInterfaces()
					where typeof(IROI).IsAssignableFrom(p)
					select p).Count();
				if (num3 == num2)
				{
					interfaceType = item;
					break;
				}
			}
		}
		if (interfaceType != null)
		{
			Type type = ControlTypeAndInterfaceDic.FirstOrDefault((Tuple<Type, Type> p) => p.Item2.Equals(interfaceType))?.Item1;
			if (type != null)
			{
				return type;
			}
			throw new ArgumentNullException("Can't Find Display ROI Control Type");
		}
		Type type2 = ControlTypeAndInterfaceDic.FirstOrDefault((Tuple<Type, Type> p) => p.Item2.Equals(dataType))?.Item1;
		if (type2 != null)
		{
			return type2;
		}
		IEnumerable<Type> source = from p in dataType.GetInterfaces()
			where typeof(IToolROI).IsAssignableFrom(p)
			select p;
		if (source.Count() != 0)
		{
			return null;
		}
		throw new ArgumentNullException("Can't Find Display ROI Control Type");
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null && !(value.ToString() == "{DisconnectedItem}"))
		{
			Type controlTypeByInterfaceType = GetControlTypeByInterfaceType(value.GetType());
			ROIControlBase rOIControlBase = Activator.CreateInstance(controlTypeByInterfaceType) as ROIControlBase;
			rOIControlBase.DataContext = value;
			return rOIControlBase;
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
