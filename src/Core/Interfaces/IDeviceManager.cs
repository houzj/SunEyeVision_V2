using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalconDotNet;
using SunEyeVision.Core.Models;

namespace SunEyeVision.Core.Interfaces
{
    /// <summary>
    /// 设备管理器接口
    /// </summary>
    public interface IDeviceManager
    {
        /// <summary>
        /// 检测设备
        /// </summary>
        /// <returns>设备列表</returns>
        Task<List<DeviceInfo>> DetectDevicesAsync();

        /// <summary>
        /// 连接设备
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>连接结果</returns>
        Task<bool> ConnectDeviceAsync(string deviceId);

        /// <summary>
        /// 断开设备
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>断开结果</returns>
        Task<bool> DisconnectDeviceAsync(string deviceId);

        /// <summary>
        /// 从设备获取图像
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>获取的图像</returns>
        Task<HImage> CaptureImageAsync(string deviceId);

        /// <summary>
        /// 获取已连接设备列表
        /// </summary>
        /// <returns>已连接设备列表</returns>
        List<string> GetConnectedDevices();
    }
}
