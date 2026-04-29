using System;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 图像数据载体 - 对应Halcon的HImage
    /// </summary>
    /// <remarks>
    /// 纯不可变数据类，不含任何算法逻辑。
    /// 所有图像操作通过 <see cref="CvOp"/> 静态类执行。
    /// </remarks>
    public class CvImage : IDisposable
    {
        private readonly Mat _image;
        private readonly bool _ownsImage;

        /// <summary>构造函数</summary>
        /// <param name="image">输入Mat</param>
        /// <param name="ownsImage">是否拥有所有权（true=Dispose时释放）</param>
        public CvImage(Mat image, bool ownsImage = false)
        {
            _image = image;
            _ownsImage = ownsImage;
        }

        /// <summary>从已有图像创建克隆副本</summary>
        public static CvImage FromMat(Mat image) => new(image.Clone(), ownsImage: true);

        /// <summary>创建纯色图像</summary>
        public static CvImage Create(Size size, Scalar color, int channels = 1)
        {
            var type = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
            return new CvImage(new Mat(size, type, color), ownsImage: true);
        }

        /// <summary>底层Mat图像</summary>
        public Mat Image => _image;

        /// <summary>图像尺寸</summary>
        public Size Size => _image.Size();

        /// <summary>通道数</summary>
        public int Channels => _image.Channels();

        /// <summary>是否为空</summary>
        public bool Empty => _image == null || _image.Empty();

        public void Dispose()
        {
            if (_ownsImage && _image != null) _image.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
