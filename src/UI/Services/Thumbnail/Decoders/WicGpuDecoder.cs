using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;
using SunEyeVision.Core.IO;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Services.Thumbnail.Decoders;

namespace SunEyeVision.UI.Services.Thumbnail.Decoders
{
    /// <summary>
    /// WIC GPU解码器 - 基于WPF内置WIC硬件加速
    /// </summary>
    public class WicGpuDecoder : IThumbnailDecoder
    {
        // 优化：提升并发限制到8，充分利用现代GPU解码能力
        private static readonly SemaphoreSlim _gpuDecodeSemaphore = new SemaphoreSlim(8, 8);
        private static int _waitingCount = 0;
        private static int _decodingCount = 0;
        
        // RPC失败计数器 - 用于自动降级
        private static int _rpcFailureCount = 0;
        private const int MAX_RPC_FAILURES = 3;
        private static bool _rpcDisabled = false;
        
        // RPC_S_SERVER_UNAVAILABLE = 0x000006BA = 1722
        private const int RPC_S_SERVER_UNAVAILABLE = 1722;
        private const int HRESULT_RPC_UNAVAILABLE = unchecked((int)0x800706BA);
        
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public bool IsHardwareAccelerated { get; private set; }
        
        private const int LARGE_IMAGE_THRESHOLD = 4000 * 3000;
        private const int HUGE_IMAGE_THRESHOLD = 8000 * 6000;

        public bool Initialize()
        {
            if (_isInitialized)
                return IsHardwareAccelerated;

            try
            {
int tier = System.Windows.Media.RenderCapability.Tier >> 16;
                bool hasGPU = tier > 0;

                if (!hasGPU)
                {
_isInitialized = true;
                    return false;
                }

                IsHardwareAccelerated = true;
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
_isInitialized = true;
                return false;
            }
        }

        public BitmapImage? DecodeThumbnail(string filePath, int size, byte[]? prefetchedData = null, bool verboseLog = false, bool isHighPriority = false)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            var totalSw = Stopwatch.StartNew();

            try
            {
                if (!File.Exists(filePath))
                {
return null;
                }

                var waitSw = Stopwatch.StartNew();
                int currentWaiting = Interlocked.Increment(ref _waitingCount);

                bool gotSlot = _gpuDecodeSemaphore.Wait(5000);
                
                waitSw.Stop();
                Interlocked.Decrement(ref _waitingCount);
                
                if (!gotSlot)
                {
return null;
                }
                
                int currentDecoding = Interlocked.Increment(ref _decodingCount);
                
                if (verboseLog || currentWaiting > 1)
                {
}

                try
                {
                    var readSw = Stopwatch.StartNew();
                    byte[] imageBytes;
                    if (prefetchedData != null && prefetchedData.Length > 0)
                    {
                        imageBytes = prefetchedData;
                        readSw.Stop();
}
                    else
                    {
if (!File.Exists(filePath))
                        {
return null;
                        }
                        
                        try
                        {
                            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
                            imageBytes = new byte[fs.Length];
                            int bytesRead = fs.Read(imageBytes, 0, imageBytes.Length);
                            if (bytesRead != imageBytes.Length && imageBytes.Length > 0)
                            {
                                Array.Resize(ref imageBytes, bytesRead);
                            }
                            readSw.Stop();
}
                        catch (FileNotFoundException)
                        {
return null;
                        }
                        catch (IOException ioEx)
                        {
return null;
                        }
                    }
                    
                    long fileSizeKB = imageBytes.Length / 1024;
                    string fileSizeMB = fileSizeKB > 1024 ? $"{fileSizeKB / 1024.0:F1}MB" : $"{fileSizeKB}KB";
                    
                    var infoSw = Stopwatch.StartNew();
                    int originalWidth = 0, originalHeight = 0;
                    string formatInfo = "";
                    long totalPixels = 0;
                    
                    if (verboseLog)
                    {
                        try
                        {
                            using var memStream = new MemoryStream(imageBytes);
                            var decoder = BitmapDecoder.Create(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnDemand);
                            if (decoder.Frames.Count > 0)
                            {
                                var frame = decoder.Frames[0];
                                originalWidth = frame.PixelWidth;
                                originalHeight = frame.PixelHeight;
                                formatInfo = $"格式:{decoder.CodecInfo.FriendlyName}";
                                totalPixels = (long)originalWidth * originalHeight;
                            }
                        }
                        catch { }
                    }
                    infoSw.Stop();
                    
                    string resolutionInfo = originalWidth > 0 
                        ? $"原始:{originalWidth}x{originalHeight}({totalPixels / 1000000.0:F1}MP)" 
                        : "";

                    var decodeSw = Stopwatch.StartNew();
                    
                    // 检查RPC是否已被禁用
                    if (_rpcDisabled)
                    {
                        return DecodeWithCpu(filePath, size, prefetchedData, totalSw);
                    }
                    
                    BitmapImage? bitmap = null;
                    bool rpcFailed = false;
                    
                    try
                    {
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();

                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = size;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                        MemoryStream? decodeStream = null;
                        try
                        {
                            decodeStream = new MemoryStream(imageBytes);
                            bitmap.StreamSource = decodeStream;
                            bitmap.Rotation = Rotation.Rotate0;
                            bitmap.EndInit();
                        }
                        finally
                        {
                            decodeStream?.Dispose();
                        }
                    }
                    catch (COMException comEx) when (comEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || 
                                                      (comEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
                    {
                        // RPC服务器不可用 - 记录失败并降级
                        rpcFailed = true;
                        int failures = System.Threading.Interlocked.Increment(ref _rpcFailureCount);
if (failures >= MAX_RPC_FAILURES)
                        {
                            _rpcDisabled = true;
}
                        
                        return DecodeWithCpu(filePath, size, prefetchedData, totalSw);
                    }
                    catch (Exception ex) when (ex.Message.Contains("RPC") || ex.Message.Contains("服务器不可用"))
                    {
                        // 其他可能的RPC相关异常
                        rpcFailed = true;
return DecodeWithCpu(filePath, size, prefetchedData, totalSw);
                    }
                    
                    decodeSw.Stop();

                    var freezeSw = Stopwatch.StartNew();
                    
                    try
                    {
                        bitmap?.Freeze();
                    }
                    catch (COMException comEx) when (comEx.ErrorCode == HRESULT_RPC_UNAVAILABLE || 
                                                      (comEx.ErrorCode & 0xFFFF) == RPC_S_SERVER_UNAVAILABLE)
                    {
                        // Freeze时的RPC异常
return DecodeWithCpu(filePath, size, prefetchedData, totalSw);
                    }
                    
                    freezeSw.Stop();

                    totalSw.Stop();

                    if (verboseLog)
                    {
                        string diagnosticInfo = string.IsNullOrEmpty(resolutionInfo) 
                            ? $"文件:{fileSizeMB}" 
                            : $"{resolutionInfo} 文件:{fileSizeMB} {formatInfo}";
if (totalPixels > HUGE_IMAGE_THRESHOLD)
                        {
}
                    }

                    if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    {
return null;
                    }

                    return bitmap;
                }
                finally
                {
                    _gpuDecodeSemaphore.Release();
                    Interlocked.Decrement(ref _decodingCount);
                }
            }
            catch (Exception ex)
            {
                totalSw.Stop();
return null;
            }
        }

        private BitmapImage? DecodeWithCpu(string filePath, int size, byte[]? prefetchedData, Stopwatch totalSw)
        {
            try
            {
                byte[] imageBytes;
                if (prefetchedData != null && prefetchedData.Length > 0)
                {
                    imageBytes = prefetchedData;
                }
                else
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
                    imageBytes = new byte[fs.Length];
                    fs.Read(imageBytes, 0, imageBytes.Length);
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = size;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                
                MemoryStream? decodeStream = null;
                try
                {
                    decodeStream = new MemoryStream(imageBytes);
                    bitmap.StreamSource = decodeStream;
                    bitmap.Rotation = Rotation.Rotate0;
                    bitmap.EndInit();
                }
                finally
                {
                    decodeStream?.Dispose();
                }
                
                bitmap.Freeze();
                totalSw.Stop();
return bitmap;
            }
            catch (Exception ex)
            {
                totalSw.Stop();
return null;
            }
        }

        public BitmapImage? DecodeThumbnailSafe(
            IFileAccessManager? fileManager,
            string filePath,
            int size,
            byte[]? prefetchedData = null,
            bool verboseLog = false,
            bool isHighPriority = false)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
CleanupScheduler.MarkFileInUse(filePath);
            
            try
            {
                if (fileManager != null)
                {
                    using var scope = fileManager.CreateAccessScope(filePath, FileAccessIntent.Read, FileType.OriginalImage);
                    
                    if (!scope.IsGranted)
                    {
return null;
                    }

                    var result = DecodeThumbnail(filePath, size, prefetchedData, verboseLog, isHighPriority);
return result;
                }
                else
                {
                    var result = DecodeThumbnail(filePath, size, prefetchedData, verboseLog, isHighPriority);
return result;
                }
            }
            catch (Exception ex)
            {
throw;
            }
            finally
            {
                CleanupScheduler.ReleaseFile(filePath);
}
        }

        public void Dispose()
        {
            _isInitialized = false;
            IsHardwareAccelerated = false;
}
    }
}
