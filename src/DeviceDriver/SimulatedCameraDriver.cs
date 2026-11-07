using System;
using System.Threading;
using OpenCvSharp;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.Core.Models;
using Events = SunEyeVision.Core.Events;

namespace SunEyeVision.DeviceDriver
{
    /// <summary>
    /// Simulated camera driver (for testing) with trigger support
    /// </summary>
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

                // Simulate connection delay
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

        public override Mat CaptureImage()
        {
            if (!IsConnected)
            {
                Logger.LogError("Device not connected, cannot capture image");
                return null;
            }

            try
            {
                Logger.Info($"Capturing image from device: {DeviceName}");

                // Generate simulated image data
                var width = 640;
                var height = 480;
                var channels = 3;
                var data = new byte[width * height * channels];

                // Generate random noise image
                _random.NextBytes(data);

                // Create OpenCvSharp.Mat from byte array
                var image = new Mat(height, width, MatType.CV_8UC(channels));
                System.Runtime.InteropServices.Marshal.Copy(data, 0, image.Data, data.Length);
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
                    Mat image = null;
                    bool shouldPublishEvent = false;

                    // Based on trigger mode, decide whether to capture
                    switch (TriggerMode)
                    {
                        case TriggerMode.None:
                            // Continuous capture mode
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;

                        case TriggerMode.Software:
                            // Software trigger mode - wait for TriggerCapture() call
                            Thread.Sleep(100);
                            continue;

                        case TriggerMode.Hardware:
                            // Simulate hardware trigger - capture every 100ms
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;

                        case TriggerMode.Timer:
                            // Timer trigger mode - capture every 100ms
                            image = CaptureImage();
                            shouldPublishEvent = true;
                            break;
                    }

                    if (image != null && shouldPublishEvent)
                    {
                        // Trigger image captured event
                        OnImageCaptured(image);
                    }

                    // Simulate frame rate (~30 FPS)
                    Thread.Sleep(33);
                }
                catch (Exception ex)
                {
                    // Ignore exception, continue capturing
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
