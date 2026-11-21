using System.Globalization;
using System.Text;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>
/// 原厂 IfBranchText 的静态格式接缝。原厂行格式为
/// name:min:max:state:runModuleId;，状态值为 1 或 2，未选择运行模块使用 -1。
/// </summary>
public static class IfBranchTextCodec
{
    public const int OkState = 1;
    public const int NgState = 2;
    public const int UnselectedRunModuleId = -1;
    public const double IntegerMinimum = -999_999_999d;
    public const double IntegerMaximum = 999_999_999d;
    public const double FloatMinimum = -100_000d;
    public const double FloatMaximum = 100_000d;
    public const double DefaultMinimum = 1d;
    public const double DefaultMaximum = 1000d;
    /// <summary>原厂动态容器初始显示一条 int 条件行。</summary>
    public const string DefaultSerialized = "int0:1:1000:1:-1;";

    public sealed record Row(string Name, double Minimum, double Maximum, int State, int RunModuleId)
    {
        public bool IsInteger => Name.Contains("int", StringComparison.OrdinalIgnoreCase);
    }

    public static string Serialize(IEnumerable<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var materialized = rows.ToArray();
        if (materialized.Length == 0) return "0";

        var builder = new StringBuilder();
        foreach (var row in materialized)
        {
            Validate(row);
            builder.Append(row.Name).Append(':')
                .Append(Format(row.Minimum)).Append(':')
                .Append(Format(row.Maximum)).Append(':')
                .Append(row.State.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(row.RunModuleId.ToString(CultureInfo.InvariantCulture)).Append(';');
        }
        return builder.ToString();
    }

    public static IReadOnlyList<Row> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "0") return Array.Empty<Row>();
        var rows = new List<Row>();
        foreach (var item in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(':');
            if (parts.Length != 5 || string.IsNullOrWhiteSpace(parts[0]))
                throw new FormatException("IfBranchText 行必须包含名称、上下限、状态和运行模块 ID。");
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var state)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var runModuleId))
                throw new FormatException("IfBranchText 行包含无效数值。");
            var row = new Row(parts[0], minimum, maximum, state, runModuleId);
            Validate(row);
            rows.Add(row);
        }
        return rows.AsReadOnly();
    }

    private static void Validate(Row row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(row.Name)) throw new ArgumentException("IfBranchText 行名称不能为空。", nameof(row));
        var minimum = row.IsInteger ? IntegerMinimum : FloatMinimum;
        var maximum = row.IsInteger ? IntegerMaximum : FloatMaximum;
        if (!double.IsFinite(row.Minimum) || !double.IsFinite(row.Maximum)
            || row.Minimum < minimum || row.Maximum > maximum || row.Minimum > row.Maximum)
            throw new ArgumentOutOfRangeException(nameof(row), "IfBranchText 行范围超出原厂边界。");
        if (row.State is not OkState and not NgState)
            throw new ArgumentOutOfRangeException(nameof(row), "IfBranchText 状态必须是 OK 或 NG。");
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
