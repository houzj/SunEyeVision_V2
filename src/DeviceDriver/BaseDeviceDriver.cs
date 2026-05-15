using System;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;

namespace SunEyeVision.DeviceDriver
{
    public enum TriggerMode
    {
        None,
        Hardware,
        Software,
        Timer
    }

    public class ImageCapturedEventArgs : EventArgs
    {
        public string DeviceId { get; set; }
        public HImage Image { get; set; }
        public DateTime Timestamp { get; set; }
        public long FrameNumber { get; set; }
    }

    public abstract class BaseDeviceDriver : IDisposable
    {
        public string DeviceId { get; protected set; }
        public string DeviceName { get; protected set; }
        public string DeviceType { get; protected set; }
        public bool IsConnected { get; protected set; }
        public TriggerMode TriggerMode { get; protected set; }
        protected ILogger Logger { get; private set; }
        public event EventHandler<ImageCapturedEventArgs> ImageCaptured;
        private bool _disposed;
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

        public abstract bool Connect();
        public abstract bool Disconnect();
        public abstract HImage CaptureImage();
        public abstract bool StartContinuousCapture();
        public abstract bool StopContinuousCapture();

        public virtual bool SetTriggerMode(TriggerMode mode)
        {
            TriggerMode = mode;
            Logger.LogInfo($"Trigger mode set to: {mode}");
            return true;
        }

        public virtual HImage TriggerCapture()
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

        protected virtual void OnImageCaptured(HImage image)
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

        public abstract DeviceInfo GetDeviceInfo();
        public abstract bool SetParameter(string key, object value);
        public abstract T GetParameter<T>(string key);

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
                    if (IsConnected)
                    {
                        Disconnect();
                    }
                }
                _disposed = true;
            }
        }

        ~BaseDeviceDriver()
        {
            Dispose(false);
        }
    }
}
