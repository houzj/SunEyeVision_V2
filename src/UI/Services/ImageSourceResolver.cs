using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.UI.Services;

public enum ImageSourceResolutionKind { Found, None, Ambiguous, InvalidSelection }
public sealed record ImageSourceResolution(ImageSourceResolutionKind Kind, NodeDefinition? Source = null);

/// <summary>Resolve one preview source using actual session graph identities and admitted host capabilities.</summary>
public static class ImageSourceResolver
{
 public static ImageSourceResolution Resolve(DocumentSession session, WorkflowDefinition root, NodeDefinition target)
 {
  ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(root);
  ArgumentNullException.ThrowIfNull(target);
  if (root.IsSubroutine || !session.GetWorkflows().Any(flow => ReferenceEquals(flow, root)))
   return new(ImageSourceResolutionKind.InvalidSelection);
  var nodes = session.GetNodes(root.Id).ToArray();
  if (!nodes.Any(node => ReferenceEquals(node, target)) || nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() != nodes.Length)
   return new(ImageSourceResolutionKind.InvalidSelection);
  var revision = session.ChangeRevision;
  var configurations = nodes.ToDictionary(node => node.Id, node => node.Configuration, StringComparer.Ordinal);
  var byId = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
  var upstream = session.GetConnections(root.Id).GroupBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
   .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
  var resolving = new HashSet<string>(StringComparer.Ordinal);
  bool IsImageSource(NodeDefinition node) => session.GetNodeHostInputDeclaration(root.Id, node) is
   { CapabilityId: "image-source", Version: 1 };
  ImageSourceResolution ResolveBranch(string id)
  {
   if (!byId.TryGetValue(id, out var start)) return new(ImageSourceResolutionKind.None);
   if (!resolving.Add(id)) return new(ImageSourceResolutionKind.InvalidSelection);
   try
   {
    if (IsImageSource(start)) return new(ImageSourceResolutionKind.Found, start);
    var visited = new HashSet<string>(StringComparer.Ordinal) { id };
    var pending = new Queue<string>(); var sources = new List<NodeDefinition>(); pending.Enqueue(id);
    while (pending.TryDequeue(out var current))
    {
     if (!upstream.TryGetValue(current, out var parents)) continue;
     foreach (var parentId in parents)
     {
      if (!visited.Add(parentId) || !byId.TryGetValue(parentId, out var parent)) continue;
      if (IsImageSource(parent)) sources.Add(parent); else pending.Enqueue(parentId);
     }
    }
    string? selectedId = null;
    if (start.SelectedDisplayReference is { } expression)
    {
     try
     {
      var reference = ParameterSettingCanonical.Deserialize(expression);
      if (reference.ScopeId != root.Id) return new(ImageSourceResolutionKind.InvalidSelection);
      selectedId = reference.NodeId;
     }
     catch (System.Text.Json.JsonException) { return new(ImageSourceResolutionKind.InvalidSelection); }
    }
    if (!string.IsNullOrEmpty(selectedId) && selectedId != id)
     return visited.Contains(selectedId) ? ResolveBranch(selectedId) : new(ImageSourceResolutionKind.InvalidSelection);
    return sources.Count switch { 0 => new(ImageSourceResolutionKind.None), 1 => new(ImageSourceResolutionKind.Found, sources[0]), _ => new(ImageSourceResolutionKind.Ambiguous) };
   }
   finally { resolving.Remove(id); }
  }
  var result = ResolveBranch(target.Id);
  var currentNodes = session.GetNodes(root.Id);
  if (session.ChangeRevision != revision || !session.GetWorkflows().Any(flow => ReferenceEquals(flow, root)) ||
      currentNodes.Count != nodes.Length || nodes.Any(node => !currentNodes.Any(current => ReferenceEquals(current, node)) ||
       !ReferenceEquals(node.Configuration, configurations[node.Id])))
   throw new InvalidOperationException("图像来源解析期间方案、节点或配置已变化。");
  return result;
 }
}
