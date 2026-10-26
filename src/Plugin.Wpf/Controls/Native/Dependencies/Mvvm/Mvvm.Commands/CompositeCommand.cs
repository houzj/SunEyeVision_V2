using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Properties;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;

/// <summary>
///     The CompositeCommand composes one or more ICommands.
/// </summary>
public class CompositeCommand : ICommand
{
	private readonly bool _monitorCommandActivity;

	private readonly EventHandler _onRegisteredCommandCanExecuteChangedHandler;

	private readonly List<ICommand> _registeredCommands = new List<ICommand>();

	private List<WeakReference> _canExecuteChangedHandlers;

	/// <summary>
	///     Gets the list of all the registered commands.
	/// </summary>
	/// <value>A list of registered commands.</value>
	/// <remarks>This returns a copy of the commands subscribed to the CompositeCommand.</remarks>
	public IList<ICommand> RegisteredCommands
	{
		get
		{
			lock (_registeredCommands)
			{
				return _registeredCommands.ToList();
			}
		}
	}

	/// <summary>
	///     Occurs when any of the registered commands raise <see cref="E:System.Windows.Input.ICommand.CanExecuteChanged" />. You must keep a hard
	///     reference to the handler to avoid garbage collection and unexpected results. See remarks for more information.
	/// </summary>
	/// <remarks>
	///     When subscribing to the <see cref="E:System.Windows.Input.ICommand.CanExecuteChanged" /> event using
	///     code (not when binding using XAML) will need to keep a hard reference to the event handler. This is to prevent
	///     garbage collection of the event handler because the command implements the Weak Event pattern so it does not have
	///     a hard reference to this handler. An example implementation can be seen in the CompositeCommand and
	///     CommandBehaviorBase
	///     classes. In most scenarios, there is no reason to sign up to the CanExecuteChanged event directly, but if you do,
	///     you
	///     are responsible for maintaining the reference.
	/// </remarks>
	/// <example>
	///     The following code holds a reference to the event handler. The myEventHandlerReference value should be stored
	///     in an instance member to avoid it from being garbage collected.
	///     <code>
	/// EventHandler myEventHandlerReference = new EventHandler(this.OnCanExecuteChanged);
	/// command.CanExecuteChanged += myEventHandlerReference;
	/// </code>
	/// </example>
	public event EventHandler CanExecuteChanged
	{
		add
		{
			WeakEventHandlerManager.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
		}
		remove
		{
			WeakEventHandlerManager.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
		}
	}

	/// <summary>
	///     Initializes a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.CompositeCommand" />.
	/// </summary>
	public CompositeCommand()
	{
		_onRegisteredCommandCanExecuteChangedHandler = OnRegisteredCommandCanExecuteChanged;
	}

	/// <summary>
	///     Initializes a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.CompositeCommand" />.
	/// </summary>
	/// <param name="monitorCommandActivity">Indicates when the command activity is going to be monitored.</param>
	public CompositeCommand(bool monitorCommandActivity)
		: this()
	{
		_monitorCommandActivity = monitorCommandActivity;
	}

	/// <summary>
	///     Forwards <see cref="M:System.Windows.Input.ICommand.CanExecute(System.Object)" /> to the registered commands and returns
	///     <see langword="true" /> if all of the commands return <see langword="true" />.
	/// </summary>
	/// <param name="parameter">
	///     Data used by the command.
	///     If the command does not require data to be passed, this object can be set to <see langword="null" />.
	/// </param>
	/// <returns>
	///     <see langword="true" /> if all of the commands return <see langword="true" />; otherwise,
	///     <see langword="false" />.
	/// </returns>
	public virtual bool CanExecute(object parameter)
	{
		bool result = false;
		ICommand[] array;
		lock (_registeredCommands)
		{
			array = _registeredCommands.ToArray();
		}
		ICommand[] array2 = array;
		foreach (ICommand command in array2)
		{
			if (ShouldExecute(command))
			{
				if (!command.CanExecute(parameter))
				{
					return false;
				}
				result = true;
			}
		}
		return result;
	}

	/// <summary>
	///     Forwards <see cref="M:System.Windows.Input.ICommand.Execute(System.Object)" /> to the registered commands.
	/// </summary>
	/// <param name="parameter">
	///     Data used by the command.
	///     If the command does not require data to be passed, this object can be set to <see langword="null" />.
	/// </param>
	public virtual void Execute(object parameter)
	{
		Queue<ICommand> queue;
		lock (_registeredCommands)
		{
			queue = new Queue<ICommand>(_registeredCommands.Where(ShouldExecute).ToList());
		}
		while (queue.Count > 0)
		{
			queue.Dequeue().Execute(parameter);
		}
	}

	/// <summary>
	///     Adds a command to the collection and signs up for the <see cref="E:System.Windows.Input.ICommand.CanExecuteChanged" /> event of it.
	/// </summary>
	/// <remarks>
	///     If this command is set to monitor command activity, and <paramref name="command" />
	///     implements the <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.IActiveAware" /> interface, this method will subscribe to its
	///     <see cref="E:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.IActiveAware.IsActiveChanged" /> event.
	/// </remarks>
	/// <param name="command">The command to register.</param>
	public virtual void RegisterCommand(ICommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		if (command == this)
		{
			throw new ArgumentException(Resources.CannotRegisterCompositeCommandInItself);
		}
		lock (_registeredCommands)
		{
			if (_registeredCommands.Contains(command))
			{
				throw new InvalidOperationException(Resources.CannotRegisterSameCommandTwice);
			}
			_registeredCommands.Add(command);
		}
		command.CanExecuteChanged += _onRegisteredCommandCanExecuteChangedHandler;
		OnCanExecuteChanged();
		if (_monitorCommandActivity && command is IActiveAware activeAware)
		{
			activeAware.IsActiveChanged += OnIsActiveChanged;
		}
	}

	/// <summary>
	///     Removes a command from the collection and removes itself from the <see cref="E:System.Windows.Input.ICommand.CanExecuteChanged" /> event
	///     of it.
	/// </summary>
	/// <param name="command">The command to unregister.</param>
	public virtual void UnregisterCommand(ICommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		bool flag;
		lock (_registeredCommands)
		{
			flag = _registeredCommands.Remove(command);
		}
		if (flag)
		{
			command.CanExecuteChanged -= _onRegisteredCommandCanExecuteChangedHandler;
			OnCanExecuteChanged();
			if (_monitorCommandActivity && command is IActiveAware activeAware)
			{
				activeAware.IsActiveChanged -= OnIsActiveChanged;
			}
		}
	}

	private void OnRegisteredCommandCanExecuteChanged(object sender, EventArgs e)
	{
		OnCanExecuteChanged();
	}

	/// <summary>
	///     Evaluates if a command should execute.
	/// </summary>
	/// <param name="command">The command to evaluate.</param>
	/// <returns>
	///     A <see cref="T:System.Boolean" /> value indicating whether the command should be used
	///     when evaluating <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.CompositeCommand.CanExecute(System.Object)" /> and <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.CompositeCommand.Execute(System.Object)" />.
	/// </returns>
	/// <remarks>
	///     If this command is set to monitor command activity, and <paramref name="command" />
	///     implements the <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.IActiveAware" /> interface,
	///     this method will return <see langword="false" /> if the command's <see cref="P:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.IActiveAware.IsActive" />
	///     property is <see langword="false" />; otherwise it always returns <see langword="true" />.
	/// </remarks>
	protected virtual bool ShouldExecute(ICommand command)
	{
		IActiveAware activeAware = command as IActiveAware;
		if (_monitorCommandActivity && activeAware != null)
		{
			return activeAware.IsActive;
		}
		return true;
	}

	/// <summary>
	///     Raises <see cref="E:System.Windows.Input.ICommand.CanExecuteChanged" /> on the UI thread so every
	///     command invoker can requery <see cref="M:System.Windows.Input.ICommand.CanExecute(System.Object)" /> to check if the
	///     <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.CompositeCommand" /> can execute.
	/// </summary>
	protected virtual void OnCanExecuteChanged()
	{
		WeakEventHandlerManager.CallWeakReferenceHandlers(this, _canExecuteChangedHandlers);
	}

	/// <summary>
	///     Handler for IsActiveChanged events of registered commands.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">EventArgs to pass to the event.</param>
	private void OnIsActiveChanged(object sender, EventArgs e)
	{
		OnCanExecuteChanged();
	}
}
