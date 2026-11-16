using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Service;

/// <summary>
/// 装饰层宿主：把装饰层面板暴露给扩展装饰器，坐标基准为工作区。
/// </summary>
public interface IExtensionProvidePanel
{
    /// <summary>承载装饰面板的图层，坐标与工作区一致。</summary>
    Panel AdornerLayer { get; }
}

/// <summary>
/// 扩展装饰器：为宿主控件提供装饰面板所需的生命周期与坐标基准。
/// </summary>
public interface IExtensionDecorator
{
    /// <summary>装饰器可用的服务容器。</summary>
    IServiceProvider Services { get; }
}

/// <summary>
/// 装饰面板基类：子元素以被装饰元素的左上角为坐标基准。
/// </summary>
public class AdornerPanel : Panel
{
    /// <summary>被装饰的元素。</summary>
    public FrameworkElement? AdornedElement { get; private set; }

    /// <summary>工作区元素，提供装饰面板的坐标原点与尺寸。</summary>
    public FrameworkElement? WorkAreaElement { get; set; }

    /// <summary>被装饰元素附带的标记，供具体装饰器区分用途。</summary>
    public object? AdornedTag { get; private set; }

    /// <summary>被装饰元素的变换是否换算到工作区坐标。</summary>
    public bool IsAdornedElementTransformToWorkAreaElement { get; set; } = true;

    /// <summary>装饰面板相对工作区的左偏移。</summary>
    public double Left { get; set; }

    /// <summary>装饰面板相对工作区的上偏移。</summary>
    public double Top { get; set; }

    /// <summary>工作区缩放变换，供装饰内容按比例换算。</summary>
    public Transform? Transform { get; set; }

    /// <summary>子面板相对装饰面板的左偏移。</summary>
    public double OffsetLeft { get; private set; }

    /// <summary>子面板相对装饰面板的上偏移。</summary>
    public double OffsetTop { get; private set; }

    /// <summary>设置子面板偏移并重排。</summary>
    public void SetOffsets(double left, double top)
    {
        OffsetLeft = left;
        OffsetTop = top;
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>设置被装饰元素并刷新布局。</summary>
    public void SetAdornedElement(FrameworkElement element, object? tag)
    {
        ArgumentNullException.ThrowIfNull(element);
        AdornedElement = element;
        AdornedTag = tag;
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>装饰面板铺满工作区。</summary>
    protected virtual Size ResolvePanelSize(Size availableSize)
        => WorkAreaElement?.RenderSize is { Width: > 0, Height: > 0 } size ? size : availableSize;

    /// <summary>子元素的布局槽，基类以被装饰元素为准。</summary>
    protected virtual Rect ResolveChildArrangeRect(Size panelSize)
    {
        FrameworkElement? adorned = AdornedElement;
        if (adorned is null)
        {
            return new Rect(panelSize);
        }

        FrameworkElement root = WorkAreaElement ?? adorned;
        Point origin = adorned.TranslatePoint(new Point(0, 0), root);
        return new Rect(origin, new Size(adorned.ActualWidth, adorned.ActualHeight));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size panelSize = ResolvePanelSize(availableSize);
        Size childConstraint = ResolveChildArrangeRect(panelSize).Size;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childConstraint);
        }

        return panelSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size panelSize = ResolvePanelSize(finalSize);
        Rect arrangeRect = ResolveChildArrangeRect(panelSize);
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(arrangeRect);
        }

        return panelSize;
    }
}

/// <summary>
/// 附着到工作区的装饰面板：子元素直接使用工作区坐标，不做元素级偏移。
/// </summary>
public class AdornerPanelAttachToWorkArea : AdornerPanel
{
    protected override Rect ResolveChildArrangeRect(Size panelSize) => new(panelSize);
}

/// <summary>
/// 装饰器提供者：为一个宿主元素维护一组装饰面板。
/// </summary>
public abstract class AdornerProvider
{
    private readonly List<AdornerPanel> _adorners = new();

    /// <summary>该提供者维护的装饰面板。</summary>
    public IList<AdornerPanel> Adorners => _adorners;

    /// <summary>扩展目标元素。</summary>
    public FrameworkElement? ExtendedItem { get; private set; }

    /// <summary>是否已完成初始化。</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>绑定扩展目标并触发初始化。</summary>
    public void Initialize(FrameworkElement extendedItem)
    {
        ArgumentNullException.ThrowIfNull(extendedItem);
        ExtendedItem = extendedItem;
        OnInitialized();
        IsInitialized = true;
    }

    /// <summary>移除装饰并释放引用。</summary>
    public void Remove()
    {
        OnRemove();
        _adorners.Clear();
        ExtendedItem = null;
        IsInitialized = false;
    }

    /// <summary>初始化回调，子类在此创建装饰面板。</summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>移除回调，子类在此释放装饰资源。</summary>
    protected virtual void OnRemove()
    {
    }
}

/// <summary>
/// 装饰器服务容器，按类型提供装饰过程所需的服务实例。
/// </summary>
public sealed class ExtensionDecorator : IExtensionDecorator
{
    private readonly Dictionary<Type, object> _services = new();

    public ExtensionDecorator()
    {
        Services = new DecoratorServiceProvider(_services);
    }

    /// <inheritdoc />
    public IServiceProvider Services { get; }

    /// <summary>注册服务实例，同类型后注册者覆盖先注册者。</summary>
    public void Register<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    private sealed class DecoratorServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public DecoratorServiceProvider(Dictionary<Type, object> services) => _services = services;

        public object? GetService(Type serviceType)
            => _services.TryGetValue(serviceType, out object? service) ? service : null;
    }
}
