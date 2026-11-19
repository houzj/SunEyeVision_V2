using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>
/// 结果页显示节点的声明解析。对应原厂 Display.xml 的对象清单：
/// 工具在 XML 里声明「哪个端口显示成什么名字、如何分组」，平台负责建树与校验。
/// </summary>
public static class OutputDisplayDeclaration
{
    /// <summary>
    /// 解析 &lt;OutputNodes&gt; 段；缺失时返回空，由使用方决定是否允许。
    /// <paramref name="resolveId"/> 用于把 XML 里的简写身份映射成真实端口身份。
    /// </summary>
    public static IReadOnlyList<OutputDisplayNode> Load(XElement? section, Func<string, string>? resolveId = null)
    {
        if (section is null) return Array.Empty<OutputDisplayNode>();
        var resolve = resolveId ?? (id => id);
        var nodes = section.Elements("Node").Select(item => Node(item, resolve)).ToArray();
        // 隐藏项只为注销端口在结果树中的显示，与可见节点同级，不参与兄弟唯一性之外的结构校验。
        var hidden = section.Elements("Hidden").Select(item => new OutputDisplayNode(
            resolve(Required(item, "id")), Required(item, "label"), displayInResultPanel: false)).ToArray();
        return OutputDisplayNode.Freeze(nodes.Concat(hidden));
    }

    /// <summary>校验声明的叶子与端口集合一致；端口 id 必须全部存在。</summary>
    public static void Validate(IReadOnlyList<OutputDisplayNode> nodes, IReadOnlyList<Port> ports)
        => OutputDisplayNode.Validate(nodes, ports);

    private static OutputDisplayNode Node(XElement element, Func<string, string> resolve)
    {
        Shape(element);
        var children = element.Elements("Node").Select(item => Node(item, resolve)).ToArray();
        return new OutputDisplayNode(
            resolve(Required(element, "id")),
            Required(element, "label"),
            children.Length == 0 ? null : children,
            isAuxiliaryImage: Flag(element, "auxiliary"),
            displayInResultPanel: element.Attribute("panel") is null || Flag(element, "panel"),
            isModuleExecutionSummary: Flag(element, "summary"),
            portId: Optional(element, "port") is { } port ? resolve(port) : null,
            memberPath: Optional(element, "member")?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Shape(XElement element)
    {
        string[] attributes = { "id", "label", "port", "member", "auxiliary", "panel", "summary" };
        if (element.Name.NamespaceName.Length != 0
            || element.Attributes().Any(a => a.Name.NamespaceName.Length != 0 || !attributes.Contains(a.Name.LocalName))
            || element.Elements().Any(e => e.Name.NamespaceName.Length != 0 || (e.Name.LocalName != "Node" && e.Name.LocalName != "Hidden"))
            || element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
            throw new InvalidDataException("Unknown output display element/attribute/content: " + element.Name);
    }

    private static string Required(XElement element, string name)
        => Optional(element, name) ?? throw new InvalidDataException("Missing/empty output display attribute: " + name);

    private static string? Optional(XElement element, string name)
        => (string?)element.Attribute(name) is { Length: > 0 } value ? value : null;

    private static bool Flag(XElement element, string name)
        => element.Attribute(name) is not { } item
            || (bool.TryParse(item.Value, out var value) ? value
                : throw new InvalidDataException("Invalid output display flag: " + name));
}
