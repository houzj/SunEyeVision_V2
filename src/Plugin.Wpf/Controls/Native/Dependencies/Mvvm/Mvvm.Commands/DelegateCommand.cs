using System;
using System.Reflection;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Properties;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;

/// <summary>
///     An <see cref="T:System.Windows.Input.ICommand" /> whose delegates do not take any parameters for <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand.Execute" /> and
///     <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand.CanExecute" />.
/// </summary>
/// <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommandBase" />
/// <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" />
public class DelegateCommand : DelegateCommandBase
{
	/// <summary>
	///     Creates a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /> with the <see cref="T:System.Action" /> to invoke on execution.
	/// </summary>
	/// <param name="executeMethod">The <see cref="T:System.Action" /> to invoke when <see cref="M:System.Windows.Input.ICommand.Execute(System.Object)" /> is called.</param>
	public DelegateCommand(Action executeMethod)
		: this(executeMethod, () => true)
	{
	}

	/// <summary>
	///     Creates a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /> with the <see cref="T:System.Action" /> to invoke on execution
	///     and a <see langword="Func" /> to query for determining if the command can execute.
	/// </summary>
	/// <param name="executeMethod">The <see cref="T:System.Action" /> to invoke when <see cref="M:System.Windows.Input.ICommand.Execute(System.Object)" /> is called.</param>
	/// <param name="canExecuteMethod">
	///     The <see cref="T:System.Func`1" /> to invoke when <see cref="M:System.Windows.Input.ICommand.CanExecute(System.Object)" /> is
	///     called
	/// </param>
	/// <param name="isAutomaticRequeryDisabled"></param>
	public DelegateCommand(Action executeMethod, Func<bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
		: base((object o) =>
		{
			executeMethod();
		}, (object o) => canExecuteMethod(), isAutomaticRequeryDisabled)
	{
		if (executeMethod == null || canExecuteMethod == null)
		{
			throw new ArgumentNullException("executeMethod", Resources.DelegateCommandDelegatesCannotBeNull);
		}
	}

	private DelegateCommand(Func<Task> executeMethod)
		: this(executeMethod, () => true)
	{
	}

	private DelegateCommand(Func<Task> executeMethod, Func<bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
		: base((object o) => executeMethod(), (object o) => canExecuteMethod(), isAutomaticRequeryDisabled)
	{
		if (executeMethod == null || canExecuteMethod == null)
		{
			throw new ArgumentNullException("executeMethod", Resources.DelegateCommandDelegatesCannotBeNull);
		}
	}

	/// <summary>
	///     Factory method to create a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /> from an awaitable handler method.
	/// </summary>
	/// <param name="executeMethod">Delegate to execute when Execute is called on the command.</param>
	/// <returns>Constructed instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /></returns>
	public static DelegateCommand FromAsyncHandler(Func<Task> executeMethod)
	{
		return new DelegateCommand(executeMethod);
	}

	/// <summary>
	///     Factory method to create a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /> from an awaitable handler method.
	/// </summary>
	/// <param name="executeMethod">
	///     Delegate to execute when Execute is called on the command. This can be null to just hook up
	///     a CanExecute delegate.
	/// </param>
	/// <param name="canExecuteMethod">Delegate to execute when CanExecute is called on the command. This can be null.</param>
	/// <param name="isAutomaticRequeryDisabled"></param>
	/// <returns>Constructed instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand" /></returns>
	public static DelegateCommand FromAsyncHandler(Func<Task> executeMethod, Func<bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
	{
		return new DelegateCommand(executeMethod, canExecuteMethod, isAutomaticRequeryDisabled);
	}

	/// <summary>
	///     Executes the command.
	/// </summary>
	public virtual async Task Execute()
	{
		await Execute(null);
	}

	/// <summary>
	///     Determines if the command can be executed.
	/// </summary>
	/// <returns>Returns <see langword="true" /> if the command can execute, otherwise returns <see langword="false" />.</returns>
	public virtual bool CanExecute()
	{
		return CanExecute(null);
	}
}
/// <summary>
///     An <see cref="T:System.Windows.Input.ICommand" /> whose delegates can be attached for <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1.Execute(`0)" /> and <see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1.CanExecute(`0)" />
///     .
/// </summary>
/// <typeparam name="T">Parameter type.</typeparam>
/// <remarks>
///     The constructor deliberately prevents the use of value types.
///     Because ICommand takes an object, having a value type for T would cause unexpected behavior when CanExecute(null)
///     is called during XAML initialization for command bindings.
///     Using default(T) was considered and rejected as a solution because the implementor would not be able to distinguish
///     between a valid and defaulted values.
///     <para />
///     Instead, callers should support a value type by using a nullable value type and checking the HasValue property
///     before using the Value property.
///     <example>
///         <code>
/// public MyClass()
/// {
///     this.submitCommand = new DelegateCommand&lt;int?&gt;(this.Submit, this.CanSubmit);
/// }
///
/// private bool CanSubmit(int? customerId)
/// {
///     return (customerId.HasValue &amp;&amp; customers.Contains(customerId.Value));
/// }
///     </code>
///     </example>
/// </remarks>
public class DelegateCommand<T> : DelegateCommandBase
{
	/// <summary>
	///     Initializes a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" />.
	/// </summary>
	/// <param name="executeMethod">
	///     Delegate to execute when Execute is called on the command. This can be null to just hook up
	///     a CanExecute delegate.
	/// </param>
	/// <remarks><see cref="M:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1.CanExecute(`0)" /> will always return true.</remarks>
	public DelegateCommand(Action<T> executeMethod)
		: this(executeMethod, (Func<T, bool>)((T o) => true), false)
	{
	}

	/// <summary>
	///     Initializes a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" />.
	/// </summary>
	/// <param name="executeMethod">
	///     Delegate to execute when Execute is called on the command. This can be null to just hook up
	///     a CanExecute delegate.
	/// </param>
	/// <param name="canExecuteMethod">Delegate to execute when CanExecute is called on the command. This can be null.</param>
	/// <param name="isAutomaticRequeryDisabled"></param>
	/// <exception cref="T:System.ArgumentNullException">
	///     When both <paramref name="executeMethod" /> and
	///     <paramref name="canExecuteMethod" /> ar <see langword="null" />.
	/// </exception>
	public DelegateCommand(Action<T> executeMethod, Func<T, bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
		: base((object o) =>
		{
			executeMethod((T)o);
		}, (object o) => canExecuteMethod((T)o), isAutomaticRequeryDisabled)
	{
		if (executeMethod == null || canExecuteMethod == null)
		{
			throw new ArgumentNullException("executeMethod", Resources.DelegateCommandDelegatesCannotBeNull);
		}
		TypeInfo typeInfo = typeof(T).GetTypeInfo();
		if (typeInfo.IsValueType && (!typeInfo.IsGenericType || !typeof(Nullable<>).GetTypeInfo().IsAssignableFrom(typeInfo.GetGenericTypeDefinition().GetTypeInfo())))
		{
			throw new InvalidCastException(Resources.DelegateCommandInvalidGenericPayloadType);
		}
	}

	private DelegateCommand(Func<T, Task> executeMethod)
		: this(executeMethod, (Func<T, bool>)((T o) => true), false)
	{
	}

	private DelegateCommand(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
		: base((object o) => executeMethod((T)o), (object o) => canExecuteMethod((T)o), isAutomaticRequeryDisabled)
	{
		if (executeMethod == null || canExecuteMethod == null)
		{
			throw new ArgumentNullException("executeMethod", Resources.DelegateCommandDelegatesCannotBeNull);
		}
	}

	/// <summary>
	///     Factory method to create a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" /> from an awaitable handler method.
	/// </summary>
	/// <param name="executeMethod">Delegate to execute when Execute is called on the command.</param>
	/// <returns>Constructed instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" /></returns>
	public static DelegateCommand<T> FromAsyncHandler(Func<T, Task> executeMethod)
	{
		return new DelegateCommand<T>(executeMethod);
	}

	/// <summary>
	///     Factory method to create a new instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" /> from an awaitable handler method.
	/// </summary>
	/// <param name="executeMethod">
	///     Delegate to execute when Execute is called on the command. This can be null to just hook up
	///     a CanExecute delegate.
	/// </param>
	/// <param name="canExecuteMethod">Delegate to execute when CanExecute is called on the command. This can be null.</param>
	/// <param name="isAutomaticRequeryDisabled"></param>
	/// <returns>Constructed instance of <see cref="T:SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands.DelegateCommand`1" /></returns>
	public static DelegateCommand<T> FromAsyncHandler(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod, bool isAutomaticRequeryDisabled = false)
	{
		return new DelegateCommand<T>(executeMethod, canExecuteMethod, isAutomaticRequeryDisabled);
	}

	/// <summary>
	///     Determines if the command can execute by invoked the <see cref="T:System.Func`2" /> provided during construction.
	/// </summary>
	/// <param name="parameter">Data used by the command to determine if it can execute.</param>
	/// <returns>
	///     <see langword="true" /> if this command can be executed; otherwise, <see langword="false" />.
	/// </returns>
	public virtual bool CanExecute(T parameter)
	{
		return CanExecute((object)parameter);
	}

	/// <summary>
	///     Executes the command and invokes the <see cref="T:System.Action`1" /> provided during construction.
	/// </summary>
	/// <param name="parameter">Data used by the command.</param>
	public virtual async Task Execute(T parameter)
	{
		await Execute(parameter);
	}
}
