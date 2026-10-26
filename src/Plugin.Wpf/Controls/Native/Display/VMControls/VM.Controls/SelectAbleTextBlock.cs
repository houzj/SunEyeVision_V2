using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Apps.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class SelectAbleTextBlock : TextBlock, INotifyPropertyChanged
{
	private TextPointer _startpoz;

	private TextPointer _endpoz;

	public static readonly DependencyProperty SelectionBrushProperty = DependencyProperty.Register("SelectionBrush", typeof(Brush), typeof(SelectAbleTextBlock), new FrameworkPropertyMetadata(Brushes.LightBlue));

	private readonly DelegateCommand _copycommand;

	public TextRange Selection { get; private set; }

	public bool HasSelection
	{
		get
		{
			if (Selection != null)
			{
				return !Selection.IsEmpty;
			}
			return false;
		}
	}

	public Brush SelectionBrush
	{
		get
		{
			return (Brush)GetValue(SelectionBrushProperty);
		}
		set
		{
			SetValue(SelectionBrushProperty, value);
		}
	}

	public ICommand CopyCommand => _copycommand;

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler SelectionChanged;

	public SelectAbleTextBlock()
	{
		Focusable = true;
		_copycommand = new DelegateCommand(Copy);
		OnPropertyChanged("CopyCommand");
		ContextMenu = new HikContextMenu();
		MenuItem menuItem = new MenuItem
		{
			Command = CopyCommand,
			Header = AppLocalizationService.GetLang("Lang_Copy"),
			InputGestureText = "Ctrl+C"
		};
		Binding binding = new Binding
		{
			Source = this,
			Path = new PropertyPath("HasSelection"),
			Mode = BindingMode.Default
		};
		menuItem.SetBinding(UIElement.IsEnabledProperty, binding);
		ContextMenu.Items?.Add(menuItem);
	}

	protected void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		Keyboard.Focus(this);
		ReleaseMouseCapture();
		base.OnMouseLeftButtonUp(e);
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		Point position = e.GetPosition(this);
		_startpoz = GetPositionFromPoint(position, snapToText: true);
		CaptureMouse();
		base.OnMouseLeftButtonDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (IsMouseCaptured)
		{
			Point position = e.GetPosition(this);
			_endpoz = GetPositionFromPoint(position, snapToText: true);
			ClearSelection();
			Selection = new TextRange(_startpoz, _endpoz);
			Selection.ApplyPropertyValue(TextElement.BackgroundProperty, SelectionBrush);
			CommandManager.InvalidateRequerySuggested();
			OnSelectionChanged(EventArgs.Empty);
			OnPropertyChanged("HasSelection");
		}
		base.OnMouseMove(e);
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		if (Keyboard.Modifiers == ModifierKeys.Control)
		{
			switch (e.Key)
			{
			case Key.C:
				Copy();
				break;
			case Key.A:
				SelectAll();
				break;
			}
		}
		base.OnKeyUp(e);
	}

	protected override void OnLostFocus(RoutedEventArgs e)
	{
		ClearSelection();
	}

	public void Copy()
	{
		if (HasSelection)
		{
			Clipboard.SetDataObject(Selection.Text);
		}
	}

	public void ClearSelection()
	{
		new TextRange(ContentStart, ContentEnd).ApplyPropertyValue(TextElement.BackgroundProperty, null);
		Selection = null;
		OnPropertyChanged("HasSelection");
	}

	public void SelectAll()
	{
		Selection = new TextRange(ContentStart, ContentEnd);
		Selection.ApplyPropertyValue(TextElement.BackgroundProperty, SelectionBrush);
		OnPropertyChanged("HasSelection");
	}

	protected virtual void OnSelectionChanged(EventArgs e)
	{
		SelectionChanged?.Invoke(this, e);
	}
}
