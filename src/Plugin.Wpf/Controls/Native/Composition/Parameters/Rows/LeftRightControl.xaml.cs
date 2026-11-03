using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Rows;

public partial class LeftRightControl : Grid, IComponentConnector
{
	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register("LeftContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null, OnLeftContentChanged));

	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register("RightContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null, OnRightContentChanged));

	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(LeftRightControl), new PropertyMetadata(89.0));

	public static readonly DependencyProperty IsDisplayLeftProperty = DependencyProperty.Register("IsDisplayLeft", typeof(bool), typeof(LeftRightControl), new PropertyMetadata(true));

	public object LeftContent
	{
		get
		{
			return GetValue(LeftContentProperty);
		}
		set
		{
			SetValue(LeftContentProperty, value);
		}
	}

	public object RightContent
	{
		get
		{
			return GetValue(RightContentProperty);
		}
		set
		{
			SetValue(RightContentProperty, value);
		}
	}

	public double ChildLeftWidth
	{
		get
		{
			return (double)GetValue(ChildLeftWidthProperty);
		}
		set
		{
			SetValue(ChildLeftWidthProperty, value);
		}
	}

	public bool IsDisplayLeft
	{
		get
		{
			return (bool)GetValue(IsDisplayLeftProperty);
		}
		set
		{
			SetValue(IsDisplayLeftProperty, value);
		}
	}

	private static void OnLeftContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as LeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		children.Remove((d as LeftRightControl).PartText);
		if (uIElement != null)
		{
			children.Insert(0, uIElement);
			uIElement.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF666666")));
			uIElement.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		}
	}

	private static void OnRightContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as LeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		if (uIElement != null)
		{
			children.Add(uIElement);
			uIElement.SetValue(Grid.ColumnProperty, 1);
			uIElement.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
		}
	}

	public LeftRightControl()
	{
		InitializeComponent();
        Loaded += OnAttached;
        IsVisibleChanged += (_, _) => { if (IsVisible) QueueAncestorBindings(); };
        Unloaded += (_, _) =>
        {
            _attachmentGeneration++;
            _attachment?.Abort();
            _attachment = null;
        };
	}
    // A row initially collapsed can retain a failed ancestor expression after attachment.
    // Reattach only the original exact-host bindings; preserve explicit consumer values.
    private DispatcherOperation? _attachment;
    private long _attachmentGeneration;
    private void OnAttached(object sender, RoutedEventArgs args) => QueueAncestorBindings();
    private void QueueAncestorBindings()
    {
        _attachment?.Abort();
        var generation = ++_attachmentGeneration;
        _attachment = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (generation != _attachmentGeneration || !IsLoaded || !IsVisible) return;
            _attachment = null;
            DependencyObject? ancestor = VisualTreeHelper.GetParent(this);
            while (ancestor is not null && ancestor is not SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core.ParamListView)
                ancestor = VisualTreeHelper.GetParent(ancestor);
            if (ancestor is null) return;
            Reattach(MarginProperty, "ChildMargin", false);
            Reattach(ChildLeftWidthProperty, "ChildLeftWidth", true);
        }));
    }
    private void Reattach(DependencyProperty property, string path, bool styleLease)
    {
        var expression = BindingOperations.GetBindingExpression(this, property);
        if (expression is null || expression.Status == BindingStatus.Active) return;
        var binding = expression.ParentBinding;
        if (binding.Path?.Path != path || binding.RelativeSource?.Mode != RelativeSourceMode.FindAncestor
            || binding.RelativeSource.AncestorType != typeof(SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core.ParamListView)) return;
        if (binding.RelativeSource.AncestorLevel != 1) return;
        if (!ReferenceEquals(expression, BindingOperations.GetBindingExpression(this, property))) return;
        if (styleLease && ReadLocalValue(property) == DependencyProperty.UnsetValue)
        {
            // Preserve the original style trigger and its absent-host converter fallback.
            var originalStyle = Style;
            if (originalStyle is null || !ReferenceEquals(originalStyle, TryFindResource(typeof(LeftRightControl)))) return;
            SetCurrentValue(StyleProperty, null);
            SetCurrentValue(StyleProperty, originalStyle);
            return;
        }
        BindingOperations.ClearBinding(this, property);
        BindingOperations.SetBinding(this, property, binding);
    }

}
