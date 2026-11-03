namespace SunEyeVision.Domain.Workflows;

/// <summary>Node identity within one workflow; the owning document session is checked separately.</summary>
public sealed record ScopedNodeIdentity(string WorkflowId, string NodeId);
