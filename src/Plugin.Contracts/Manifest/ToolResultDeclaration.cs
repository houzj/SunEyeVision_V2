using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Contracts.Manifest;

/// <summary>结果形态：行表（一条记录多行）或属性列（若干标量/图像端口各占一行）。</summary>
public enum ToolResultShape
{
    /// <summary>行表：单个行序列输出，列引用行内成员。</summary>
    Rows,
    /// <summary>属性列：每个字段是独立输出端口，列直接引用端口（原厂 PropertyList）。</summary>
    PropertyList
}

/// <summary>结果行上的一个成员（对应原件结果行 record 的端口）。</summary>
public sealed record ToolResultField
{
    public ToolResultField(string id, string label, TypeRef type, bool required = true,
        bool nullable = false, NumericStorage? storage = null, bool showInResultPanel = true,
        IReadOnlyList<ToolResultField>? children = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(type);
        var childList = (children ?? Array.Empty<ToolResultField>()).ToArray();
        if (childList.Any(child => child is null))
            throw new ArgumentException($"结果字段 {id} 的子节点不能包含空项。", nameof(children));
        if (childList.Select(child => child.Id).Distinct(StringComparer.Ordinal).Count() != childList.Length)
            throw new ArgumentException($"结果字段 {id} 的子节点身份重复。", nameof(children));
        Id = id;
        Label = label;
        Type = type;
        Required = required;
        Nullable = nullable;
        Storage = storage;
        ShowInResultPanel = showInResultPanel;
        Children = Array.AsReadOnly(childList);
    }

    public string Id { get; }
    public string Label { get; }
    public TypeRef Type { get; }
    public bool Required { get; }
    /// <summary>端口可空（原厂结果端口 Nullable）。</summary>
    public bool Nullable { get; }
    /// <summary>数值存储精度（原厂 Int32/Float32）。</summary>
    public NumericStorage? Storage { get; }
    /// <summary>是否出现在结果树（原厂 OutputDisplayNode.DisplayInResultPanel）。</summary>
    public bool ShowInResultPanel { get; }
    /// <summary>记录型字段在结果树里的子节点标签；为空时按成员 Id 生成。</summary>
    public IReadOnlyList<ToolResultField> Children { get; }
}

/// <summary>结果表的一列；<see cref="FieldId"/> 必须指向同一份声明里的 <see cref="ToolResultField"/>。</summary>
public sealed record ToolResultColumn
{
    public ToolResultColumn(string fieldId, string label, int? decimalPlaces = null, string unit = "", string? key = null,
        // 原厂属性列表列可自带叠加几何与几何来源（都指向行内成员）。
        string? geometryMember = null, string? geometrySourceMember = null,
        // 嵌套成员路径（如 oriented-bounds.angle-degrees）；给定时优先于 fieldId 单段路径。
        IReadOnlyList<string>? memberPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (decimalPlaces is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        FieldId = fieldId;
        Label = label;
        DecimalPlaces = decimalPlaces;
        Unit = unit ?? string.Empty;
        // 列标识可与字段身份不同（原厂显示列 Key 与字段名分离）。
        Key = string.IsNullOrWhiteSpace(key) ? fieldId : key;
        GeometryMember = string.IsNullOrWhiteSpace(geometryMember) ? null : geometryMember;
        GeometrySourceMember = string.IsNullOrWhiteSpace(geometrySourceMember) ? null : geometrySourceMember;
        MemberPath = memberPath is { Count: > 0 } ? memberPath : null;
    }

    public string FieldId { get; }
    public string Label { get; }
    public int? DecimalPlaces { get; }
    public string Unit { get; }
    public string Key { get; }
    /// <summary>列自带叠加几何的行内成员名（原厂属性列表的 Geometry）。</summary>
    public string? GeometryMember { get; }
    /// <summary>列叠加几何的来源区域成员名（原厂属性列表的 GeometrySource）。</summary>
    public string? GeometrySourceMember { get; }
    /// <summary>列的嵌套成员路径；为 null 时按 fieldId 单段取值。</summary>
    public IReadOnlyList<string>? MemberPath { get; }
}

/// <summary>
/// 结果显示槽位：Descriptor 需要的行内路径全部用字段身份表达，
/// 编译器据此生成 <c>ResultFieldPath</c>，工具不再手写 Descriptor。
/// </summary>
public sealed record ToolResultSlots
{
    public ToolResultSlots(string identity, string rectangle, string geometrySource, string textValue, string passed,
        string? innerRectangle = null, string? centroid = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity, nameof(identity));
        ArgumentException.ThrowIfNullOrWhiteSpace(rectangle, nameof(rectangle));
        ArgumentException.ThrowIfNullOrWhiteSpace(geometrySource, nameof(geometrySource));
        ArgumentException.ThrowIfNullOrWhiteSpace(textValue, nameof(textValue));
        ArgumentException.ThrowIfNullOrWhiteSpace(passed, nameof(passed));
        Identity = identity;
        Rectangle = rectangle;
        GeometrySource = geometrySource;
        TextValue = textValue;
        Passed = passed;
        InnerRectangle = innerRectangle;
        Centroid = centroid;
    }

    public string Identity { get; }
    public string Rectangle { get; }
    public string GeometrySource { get; }
    public string TextValue { get; }
    public string Passed { get; }
    public string? InnerRectangle { get; }
    public string? Centroid { get; }
}

/// <summary>
/// 结果声明。对应原件结果行 + 结果表列 + 显示映射；不含算法与判定实现。
/// </summary>
public sealed record ToolResultDeclaration
{
    public ToolResultDeclaration(string? rowsPortId, IReadOnlyList<ToolResultField> fields,
        ToolResultSlots? slots = null, IReadOnlyList<ToolResultColumn>? columns = null,
        ResultProjectionKind kind = ResultProjectionKind.StandardTable, string? rendererKey = null,
        ResultDisplayColor? selectedRectangleColor = null, ToolResultShape shape = ToolResultShape.Rows)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count == 0) throw new ArgumentException("结果声明至少需要一个字段。", nameof(fields));
        if (fields.Any(field => field is null)) throw new ArgumentException("结果字段不能包含空项。", nameof(fields));
        var ids = fields.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != fields.Count) throw new ArgumentException("结果字段身份重复。", nameof(fields));
        // 原厂 Output 段与 Display.xml 是两份独立声明：属性列结果同样可以带行显示合同
        // （端口 + 行槽位），用于 ImageViewConfig 引用某个行端口；二者必须成对出现。
        if (shape == ToolResultShape.Rows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rowsPortId);
            ArgumentNullException.ThrowIfNull(slots);
        }
        else if ((slots is null) != (rowsPortId is null))
            throw new ArgumentException("属性列的显示合同必须同时给出行端口与行槽位。", nameof(slots));
        // 属性列形态的行槽位指向**行端口**的成员，不在 Fields（=输出端口）里，
        // 其合法性由 ResultDisplayDescriptor.Validate 对 schema 解析后校验。
        if (shape == ToolResultShape.Rows && slots is not null)
            foreach (var slot in new[]
                     {
                         slots.Identity, slots.Rectangle, slots.GeometrySource, slots.TextValue, slots.Passed,
                         slots.InnerRectangle, slots.Centroid
                     })
                if (slot is not null && !ids.Contains(slot))
                    throw new ArgumentException($"结果槽位引用了未声明的字段：{slot}。", nameof(slots));
        var declaredColumns = (columns ?? Array.Empty<ToolResultColumn>()).ToArray();
        if (declaredColumns.Any(column => column is null || !ids.Contains(column.FieldId)))
            throw new ArgumentException("结果列必须引用已声明的结果字段。", nameof(columns));
        if (declaredColumns.Select(column => column.FieldId).Distinct(StringComparer.Ordinal).Count()
            != declaredColumns.Length)
            throw new ArgumentException("结果列身份重复。", nameof(columns));
        if (kind == ResultProjectionKind.Renderer && string.IsNullOrWhiteSpace(rendererKey))
            throw new ArgumentException("Renderer 投影必须声明渲染键。", nameof(rendererKey));
        RowsPortId = rowsPortId;
        Fields = Array.AsReadOnly(fields.ToArray());
        Slots = slots;
        Columns = Array.AsReadOnly(declaredColumns);
        Kind = kind;
        RendererKey = rendererKey;
        SelectedRectangleColor = selectedRectangleColor;
        Shape = shape;
    }

    /// <summary>行表形态的行端口身份；属性列形态为 null。</summary>
    public string? RowsPortId { get; }
    public IReadOnlyList<ToolResultField> Fields { get; }
    /// <summary>行槽位；属性列形态为 null。</summary>
    public ToolResultSlots? Slots { get; }
    public ToolResultShape Shape { get; }
    public IReadOnlyList<ToolResultColumn> Columns { get; }
    public ResultProjectionKind Kind { get; }
    public string? RendererKey { get; }
    /// <summary>选中匹配框颜色（原件 SelectedRectangleColor）；为空表示用系统默认。</summary>
    public ResultDisplayColor? SelectedRectangleColor { get; }
}
