using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Models.Visualization;

/// <summary>
/// An explicit source address local to one ToolResults instance. FieldId is the declared
/// output port ID; member paths contain stable declared field IDs, never display names.
/// This address does not identify a run or infer an element from its display position.
/// </summary>
public sealed class DeclaredElementAddress
{
    public DeclaredElementAddress(string fieldId, long elementId, IReadOnlyList<string> memberFieldPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (elementId < 0) throw new ArgumentOutOfRangeException(nameof(elementId));
        ArgumentNullException.ThrowIfNull(memberFieldPath);
        var path = new string[memberFieldPath.Count];
        for (var index = 0; index < path.Length; index++)
        {
            var field = memberFieldPath[index];
            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("Member paths require nonempty declared field IDs.", nameof(memberFieldPath));
            path[index] = field;
        }
        FieldId = fieldId;
        ElementId = elementId;
        MemberFieldPath = Array.AsReadOnly(path);
    }

    public string FieldId { get; }
    public long ElementId { get; }
    public IReadOnlyList<string> MemberFieldPath { get; }
}
