using System;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Models.Imaging
{
    /// <summary>
    /// 图像数据类
    /// </summary>
    /// <remarks>
    /// 封装图像数据的完整信息，包括像素数据、格式、尺寸等。
    /// 设计为不可变对象，确保线程安全。
    /// </remarks>
    [Obsolete("ImageData类型已过时，请使用 HalconDotNet.HImage 类型替代。此类型将在v2.0版本中移除。", false)]
    public sealed class ImageData : IEquatable<ImageData>, IDisposable
    {
        private readonly byte[]? _pixelData;
        private readonly IntPtr _nativePointer;
        private readonly bool _ownsNativePointer;
        private bool _disposed;

        /// <summary>
        /// 创建托管字节数组图像
        /// </summary>
        public ImageData(byte[] pixelData, int width, int height, PixelFormat pixelFormat)
        {
            _pixelData = pixelData ?? throw new ArgumentNullException(nameof(pixelData));
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = width * pixelFormat.GetBytesPerPixel();
            _nativePointer = IntPtr.Zero;
            _ownsNativePointer = false;
        }

        /// <summary>
        /// 创建托管字节数组图像（指定步长）
        /// </summary>
        public ImageData(byte[] pixelData, int width, int height, int stride, PixelFormat pixelFormat)
        {
            _pixelData = pixelData ?? throw new ArgumentNullException(nameof(pixelData));
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = stride;
            _nativePointer = IntPtr.Zero;
            _ownsNativePointer = false;
        }

        /// <summary>
        /// 创建非托管内存图像
        /// </summary>
        public ImageData(IntPtr nativePointer, int width, int height, int stride, PixelFormat pixelFormat, bool ownsPointer = false)
        {
            if (nativePointer == IntPtr.Zero)
                throw new ArgumentException("非托管指针不能为空", nameof(nativePointer));

            _pixelData = null;
            _nativePointer = nativePointer;
            _ownsNativePointer = ownsPointer;
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = stride;
        }

        /// <summary>
        /// 创建空图像
        /// </summary>
        public static ImageData CreateEmpty(int width, int height, PixelFormat pixelFormat)
        {
            int bytesPerPixel = pixelFormat.GetBytesPerPixel();
            int stride = width * bytesPerPixel;
            byte[] pixelData = new byte[stride * height];
            return new ImageData(pixelData, width, height, stride, pixelFormat);
        }

        /// <summary>
        /// 图像宽度（像素）
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// 图像高度（像素）
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// 像素格式
        /// </summary>
        public PixelFormat PixelFormat { get; }

        /// <summary>
        /// 每行字节数（可能有填充）
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// 图像尺寸
        /// </summary>
        public (int Width, int Height) Size => (Width, Height);

        /// <summary>
        /// 像素总数
        /// </summary>
        public int PixelCount => Width * Height;

        /// <summary>
        /// 数据大小（字节）
        /// </summary>
        public int DataSize => Stride * Height;

        /// <summary>
        /// 是否为托管数据
        /// </summary>
        public bool IsManaged => _pixelData != null;

        /// <summary>
        /// 是否为非托管数据
        /// </summary>
        public bool IsNative => _nativePointer != IntPtr.Zero;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 获取托管像素数据
        /// </summary>
        /// <returns>像素数据数组，如果是非托管数据则返回null</returns>
        public byte[]? GetPixelData()
        {
            ThrowIfDisposed();
            return _pixelData;
        }

        /// <summary>
        /// 获取非托管数据指针
        /// </summary>
        public IntPtr GetNativePointer()
        {
            ThrowIfDisposed();
            return _nativePointer;
        }

        /// <summary>
        /// 转换为托管数组（如果是非托管数据则复制）
        /// </summary>
        public byte[] ToManagedArray()
        {
            ThrowIfDisposed();

            if (_pixelData != null)
                return _pixelData;

            byte[] managed = new byte[DataSize];
            System.Runtime.InteropServices.Marshal.Copy(_nativePointer, managed, 0, DataSize);
            return managed;
        }

        /// <summary>
        /// 获取指定位置的像素值（仅适用于Mono8格式）
        /// </summary>
        public byte GetPixel(int x, int y)
        {
            ThrowIfDisposed();

            if (PixelFormat != PixelFormat.Mono8)
                throw new NotSupportedException("GetPixel仅支持Mono8格式，其他格式请使用GetPixelData()");

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"坐标({x}, {y})超出图像范围({Width}x{Height})");

            int offset = y * Stride + x;

            if (_pixelData != null)
                return _pixelData[offset];

            unsafe
            {
                byte* ptr = (byte*)_nativePointer;
                return ptr[offset];
            }
        }

        /// <summary>
        /// 设置指定位置的像素值（仅适用于Mono8格式）
        /// </summary>
        public void SetPixel(int x, int y, byte value)
        {
            ThrowIfDisposed();

            if (PixelFormat != PixelFormat.Mono8)
                throw new NotSupportedException("SetPixel仅支持Mono8格式");

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"坐标({x}, {y})超出图像范围({Width}x{Height})");

            int offset = y * Stride + x;

            if (_pixelData != null)
            {
                _pixelData[offset] = value;
            }
            else
            {
                unsafe
                {
                    byte* ptr = (byte*)_nativePointer;
                    ptr[offset] = value;
                }
            }
        }

        /// <summary>
        /// 获取ROI区域的图像数据
        /// </summary>
        public ImageData GetRoi(Rect2d roi)
        {
            ThrowIfDisposed();

            int x = (int)Math.Max(0, roi.Left);
            int y = (int)Math.Max(0, roi.Top);
            int w = (int)Math.Min(Width - x, roi.Width);
            int h = (int)Math.Min(Height - y, roi.Height);

            if (w <= 0 || h <= 0)
                throw new ArgumentException("ROI区域无效");

            int bytesPerPixel = PixelFormat.GetBytesPerPixel();
            byte[] newData = new byte[w * bytesPerPixel * h];

            if (_pixelData != null)
            {
                for (int row = 0; row < h; row++)
                {
                    int srcOffset = (y + row) * Stride + x * bytesPerPixel;
                    int dstOffset = row * w * bytesPerPixel;
                    Array.Copy(_pixelData, srcOffset, newData, dstOffset, w * bytesPerPixel);
                }
            }
            else
            {
                unsafe
                {
                    byte* srcPtr = (byte*)_nativePointer;
                    fixed (byte* dstPtr = newData)
                    {
                        for (int row = 0; row < h; row++)
                        {
                            int srcOffset = (y + row) * Stride + x * bytesPerPixel;
                            int dstOffset = row * w * bytesPerPixel;
                            for (int i = 0; i < w * bytesPerPixel; i++)
                            {
                                dstPtr[dstOffset + i] = srcPtr[srcOffset + i];
                            }
                        }
                    }
                }
            }

            return new ImageData(newData, w, h, PixelFormat);
        }

        /// <summary>
        /// 深拷贝图像
        /// </summary>
        public ImageData Clone()
        {
            ThrowIfDisposed();

            if (_pixelData != null)
            {
                byte[] newData = new byte[_pixelData.Length];
                Array.Copy(_pixelData, newData, _pixelData.Length);
                return new ImageData(newData, Width, Height, Stride, PixelFormat);
            }
            else
            {
                return new ImageData(ToManagedArray(), Width, Height, Stride, PixelFormat);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ImageData));
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_ownsNativePointer && _nativePointer != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(_nativePointer);
            }

            _disposed = true;
        }

        public bool Equals(ImageData? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Width == other.Width &&
                   Height == other.Height &&
                   PixelFormat == other.PixelFormat &&
                   Stride == other.Stride;
        }

        public override bool Equals(object? obj) => Equals(obj as ImageData);

        public override int GetHashCode() => HashCode.Combine(Width, Height, PixelFormat, Stride);

        public override string ToString() => $"ImageData({Width}x{Height}, {PixelFormat}, {(IsManaged ? "Managed" : "Native")})";

        /// <summary>
        /// 从数组创建图像（自动推断尺寸）
        /// </summary>
        public static ImageData FromArray(byte[] data, int width, int height, PixelFormat format)
        {
            return new ImageData(data, width, height, format);
        }

        /// <summary>
        /// 创建纯色图像
        /// </summary>
        public static ImageData CreateSolid(int width, int height, PixelFormat format, byte value = 0)
        {
            var image = CreateEmpty(width, height, format);
            if (value != 0)
            {
                var data = image.GetPixelData();
                if (data != null) Array.Fill(data, value);
            }
            return image;
        }
    }
}
