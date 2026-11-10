using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Service;

/// <summary>
/// 可被扩展装饰的项：原件由节点、工具项等实现，宿主据此外挂装饰服务。
/// </summary>
public interface IExtensionItem
{
    /// <summary>该项的装饰器，提供装饰过程所需的服务容器。</summary>
    ExtensionItemDecorator ExtensionDecorator { get; }
}

/// <summary>
/// 可装饰数据源：对外暴露用来解析显示名与身份的原始对象。
/// </summary>
public interface IDecorate
{
    /// <summary>被装饰的源对象。</summary>
    object? DecorateSource { get; }
}

/// <summary>
/// 扩展项装饰器：为扩展项注册并分发装饰服务，对应原件 ExtensionItemDecorator。
/// </summary>
public sealed class ExtensionItemDecorator
{
    private readonly ExtensionDecorator _decorator = new();

    public ExtensionItemDecorator(FrameworkElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    /// <summary>被装饰的扩展项元素。</summary>
    public FrameworkElement Owner { get; }

    /// <summary>装饰过程可用的服务容器。</summary>
    public IServiceProvider Services => _decorator.Services;

    /// <summary>注册服务实例，供装饰器在装饰时取用。</summary>
    public void Register<T>(T service) where T : class => _decorator.Register(service);
}

/// <summary>
/// 扩展网格：承载扩展子项的容器，对应原件 ExtensionGrid。
/// </summary>
public class ExtensionGrid : Grid
{
    /// <summary>扩展项附加的装饰器，子类可在模板就绪后注册服务。</summary>
    public ExtensionItemDecorator? ExtensionDecorator { get; protected set; }
}

/// <summary>
/// 扩展服务端：由工作区实现，负责在鼠标事件上分派装饰扩展。
/// </summary>
public abstract class DefaultExtensionServer
{
    /// <summary>扩展作用的目标元素，通常是工作区面板。</summary>
    protected UIElement? Context { get; private set; }

    /// <summary>绑定作用目标并触发初始化。</summary>
    public void Attach(UIElement context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        OnInitialized();
    }

    /// <summary>初始化回调，子类在此挂接需要的事件。</summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>判断某个扩展项当前是否应被装饰。</summary>
    public virtual bool ShouldApplyExtensions(IExtensionItem extendedItem) => true;
}

/// <summary>
/// 携带源对象的事件参数，供节点、工具项的数据查询回调使用。
/// </summary>
public class ObjectEventArgs : EventArgs
{
    /// <summary>附加的参数对象。</summary>
    public object? Param { get; set; }
}

/// <summary>
/// 图形页事件参数，携带落点与附加数据。
/// </summary>
public class DiagramEventArgs : ObjectEventArgs
{
    /// <summary>画布坐标系中的位置。</summary>
    public Point Point { get; set; }
}
