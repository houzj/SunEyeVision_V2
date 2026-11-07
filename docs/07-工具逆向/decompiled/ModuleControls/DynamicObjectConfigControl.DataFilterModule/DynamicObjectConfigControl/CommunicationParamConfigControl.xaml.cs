using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Interface;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

public partial class CommunicationParamConfigControl : Control, IParentNode, IComponentConnector
{
	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(CommunicationParamConfigControl), new PropertyMetadata(null));

	private IVmModule _module;

	private IParamsConfig _param;

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public IVmModule Module
	{
		get
		{
			if (_module != null)
			{
				return _module;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return _module;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return _module;
			}
			if (nameValueItem.GetTopBaseNode() is IOwner owner)
			{
				_module = owner.Owner as IVmModule;
			}
			return _module;
		}
	}

	public IParamsConfig Param
	{
		get
		{
			if (_param != null)
			{
				return _param;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return null;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return null;
			}
			if (nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_param = algorithmTabRoot.UserStringData;
			}
			return _param;
		}
	}

	public CommunicationParamConfigControl()
	{
		InitializeComponent();
	}

	public object GetParentNode()
	{
		throw new NotImplementedException();
	}

	public void SetParentNode(object parentNode)
	{
		throw new NotImplementedException();
	}
}
