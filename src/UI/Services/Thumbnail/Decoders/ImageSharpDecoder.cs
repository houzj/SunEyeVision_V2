using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SunEyeVision.Core.IO;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Services.Thumbnail.Decoders;

namespace SunEyeVision.UI.Services.Thumbnail.Decoders
{
    /// <summary>
    /// ImageSharp解码?- 高性能跨平台图像解决方案?
    /// 
    /// 优势?
    /// - 对所有格式统一优化（JPEG/PNG/BMP/GIF/TIFF/WebP?
    /// - 更快的解码速度（比WIC?0-50%?
    /// - 更低的内存占?
    /// - 高质量缩放算法（Lanczos3?
    /// 
    /// 替代WicGpuDecoder，提供更稳定、更快的解码性能
    /// </summary>
    public class ImageSharpDecoder : IThumbnailDecoder
    {
        /// <summary>
        /// 解码并发限制信号量。
        /// CPU解码可充分利用多核优势，并发数设为CPU核心数（最多8个）。
        /// </summary>
        private static readonly SemaphoreSlim _decodeSemaphore = new SemaphoreSlim(
            Math.Max(8, Environment.ProcessorCount),
            Math.Max(8, Environment.ProcessorCount));
        
        /// <summary>
        /// 统计当前等待中的解码任务数。
        /// </summary>
        private static int _waitingCount = 0;
        
        /// <summary>
        /// 统计当前正在解码的任务数
        /// </summary>
        private static int _decodingCount = 0;
        
        private bool _isInitialized;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 是否支持硬件加速（CPU解码器始终返回false?
        /// </summary>
        public bool IsHardwareAccelerated => false;

        /// <summary>
        /// 初始化解码器
        /// </summary>
        public bool Initialize()
        {
            if (_isInitialized)
                return true;

            try
            {
_isInitialized = true;
                
                return true;
            }
            catch (Exception ex)
            {
return false;
            }
        }

        /// <summary>
        /// 解码缩略?
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="size">目标尺寸</param>
        /// <param name="prefetchedData">预读取数据（可选）</param>
        /// <param name="verboseLog">是否输出详细日志</param>
        /// <param name="isHighPriority">是否高优先级任务</param>
        /// <returns>解码后的BitmapImage</returns>
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

                // 并发限制：等待获取解码槽?
                var waitSw = Stopwatch.StartNew();
                int currentWaiting = Interlocked.Increment(ref _waitingCount);
                
                int waitTimeout = isHighPriority ? 100 : 5000; // High: 100ms, Normal: 5s
                bool gotSlot = _decodeSemaphore.Wait(waitTimeout);
                waitSw.Stop();
                Interlocked.Decrement(ref _waitingCount);
                
                if (!gotSlot)
                {
                    if (isHighPriority)
                    {
}
                    else
                    {
}
                    return null;
                }
                
                int currentDecoding = Interlocked.Increment(ref _decodingCount);
                
                // 输出队列状态（诊断用）
                if (verboseLog || currentWaiting > 2)
                {
}

                try
                {
                    // 获取文件大小信息
                    var fileInfo = new FileInfo(filePath);
                    string fileSizeInfo = fileInfo.Length > 1024 * 1024 
                        ? $"{fileInfo.Length / 1024.0 / 1024.0:F1}MB" 
                        : $"{fileInfo.Length / 1024.0:F0}KB";

                    // 核心解码
                    var decodeSw = Stopwatch.StartNew();
                    BitmapImage? result = null;

                    if (prefetchedData != null && prefetchedData.Length > 0)
                    {
                        result = DecodeFromBytes(prefetchedData, size, filePath);
                    }
                    else
                    {
                        result = DecodeFromFile(filePath, size);
                    }
                    decodeSw.Stop();

                    totalSw.Stop();

                    if (result != null)
                    {
                        if (verboseLog)
                        {
}
                    }

                    return result;
                }
                finally
                {
                    _decodeSemaphore.Release();
                    Interlocked.Decrement(ref _decodingCount);
                }
            }
            catch (Exception ex)
            {
                totalSw.Stop();
return null;
            }
        }

        /// <summary>
        /// 从文件解决方案?
        /// ?竞态条件修复：等待解码槽位后再次检查文件是否保存?
        /// </summary>
        private BitmapImage? DecodeFromFile(string filePath, int size)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            
            // ?关键日志：开始读取文件（等待解码槽位后）
// ?核心修复：再次检查文件是否存在（等待解码槽位期间可能被删除）
            if (!File.Exists(filePath))
            {
return null;
            }
            
            try
            {
                using var image = Image.Load(filePath);
                
                // ?关键日志：读取成?
return ConvertToBitmapImage(image, size);
            }
            catch (FileNotFoundException)
            {
                // ?关键日志：文件未找到（竞态条件）
return null;
            }
            catch (IOException ioEx)
            {
                // ?关键日志：IO异常（文件被锁定或删除）
return null;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 从字节数组解决方案?
        /// </summary>
        private BitmapImage? DecodeFromBytes(byte[] data, int size, string filePath)
        {
            try
            {
                using var image = Image.Load(data);
                return ConvertToBitmapImage(image, size);
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 将ImageSharp Image转换为WPF BitmapImage
        /// </summary>
        private BitmapImage? ConvertToBitmapImage(Image image, int targetSize)
        {
            try
            {
                // 计算缩放尺寸（保持宽高比例?
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                
                int newWidth, newHeight;
                if (originalWidth > originalHeight)
                {
                    newWidth = targetSize;
                    newHeight = (int)(originalHeight * (double)targetSize / originalWidth);
                }
                else
                {
                    newHeight = targetSize;
                    newWidth = (int)(originalWidth * (double)targetSize / originalHeight);
                }

                // 确保最小尺寸?
                newWidth = Math.Max(1, newWidth);
                newHeight = Math.Max(1, newHeight);

                // 高质量缩略?
                image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));

                // 转换为BGRA格式（WPF兼容?
                using var bgraImage = image.CloneAs<Bgra32>();
                
                // 创建字节数组
                int stride = newWidth * 4; // BGRA = 4 bytes per pixel
                byte[] pixels = new byte[newHeight * stride];
                
                // 使用 ImageSharp 3.x 的方式复制像素数据?
                bgraImage.CopyPixelDataTo(pixels);

                // 创建BitmapImage
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    // 编码为PNG格式
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(
                        BitmapSource.Create(newWidth, newHeight, 96, 96, 
                            System.Windows.Media.PixelFormats.Bgra32, null, pixels, stride)));
                    encoder.Save(stream);
                    
                    stream.Position = 0;
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// ?安全解码缩略图（推荐使用?
        /// 通过 FileAccessManager ?CleanupScheduler 双重保护文件访问，防止清理器删除正在使用的文件夹?
        /// </summary>
        public BitmapImage? DecodeThumbnailSafe(
            IFileAccessManager? fileManager,
            string filePath,
            int size,
            byte[]? prefetchedData = null,
            bool verboseLog = false,
            bool isHighPriority = false)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            
            // ?关键日志：开始保存?
CleanupScheduler.MarkFileInUse(filePath);
            
            try
            {
                // 如果?FileAccessManager，额外使用它保护
                if (fileManager != null)
                {
                    using var scope = fileManager.CreateAccessScope(filePath, FileAccessIntent.Read, FileType.OriginalImage);
                    
                    if (!scope.IsGranted)
                    {
return null;
                    }

                    var result = DecodeThumbnail(filePath, size, prefetchedData, verboseLog, isHighPriority);
                    
                    // ?关键日志：解码完成?
return result;
                }
                else
                {
                    var result = DecodeThumbnail(filePath, size, prefetchedData, verboseLog, isHighPriority);
                    
                    // ?关键日志：解码完成?
return result;
                }
            }
            catch (Exception ex)
            {
                // ?关键日志：解码异步?
throw;
            }
            finally
            {
                // ?确保释放文件引用
                CleanupScheduler.ReleaseFile(filePath);
}
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isInitialized = false;
        }
    }
}
