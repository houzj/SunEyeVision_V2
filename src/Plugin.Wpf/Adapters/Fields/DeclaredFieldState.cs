using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>One editor session's accepted setting and errors; no visuals, defaults or execution access.</summary>
internal sealed class DeclaredFieldState : IDisposable
{
    private Action<ParameterSetting>? _write;
    private ParameterSetting _accepted;
    private ConstantSetting? _constantBackup;
    private readonly bool _allowUnboundReference;
    private bool _writing;
    public Port Port { get; }
    public ParameterSetting Accepted => CopySetting(_accepted);
    public ConstantSetting? ConstantBackup => _constantBackup is null ? null : (ConstantSetting)CopySetting(_constantBackup);
    public bool IsReferenceMode { get; private set; }
    public string EditError { get; private set; } = "";
    public string SourceError { get; private set; } = "";
    public bool IsDisposed { get; private set; }
    public bool MissingReference => IsReferenceMode && _accepted is not ReferenceSetting
        && !(_allowUnboundReference && _accepted is UnsetSetting);
    public bool HasInvalidInput => MissingReference || (_accepted is UnsetSetting && Port.Required && !_allowUnboundReference)
        || EditError.Length != 0 || SourceError.Length != 0;
    public string Error => MissingReference ? "请选择订阅来源，或切回数值模式。"
        : _accepted is UnsetSetting && Port.Required && !_allowUnboundReference ? "必填项尚未设置。"
        : EditError.Length != 0 ? EditError : SourceError;
    public event Action? Changed;

    public DeclaredFieldState(Port port, ParameterSetting initial, Action<ParameterSetting> write,
        ConstantSetting? constantBackup = null, bool allowUnboundReference = false)
    {
        Port = port; _write = write;
        _accepted = PrepareInitial(initial);
        _constantBackup = initial is ConstantSetting ? (ConstantSetting)CopySetting(_accepted)
            : constantBackup is null ? null : (ConstantSetting)PrepareInitial(constantBackup);
        IsReferenceMode = initial is ReferenceSetting || port.EditModes == FieldEditModes.Reference;
        _allowUnboundReference = allowUnboundReference;
    }

    public bool TrySet(ParameterSetting proposal)
    {
        if (IsDisposed || _writing) return false;
        var changed = false;
        var accepted = false;
        _writing = true;
        try
        {
            if (proposal is UnsetSetting && Port.Required) throw new ArgumentException("必填项不能清除。");
            if (proposal is ConstantSetting constant)
            {
                if (Port.Type.Shape == ValueShape.Resource) throw new ArgumentException("图像和区域资源必须绑定本次执行来源。");
                if (Port.EditModes != FieldEditModes.None && (Port.EditModes & FieldEditModes.Constant) == 0)
                    throw new ArgumentException("此字段不允许常量。");
                proposal = new ConstantSetting(ValueRules.Freeze(Port, constant.Value));
            }
            else if (proposal is ReferenceSetting && (Port.EditModes & FieldEditModes.Reference) == 0)
                throw new ArgumentException("此字段不允许订阅。");
            var same = SameSetting(_accepted, proposal);
            if (!same) _write!(CopySetting(proposal));
            // The real host callback can synchronously rebind and dispose this old session.
            if (IsDisposed) return false;
            var referenceMode = proposal is ReferenceSetting;
            changed = !same || IsReferenceMode != referenceMode || EditError.Length != 0;
            _accepted = proposal;
            if (proposal is ConstantSetting nextConstant) _constantBackup = nextConstant;
            IsReferenceMode = referenceMode;
            EditError = "";
            accepted = true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            if (IsDisposed) return false;
            changed = EditError != error.Message;
            EditError = error.Message;
        }
        finally { _writing = false; }
        // Observer failures must not be mistaken for a rejected host proposal.
        if (changed && !IsDisposed) Changed?.Invoke();
        return accepted;
    }

    public void EnterReferenceMode()
    {
        if (IsDisposed || IsReferenceMode) return;
        if ((Port.EditModes & FieldEditModes.Reference) == 0)
        { SetEditError("此字段不允许订阅。"); return; }
        IsReferenceMode = true; Changed?.Invoke();
    }

    public bool TryUseConstant()
    {
        if (IsDisposed) return false;
        if (ConstantBackup is { } value) return TrySet(value);
        SetEditError("没有已保存的常量值，请提供明确的参数默认值。");
        return false;
    }

    public void SetEditError(string error)
    {
        if (IsDisposed || EditError == error) return;
        EditError = error; Changed?.Invoke();
    }

    public void SetSourceError(string error)
    {
        if (IsDisposed || SourceError == error) return;
        SourceError = error; Changed?.Invoke();
    }

    private static bool SameSetting(ParameterSetting left, ParameterSetting right) => (left, right) switch
    {
        (UnsetSetting, UnsetSetting) => true,
        (ConstantSetting a, ConstantSetting b) => SameValue(a.Value, b.Value),
        (ReferenceSetting a, ReferenceSetting b) => SameReference(a.Reference, b.Reference),
        _ => false
    };

    private ParameterSetting PrepareInitial(ParameterSetting value)
    {
        if (value is not ConstantSetting constant) return value;
        try { return new ConstantSetting(ValueRules.Freeze(Port, constant.Value)); }
        catch (ArgumentException) when (Port.Choices is not null && constant.Value is string)
        {
            // Retain a removed choice for explicit user repair, but still enforce its scalar type.
            EditError = "当前枚举值不在声明选项中，请重新选择。";
            return new ConstantSetting(ValueRules.Freeze(Port with { Choices = null }, constant.Value));
        }
    }

    private static ParameterSetting CopySetting(ParameterSetting value) => value is ConstantSetting { Value: double[] array }
        ? new ConstantSetting(array.ToArray()) : value;

    private static bool SameReference(OutputReference a, OutputReference b) => a.NodeId == b.NodeId
        && a.ScopeId == b.ScopeId && a.PortId == b.PortId && a.Type == b.Type && a.Storage == b.Storage
        && a.MemberFieldPath.SequenceEqual(b.MemberFieldPath) && SameSelection(a.Selection, b.Selection);

    private static bool SameSelection(BindingSelection a, BindingSelection b) => (a, b) switch
    {
        (BindingSelection.AllItems, BindingSelection.AllItems) => true,
        (BindingSelection.AtConstant x, BindingSelection.AtConstant y) => x.Index == y.Index,
        (BindingSelection.AtField x, BindingSelection.AtField y) => SameReference(x.Reference, y.Reference),
        _ => false
    };

    private static bool SameValue(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is double[] aa && right is double[] bb) return aa.SequenceEqual(bb);
        if (left is RecordValue a && right is RecordValue b)
        {
            if (a.Declaration != b.Declaration) return false;
            var fields = a.CopyFields(); var other = b.CopyFields();
            return fields.Count == other.Count && fields.All(field => other.TryGetValue(field.Key, out var value)
                && SameValue(field.Value, value));
        }
        if (left is SequenceValue x && right is SequenceValue y)
            return x.Declaration == y.Declaration && x.Count == y.Count
                && Enumerable.Range(0, x.Count).All(index => SameValue(x[index], y[index]));
        return Equals(left, right);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; Changed = null; _write = null;
    }
}
