using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class AutoSelectTextBox : TextBox
{
	public static readonly DependencyProperty AutoSelectBehaviorProperty;

	public static readonly DependencyProperty AutoMoveFocusProperty;

	public static readonly RoutedEvent QueryMoveFocusEvent;

	public AutoSelectBehavior AutoSelectBehavior
	{
		get
		{
			return (AutoSelectBehavior)GetValue(AutoSelectBehaviorProperty);
		}
		set
		{
			SetValue(AutoSelectBehaviorProperty, value);
		}
	}

	public bool AutoMoveFocus
	{
		get
		{
			return (bool)GetValue(AutoMoveFocusProperty);
		}
		set
		{
			SetValue(AutoMoveFocusProperty, value);
		}
	}

	static AutoSelectTextBox()
	{
		AutoSelectBehaviorProperty = DependencyProperty.Register("AutoSelectBehavior", typeof(AutoSelectBehavior), typeof(AutoSelectTextBox), new UIPropertyMetadata(AutoSelectBehavior.Never));
		AutoMoveFocusProperty = DependencyProperty.Register("AutoMoveFocus", typeof(bool), typeof(AutoSelectTextBox), new UIPropertyMetadata(false));
		QueryMoveFocusEvent = EventManager.RegisterRoutedEvent("QueryMoveFocus", RoutingStrategy.Bubble, typeof(QueryMoveFocusEventHandler), typeof(AutoSelectTextBox));
		AutomationProperties.AutomationIdProperty.OverrideMetadata(typeof(AutoSelectTextBox), new UIPropertyMetadata("AutoSelectTextBox"));
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (!AutoMoveFocus)
		{
			base.OnPreviewKeyDown(e);
			return;
		}
		if (e.Key == Key.Left && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
		{
			e.Handled = MoveFocusLeft();
		}
		if (e.Key == Key.Right && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
		{
			e.Handled = MoveFocusRight();
		}
		if ((e.Key == Key.Up || e.Key == Key.Prior) && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
		{
			e.Handled = MoveFocusUp();
		}
		if ((e.Key == Key.Down || e.Key == Key.Next) && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control))
		{
			e.Handled = MoveFocusDown();
		}
		base.OnPreviewKeyDown(e);
	}

	protected override void OnPreviewGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnPreviewGotKeyboardFocus(e);
		if (AutoSelectBehavior == AutoSelectBehavior.OnFocus && !TreeHelper.IsDescendantOf(e.OldFocus as DependencyObject, this))
		{
			SelectAll();
		}
	}

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		if (AutoSelectBehavior != AutoSelectBehavior.Never && !IsKeyboardFocusWithin)
		{
			Focus();
			e.Handled = true;
		}
	}

	protected override void OnTextChanged(TextChangedEventArgs e)
	{
		base.OnTextChanged(e);
		if (AutoMoveFocus && Text.Length != 0 && Text.Length == MaxLength && CaretIndex == MaxLength && CanMoveFocus(FocusNavigationDirection.Right, reachedMax: true))
		{
			FocusNavigationDirection focusNavigationDirection = ((FlowDirection == FlowDirection.LeftToRight) ? FocusNavigationDirection.Right : FocusNavigationDirection.Left);
			MoveFocus(new TraversalRequest(focusNavigationDirection));
		}
	}

	private bool CanMoveFocus(FocusNavigationDirection direction, bool reachedMax)
	{
		QueryMoveFocusEventArgs e = new QueryMoveFocusEventArgs(direction, reachedMax);
		RaiseEvent(e);
		return e.CanMoveFocus;
	}

	private bool MoveFocusLeft()
	{
		if (FlowDirection == FlowDirection.LeftToRight)
		{
			if (CaretIndex == 0 && SelectionLength == 0)
			{
				if (ComponentCommands.MoveFocusBack.CanExecute(null, this))
				{
					ComponentCommands.MoveFocusBack.Execute(null, this);
					return true;
				}
				if (CanMoveFocus(FocusNavigationDirection.Left, reachedMax: false))
				{
					MoveFocus(new TraversalRequest(FocusNavigationDirection.Left));
					return true;
				}
			}
		}
		else if (CaretIndex == Text.Length && SelectionLength == 0)
		{
			if (ComponentCommands.MoveFocusBack.CanExecute(null, this))
			{
				ComponentCommands.MoveFocusBack.Execute(null, this);
				return true;
			}
			if (CanMoveFocus(FocusNavigationDirection.Left, reachedMax: false))
			{
				MoveFocus(new TraversalRequest(FocusNavigationDirection.Left));
				return true;
			}
		}
		return false;
	}

	private bool MoveFocusRight()
	{
		if (FlowDirection == FlowDirection.LeftToRight)
		{
			if (CaretIndex == Text.Length && SelectionLength == 0)
			{
				if (ComponentCommands.MoveFocusForward.CanExecute(null, this))
				{
					ComponentCommands.MoveFocusForward.Execute(null, this);
					return true;
				}
				if (CanMoveFocus(FocusNavigationDirection.Right, reachedMax: false))
				{
					MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
					return true;
				}
			}
		}
		else if (CaretIndex == 0 && SelectionLength == 0)
		{
			if (ComponentCommands.MoveFocusForward.CanExecute(null, this))
			{
				ComponentCommands.MoveFocusForward.Execute(null, this);
				return true;
			}
			if (CanMoveFocus(FocusNavigationDirection.Right, reachedMax: false))
			{
				MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
				return true;
			}
		}
		return false;
	}

	private bool MoveFocusUp()
	{
		if (GetLineIndexFromCharacterIndex(SelectionStart) == 0)
		{
			if (ComponentCommands.MoveFocusUp.CanExecute(null, this))
			{
				ComponentCommands.MoveFocusUp.Execute(null, this);
				return true;
			}
			if (CanMoveFocus(FocusNavigationDirection.Up, reachedMax: false))
			{
				MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
				return true;
			}
		}
		return false;
	}

	private bool MoveFocusDown()
	{
		if (GetLineIndexFromCharacterIndex(SelectionStart) == LineCount - 1)
		{
			if (ComponentCommands.MoveFocusDown.CanExecute(null, this))
			{
				ComponentCommands.MoveFocusDown.Execute(null, this);
				return true;
			}
			if (CanMoveFocus(FocusNavigationDirection.Down, reachedMax: false))
			{
				MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
				return true;
			}
		}
		return false;
	}
}
