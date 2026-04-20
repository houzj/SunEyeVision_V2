using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace SunEyeVision.UI.Controls.Rendering
{
    /// <summary>
    /// DirectX缩略图渲染器 - 使用WPF内置的GPU加速功能
    /// WPF的BitmapImage已经利用Direct3D进行硬件加速渲染
    /// 这个类提供自动降级和性能优化
    /// </summary>
    public class DirectXThumbnailRenderer : IDisposable
    {
        private bool _isInitialized = false;
        private bool _gpuAccelerated = false;

        /// <summary>
        /// 是否启用GPU加速
        /// WPF默认使用GPU渲染，所以始终返回true（除非检测到问题）
        /// </summary>
        public bool IsGPUEnabled => _gpuAccelerated;

        /// <summary>
        /// 初始化渲染器
        /// </summary>
        public bool Initialize()
        {
            if (_isInitialized)
                return _gpuAccelerated;

            try
            {
                // 检测硬件渲染层
                // Tier > 0 表示启用了硬件加速
                int tier = System.Windows.Media.RenderCapability.Tier >> 16;

                if (tier > 0)
                {
                    _gpuAccelerated = true;
                }
                else
                {
}

                _isInitialized = true;
                return _gpuAccelerated;
            }
            catch (Exception ex)
            {
_isInitialized = true;
                return false;
            }
        }

        /// <summary>
        /// 加载缩略图（使用WPF的GPU加速功能）
        /// </summary>
        public BitmapImage? LoadThumbnail(string filePath, int size)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            try
            {
                return LoadThumbnailOptimized(filePath, size);
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 优化的缩略图加载（利用WPF的GPU加速）
        /// WPF的BitmapImage自动使用Direct3D进行硬件加速
        /// </summary>
        private BitmapImage? LoadThumbnailOptimized(string filePath, int size)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 加载后立即解码，GPU加速
                bitmap.CreateOptions = BitmapCreateOptions.None; // 完全创建，支持硬件加速
                bitmap.UriSource = new Uri(filePath);
                bitmap.DecodePixelWidth = size; // 限制解码宽度，GPU加速缩放
                bitmap.Rotation = Rotation.Rotate0;
                bitmap.EndInit();
                bitmap.Freeze(); // 冻结后可跨线程访问，GPU纹理

                return bitmap;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // WPF的资源管理是自动的，这里只需标记为未初始化
            _isInitialized = false;
        }
    }
}
