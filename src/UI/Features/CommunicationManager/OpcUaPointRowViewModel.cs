using System;
using System.Globalization;
using System.Linq;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>One configured point plus transient monitoring/edit state.</summary>
public sealed class OpcUaPointRowViewModel : ViewModelBase
{
    private string _valueText = "—";
    private string _typeText = "—";
    private string _accessText = "—";
    private string _qualityText = "等待连接";
    private string _updatedText = "—";
    private string _errorText = string.Empty;
    private string _draftValue = string.Empty;
    private bool _isEditing;
    private bool _isWriting;
    private bool _isWritable;
    private bool _isSupportedScalar;
    private BuiltInType _type;

    public OpcUaPointRowViewModel(OpcUaDataPoint point) => Point = point;
    public OpcUaDataPoint Point { get; }
    public string Name => Point.Name;
    public string NodeId => Point.NodeId;
    public string ValueText { get => _valueText; private set => SetProperty(ref _valueText, value); }
    public string TypeText { get => _typeText; private set => SetProperty(ref _typeText, value); }
    public string AccessText { get => _accessText; private set => SetProperty(ref _accessText, value); }
    public string QualityText { get => _qualityText; private set => SetProperty(ref _qualityText, value); }
    public string UpdatedText { get => _updatedText; private set => SetProperty(ref _updatedText, value); }
    public string ErrorText { get => _errorText; set => SetProperty(ref _errorText, value); }
    public string DraftValue { get => _draftValue; set => SetProperty(ref _draftValue, value); }
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }
    public bool IsWriting
    {
        get => _isWriting;
        set
        {
            if (!SetProperty(ref _isWriting, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsValueReadOnly));
            OnPropertyChanged(nameof(IsNotWriting));
        }
    }
    public bool IsNotWriting => !_isWriting;
    public bool IsValueReadOnly => !CanEdit;
    public BuiltInType BuiltInType => _type;
    public bool CanEdit => !_isWriting && _isWritable && _isSupportedScalar && QualityText == "Good";

    public void Apply(OpcUaPointValueEventArgs update)
    {
        if (update.ErrorMessage is { Length: > 0 } error)
        {
            ErrorText = error;
            QualityText = "无效";
            ValueText = "—";
            _isWritable = false;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IsValueReadOnly));
            if (!IsEditing && !IsWriting) DraftValue = ValueText;
            return;
        }
        _type = update.BuiltInType;
        _isWritable = update.CanWrite;
        _isSupportedScalar = update.IsSupportedScalar;
        TypeText = _type == BuiltInType.Null ? "—" : _type.ToString();
        AccessText = _isWritable ? (_isSupportedScalar ? "读写" : "读写(暂不支持编辑)") : "只读";
        ValueText = update.Value is Array array
            ? "[" + string.Join(", ", array.Cast<object?>().Take(16)
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))) +
              (array.Length > 16 ? ", …" : string.Empty) + "]"
            : Convert.ToString(update.Value, CultureInfo.InvariantCulture) ?? "(null)";
        QualityText = StatusCode.IsGood(update.StatusCode) ? "Good" : update.StatusCode.ToString();
        UpdatedText = update.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        ErrorText = string.Empty;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsValueReadOnly));
        if (!IsEditing && !IsWriting) DraftValue = ValueText;
    }

    public void MarkDisconnected()
    {
        IsEditing = false;
        DraftValue = ValueText;
        QualityText = "已断线/非实时";
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsValueReadOnly));
    }

    public void CancelEdit()
    {
        DraftValue = ValueText;
        IsEditing = false;
        ErrorText = string.Empty;
    }

    public void UpdatePointConfiguration(OpcUaDataPoint point)
    {
        Point.NamespaceIndex = point.NamespaceIndex;
        Point.TagName = point.TagName;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(NodeId));
        DraftValue = ValueText;
        ErrorText = string.Empty;
    }
}
