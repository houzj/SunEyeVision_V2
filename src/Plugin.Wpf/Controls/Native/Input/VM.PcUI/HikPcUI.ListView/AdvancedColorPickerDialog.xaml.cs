using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using System.Runtime.ExceptionServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;

public partial class AdvancedColorPickerDialog : UserControl, IComponentConnector
{
	private byte[] _pixels;

	public Color CurrentColor = Colors.Gray;

	public static DependencyProperty SelectedCurrentColorProperty = DependencyProperty.Register("SelectedCurrentColor", typeof(SolidColorBrush), typeof(AdvancedColorPickerDialog), new PropertyMetadata(Brushes.White, OnAcceptedColorChanged));

	private bool _initialized;
	private bool _syncing;
	private bool _proposing;
	private Color _proposal;
	private bool _proposalTainted;
	private long _proposalGeneration;
	private long _syncGeneration;

	private readonly TranslateTransform _markerTransform = new TranslateTransform();

	public SolidColorBrush SelectedCurrentColor
	{
		get
		{
			return (SolidColorBrush)GetValue(SelectedCurrentColorProperty);
		}
		set
		{
			SetValue(SelectedCurrentColorProperty, value);
		}
	}

	public event EventHandler DialogResultEvent;

	public event DragDeltaEventHandler Drag;

	private void OnDragDelta(DragDeltaEventArgs e)
	{
		Drag?.Invoke(this, e);
	}

	public AdvancedColorPickerDialog()
	{
		InitializeComponent();
		colorMarker.RenderTransform = _markerTransform;
		colorMarker.RenderTransformOrigin = new Point(1.0, 1.0);
		borderColorChart.Cursor = Cursors.Cross;
		_initialized = true;
		SynchronizeAccepted(SelectedCurrentColor.Color);
		brightnessSlider.ValueChanged += BrightnessSliderValueChanged;
		IsVisibleChanged += (_, _) =>
		{
			if (IsVisible && SelectedCurrentColor is SolidColorBrush accepted) SynchronizeAccepted(accepted.Color);
		};
	}

	private static void OnAcceptedColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		var picker = (AdvancedColorPickerDialog)sender;
		if (!picker._initialized || e.NewValue is not SolidColorBrush brush) return;
		if (picker._proposing)
		{
			if (!picker._proposalTainted && SameArgb(brush.Color, picker._proposal))
			{ picker.CurrentColor = brush.Color; return; }
			picker._proposalTainted = true;
		}
		picker.SynchronizeAccepted(brush.Color);
	}

	private void SynchronizeAccepted(Color color)
	{
		var generation = ++_syncGeneration;
		var wasSyncing = _syncing;
		_syncing = true;
		try
		{
			CurrentColor = color;
			_pixels = new[] { color.B, color.G, color.R, color.A };
			Exception failure = null;
			try { UpdateSlider(); }
			catch (Exception error) { failure = error; }
			try { if (generation == _syncGeneration) brightnessSlider.Value = 0.5; }
			catch (Exception error)
			{
				if (failure != null) throw new AggregateException(failure, error);
				throw;
			}
			if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
		}
		finally { _syncing = wasSyncing; }
	}

	private static bool SameArgb(Color a, Color b) => a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;

	private bool Submit(Color color)
	{
		if (!IsLoaded || !IsVisible || DataContext is not ColorPickerControlView owner) return false;
		var binding = GetBindingExpression(SelectedCurrentColorProperty);
		var ownerBinding = owner.GetBindingExpression(ColorPickerControlView.CurrentColorProperty);
		var ownerContext = owner.DataContext;
		var ownerSource = ownerBinding?.DataItem;
		var ticket = ++_proposalGeneration;
		_proposal = color;
		_proposing = true;
		_proposalTainted = false;
		bool SameSession() => IsLoaded && IsVisible && ReferenceEquals(owner, DataContext)
			&& ReferenceEquals(binding, GetBindingExpression(SelectedCurrentColorProperty))
			&& ReferenceEquals(ownerBinding, owner.GetBindingExpression(ColorPickerControlView.CurrentColorProperty))
			&& ReferenceEquals(ownerContext, owner.DataContext) && ReferenceEquals(ownerSource, ownerBinding?.DataItem);
		Exception failure = null;
		try { owner.SubmitSelection(color); }
		catch (Exception error) { failure = error; }
		try
		{
			if (_proposalGeneration == ticket)
			{
				_proposing = false;
				if (SameSession()
					&& owner.CurrentColor is SolidColorBrush accepted
					&& (failure != null || _proposalTainted || !SameArgb(accepted.Color, color))) SynchronizeAccepted(accepted.Color);
			}
		}
		catch (Exception cleanup)
		{
			if (failure != null) throw new AggregateException(failure, cleanup);
			throw;
		}
		if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
		return _proposalGeneration == ticket && !_proposalTainted && SameSession()
			&& owner.CurrentColor is SolidColorBrush final && SameArgb(final.Color, color);
	}

	private void Image_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (IsLoaded && IsVisible && DataContext is ColorPickerControlView
			&& e.Source is Image image && TryReadChartPixel(image, out var pixels))
		{
			var marker = Mouse.GetPosition(borderColorChart);
			CommitChartPixel(pixels, marker);
		}
	}

	private static bool TryReadChartPixel(Image image, out byte[] pixels)
	{
		pixels = Array.Empty<byte>();
		if (image.Source is not BitmapSource source) return false;
		var point = Mouse.GetPosition(image);
		int x = (int)point.X, y = (int)point.Y;
		if (point.X < 0 || point.Y < 0 || x >= source.PixelWidth || y >= source.PixelHeight) return false;
		try
		{
			var sample = new CroppedBitmap(source, new Int32Rect(x, y, 1, 1));
			pixels = new byte[4];
			sample.CopyPixels(pixels, 4, 0);
			return true;
		}
		catch (ArgumentException) { return false; }
		catch (InvalidOperationException) { return false; }
	}

	private bool CommitChartPixel(byte[] pixels, Point marker)
	{
		var color = Color.FromRgb(pixels[2], pixels[1], pixels[0]);
		if (DataContext is not ColorPickerControlView owner) return false;
		var binding = GetBindingExpression(SelectedCurrentColorProperty);
		var ownerBinding = owner.GetBindingExpression(ColorPickerControlView.CurrentColorProperty);
		var ownerContext = owner.DataContext;
		var ownerSource = ownerBinding?.DataItem;
		if (!Submit(color)) return false;
		var generation = _syncGeneration + 1;
		bool Current() => IsLoaded && IsVisible && ReferenceEquals(owner, DataContext)
			&& ReferenceEquals(binding, GetBindingExpression(SelectedCurrentColorProperty))
			&& ReferenceEquals(ownerBinding, owner.GetBindingExpression(ColorPickerControlView.CurrentColorProperty))
			&& ReferenceEquals(ownerContext, owner.DataContext) && ReferenceEquals(ownerSource, ownerBinding?.DataItem)
			&& _syncGeneration == generation && owner.CurrentColor is SolidColorBrush accepted && SameArgb(accepted.Color, color);
		Exception failure = null;
		try { SynchronizeAccepted(color); }
		catch (Exception error) { failure = error; }
		try { if (Current()) UpdateMarkerPosition(marker, Current); }
		catch (Exception error)
		{
			if (failure != null) throw new AggregateException(failure, error);
			throw;
		}
		if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
		return Current();
	}

	private void UpdateMarkerPosition(Point marker, Func<bool> current)
	{
		_markerTransform.X = marker.X - borderColorChart.ActualWidth / 2.0;
		if (current()) _markerTransform.Y = marker.Y - borderColorChart.ActualHeight / 2.0;
	}

	private void BrightnessSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_syncing || !IsLoaded || !IsVisible || DataContext is not ColorPickerControlView) return;
		if (_pixels == null)
		{
			_pixels = new byte[3];
			_pixels[2] = CurrentColor.R;
			_pixels[1] = CurrentColor.G;
			_pixels[0] = CurrentColor.B;
		}
		Color color = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		float num = (float)e.NewValue;
		float r;
		float g;
		float b;
		if (num >= 0.5f)
		{
			r = color.ScR + (1f - color.ScR) * (num - 0.5f) * 2f;
			g = color.ScG + (1f - color.ScG) * (num - 0.5f) * 2f;
			b = color.ScB + (1f - color.ScB) * (num - 0.5f) * 2f;
		}
		else
		{
			r = color.ScR * num * 2f;
			g = color.ScG * num * 2f;
			b = color.ScB * num * 2f;
		}
		CurrentColor = Color.FromScRgb(1f, r, g, b);
		Submit(CurrentColor);
	}

	private void UpdateSlider()
	{
		var generation = _syncGeneration;
		Color color = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		float r = 1f * color.ScR;
		float g = 1f * color.ScG;
		float b = 1f * color.ScB;
		Border border = brightnessSliderBorder;
		LinearGradientBrush linearGradientBrush = border.Background as LinearGradientBrush;
		linearGradientBrush.StartPoint = new Point(0.0, 1.0);
		if (generation != _syncGeneration) return;
		linearGradientBrush.EndPoint = new Point(0.0, 0.0);
		if (generation != _syncGeneration) return;
		linearGradientBrush.GradientStops[0].Color = Color.FromScRgb(1f, 0f, 0f, 0f);
		if (generation != _syncGeneration) return;
		linearGradientBrush.GradientStops[1].Color = Color.FromScRgb(1f, r, g, b);
		if (generation != _syncGeneration) return;
		linearGradientBrush.GradientStops[2].Color = Color.FromScRgb(1f, 1f, 1f, 1f);
	}

	private void Image_MouseMove(object sender, MouseEventArgs e)
	{
		if (IsLoaded && IsVisible && DataContext is ColorPickerControlView
			&& Mouse.LeftButton == MouseButtonState.Pressed && e.Source is Image image
			&& Mouse.GetPosition(image).Y <= image.ActualHeight - 2.0 && TryReadChartPixel(image, out var pixels))
		{
			var marker = Mouse.GetPosition(borderColorChart);
			if (CommitChartPixel(pixels, marker)) Mouse.Synchronize();
		}
		e.Handled = true;
	}
}
