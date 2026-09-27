using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed class ModbusAddressPointEditorViewModel : ViewModelBase
{
    private string _name = "Address";
    private int _startAddress;
    private int _count = 20;
    private string _intBits = "16bit";
    private string _accessMode = "读写";
    private string _byteOrder = "ABCD";
    private bool _pollEnabled;
    private int _timeoutMs = 200;
    private string _validationMessage = string.Empty;

    public ModbusAddressPointEditorViewModel(int startAddress, ModbusAddressPoint? source = null)
    {
        if (source != null)
        {
            _name = source.Name;
            _startAddress = source.StartAddress;
            _count = source.Count;
            _intBits = source.IntBits;
            _accessMode = source.AccessMode;
            _byteOrder = source.ByteOrder;
            _pollEnabled = source.PollEnabled;
            _timeoutMs = source.TimeoutMs;
            ExistingPoint = source;
        }
        else
        {
            _startAddress = Math.Max(0, startAddress);
        }
    }

    public ModbusAddressPoint? ExistingPoint { get; }
    public ObservableCollection<string> IntBitsOptions { get; } = new(new[] { "16bit", "32bit" });
    public ObservableCollection<string> AccessModeOptions { get; } = new(new[] { "读写", "只读", "只写" });
    public ObservableCollection<string> ByteOrderOptions { get; } = new(new[] { "ABCD", "BADC", "CDAB", "DCBA" });

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public int StartAddress { get => _startAddress; set => SetProperty(ref _startAddress, Math.Max(0, value)); }
    public int Count { get => _count; set => SetProperty(ref _count, Math.Max(1, value)); }
    public string IntBits { get => _intBits; set => SetProperty(ref _intBits, value is "32bit" ? "32bit" : "16bit"); }
    public string AccessMode { get => _accessMode; set => SetProperty(ref _accessMode, value); }
    public string ByteOrder { get => _byteOrder; set => SetProperty(ref _byteOrder, value); }
    public bool PollEnabled { get => _pollEnabled; set => SetProperty(ref _pollEnabled, value); }
    public int TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, Math.Clamp(value, 50, 120000)); }
    public string ValidationMessage { get => _validationMessage; private set => SetProperty(ref _validationMessage, value); }
    public ModbusAddressPoint? Result { get; private set; }
    public event EventHandler? RequestClose;

    public ICommand ConfirmCommand => new RelayCommand(_ => Confirm());
    public ICommand CancelCommand => new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "请输入地址名称。";
            return;
        }

        if (StartAddress > ushort.MaxValue || Count < 1 || (long)StartAddress + Count - 1 > ushort.MaxValue)
        {
            ValidationMessage = "地址范围必须在 0 到 65535 之间。";
            return;
        }

        Result = new ModbusAddressPoint
        {
            Id = ExistingPoint?.Id ?? Guid.NewGuid().ToString("N"),
            Name = Name.Trim(),
            StartAddress = StartAddress,
            Count = Count,
            IntBits = IntBits,
            AccessMode = AccessMode,
            ByteOrder = ByteOrder,
            PollEnabled = PollEnabled,
            TimeoutMs = TimeoutMs,
            CurrentValue = ExistingPoint?.CurrentValue ?? string.Empty,
            ReceiveRecords = ExistingPoint?.ReceiveRecords is { } records ? new ObservableCollection<string>(records) : new()
        };
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
