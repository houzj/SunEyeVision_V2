using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using SunEyeVision.Core.Interfaces;

namespace SunEyeVision.DeviceDriver
{
    /// <summary>
    /// Device manager
    /// </summary>
    public class DeviceManager : IDeviceManager
    {
        /// <summary>
        /// List of registered device drivers
        /// </summary>
        private Dictionary<string, BaseDeviceDriver> _registeredDrivers;

        /// <summary>
        /// Logger
        /// </summary>
        private ILogger Logger { get; set; }

        public DeviceManager(ILogger logger)
        {
            Logger = logger;
            _registeredDrivers = new Dictionary<string, BaseDeviceDriver>();
        }

        /// <summary>
        /// Register device driver
        /// </summary>
        public void RegisterDriver(BaseDeviceDriver driver)
        {
            if (_registeredDrivers.ContainsKey(driver.DeviceId))
            {
                Logger.LogWarning($"Device driver already exists: {driver.DeviceId}");
                return;
            }

            _registeredDrivers[driver.DeviceId] = driver;
            Logger.LogInfo($"Registered device driver: {driver.DeviceName} (ID: {driver.DeviceId})");
        }

        /// <summary>
        /// Unregister device driver
        /// </summary>
        public bool UnregisterDriver(string deviceId)
        {
            if (_registeredDrivers.TryGetValue(deviceId, out var driver))
            {
                if (driver.IsConnected)
                {
                    driver.Disconnect();
                }

                driver.Dispose();
                _registeredDrivers.Remove(deviceId);
                Logger.LogInfo($"Unregistered device driver: {driver.DeviceName} (ID: {deviceId})");
                return true;
            }

            return false;
        }

        public async Task<List<DeviceInfo>> DetectDevicesAsync()
        {
            var devices = new List<DeviceInfo>();

            Logger.LogInfo("Starting device detection");

            foreach (var driver in _registeredDrivers.Values)
            {
                try
                {
                    var info = driver.GetDeviceInfo();
                    devices.Add(info);
                    Logger.LogInfo($"Detected device: {info.DeviceName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to detect device: {driver.DeviceName}", ex);
                }
            }

            Logger.LogInfo($"Detection completed, found {devices.Count} devices");

            await Task.CompletedTask;
            return devices;
        }

        public async Task<bool> ConnectDeviceAsync(string deviceId)
        {
            if (!_registeredDrivers.TryGetValue(deviceId, out var driver))
            {
                Logger.LogError($"Device driver does not exist: {deviceId}");
                await Task.CompletedTask;
                return false;
            }

            try
            {
                driver.Connect();
                Logger.LogInfo($"Connected to device: {driver.DeviceName}");
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to connect device: {deviceId}", ex);
                await Task.CompletedTask;
                return false;
            }
        }

        public async Task<bool> DisconnectDeviceAsync(string deviceId)
        {
            if (!_registeredDrivers.TryGetValue(deviceId, out var driver))
            {
                Logger.LogError($"Device driver does not exist: {deviceId}");
                await Task.CompletedTask;
                return false;
            }

            try
            {
                driver.Disconnect();
                Logger.LogInfo($"Disconnected from device: {driver.DeviceName}");
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to disconnect device: {deviceId}", ex);
                await Task.CompletedTask;
                return false;
            }
        }

        public async Task<HImage> CaptureImageAsync(string deviceId)
        {
            if (!_registeredDrivers.TryGetValue(deviceId, out var driver))
            {
                Logger.LogError($"Device driver does not exist: {deviceId}");
                await Task.CompletedTask;
                return null;
            }

            if (!driver.IsConnected)
            {
                Logger.LogError($"Device not connected: {deviceId}");
                await Task.CompletedTask;
                return null;
            }

            try
            {
                var image = driver.CaptureImage();
                await Task.CompletedTask;
                return image;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to capture image: {deviceId}", ex);
                await Task.CompletedTask;
                return null;
            }
        }

        /// <summary>
        /// Get list of connected devices
        /// </summary>
        public List<string> GetConnectedDevices()
        {
            return _registeredDrivers.Values
                .Where(d => d.IsConnected)
                .Select(d => d.DeviceId)
                .ToList();
        }

        /// <summary>
        /// Get list of registered devices
        /// </summary>
        public List<BaseDeviceDriver> GetRegisteredDrivers()
        {
            return _registeredDrivers.Values.ToList();
        }

        /// <summary>
        /// Release all resources
        /// </summary>
        public void Dispose()
        {
            foreach (var driver in _registeredDrivers.Values)
            {
                try
                {
                    if (driver.IsConnected)
                    {
                        driver.Disconnect();
                    }
                    driver.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error disposing driver: {driver.DeviceName}", ex);
                }
            }

            _registeredDrivers.Clear();
        }
    }
}
