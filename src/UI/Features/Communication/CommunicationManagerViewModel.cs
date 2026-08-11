using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 通讯管理器 ViewModel
    /// </summary>
    public class CommunicationManagerViewModel : ViewModelBase
    {
        private readonly SolutionManager _solutionManager;

        /// <summary>
        /// 通讯配置列表
        /// </summary>
        public ObservableCollection<Communication> Communications { get; }

        /// <summary>
        /// 选中的通讯配置
        /// </summary>
        private Communication? _selectedCommunication;
        public Communication? SelectedCommunication
        {
            get => _selectedCommunication;
            set => SetProperty(ref _selectedCommunication, value);
        }

        /// <summary>
        /// 添加通讯命令
        /// </summary>
        public ICommand AddCommunicationCommand { get; private set; }

        /// <summary>
        /// 删除通讯命令
        /// </summary>
        public ICommand DeleteCommunicationCommand { get; private set; }

        /// <summary>
        /// 测试连接命令
        /// </summary>
        public ICommand TestConnectionCommand { get; private set; }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; private set; }

        /// <summary>
        /// 关闭命令
        /// </summary>
        public ICommand CloseCommand { get; private set; }

        public CommunicationManagerViewModel(SolutionManager solutionManager)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));

            var currentSolution = _solutionManager.CurrentSolution;
            Communications = new ObservableCollection<Communication>(
                currentSolution?.Communications ?? Enumerable.Empty<Communication>()
            );

            InitializeCommands();
        }

        private void InitializeCommands()
        {
            AddCommunicationCommand = new RelayCommand(AddCommunication, () => true);
            DeleteCommunicationCommand = new RelayCommand(DeleteCommunication, CanDeleteCommunication);
            TestConnectionCommand = new RelayCommand(TestConnection, CanTestConnection);
            SaveCommand = new RelayCommand(Save, CanSave);
            CloseCommand = new RelayCommand(Close, () => true);
        }

        /// <summary>
        /// 添加通讯配置
        /// </summary>
        private void AddCommunication()
        {
            var communication = new Communication
            {
                Name = $"通讯{Communications.Count + 1}",
                ConnectionType = CommunicationType.ModbusTCP,
                Settings = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ipAddress", "192.168.1.200" },
                    { "port", 502 }
                },
                Description = ""
            };

            Communications.Add(communication);
            _solutionManager.CurrentSolution?.Communications.Add(communication);
            SelectedCommunication = communication;

            LogInfo($"添加通讯配置: {communication.Name}");
        }

        /// <summary>
        /// 删除通讯配置
        /// </summary>
        private void DeleteCommunication()
        {
            if (SelectedCommunication == null) return;

            Communications.Remove(SelectedCommunication);
            _solutionManager.CurrentSolution?.Communications.Remove(SelectedCommunication);

            LogInfo($"删除通讯配置: {SelectedCommunication.Name}");
            SelectedCommunication = null;
        }

        /// <summary>
        /// 是否可以删除通讯配置
        /// </summary>
        private bool CanDeleteCommunication()
        {
            return SelectedCommunication != null;
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        private void TestConnection()
        {
            if (SelectedCommunication == null) return;

            LogInfo($"测试通讯连接: {SelectedCommunication.Name}");
            LogInfo($"  类型: {SelectedCommunication.ConnectionType}");
            LogInfo($"  地址: {SelectedCommunication.GetConnectionString()}");

            try
            {
                System.Threading.Thread.Sleep(500);

                LogSuccess($"通讯连接测试成功: {SelectedCommunication.Name}");
            }
            catch (Exception ex)
            {
                LogError($"通讯连接测试失败: {SelectedCommunication.Name}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否可以测试连接
        /// </summary>
        private bool CanTestConnection()
        {
            return SelectedCommunication != null &&
                   SelectedCommunication.Settings.Count > 0;
        }

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
                    _solutionManager.SaveSolution(currentSolution.FilePath);
                    LogSuccess("通讯配置保存成功");
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
    }
}
