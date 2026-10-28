using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;

namespace SunEyeVision.Plugin.Wpf.Adapters.Text;

/// <summary>One host snapshot. Tokens are formatted from formal identities, never display labels.</summary>
public sealed record TextExpressionSnapshot(
    IReadOnlyList<DeclaredReferenceCandidate> Candidates,
    Func<DeclaredReferenceCandidate,string> FormatExpression,
    Func<bool> IsCurrent);

/// <summary>Edits constant text containing host-owned expressions; does not resolve or execute them.</summary>
public sealed record TextExpressionOptions(
    Func<TextExpressionSnapshot> ReadCandidates,
    Func<string,string?>? ValidateText=null)
{
    internal void Validate(Port port)
    {
        ArgumentNullException.ThrowIfNull(ReadCandidates);
        if(port.Type.Shape!=ValueShape.Scalar||port.Type.Scalar!=ScalarKind.Text
            ||port.EditModes!=FieldEditModes.None&&(port.EditModes&FieldEditModes.Constant)==0||port.Choices is not null)
            throw new ArgumentException("文本表达式只适用于可编辑的非枚举字符串。");
    }
}
