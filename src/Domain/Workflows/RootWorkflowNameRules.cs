namespace SunEyeVision.Domain.Workflows;

/// <summary>Root workflow naming: character restrictions follow the observed setter;
/// trimming, length and ordinal case-insensitive uniqueness are platform policy.</summary>
public static class RootWorkflowNameRules
{
    private static readonly char[] Forbidden = ['?', ';', '.', '[', ']', '<', '>', '(', ')', '`'];

    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 64 || normalized.IndexOfAny(Forbidden) >= 0)
            throw new ArgumentException("根流程名称须为 1 至 64 个字符，且不能包含 ? ; . [ ] < > ( ) `。", nameof(name));
        return normalized;
    }

    public static void ValidateStored(string name)
    {
        if (!string.Equals(name, Normalize(name), StringComparison.Ordinal))
            throw new ArgumentException("保存的根流程名称不能含首尾空白。", nameof(name));
    }
}
