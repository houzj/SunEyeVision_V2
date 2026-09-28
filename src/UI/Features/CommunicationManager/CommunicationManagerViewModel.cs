using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Devices.Communication;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using S7.Net;


namespace SunEyeVision.UI.Features.CommunicationManager
{
    /// <summary>
    /// 通讯管理器 ViewModel
    /// </summary>
    public class CommunicationManagerViewModel : ViewModelBase
    {
        private readonly SolutionManager _solutionManager;
        private readonly CommunicationRuntimeManager _runtimeManager = ServiceInitializer.CommunicationService.Composition.Runtime;
        private readonly CommunicationPollingCoordinator _polling = ServiceInitializer.CommunicationService.Composition.Polling;
        private readonly Dictionary<string, CommunicationDeviceWorkspaceViewModel> _deviceWorkspaces = new();

        private CommunicationDeviceWorkspaceViewModel? _selectedWorkspace;
        public CommunicationDeviceWorkspaceViewModel? SelectedWorkspace
        {
            get => _selectedWorkspace;
            private set => SetProperty(ref _selectedWorkspace, value);
        }

        public bool IsSelectedConnected => SelectedCommunication != null && _runtimeManager.IsConnected(SelectedCommunication.Id);

        /// <summary>
        /// 通讯配置列表
        /// </summary>
        public ObservableCollection<Communication> Communications { get; }
        public ObservableCollection<string> ModbusModeOptions { get; } = new(new[] { "TcpClient", "串口" });
        public ObservableCollection<string> SerialPortOptions { get; } = new();
        public ObservableCollection<int> BaudRateOptions { get; } = new(SerialPortCatalog.GetBaudRates(115200));
        public ObservableCollection<string> ParityOptions { get; } =
            new(new[] { "无校验", "奇校验", "偶校验", "标记校验", "空格校验" });
        public ObservableCollection<string> OpcUaSecurityModeOptions { get; } = new(new[] { "None", "Sign", "SignAndEncrypt" });
        public ObservableCollection<TrustedOpcUaCertificate> TrustedOpcUaCertificates { get; } = new();
        private TrustedOpcUaCertificate? _selectedTrustedOpcUaCertificate;
        public TrustedOpcUaCertificate? SelectedTrustedOpcUaCertificate
        {
            get => _selectedTrustedOpcUaCertificate;
            set
            {
                if (SetProperty(ref _selectedTrustedOpcUaCertificate, value))
                    OnPropertyChanged(nameof(HasSelectedTrustedOpcUaCertificate));
            }
        }
        public bool HasSelectedTrustedOpcUaCertificate => SelectedTrustedOpcUaCertificate != null;
        public ObservableCollection<string> S7CpuTypeOptions { get; } = new(new[] { "S7200", "S7200Smart", "S7300", "S7400", "S71200", "S71500" });

        /// <summary>
        /// 选中的通讯配置
        /// </summary>
        private Communication? _selectedCommunication;
        public Communication? SelectedCommunication
        {
            get => _selectedCommunication;
            set
            {
                if (SetProperty(ref _selectedCommunication, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCommunication));
                    OnPropertyChanged(nameof(HasNoSelectedCommunication));
                    OnPropertyChanged(nameof(IsSelectedConnected));
                    SelectedWorkspace = value != null && _deviceWorkspaces.TryGetValue(value.Id, out var workspace)
                        ? workspace
                        : null;
                    RefreshSerialPorts();
                    RefreshTrustedOpcUaCertificates();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasSelectedCommunication => SelectedCommunication != null;
        public bool HasNoSelectedCommunication => SelectedCommunication == null;

        public void RefreshTrustedOpcUaCertificates()
        {
            TrustedOpcUaCertificates.Clear();
            foreach (var certificate in _runtimeManager.GetTrustedOpcUaCertificates())
                TrustedOpcUaCertificates.Add(certificate);
            SelectedTrustedOpcUaCertificate = null;
        }

        public TrustedOpcUaCertificate TrustOpcUaServerCertificate(string filePath)
        {
            var certificate = _runtimeManager.TrustOpcUaServerCertificate(filePath);
            RefreshTrustedOpcUaCertificates();
            LogWarning($"已显式信任 OPC UA 服务器证书：{certificate.Subject}（{certificate.Thumbprint}）。新信任将在重新连接后生效。");
            return certificate;
        }

        public TrustedOpcUaCertificate InspectOpcUaServerCertificate(string filePath) => OpcUaTrustStore.InspectFile(filePath);

        public bool RemoveTrustedOpcUaServerCertificate(string thumbprint)
        {
            var removed = _runtimeManager.RemoveTrustedOpcUaServerCertificate(thumbprint);
            RefreshTrustedOpcUaCertificates();
            if (removed)
                LogWarning($"已撤销 OPC UA 证书信任：{thumbprint}。已建立的会话不会自动断开。");
            return removed;
        }

        /// <summary>
        /// 添加通讯命令
        /// </summary>
        public ICommand AddCommunicationCommand { get; private set; }

        /// <summary>
        /// 删除通讯命令
        /// </summary>
        public ICommand DeleteCommunicationCommand { get; private set; }

        public ICommand ToggleDeviceCommand { get; private set; }
        public ICommand SendDataCommand { get; private set; }
        public ICommand ClearReceivedDataCommand { get; private set; }
        public ICommand ClearSentDataCommand { get; private set; }
        public ICommand ExecuteModbusOperationCommand { get; private set; }
        public ICommand ExecuteS7OperationCommand { get; private set; }
        public ICommand BrowseOpcUaCommand { get; private set; }
        public ICommand ReadOpcUaCommand { get; private set; }
        public ICommand WriteOpcUaCommand { get; private set; }
        public ICommand SubscribeOpcUaCommand { get; private set; }
        public ICommand UnsubscribeOpcUaCommand { get; private set; }
        public ICommand ExecuteCipCommand { get; private set; }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; private set; }

        /// <summary>
        /// 关闭命令
        /// </summary>
        public ICommand CloseCommand { get; private set; }

        /// <summary>
        /// 请求打开“新建设备”对话框，由宿主窗口负责创建 WPF 对话框。
        /// </summary>
        public event EventHandler? RequestAddDevice;

        public CommunicationManagerViewModel(SolutionManager solutionManager)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _runtimeManager.DataReceived += RuntimeManager_DataReceived;
            _runtimeManager.ConnectionClosed += RuntimeManager_ConnectionClosed;
            _runtimeManager.StatusChanged += RuntimeManager_StatusChanged;
            _polling.BatchReceived += Polling_BatchReceived;

            var currentSolution = _solutionManager.CurrentSolution;
            Communications = new ObservableCollection<Communication>(
                currentSolution?.Communications ?? Enumerable.Empty<Communication>()
            );
            foreach (var communication in Communications)
            {
                AddWorkspace(communication);
                communication.RuntimeStatus = MapStatus(_runtimeManager.GetStatus(new CommunicationDeviceRef(communication.Id)).State);
            }

            RefreshSerialPorts();

            InitializeCommands();
        }

        public void RefreshSerialPorts()
        {
            var current = SelectedCommunication?.SerialPort;
            var currentBaudRate = SelectedCommunication?.BaudRate ?? 115200;

            SerialPortOptions.Clear();
            foreach (var port in SerialPortCatalog.GetPorts(current))
                SerialPortOptions.Add(port);

            BaudRateOptions.Clear();
            foreach (var baudRate in SerialPortCatalog.GetBaudRates(currentBaudRate))
                BaudRateOptions.Add(baudRate);

            // 清空 ItemsSource 时，ComboBox 的 TwoWay SelectedItem 可能会暂时回写
            // null/0。恢复刷新前的值，避免打开设备详情或新增设备后配置被清空。
            if (SelectedCommunication != null)
            {
                SelectedCommunication.SerialPort = current ?? string.Empty;
                SelectedCommunication.BaudRate = currentBaudRate > 0 ? currentBaudRate : 115200;
            }
        }

        private void InitializeCommands()
        {
            AddCommunicationCommand = new RelayCommand(AddCommunication, () => true);
            DeleteCommunicationCommand = new RelayCommand(async () => await DeleteCommunicationAsync(), CanDeleteCommunication);
            ToggleDeviceCommand = new RelayCommand<Communication>(async device => await ToggleConnectionAsync(device), CanToggleConnection);
            SendDataCommand = new RelayCommand(async () => await SendDataAsync(), CanSendData);
            ClearReceivedDataCommand = new RelayCommand(
                () => SelectedWorkspace?.ClearReceivedHistory(),
                () => !string.IsNullOrEmpty(SelectedWorkspace?.ReceivedText));
            ClearSentDataCommand = new RelayCommand(
                () => SelectedWorkspace?.ClearSentHistory(),
                () => !string.IsNullOrEmpty(SelectedWorkspace?.SentText));
            ExecuteModbusOperationCommand = new RelayCommand(
                async () => await ExecuteModbusOperationAsync(), CanExecuteModbusOperation);
            ExecuteS7OperationCommand = new RelayCommand(async () => await ExecuteS7OperationAsync(), CanExecuteS7Operation);
            BrowseOpcUaCommand = new RelayCommand(async () => await BrowseOpcUaAsync(), CanExecuteOpcUaOperation);
            ReadOpcUaCommand = new RelayCommand(async () => await ReadOpcUaAsync(), CanExecuteOpcUaOperation);
            WriteOpcUaCommand = new RelayCommand(async () => await WriteOpcUaAsync(), CanExecuteOpcUaOperation);
            SubscribeOpcUaCommand = new RelayCommand(async () => await SubscribeOpcUaAsync(), CanExecuteOpcUaOperation);
            UnsubscribeOpcUaCommand = new RelayCommand(async () => await UnsubscribeOpcUaAsync(), CanExecuteOpcUaOperation);
            ExecuteCipCommand = new RelayCommand(async () => await ExecuteCipAsync(), CanExecuteCipOperation);
            SaveCommand = new RelayCommand(Save, CanSave);
            CloseCommand = new RelayCommand(Close, () => true);
        }

        private void AddWorkspace(Communication communication)
        {
            if (_deviceWorkspaces.ContainsKey(communication.Id))
                return;

            var workspace = new CommunicationDeviceWorkspaceViewModel(communication);
            workspace.PropertyChanged += Workspace_PropertyChanged;
            workspace.AddressConfigurationChanged += Workspace_AddressConfigurationChanged;
            communication.PropertyChanged += Communication_PropertyChanged;
            _deviceWorkspaces.Add(communication.Id, workspace);
            var cached = _polling.GetLatest(communication.Id)
                .Select(result => workspace.ModbusAddressPoints.FirstOrDefault(point => point.Id == result.AddressId) is { } point
                    ? new ModbusPollUpdate(point, result.Values, result.Timestamp, result.StartAddress)
                    : (ModbusPollUpdate?)null)
                .Where(update => update.HasValue)
                .Select(update => update!.Value)
                .ToArray();
            if (cached.Length > 0)
                workspace.ApplyModbusPollResults(cached);
        }

        private void RemoveWorkspace(string communicationId)
        {
            StopModbusPolling(communicationId);
            if (!_deviceWorkspaces.Remove(communicationId, out var workspace))
                return;

            workspace.PropertyChanged -= Workspace_PropertyChanged;
            workspace.AddressConfigurationChanged -= Workspace_AddressConfigurationChanged;
            workspace.Communication.PropertyChanged -= Communication_PropertyChanged;
            if (SelectedWorkspace == workspace)
                SelectedWorkspace = null;
        }

        private static void Workspace_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
            CommandManager.InvalidateRequerySuggested();

        private void Workspace_AddressConfigurationChanged(object? sender, EventArgs e)
        {
            if (sender is CommunicationDeviceWorkspaceViewModel workspace)
                UpdateModbusPollPlan(workspace);
        }

        private void Communication_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Communication.PollInterval) && sender is Communication device &&
                _deviceWorkspaces.TryGetValue(device.Id, out var workspace))
                UpdateModbusPollPlan(workspace);
        }

        private bool CanSendData() =>
            SelectedWorkspace is { IsBusy: false } workspace &&
            _runtimeManager.IsConnected(workspace.Communication.Id) &&
            !string.IsNullOrWhiteSpace(workspace.SendText);

        private bool CanExecuteModbusOperation() =>
            SelectedWorkspace is { IsBusy: false } workspace &&
            workspace.Communication.IsModbus && _runtimeManager.IsConnected(workspace.Communication.Id);

        private async Task ExecuteModbusOperationAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || workspace.IsBusy || !workspace.Communication.IsModbus) return;

            try
            {
                if (workspace.ModbusAddress is < 0 or > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(workspace.ModbusAddress), "地址必须在 0 到 65535 之间。");
                var maxQuantity = workspace.ModbusOperation == "读取离散输入" ? 2000 : 125;
                if (workspace.ModbusCount < 1 || workspace.ModbusCount > maxQuantity)
                    throw new ArgumentOutOfRangeException(nameof(workspace.ModbusCount), $"当前操作的读取数量必须在 1 到 {maxQuantity} 之间。");

                workspace.IsBusy = true;
                var address = (ushort)workspace.ModbusAddress;
                string result;
                switch (workspace.ModbusOperation)
                {
                    case "读取离散输入":
                    {
                        if (workspace.ModbusCount > 2000) throw new ArgumentOutOfRangeException(nameof(workspace.ModbusCount), "离散输入读取数量不能超过 2000。");
                        var values = await _runtimeManager.ReadModbusInputsAsync(workspace.Communication.Id, address, (ushort)workspace.ModbusCount);
                        result = string.Join(", ", values.Select((value, index) => $"{address + index}: {(value ? 1 : 0)}"));
                        break;
                    }
                    case "读取保持寄存器":
                    {
                        var values = await _runtimeManager.ReadModbusHoldingRegistersAsync(workspace.Communication.Id, address, (ushort)workspace.ModbusCount);
                        result = string.Join(", ", values.Select((value, index) => $"{address + index}: {value} (0x{value:X4})"));
                        break;
                    }
                    case "读取输入寄存器":
                    {
                        var values = await _runtimeManager.ReadModbusInputRegistersAsync(workspace.Communication.Id, address, (ushort)workspace.ModbusCount);
                        result = string.Join(", ", values.Select((value, index) => $"{address + index}: {value} (0x{value:X4})"));
                        break;
                    }
                    case "写单寄存器":
                    {
                        if (!ushort.TryParse(workspace.ModbusWriteValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                            throw new FormatException("寄存器值请输入 0 到 65535 的整数。");
                        await _runtimeManager.WriteModbusRegisterAsync(workspace.Communication.Id, address, value);
                        result = $"已写入寄存器 {address}：{value} (0x{value:X4})";
                        break;
                    }
                    case "写多个寄存器":
                    {
                        var values = workspace.ModbusWriteValue.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(token => ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                                ? parsed
                                : throw new FormatException($"寄存器值“{token}”无效，范围为 0 到 65535。"))
                            .ToArray();
                        if (values.Length != workspace.ModbusCount) throw new FormatException("多寄存器写入数量必须与“数量”字段一致。");
                        await _runtimeManager.WriteModbusRegistersAsync(workspace.Communication.Id, address, values);
                        result = $"已写入 {values.Length} 个寄存器：{string.Join(", ", values.Select(value => $"{value} (0x{value:X4})"))}";
                        break;
                    }
                    default:
                        throw new InvalidOperationException("请选择有效的 Modbus 操作。");
                }

                workspace.ModbusResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {workspace.ModbusOperation} 地址 {address}\n{result}";
                LogSuccess($"{workspace.Communication.Name}：{workspace.ModbusOperation}完成");
            }
            catch (Exception ex)
            {
                workspace.ModbusResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] 操作失败\n{ex.Message}";
                LogError($"{workspace.Communication.Name} Modbus 操作失败：{ex.Message}");
            }
            finally
            {
                workspace.IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanExecuteS7Operation() =>
            SelectedWorkspace is { IsBusy: false } workspace && workspace.Communication.IsS7 && _runtimeManager.IsConnected(workspace.Communication.Id);

        private async Task ExecuteS7OperationAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || workspace.IsBusy || !workspace.Communication.IsS7) return;
            try
            {
                if (workspace.S7DataBlock is < 0 or > 65535 || workspace.S7StartByte is < 0 or > 0xFFFFFF)
                    throw new ArgumentOutOfRangeException(nameof(workspace.S7StartByte), "DB 编号或起始字节超出有效范围。");
                workspace.IsBusy = true;
                var area = workspace.S7Area switch
                {
                    "DataBlock" => DataType.DataBlock,
                    "Marker" => DataType.Memory,
                    "Input" => DataType.Input,
                    "Output" => DataType.Output,
                    _ => throw new InvalidOperationException("请选择有效的 S7 数据区。")
                };
                byte[] result;
                string operation;
                if (string.IsNullOrWhiteSpace(workspace.S7WriteHex))
                {
                    if (workspace.S7ByteCount is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(workspace.S7ByteCount), "读取长度必须为 1 到 2000 字节。");
                    result = await _runtimeManager.ReadS7BytesAsync(workspace.Communication.Id, area, workspace.S7DataBlock, workspace.S7StartByte, workspace.S7ByteCount);
                    operation = "读取";
                }
                else
                {
                    var payload = CommunicationPayloadCodec.ParseHex(workspace.S7WriteHex);
                    await _runtimeManager.WriteS7BytesAsync(workspace.Communication.Id, area, workspace.S7DataBlock, workspace.S7StartByte, payload);
                    result = payload;
                    operation = "写入已确认";
                }
                workspace.S7ResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {operation} {workspace.S7Area} DB{workspace.S7DataBlock} 字节 {workspace.S7StartByte}\nHEX: {CommunicationPayloadCodec.FormatHex(result)}";
                LogSuccess($"{workspace.Communication.Name}：S7 {operation}完成");
            }
            catch (Exception ex)
            {
                workspace.S7ResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] 操作失败\n{ex.Message}";
                LogError($"{workspace.Communication.Name} S7 操作失败：{ex.Message}");
            }
            finally
            {
                workspace.IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanExecuteOpcUaOperation() => SelectedWorkspace is { IsBusy: false } workspace &&
            workspace.Communication.IsOpcUa && _runtimeManager.IsConnected(workspace.Communication.Id) &&
            !string.IsNullOrWhiteSpace(workspace.OpcUaNodeId);

        private async Task BrowseOpcUaAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || !workspace.Communication.IsOpcUa || workspace.IsBusy) return;
            await RunOpcUaOperationAsync(workspace, async () =>
            {
                var items = await _runtimeManager.BrowseOpcUaAsync(workspace.Communication.Id, workspace.OpcUaNodeId);
                workspace.OpcUaBrowseItems.Clear();
                foreach (var item in items) workspace.OpcUaBrowseItems.Add(item);
                return $"浏览完成：{items.Count} 个子节点";
            });
        }

        private async Task ReadOpcUaAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || !workspace.Communication.IsOpcUa || workspace.IsBusy) return;
            await RunOpcUaOperationAsync(workspace, async () =>
            {
                var value = await _runtimeManager.ReadOpcUaValueAsync(workspace.Communication.Id, workspace.OpcUaNodeId);
                workspace.OpcUaValueText = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                return $"值：{workspace.OpcUaValueText}\n类型：{value.WrappedValue.TypeInfo?.BuiltInType}\n状态：{value.StatusCode}\n源时间：{value.SourceTimestamp:yyyy-MM-dd HH:mm:ss.fff}";
            });
        }

        private async Task WriteOpcUaAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || !workspace.Communication.IsOpcUa || workspace.IsBusy) return;
            await RunOpcUaOperationAsync(workspace, async () =>
            {
                object value = bool.TryParse(workspace.OpcUaValueText, out var boolean) ? boolean :
                    long.TryParse(workspace.OpcUaValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer :
                    double.TryParse(workspace.OpcUaValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : workspace.OpcUaValueText;
                await _runtimeManager.WriteOpcUaValueAsync(workspace.Communication.Id, workspace.OpcUaNodeId, value);
                return $"已写入值：{workspace.OpcUaValueText}";
            });
        }

        private async Task SubscribeOpcUaAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || !workspace.Communication.IsOpcUa || workspace.IsBusy) return;
            await RunOpcUaOperationAsync(workspace, async () =>
            {
                await _runtimeManager.SubscribeOpcUaValueAsync(workspace.Communication.Id, workspace.OpcUaNodeId, workspace.OpcUaSamplingInterval);
                return $"已订阅 {workspace.OpcUaNodeId}，采样间隔 {workspace.OpcUaSamplingInterval} ms；通知将显示在接收数据中。";
            });
        }

        private async Task UnsubscribeOpcUaAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || !workspace.Communication.IsOpcUa || workspace.IsBusy) return;
            await RunOpcUaOperationAsync(workspace, async () =>
            {
                await _runtimeManager.UnsubscribeOpcUaValueAsync(workspace.Communication.Id, workspace.OpcUaNodeId);
                return $"已取消订阅 {workspace.OpcUaNodeId}。";
            });
        }

        private static async Task RunOpcUaOperationAsync(CommunicationDeviceWorkspaceViewModel workspace, Func<Task<string>> operation)
        {
            try
            {
                workspace.IsBusy = true;
                workspace.OpcUaResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {await operation()}";
            }
            catch (Exception ex)
            {
                workspace.OpcUaResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] 操作失败\n{ex.Message}";
            }
            finally
            {
                workspace.IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanExecuteCipOperation() => SelectedWorkspace is { IsBusy: false } workspace &&
            workspace.Communication.IsCip && _runtimeManager.IsConnected(workspace.Communication.Id);

        private async Task ExecuteCipAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || workspace.IsBusy || !workspace.Communication.IsCip) return;
            try
            {
                workspace.IsBusy = true;
                var service = checked((byte)ParseCipNumber(workspace.CipService));
                var classId = ParseCipNumber(workspace.CipClassId);
                var instanceId = ParseCipNumber(workspace.CipInstanceId);
                var attributeId = ParseCipNumber(workspace.CipAttributeId);
                var requestData = string.IsNullOrWhiteSpace(workspace.CipRequestDataHex)
                    ? Array.Empty<byte>() : CommunicationPayloadCodec.ParseHex(workspace.CipRequestDataHex);
                var response = await _runtimeManager.SendCipExplicitAsync(workspace.Communication.Id, service, classId, instanceId, attributeId, requestData);
                workspace.CipResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] 服务 0x{response.Service:X2}\n状态：0x{response.GeneralStatus:X2}{(response.GeneralStatus == 0 ? "（成功）" : "（设备拒绝/错误）")}\n附加状态：{(response.AdditionalStatus.Length == 0 ? "无" : string.Join(" ", response.AdditionalStatus.Select(item => $"0x{item:X4}")))}\n响应数据 HEX：{CommunicationPayloadCodec.FormatHex(response.Data)}";
                if (response.GeneralStatus == 0) LogSuccess($"{workspace.Communication.Name}：CIP 显式请求完成");
                else LogWarning($"{workspace.Communication.Name}：CIP 设备返回状态 0x{response.GeneralStatus:X2}");
            }
            catch (Exception ex)
            {
                workspace.CipResultText = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] 请求失败\n{ex.Message}";
                LogError($"{workspace.Communication.Name} CIP 请求失败：{ex.Message}");
            }
            finally { workspace.IsBusy = false; CommandManager.InvalidateRequerySuggested(); }
        }

        private static uint ParseCipNumber(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
            }
            else if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return number;
            throw new FormatException($"无效的 CIP 数值：{text}（支持十进制或 0x 前缀十六进制）。");
        }

        /// <summary>
        /// 添加通讯配置
        /// </summary>
        private void AddCommunication()
        {
            RequestAddDevice?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 将新建设备提交到当前方案。对话框关闭前只创建配置，不建立真实连接。
        /// </summary>
        public void AddCommunication(Communication communication)
        {
            ArgumentNullException.ThrowIfNull(communication);

            Communications.Add(communication);
            _solutionManager.CurrentSolution?.Communications.Add(communication);
            AddWorkspace(communication);
            SelectedCommunication = communication;
            // SelectedCommunication 的绑定和右侧编辑区可能在同一轮布局中建立，
            // 再刷新一次选项集合，确保新设备的串口/波特率值能匹配到 ComboBox。
            RefreshSerialPorts();

            LogInfo($"添加通讯设备配置: {communication.Name}");
        }

        public bool RenameCommunication(Communication communication, string newName)
        {
            ArgumentNullException.ThrowIfNull(communication);
            if (!Communications.Contains(communication) || string.IsNullOrWhiteSpace(newName))
                return false;

            var oldName = communication.Name;
            var normalizedName = newName.Trim();
            if (string.Equals(oldName, normalizedName, StringComparison.Ordinal))
                return false;

            communication.Name = normalizedName;
            LogInfo($"通讯设备已重命名：{oldName} → {normalizedName}");
            return true;
        }

        /// <summary>
        /// 删除通讯配置
        /// </summary>
        private async Task DeleteCommunicationAsync()
        {
            if (SelectedCommunication == null) return;

            var communication = SelectedCommunication;
            await _runtimeManager.DisconnectAsync(communication.Id);
            RemoveWorkspace(communication.Id);
            Communications.Remove(communication);
            _solutionManager.CurrentSolution?.Communications.Remove(communication);

            LogInfo($"删除通讯配置: {communication.Name}");
            SelectedCommunication = null;
        }

        /// <summary>
        /// 是否可以删除通讯配置
        /// </summary>
        private bool CanDeleteCommunication()
        {
            return SelectedCommunication != null;
        }

        private async Task ToggleConnectionAsync(Communication? target)
        {
            await ServiceInitializer.CommunicationService.ReadyAsync();
            var device = target ?? SelectedCommunication;
            if (device == null || !_deviceWorkspaces.TryGetValue(device.Id, out var workspace)) return;
            LogInfo($"[Modbus诊断] 设备开关触发：设备={device.Name}({device.Id}), 当前连接={_runtimeManager.IsConnected(device.Id)}, 协议={device.ProtocolDisplayName}, 端点={device.GetConnectionString()}", target: LogTarget.File | LogTarget.UI);
            workspace.IsBusy = true;
            device.IsRuntimeOperationInProgress = true;
            try
            {
                if (_runtimeManager.IsConnected(device.Id))
                {
                    StopModbusPolling(device.Id);
                    await _runtimeManager.DisconnectAsync(device.Id);
                    device.RuntimeStatus = CommunicationRuntimeStatus.Disconnected;
                    LogInfo(device.IsTcpServer ? $"已停止监听 {device.Name}" : device.IsUdp ? $"已解除 UDP 绑定 {device.Name}" : $"已断开 {device.Name}");
                }
                else
                {
                    device.RuntimeStatus = CommunicationRuntimeStatus.Connecting;
                    await _runtimeManager.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device));
                    device.RuntimeStatus = CommunicationRuntimeStatus.Connected;
                    if (device.IsModbus)
                        StartModbusPolling(device, workspace);
                    LogInfo($"[Modbus诊断] 设备连接完成：设备={device.Name}({device.Id}), 站号={device.UnitId}, 设备轮询间隔={device.PollInterval}ms, 地址块总数={workspace.ModbusAddressPoints.Count}, 已启用地址块={workspace.ModbusAddressPoints.Count(point => point.PollEnabled)}", target: LogTarget.File | LogTarget.UI);
                    LogSuccess(device.IsTcpServer ? $"已开始监听 {device.GetConnectionString()}" : device.IsUdp ? $"UDP 本机端口已绑定：{device.GetConnectionString()}" : $"已连接 {device.Name}");
                }
            }
            catch (Exception ex)
            {
                StopModbusPolling(device.Id);
                device.RuntimeStatus = CommunicationRuntimeStatus.Faulted;
                LogError($"连接失败：{ex.Message}");
            }
            finally
            {
                device.IsRuntimeOperationInProgress = false;
                workspace.IsBusy = false;
                if (SelectedCommunication?.Id == device.Id)
                    OnPropertyChanged(nameof(IsSelectedConnected));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StartModbusPolling(Communication device, CommunicationDeviceWorkspaceViewModel workspace)
        {
            _polling.Start(device.Id, device.PollInterval, CreateModbusPollPlan(workspace));
            LogInfo($"[Modbus诊断] 启动持续轮询：设备={device.Name}({device.Id}), 设备轮询间隔={Math.Clamp(device.PollInterval, 100, 60000)}ms, 地址块={workspace.ModbusAddressPoints.Count}, 启用={workspace.ModbusAddressPoints.Count(point => point.PollEnabled)}", target: LogTarget.File | LogTarget.UI);
        }

        private void StopModbusPolling(string communicationId)
        {
            _polling.Stop(communicationId);
            if (_deviceWorkspaces.TryGetValue(communicationId, out var workspace))
                workspace.ClearModbusPollStatus();
        }

        private static ModbusPollAddress[] CreateModbusPollPlan(CommunicationDeviceWorkspaceViewModel workspace) =>
            workspace.ModbusAddressPoints
                .Where(point => point.PollEnabled && point.AccessMode != "只写")
                .Select(point => new ModbusPollAddress(point.Id, point.StartAddress, point.Count, point.TimeoutMs))
                .ToArray();

        private void UpdateModbusPollPlan(CommunicationDeviceWorkspaceViewModel workspace)
        {
            if (_runtimeManager.IsConnected(workspace.Communication.Id))
                _polling.UpdatePlan(workspace.Communication.Id, workspace.Communication.PollInterval,
                    CreateModbusPollPlan(workspace));
        }

        private void Polling_BatchReceived(object? sender, ModbusPollBatch batch)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_polling.IsCurrentBatch(batch)) return;
                if (!_deviceWorkspaces.TryGetValue(batch.CommunicationId, out var workspace)) return;
                var updates = batch.Results
                    .Select(result => workspace.ModbusAddressPoints.FirstOrDefault(point => point.Id == result.AddressId) is { } point
                        ? new ModbusPollUpdate(point, result.Values, result.Timestamp, result.StartAddress)
                        : (ModbusPollUpdate?)null)
                    .Where(update => update.HasValue)
                    .Select(update => update!.Value)
                    .ToArray();
                workspace.ApplyModbusPollResults(updates);
            }));
        }

        public async Task SyncModbusDataAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || workspace.IsBusy || !workspace.Communication.IsModbus)
                return;
            var device = workspace.Communication;
            if (!_runtimeManager.IsConnected(device.Id))
            {
                LogWarning("请先连接 Modbus 设备，再执行数据同步。");
                return;
            }
            var points = workspace.ModbusAddressPoints.Where(point => point.AccessMode != "只写").ToArray();
            if (points.Length == 0)
            {
                LogWarning("没有已配置的可读地址块，无法执行数据同步。");
                return;
            }

            workspace.IsBusy = true;
            var updates = new List<ModbusPollUpdate>();
            var failures = 0;
            try
            {
                var lifetime = _polling.GetLifetimeToken(device.Id);
                foreach (var point in points)
                {
                    if (lifetime.IsCancellationRequested) break;
                    var startAddress = point.StartAddress;
                    var count = point.Count;
                    if (count is < 1 or > 125 || startAddress < 0 ||
                        (long)startAddress + count - 1 > ushort.MaxValue)
                    {
                        failures++;
                        LogWarning($"数据同步跳过“{point.Name}”：FC03 一次最多读取 125 个有效寄存器。");
                        continue;
                    }
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                        timeout.CancelAfter(Math.Clamp(point.TimeoutMs, 50, 120000));
                        var values = await _runtimeManager.ReadModbusHoldingRegistersAsync(device.Id,
                            (ushort)startAddress, (ushort)count, timeout.Token);
                        updates.Add(new ModbusPollUpdate(point, values, DateTimeOffset.Now, startAddress));
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                    catch (OperationCanceledException)
                    {
                        failures++;
                        LogWarning($"数据同步读取“{point.Name}”超时（{point.TimeoutMs} ms）。");
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        LogWarning($"数据同步读取“{point.Name}”失败：{ex.Message}");
                    }
                }
                if (lifetime.IsCancellationRequested)
                {
                    workspace.ModbusResultText = "设备已断开，数据同步已取消。";
                    LogWarning($"{device.Name}：{workspace.ModbusResultText}");
                    return;
                }
                var applied = workspace.ApplyModbusSyncResults(updates);
                workspace.ModbusResultText = $"数据同步完成：更新 {applied} 个地址块，失败 {failures} 个。";
                LogInfo($"{device.Name}：{workspace.ModbusResultText}", target: LogTarget.File | LogTarget.UI);
            }
            finally
            {
                workspace.IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public async Task SendModbusTestAsync(CommunicationDeviceWorkspaceViewModel workspace,
            ModbusAddressPoint point, string dataType, string content)
        {
            if (workspace.IsBusy || !workspace.Communication.IsModbus ||
                !_runtimeManager.IsConnected(workspace.Communication.Id))
                throw new InvalidOperationException("请先连接 Modbus 设备，且等待当前操作完成。");
            if (!workspace.ModbusAddressPoints.Contains(point))
                throw new InvalidOperationException("所选地址块已被删除，请重新选择。");
            var startAddress = point.StartAddress;
            var configuredCount = point.Count;
            var payload = ModbusSendTestEncoder.EncodeDetailed(point, dataType, content);
            var values = payload.Registers;
            workspace.IsBusy = true;
            var writtenCount = 0;
            try
            {
                var lifetime = _polling.GetLifetimeToken(workspace.Communication.Id);
                while (writtenCount < values.Length)
                {
                    if (!workspace.ModbusAddressPoints.Contains(point) ||
                        point.StartAddress != startAddress || point.Count != configuredCount ||
                        point.AccessMode == "只读")
                        throw new InvalidOperationException("地址块在发送过程中发生变更，已停止后续写入。");
                    var maxChunk = dataType is "int" or "float" && point.IntBits == "32bit" ? 122 : 123;
                    var chunkCount = Math.Min(maxChunk, values.Length - writtenCount);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                    timeout.CancelAfter(Math.Clamp(point.TimeoutMs, 50, 120000));
                    var address = checked((ushort)(startAddress + writtenCount));
                    if (chunkCount == 1)
                        await _runtimeManager.WriteModbusRegisterAsync(workspace.Communication.Id,
                            address, values[writtenCount], timeout.Token);
                    else
                    {
                        var chunk = new ushort[chunkCount];
                        Array.Copy(values, writtenCount, chunk, 0, chunkCount);
                        await _runtimeManager.WriteModbusRegistersAsync(workspace.Communication.Id,
                            address, chunk, timeout.Token);
                    }
                    writtenCount += chunkCount;
                }
                workspace.ModbusResultText =
                    $"发送测试成功：{point.Name}，地址 {startAddress}–{startAddress + writtenCount - 1}，写入 {writtenCount}/{configuredCount} 个寄存器。" +
                    (payload.TruncationMessage.Length > 0 ? $" {payload.TruncationMessage}" : string.Empty);
                LogSuccess($"{workspace.Communication.Name}：{workspace.ModbusResultText}");
            }
            catch (Exception ex)
            {
                var detail = writtenCount > 0
                    ? $"已写入前 {writtenCount} 个寄存器，后续失败：{ex.Message}"
                    : ex.Message;
                LogError($"{workspace.Communication.Name} 发送测试失败：{detail}");
                throw new InvalidOperationException(detail, ex);
            }
            finally
            {
                workspace.IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task SendDataAsync()
        {
            var workspace = SelectedWorkspace;
            if (workspace == null || workspace.IsBusy) return;
            var communication = workspace.Communication;
            var payload = workspace.SendText;
            var isHex = workspace.IsSendHexInput;
            byte[] payloadBytes;
            try
            {
                payloadBytes = isHex
                    ? CommunicationPayloadCodec.ParseHex(payload)
                    : Encoding.UTF8.GetBytes(payload);
            }
            catch (FormatException ex)
            {
                LogError($"HEX 发送格式错误：{ex.Message}");
                return;
            }

            workspace.IsBusy = true;
            try
            {
                var deliveredClientCount = await _runtimeManager.SendBytesAsync(communication.Id, payloadBytes);
                workspace.AppendSent(payloadBytes, DateTimeOffset.Now, isHex);
                if (workspace.SendText == payload)
                    workspace.SendText = string.Empty;
                LogInfo(communication.IsTcpServer
                    ? $"已向 {deliveredClientCount} 个 TCP 客户端发送 {(isHex ? "HEX" : "UTF-8 文本")}（{payloadBytes.Length} 字节）"
                    : $"已发送 {(isHex ? "HEX" : "UTF-8 文本")}（{payloadBytes.Length} 字节）");
            }
            catch (Exception ex) { LogError($"发送失败：{ex.Message}"); }
            finally { workspace.IsBusy = false; }
        }

        private void RuntimeManager_DataReceived(object? sender, CommunicationDataReceivedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_deviceWorkspaces.TryGetValue(e.CommunicationId, out var workspace)) return;
                workspace.AppendReceived(e.Data, e.Timestamp);
                workspace.Communication.RuntimeStatus = _runtimeManager.IsConnected(e.CommunicationId)
                    ? CommunicationRuntimeStatus.Connected : CommunicationRuntimeStatus.Disconnected;
                if (SelectedCommunication?.Id == e.CommunicationId)
                    OnPropertyChanged(nameof(IsSelectedConnected));
                CommandManager.InvalidateRequerySuggested();
            }));
        }

        private static CommunicationRuntimeStatus MapStatus(CommunicationState state) => state switch
        {
            CommunicationState.Connecting => CommunicationRuntimeStatus.Connecting,
            CommunicationState.Connected => CommunicationRuntimeStatus.Connected,
            CommunicationState.Faulted => CommunicationRuntimeStatus.Faulted,
            _ => CommunicationRuntimeStatus.Disconnected
        };

        private void RuntimeManager_StatusChanged(object? sender, CommunicationStatusSnapshot status)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_deviceWorkspaces.TryGetValue(status.Device.Value, out var workspace)) return;
                workspace.Communication.RuntimeStatus = MapStatus(status.State);
                if (SelectedCommunication?.Id == status.Device.Value)
                    OnPropertyChanged(nameof(IsSelectedConnected));
                CommandManager.InvalidateRequerySuggested();
            }));
        }

        private void RuntimeManager_ConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_runtimeManager.IsConnected(e.CommunicationId))
                    return;
                StopModbusPolling(e.CommunicationId);
                if (!_deviceWorkspaces.TryGetValue(e.CommunicationId, out var workspace)) return;
                workspace.Communication.RuntimeStatus = CommunicationRuntimeStatus.Disconnected;
                if (SelectedCommunication?.Id == e.CommunicationId)
                    OnPropertyChanged(nameof(IsSelectedConnected));
                CommandManager.InvalidateRequerySuggested();
            }));
        }

        private bool CanToggleConnection(Communication? device) => device != null && device.CanToggleRuntime &&
                                                                    device.Validate().IsValid;

        /// <summary>
        /// 保存
        /// </summary>
        private void Save()
        {
            var currentSolution = _solutionManager.CurrentSolution;
            if (currentSolution?.FilePath != null)
            {
                try
                {
                    if (_solutionManager.SaveSolution(currentSolution.FilePath))
                        LogSuccess("通讯配置保存成功");
                    else
                        LogError("通讯配置保存失败，原方案文件未被替换。");
                }
                catch (Exception ex)
                {
                    LogError($"保存通讯配置失败: {ex.Message}");
                }
            }
            else
            {
                LogWarning("解决方案未保存，无法保存通讯配置");
            }
        }

        /// <summary>
        /// 是否可以保存
        /// </summary>
        private bool CanSave()
        {
            return _solutionManager.CurrentSolution?.FilePath != null;
        }

        /// <summary>
        /// 关闭
        /// </summary>
        private void Close()
        {
            LogInfo("关闭通讯管理器");
        }

        public async ValueTask DisposeAsync()
        {
            _runtimeManager.DataReceived -= RuntimeManager_DataReceived;
            _runtimeManager.ConnectionClosed -= RuntimeManager_ConnectionClosed;
            _runtimeManager.StatusChanged -= RuntimeManager_StatusChanged;
            _polling.BatchReceived -= Polling_BatchReceived;
            foreach (var workspace in _deviceWorkspaces.Values)
            {
                workspace.PropertyChanged -= Workspace_PropertyChanged;
                workspace.AddressConfigurationChanged -= Workspace_AddressConfigurationChanged;
                workspace.Communication.PropertyChanged -= Communication_PropertyChanged;
            }
            _deviceWorkspaces.Clear();
            SelectedWorkspace = null;
            await Task.CompletedTask;
        }
    }
}
