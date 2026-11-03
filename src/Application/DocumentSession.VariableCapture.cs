using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application;

internal sealed record VariableDraftEntry(GlobalVariable Actual, GlobalVariable Opening);
internal sealed record VariableDraftCapture(DocumentSession Session, long ChangeRevision,
    IReadOnlyList<VariableDraftEntry> Entries);

public sealed partial class DocumentSession
{
    /// <summary>Capture actual membership and value/configuration baselines under both owning gates.</summary>
    internal VariableDraftCapture CaptureVariablesForEdit()
    {
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            lock (GlobalVariable.ValueGate)
            {
                var actual = Document.GlobalVariables.ToArray();
                if (actual.Any(variable => string.IsNullOrWhiteSpace(variable.Id)) ||
                    actual.Select(variable => variable.Id).Distinct(StringComparer.Ordinal).Count() != actual.Length)
                    throw new InvalidOperationException("方案变量身份为空或不唯一。");
                var opening = GlobalVariable.CaptureDefinitions(actual);
                EnsureOpen();
                if (Document.Id != _documentId || Document.GlobalVariables.Count != actual.Length ||
                    actual.Where((variable, index) => !ReferenceEquals(Document.GlobalVariables[index], variable)).Any())
                    throw new InvalidOperationException("捕获期间方案变量归属已变化。");
                var entries = actual.Select((variable, index) => new VariableDraftEntry(variable, opening[index])).ToArray();
                return new VariableDraftCapture(this, ChangeRevision, Array.AsReadOnly(entries));
            }
        }
    }
}
