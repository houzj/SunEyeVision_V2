using System;
using System.Runtime.InteropServices;
using System.Threading;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using Events = SunEyeVision.Core.Events;

namespace SunEyeVision.DeviceDriver
{
    public class SimulatedCameraDriver : BaseDeviceDriver
    {
        private bool _isCapturing;
        private Thread _captureThread;
        private Random _random;

        public SimulatedCameraDriver(string deviceId, ILogger logger)
            : base(deviceId, "Simulated Camera", "SimulatedCamera", logger)
        {
            _random = new Random();
        }

        public override bool Connect()
        {
            try
            {
                Logger.LogInfo($"Connecting to device: {DeviceName} (ID: {DeviceId})");
                Thread.Sleep(100);
                IsConnected = true;
                Logger.LogInfo($"Device connected successfully: {DeviceName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to connect device: {DeviceName}", ex);
                return false;
            }
        }

        public override bool Disconnect()
        {
            try
            {
                if (_isCapturing)
                {
                    StopContinuousCapture();
                }
                Logger.LogInfo($"Disconnecting device: {DeviceName}");
                IsConnected = false;
                Logger.LogInfo($"Device disconnected successfully: {DeviceName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to disconnect device: {DeviceName}", ex);
                return false;
            }
        }

        public override HImage CaptureImage()
        {
            if (!IsConnected)
            {
                Logger.LogError("Device not connected, cannot capture image");
                return null;
            }

            try
            {
                Logger.Info($"Capturing image from device: {DeviceName}");

                int width = 640, height = 480, channels = 3;
                var data = new byte[width * height * channels];
                _random.NextBytes(data);

                // Create Halcon image from interleaved RGB byte data
                IntPtr ptr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, ptr, data.Length);

                HOperatorSet.GenImageInterleaved(out var hObj, ptr, "rgb", width, height, 0, "byte", 0, 0, 0, 0, 0, 0);
                HImage image = new HImage(hObj);

                Marshal.FreeHGlobal(ptr);
                Logger.Info($"Image captured successfully: {width}x{height}");
                return image;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to capture image: {DeviceName}", ex);
                return null;
            }
        }

        public override bool StartContinuousCapture()
        {
            if (!IsConnected)
            {
                Logger.LogError("Device not connected, cannot start continuous capture");
                return false;
            }

            if (_isCapturing)
            {
                Logger.LogWarning("Already in continuous capture mode");
                return false;
            }

            _isCapturing = true;
            _captureThread = new Thread(CaptureLoop);
            _captureThread.IsBackground = true;
            _captureThread.Start();
            Logger.LogInfo($"Starting continuous capture: {DeviceName}, TriggerMode={TriggerMode}");
            return true;
        }

        public override bool StopContinuousCapture()
        {
            if (!_isCapturing)
            {
                return true;
            }

            _isCapturing = false;
            _captureThread?.Join(1000);
            Logger.LogInfo($"Stopped continuous capture: {DeviceName}");
            return true;
        }

        private void CaptureLoop()
        {
            Logger.LogInfo($"Capture loop started: {DeviceName}");

            while (_isCapturing && IsConnected)
            {
                try
                {
                    HImage image = null;
                    bool shouldPublishEvent = false;

                    switch (TriggerMode)
                    {
                        case TriggerMode.None:
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;
                        case TriggerMode.Software:
                            Thread.Sleep(100);
                            continue;
                        case TriggerMode.Hardware:
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;
                        case TriggerMode.Timer:
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;
                    }

                    if (image != null && shouldPublishEvent)
                    {
                        OnImageCaptured(image);
                    }

                    Thread.Sleep(33);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Capture loop error: {ex.Message}");
                }
            }

            Logger.LogInfo($"Capture loop stopped: {DeviceName}");
        }

        public override DeviceInfo GetDeviceInfo()
        {
            return new DeviceInfo
            {
                DeviceId = DeviceId,
                DeviceName = DeviceName,
                Manufacturer = "Simulated",
                Model = "SimulatedCamera",
                IsConnected = IsConnected,
                Description = "Simulated camera device for testing"
            };
        }

        public override bool SetParameter(string key, object value)
        {
            Logger.LogInfo($"Setting device parameter: {key} = {value}");
            return true;
        }

        public override T GetParameter<T>(string key)
        {
            Logger.LogInfo($"Getting device parameter: {key}");
            return default(T);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isCapturing)
                {
                    StopContinuousCapture();
                }
                _captureThread = null;
                _random = null;
            }
            base.Dispose(disposing);
        }
    }
}
