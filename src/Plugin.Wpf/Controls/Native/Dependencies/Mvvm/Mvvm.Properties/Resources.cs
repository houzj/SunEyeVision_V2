using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Properties;

/// <summary>
///   Класс ресурса со строгой типизацией для поиска локализованных строк и т.д.
/// </summary>
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	/// <summary>
	///   Возвращает кэшированный экземпляр ResourceManager, использованный этим классом.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Properties.Resources", typeof(Resources).Assembly);
			}
			return resourceMan;
		}
	}

	/// <summary>
	///   Перезаписывает свойство CurrentUICulture текущего потока для всех
	///   обращений к ресурсу с помощью этого класса ресурса со строгой типизацией.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	/// <summary>
	///   Ищет локализованную строку, похожую на Cannot register a CompositeCommand in itself..
	/// </summary>
	internal static string CannotRegisterCompositeCommandInItself => ResourceManager.GetString("CannotRegisterCompositeCommandInItself", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на Cannot register the same command twice in the same CompositeCommand..
	/// </summary>
	internal static string CannotRegisterSameCommandTwice => ResourceManager.GetString("CannotRegisterSameCommandTwice", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на Neither the executeMethod nor the canExecuteMethod delegates can be null..
	/// </summary>
	internal static string DelegateCommandDelegatesCannotBeNull => ResourceManager.GetString("DelegateCommandDelegatesCannotBeNull", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на T for DelegateCommand&lt;T&gt; is not an object nor Nullable..
	/// </summary>
	internal static string DelegateCommandInvalidGenericPayloadType => ResourceManager.GetString("DelegateCommandInvalidGenericPayloadType", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на The member access expression does not access a property..
	/// </summary>
	internal static string PropertySupport_ExpressionNotProperty_Exception => ResourceManager.GetString("PropertySupport_ExpressionNotProperty_Exception", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на The expression is not a member access expression..
	/// </summary>
	internal static string PropertySupport_NotMemberAccessExpression_Exception => ResourceManager.GetString("PropertySupport_NotMemberAccessExpression_Exception", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на The referenced property is a static property..
	/// </summary>
	internal static string PropertySupport_StaticExpression_Exception => ResourceManager.GetString("PropertySupport_StaticExpression_Exception", resourceCulture);

	/// <summary>
	///   Ищет локализованную строку, похожую на .
	/// </summary>
	internal static string WeakEventHandlerNotConstructedOnUIThread => ResourceManager.GetString("WeakEventHandlerNotConstructedOnUIThread", resourceCulture);

	internal Resources()
	{
	}
}
