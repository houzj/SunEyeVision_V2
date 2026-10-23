using System;
using System.Text.Json;
using SunEyeVision.Workflow.Persistence;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow;

/// <summary>One editable document, its optional location, and its saved content baseline.</summary>
public sealed class SolutionSession
{
    public Solution Document { get; }
    public string? FilePath => Document.FilePath;
    public bool IsUntitled => string.IsNullOrEmpty(FilePath);
    private string? _savedContent;
    public bool IsDirty
    {
        get
        {
            try { return SolutionSemanticComparer.Canonicalize(Serialize(Document)) != _savedContent; }
            catch (Exception)
            { return true; } // Incomplete editor values must not appear clean or break the status display.
        }
    }
    internal SolutionSession(Solution document) => Document = document;
    internal static string Serialize(Solution document) => JsonSerializer.Serialize(document, WorkflowSerializationOptions.Default);
    internal void CaptureBaseline() => _savedContent = SolutionSemanticComparer.Canonicalize(Serialize(Document));
    internal void CommitSave(string path, string writtenContent)
    {
        Document.FilePath = path;
        Document.ResourceBaseDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        _savedContent = SolutionSemanticComparer.Canonicalize(writtenContent); // Never capture edits that arrived after the snapshot was taken.
    }
}

public sealed record SolutionSaveResult(bool FileWritten, string? Error = null, string? Warning = null)
{
    public SolutionSnapshot? WrittenSnapshot { get; init; }
}
