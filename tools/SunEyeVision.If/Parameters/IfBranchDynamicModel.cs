using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SunEyeVision.Tool.If.Runtime;

/// <summary>
/// 原厂 IfBranchModuleDynamicIO/IfBranchDynamicIO 的 Runtime 数据模型。
/// UI 只绑定本模型，文本保存统一经过 <see cref="IfBranchTextCodec"/>。
/// </summary>
public sealed class IfBranchDynamicModel : INotifyPropertyChanged
{
    private int _selectTypeIndex;
    private bool _isAllowSet = true;
    private int _decimalDigits;

    public ObservableCollection<IfBranchDynamicRow> Items { get; } = new();
    public IReadOnlyList<string> SelectValueBasicTypes { get; } = new[] { "int", "float" };

    public int SelectTypeIndex
    {
        get => _selectTypeIndex;
        set
        {
            if (value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (_selectTypeIndex == value) return;
            _selectTypeIndex = value;
            OnPropertyChanged();
        }
    }

    public bool IsAllowSet
    {
        get => _isAllowSet;
        private set
        {
            if (_isAllowSet == value) return;
            _isAllowSet = value;
            OnPropertyChanged();
        }
    }

    public int DecimalDigits
    {
        get => _decimalDigits;
        private set
        {
            if (_decimalDigits == value) return;
            _decimalDigits = value;
            foreach (var row in Items) row.DecimalDigits = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IfBranchDynamicRow AddIO()
    {
        var type = SelectTypeIndex == 0 ? "int" : "float";
        var index = Items.Count(row => row.Type == type);
        var row = new IfBranchDynamicRow(type + index, type, DecimalDigits);
        Items.Add(row);
        return row;
    }

    public void RemoveIO(IfBranchDynamicRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!Items.Remove(row)) throw new ArgumentException("动态条件行不属于当前模型。", nameof(row));
    }

    public void InitSource(int decimalDigits)
    {
        if (decimalDigits < 0) throw new ArgumentOutOfRangeException(nameof(decimalDigits));
        DecimalDigits = decimalDigits;
    }

    public void GetParam(string? branchText)
    {
        IsAllowSet = false;
        try
        {
            Items.Clear();
            foreach (var row in IfBranchTextCodec.Parse(branchText))
                Items.Add(new IfBranchDynamicRow(row.Name, row.IsInteger ? "int" : "float", DecimalDigits, row.Minimum, row.Maximum, row.State, row.RunModuleId));
        }
        finally { IsAllowSet = true; }
    }

    public string SetParamUnit()
    {
        if (!IsAllowSet) return IfBranchTextCodec.Serialize(Array.Empty<IfBranchTextCodec.Row>());
        return IfBranchTextCodec.Serialize(Items.Select(row => row.ToCodecRow()));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class IfBranchDynamicRow : INotifyPropertyChanged
{
    private double _minimum;
    private double _maximum;
    private int _state = IfBranchTextCodec.OkState;
    private int _runModuleId = IfBranchTextCodec.UnselectedRunModuleId;
    private int _decimalDigits;

    internal IfBranchDynamicRow(string name, string type, int decimalDigits,
        double? minimum = null, double? maximum = null, int state = IfBranchTextCodec.OkState, int runModuleId = IfBranchTextCodec.UnselectedRunModuleId)
    {
        Name = name;
        Type = type;
        _decimalDigits = decimalDigits;
        _minimum = minimum ?? IfBranchTextCodec.DefaultMinimum;
        _maximum = maximum ?? IfBranchTextCodec.DefaultMaximum;
        _state = state;
        _runModuleId = runModuleId;
        Validate();
    }

    public string Name { get; }
    public string Type { get; }
    public bool IsInteger => Type == "int";
    public double Minimum { get => _minimum; set { SetRange(ref _minimum, value); } }
    public double Maximum { get => _maximum; set { SetRange(ref _maximum, value); } }
    public int State { get => _state; set { if (value is not IfBranchTextCodec.OkState and not IfBranchTextCodec.NgState) throw new ArgumentOutOfRangeException(nameof(value)); _state = value; OnPropertyChanged(); } }
    public int RunModuleId { get => _runModuleId; set { _runModuleId = value; OnPropertyChanged(); } }
    public int DecimalDigits { get => _decimalDigits; internal set { _decimalDigits = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal IfBranchTextCodec.Row ToCodecRow() => new(Name, Minimum, Maximum, State, RunModuleId);

    private void SetRange(ref double field, double value)
    {
        field = value;
        Validate();
        OnPropertyChanged();
    }

    private void Validate()
    {
        _ = IfBranchTextCodec.Serialize(new[] { ToCodecRowUnsafe() });
    }

    private IfBranchTextCodec.Row ToCodecRowUnsafe() => new(Name, _minimum, _maximum, _state, _runModuleId);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
