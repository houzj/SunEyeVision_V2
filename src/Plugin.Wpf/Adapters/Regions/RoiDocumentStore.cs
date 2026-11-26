using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>屏蔽区继承的字段声明。</summary>
public sealed record MaskInheritanceSpec(string ModeFieldId, string SourceFieldId, int MaximumInherited);

/// <summary>
/// 标准 ROI 记录的读写、提案落地与撤销历史。对应原厂平台的 RoiManager：
/// 工具只声明字段身份、组元数据与继承来源，其余全部由这里完成。
/// </summary>
public sealed class RoiDocumentStore : INativeRoiEditDocument
{
    private readonly IRoiInputAccess _inputs;
    private readonly string _documentFieldId;
    private readonly string _imageInputFieldId;
    private readonly RoiInheritanceSpec _inheritance;
    private readonly MaskInheritanceSpec? _mask;
    private readonly IReadOnlyList<string> _itemNames;
    private readonly Stack<RecordValue> _undo = new(), _redo = new();

    /// <summary>
    /// 按工具 ROI 编辑声明构造：结果视图与参数页共用同一份声明，不各自复制组与继承规则。
    /// </summary>
    public static RoiDocumentStore From(RoiEditDeclaration declaration, DeclaredInputFieldsView inputs)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(inputs);
        var inheritance = declaration.Inheritance
            ?? throw new InvalidOperationException("该工具没有平台 ROI 继承声明，不能构造公共 ROI 文档。");
        var mask = declaration.MaskInheritance is { } value
            ? new MaskInheritanceSpec(value.ModeFieldId, value.SourceFieldId, value.MaximumInherited)
            : null;
        var groups = declaration.Groups
            .Select(group => new NativeRoiEditGroup(group.Group, group.Title, group.IsMask, group.MaximumCount, group.EmptyCoverage))
            .ToArray();
        return new RoiDocumentStore(inputs, declaration.DocumentFieldId, Array.AsReadOnly(groups),
            declaration.ImageFieldId, inheritance, mask, declaration.ItemNames);
    }

    public RoiDocumentStore(IRoiInputAccess inputs, string documentFieldId,
        IReadOnlyList<NativeRoiEditGroup> groups, string imageInputFieldId,
        RoiInheritanceSpec inheritance, MaskInheritanceSpec? mask = null,
        IReadOnlyList<string>? itemNames = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(inheritance);
        if (groups.Count == 0) throw new ArgumentException("ROI 文档至少需要一个组。", nameof(groups));
        _inputs = inputs;
        _documentFieldId = documentFieldId;
        _imageInputFieldId = imageInputFieldId;
        _inheritance = inheritance;
        _mask = mask;
        Groups = groups;
        _itemNames = itemNames ?? groups.Select(group => group.Title).ToArray();
    }

    public IReadOnlyList<NativeRoiEditGroup> Groups { get; }

    public RecordValue Empty => RoiDocumentValue.Empty;

    public void Write(RecordValue document)
        => _inputs.Write(_documentFieldId, new ConstantSetting(document));

    public IRoiDocument View(RecordValue document) => new RoiDocumentView(document, Groups);
    public RecordValue ToNative(RecordValue geometry) => RoiShapeProjection.ToNative(geometry);
    public RecordValue FromNative(RecordValue geometry, RecordValue? previous)
        => RoiShapeProjection.FromNative(geometry, previous);
    public bool Same(RecordValue left, RecordValue right) => RoiDocumentValue.Same(left, right);
    public string ImageInputFieldId => _imageInputFieldId;
    public string Coverage(RecordValue document, string group) => RoiDocumentValue.Coverage(document, group);
    public string ModeFieldId => _inheritance.ModeFieldId;
    public string LocalModeValue => _inheritance.LocalMode;

    public bool IsPreviewSource(string fieldId)
    {
        if (fieldId == _imageInputFieldId || fieldId == _inheritance.ModeFieldId) return true;
        foreach (var family in _inheritance.Families)
        {
            if (fieldId == family.SourceFieldId) return true;
            foreach (var member in family.Members)
                if (fieldId == RoiInheritanceMaterializer.LeafId(family, member)) return true;
        }
        return _mask is not null && (fieldId == _mask.ModeFieldId || fieldId == _mask.SourceFieldId);
    }

    public IReadOnlyList<string> PreviewInputIds(string mode, bool maskInherited)
    {
        var ids = new List<string>();
        foreach (var family in _inheritance.Families)
        {
            if (family.ModeValue == mode) ids.Add(family.SourceFieldId);
            else if (family.ParametersModeValue == mode)
                foreach (var member in family.Members) ids.Add(RoiInheritanceMaterializer.LeafId(family, member));
        }
        if (maskInherited && _mask is not null)
        {
            ids.Add(_mask.ModeFieldId);
            ids.Add(_mask.SourceFieldId);
        }
        return ids.ToArray();
    }

    public RecordValue MaterializePreview(InputValues inputs, RecordValue document)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var inspection = RoiInheritanceMaterializer.Materialize(inputs, _inheritance, document);
        return _mask is null
            ? inspection
            : PolygonStringInheritance.Materialize(inputs, _mask.ModeFieldId, _mask.SourceFieldId,
                _mask.MaximumInherited, (int)RoiDocumentValue.MaximumVertices, inspection);
    }

    public string PreviewMode()
        => RoiInheritanceMaterializer.ReadMode(_inputs.Read(_inheritance.ModeFieldId), _inheritance);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public RecordValue Read()
    {
        var setting = _inputs.Read(_documentFieldId);
        var value = setting switch
        {
            UnsetSetting => RoiDocumentValue.Empty,
            ConstantSetting { Value: RecordValue record } => record,
            _ => throw new ArgumentException("本地区域必须为纯值定义。")
        };
        RoiDocumentValue.Validate(value);
        return value;
    }

    public bool TryApply(RecordValue before, string group, RoiEditProposal proposal,
        out RecordValue after, out RoiEditSelection selection, out string error)
    {
        after = before;
        selection = new RoiEditSelection.Keep();
        error = "";
        var index = IndexOf(group);
        var rows = RoiDocumentValue.Items(before, group).ToList();
        var coverage = RoiDocumentValue.Coverage(before, group);
        var history = false;
        try
        {
            switch (proposal)
            {
                case RoiEditProposal.Create create when create.Group == group:
                    var id = Guid.NewGuid();
                    rows.Add(RoiDocumentValue.Item(id, _itemNames[index], RoiShapeProjection.FromNative(create.Geometry)));
                    coverage = RoiDocumentValue.Explicit;
                    selection = new RoiEditSelection.Select(new(group, id));
                    break;
                case RoiEditProposal.Replace replace when replace.Target.Group == group:
                    var at = rows.FindIndex(row => row.Get<string>("id") == replace.Target.Id.ToString("N"));
                    if (at < 0) throw new ArgumentException("区域已不存在。");
                    var item = rows[at];
                    rows[at] = RoiDocumentValue.Item(replace.Target.Id, item.Get<string>("name"),
                        RoiShapeProjection.FromNative(replace.Geometry, item.Get<RecordValue>("shape")),
                        item.Get<bool>("enabled"), item.Get<bool>("visible"));
                    break;
                case RoiEditProposal.Delete delete when delete.Targets.All(target => target.Group == group):
                    foreach (var target in delete.Targets)
                        if (rows.RemoveAll(row => row.Get<string>("id") == target.Id.ToString("N")) != 1)
                            throw new ArgumentException("区域已不存在。");
                    if (rows.Count == 0) coverage = Groups[index].EmptyCoverage;
                    selection = new RoiEditSelection.Clear();
                    break;
                case RoiEditProposal.SetCoverage set when set.Group == group
                    && set.Coverage.ToString() == Groups[index].EmptyCoverage:
                    rows.Clear();
                    coverage = set.Coverage.ToString();
                    selection = new RoiEditSelection.Clear();
                    break;
                case RoiEditProposal.Undo when _undo.Count > 0:
                    after = _undo.Peek(); history = true; selection = new RoiEditSelection.Clear(); break;
                case RoiEditProposal.Redo when _redo.Count > 0:
                    after = _redo.Peek(); history = true; selection = new RoiEditSelection.Clear(); break;
                default: throw new ArgumentException("此区域操作尚未接通。");
            }
            if (!history) after = ReplaceGroup(before, group, coverage, rows);
            foreach (var entry in Groups.Where(entry => entry.PolygonOnly))
                if (RoiDocumentValue.Items(after, entry.Name)
                    .Any(row => row.Get<RecordValue>("shape").Get<string>("kind") != "polygon"))
                    throw new ArgumentException("屏蔽区仅支持多边形。");
            RoiDocumentValue.Validate(after);
            if (RoiDocumentValue.Same(before, after)) throw new ArgumentException("区域内容没有变化。");
            if (proposal is RoiEditProposal.Undo) { _undo.Pop(); _redo.Push(before); }
            else if (proposal is RoiEditProposal.Redo) { _redo.Pop(); _undo.Push(before); }
            else { _undo.Push(before); _redo.Clear(); }
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException)
        {
            after = before;
            error = failure.Message;
            return false;
        }
    }

    private RecordValue ReplaceGroup(RecordValue document, string group, string coverage, List<RecordValue> rows)
    {
        var replaced = RoiDocumentValue.Group(coverage, rows);
        var values = Groups.Select(entry => entry.Name == group
            ? replaced
            : document.Get<RecordValue>(entry.Name)).ToArray();
        return RoiDocumentValue.Root(values[0], values[1]);
    }

    private int IndexOf(string group)
    {
        for (var i = 0; i < Groups.Count; i++)
            if (Groups[i].Name == group) return i;
        throw new ArgumentException("未声明的 ROI 组：" + group);
    }
}
