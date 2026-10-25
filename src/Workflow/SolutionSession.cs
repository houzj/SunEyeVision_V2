using System;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using SunEyeVision.Workflow.Persistence;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow;

/// <summary>One editable document, its optional location, and its saved content baseline.</summary>
public sealed class SolutionSession
{
    public Solution Document { get; }
    private DocumentEditSession? _editing;
    public DocumentEditSession Editing => _editing ??= new DocumentEditSession(Document);
    public string? FilePath => Document.FilePath;
    public bool IsUntitled => string.IsNullOrEmpty(FilePath);
    private string? _savedContent;
    public bool IsDirty
    {
        get
        {
            try { return _editing?.HasPendingChanges == true || SolutionSemanticComparer.Canonicalize(Serialize(Document)) != _savedContent; }
            catch (Exception)
            { return true; } // Incomplete editor values must not appear clean or break the status display.
        }
    }
    internal SolutionSession(Solution document) => Document = document;
    internal static string Serialize(Solution document)
    {
        // Capture one value generation; JSON converters and IO run after releasing ValueGate.
        var variables = GlobalVariable.CaptureDefinitions(document.GlobalVariables).ToList();
        var source = WorkflowSerializationOptions.Default;
        var options = new JsonSerializerOptions(source)
        {
            TypeInfoResolver = (source.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(info =>
            {
                if (info.Type != typeof(Solution)) return;
                var value = info.Properties.FirstOrDefault(p => p.Name == nameof(Solution.GlobalVariables));
                if (value != null) value.Get = _ => variables;
            })
        };
        return JsonSerializer.Serialize(document, options);
    }
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
