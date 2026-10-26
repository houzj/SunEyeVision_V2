using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

internal class UIElementAdorner<TElement> : Adorner where TElement : UIElement
{
	private TElement _child;

	private double _offsetLeft;

	private double _offsetTop;

	public TElement Child
	{
		get
		{
			return _child;
		}
		set
		{
			if (value != _child)
			{
				if (_child != null)
				{
					RemoveLogicalChild(_child);
					RemoveVisualChild(_child);
				}
				_child = value;
				if (_child != null)
				{
					AddLogicalChild(_child);
					AddVisualChild(_child);
				}
			}
		}
	}

	public double OffsetLeft
	{
		get
		{
			return _offsetLeft;
		}
		set
		{
			_offsetLeft = value;
			UpdateLocation();
		}
	}

	public double OffsetTop
	{
		get
		{
			return _offsetTop;
		}
		set
		{
			_offsetTop = value;
			UpdateLocation();
		}
	}

	protected override IEnumerator LogicalChildren
	{
		get
		{
			ArrayList arrayList = new ArrayList();
			if (_child != null)
			{
				arrayList.Add(_child);
			}
			return arrayList.GetEnumerator();
		}
	}

	protected override int VisualChildrenCount
	{
		get
		{
			if (_child != null)
			{
				return 1;
			}
			return 0;
		}
	}

	public UIElementAdorner(UIElement adornedElement)
		: base(adornedElement)
	{
	}

	public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
	{
		return new GeneralTransformGroup
		{
			Children = 
			{
				base.GetDesiredTransform(transform),
				(GeneralTransform)new TranslateTransform(_offsetLeft, _offsetTop)
			}
		};
	}

	public void SetOffsets(double left, double top)
	{
		_offsetLeft = left;
		_offsetTop = top;
		UpdateLocation();
	}

	protected override Size MeasureOverride(Size constraint)
	{
		if (_child == null)
		{
			return base.MeasureOverride(constraint);
		}
		_child.Measure(constraint);
		return _child.DesiredSize;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		if (_child == null)
		{
			return base.ArrangeOverride(finalSize);
		}
		_child.Arrange(new Rect(finalSize));
		return finalSize;
	}

	protected override Visual GetVisualChild(int index)
	{
		return _child;
	}

	private void UpdateLocation()
	{
		if (Parent is AdornerLayer adornerLayer)
		{
			adornerLayer.Update(AdornedElement);
		}
	}
}
