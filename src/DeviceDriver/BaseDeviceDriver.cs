using System;
using OpenCvSharp;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.Core.Models;

namespace SunEyeVision.DeviceDriver
{
    /// <summary>
    /// 触发器模式
    /// </summary>
    public enum TriggerMode
    {
        /// <summary>
        /// 无触发,连续采集
        /// </summary>
        None,

        /// <summary>
        /// 硬件触发
        /// </summary>
        Hardware,

        /// <summary>
        /// 软件触发
        /// </summary>
        Software,

        /// <summary>
        /// 定时器触发
        /// </summary>
        Timer
    }

    /// <summary>
    /// 图像捕获事件参数
    /// </summary>
    public class ImageCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 捕获的图像
        /// </summary>
        public Mat Image { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 帧编号
        /// </summary>
        public long FrameNumber { get; set; }
    }

    /// <summary>
    /// 设备驱动基类
    /// </summary>
    public abstract class BaseDeviceDriver : IDisposable
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; protected set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; protected set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public string DeviceType { get; protected set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// 触发器模式
        /// </summary>
        public TriggerMode TriggerMode { get; protected set; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        protected ILogger Logger { get; private set; }

        /// <summary>
        /// 图像捕获事件
        /// </summary>
        public event EventHandler<ImageCapturedEventArgs> ImageCaptured;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 帧计数器
        /// </summary>
        protected long _frameCount = 0;

        protected BaseDeviceDriver(string deviceId, string deviceName, string deviceType, ILogger logger)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            DeviceType = deviceType;
            Logger = logger;
            IsConnected = false;
            TriggerMode = TriggerMode.None;
        }

        /// <summary>
        /// 连接设备
        /// </summary>
        public abstract bool Connect();

        /// <summary>
        /// 断开设备
        /// </summary>
        public abstract bool Disconnect();

        /// <summary>
        /// 获取图像
        /// </summary>
        public abstract Mat CaptureImage();

        /// <summary>
        /// 开始连续采集
        /// </summary>
        public abstract bool StartContinuousCapture();

        /// <summary>
        /// 停止连续采集
        /// </summary>
        public abstract bool StopContinuousCapture();

        /// <summary>
        /// 设置触发器模式
        /// </summary>
        public virtual bool SetTriggerMode(TriggerMode mode)
        {
            TriggerMode = mode;
            Logger.LogInfo($"Trigger mode set to: {mode}");
            return true;
        }

        /// <summary>
        /// 触发图像采集 (软件触发)
        /// </summary>
        public virtual Mat TriggerCapture()
        {
            if (TriggerMode != TriggerMode.Software && TriggerMode != TriggerMode.None)
            {
                Logger.LogWarning("Cannot trigger capture in current trigger mode");
                return null;
            }

            var image = CaptureImage();
            if (image != null)
            {
                OnImageCaptured(image);
            }
            return image;
        }

        /// <summary>
        /// 触发图像捕获事件
        /// </summary>
        protected virtual void OnImageCaptured(Mat image)
        {
            _frameCount++;
            ImageCaptured?.Invoke(this, new ImageCapturedEventArgs
            {
                DeviceId = DeviceId,
                Image = image,
                Timestamp = DateTime.Now,
                FrameNumber = _frameCount
            });
        }

        /// <summary>
        /// 获取设备信息
        /// </summary>
        public abstract DeviceInfo GetDeviceInfo();

        /// <summary>
        /// 设置设备参数
        /// </summary>
        public abstract bool SetParameter(string key, object value);

        /// <summary>
        /// 获取设备参数
        /// </summary>
        public abstract T GetParameter<T>(string key);

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (IsConnected)
                    {
                        Disconnect();
                    }
                }

                // 释放非托管资源
                _disposed = true;
            }
        }

        ~BaseDeviceDriver()
        {
            Dispose(false);
        }
    }
}
