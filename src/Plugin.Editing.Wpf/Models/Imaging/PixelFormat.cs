using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Models.Imaging
{
    /// <summary>
    /// 像素格式枚举
    /// </summary>
    /// <remarks>
    /// 定义支持的图像像素格式，涵盖常见的工业相机输出格式。
    /// </remarks>
    public enum PixelFormat
    {
        /// <summary>
        /// 未定义格式
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// 8位灰度图像
        /// </summary>
        Mono8 = 1,

        /// <summary>
        /// 10位灰度图像（存储在16位中）
        /// </summary>
        Mono10 = 2,

        /// <summary>
        /// 12位灰度图像（存储在16位中）
        /// </summary>
        Mono12 = 3,

        /// <summary>
        /// 16位灰度图像
        /// </summary>
        Mono16 = 4,

        /// <summary>
        /// RGB24格式（R-G-B顺序，每通道8位）
        /// </summary>
        RGB24 = 10,

        /// <summary>
        /// BGR24格式（B-G-R顺序，每通道8位）
        /// </summary>
        BGR24 = 11,

        /// <summary>
        /// RGBA32格式（R-G-B-A顺序，每通道8位）
        /// </summary>
        RGBA32 = 12,

        /// <summary>
        /// BGRA32格式（B-G-R-A顺序，每通道8位）
        /// </summary>
        BGRA32 = 13,

        /// <summary>
        /// RGB48格式（每通道16位）
        /// </summary>
        RGB48 = 14,

        /// <summary>
        /// BGR48格式（每通道16位）
        /// </summary>
        BGR48 = 15,

        /// <summary>
        /// RGB平面格式（三个独立的Mono8平面）
        /// </summary>
        RGB24Planar = 20,

        /// <summary>
        /// YUV422格式
        /// </summary>
        YUV422 = 30,

        /// <summary>
        /// YUV420P格式
        /// </summary>
        YUV420P = 31,

        /// <summary>
        /// BayerRG8格式（需去马赛克）
        /// </summary>
        BayerRG8 = 40,

        /// <summary>
        /// BayerGB8格式
        /// </summary>
        BayerGB8 = 41,

        /// <summary>
        /// BayerGR8格式
        /// </summary>
        BayerGR8 = 42,

        /// <summary>
        /// BayerBG8格式
        /// </summary>
        BayerBG8 = 43,

        /// <summary>
        /// 32位浮点灰度图像
        /// </summary>
        MonoFloat = 50,

        /// <summary>
        /// 64位浮点灰度图像
        /// </summary>
        MonoDouble = 51
    }

    /// <summary>
    /// 像素格式扩展方法
    /// </summary>
    public static class PixelFormatExtensions
    {
        /// <summary>
        /// 获取每像素字节数
        /// </summary>
        public static int GetBytesPerPixel(this PixelFormat format)
        {
            return format switch
            {
                PixelFormat.Mono8 => 1,
                PixelFormat.Mono10 => 2,
                PixelFormat.Mono12 => 2,
                PixelFormat.Mono16 => 2,
                PixelFormat.MonoFloat => 4,
                PixelFormat.MonoDouble => 8,
                PixelFormat.RGB24 => 3,
                PixelFormat.BGR24 => 3,
                PixelFormat.RGBA32 => 4,
                PixelFormat.BGRA32 => 4,
                PixelFormat.RGB48 => 6,
                PixelFormat.BGR48 => 6,
                PixelFormat.YUV422 => 2,
                PixelFormat.YUV420P => 1, // 每像素平均
                PixelFormat.BayerRG8 => 1,
                PixelFormat.BayerGB8 => 1,
                PixelFormat.BayerGR8 => 1,
                PixelFormat.BayerBG8 => 1,
                PixelFormat.RGB24Planar => 3,
                _ => 0
            };
        }

        /// <summary>
        /// 获取每像素位数
        /// </summary>
        public static int GetBitsPerPixel(this PixelFormat format)
        {
            return GetBytesPerPixel(format) * 8;
        }

        /// <summary>
        /// 是否为灰度格式
        /// </summary>
        public static bool IsMonochrome(this PixelFormat format)
        {
            return format switch
            {
                PixelFormat.Mono8 => true,
                PixelFormat.Mono10 => true,
                PixelFormat.Mono12 => true,
                PixelFormat.Mono16 => true,
                PixelFormat.MonoFloat => true,
                PixelFormat.MonoDouble => true,
                PixelFormat.BayerRG8 => true,
                PixelFormat.BayerGB8 => true,
                PixelFormat.BayerGR8 => true,
                PixelFormat.BayerBG8 => true,
                _ => false
            };
        }

        /// <summary>
        /// 是否为颜色格式
        /// </summary>
        public static bool IsColor(this PixelFormat format)
        {
            return !IsMonochrome(format) && format != PixelFormat.Undefined;
        }

        /// <summary>
        /// 是否为Bayer格式
        /// </summary>
        public static bool IsBayer(this PixelFormat format)
        {
            return format is PixelFormat.BayerRG8 or PixelFormat.BayerGB8
                or PixelFormat.BayerGR8 or PixelFormat.BayerBG8;
        }

        /// <summary>
        /// 是否为浮点格式
        /// </summary>
        public static bool IsFloatingPoint(this PixelFormat format)
        {
            return format is PixelFormat.MonoFloat or PixelFormat.MonoDouble;
        }

        /// <summary>
        /// 获取通道数
        /// </summary>
        public static int GetChannelCount(this PixelFormat format)
        {
            return format switch
            {
                PixelFormat.RGB24 or PixelFormat.BGR24 => 3,
                PixelFormat.RGBA32 or PixelFormat.BGRA32 => 4,
                PixelFormat.RGB48 or PixelFormat.BGR48 => 3,
                PixelFormat.RGB24Planar => 3,
                PixelFormat.YUV422 => 2,
                PixelFormat.YUV420P => 3,
                _ => 1
            };
        }
    }
}
