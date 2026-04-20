using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

var pngPath = @"C:\Users\houzhongjie\Desktop\生成光之眼 Logo (1)_副本.png";
var icoPath = @"C:\Users\houzhongjie\Desktop\logo.ico";

try
{
    // 加载PNG图片
    using var originalImage = new Bitmap(pngPath);

    // ICO需要包含多个尺寸的图像
    var sizes = new[] { 16, 32, 48, 64, 128, 256 };

    using var icoStream = new FileStream(icoPath, FileMode.Create);
    using var writer = new BinaryWriter(icoStream);

    // ICO文件头（6字节）
    writer.Write((ushort)0);       // Reserved (0)
    writer.Write((ushort)1);       // Image type (1 = icon)
    writer.Write((ushort)sizes.Length); // Number of images

    // 写入图像数据偏移量
    var imageEntries = new List<(int offset, int size)>();
    int dataOffset = 6 + 16 * sizes.Length;

    foreach (var size in sizes)
    {
        // 调整图像大小
        using var resizedImage = new Bitmap(size, size);
        using (var g = Graphics.FromImage(resizedImage))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(originalImage, 0, 0, size, size);
        }

        // 转换为PNG格式的字节数组
        using var ms = new MemoryStream();
        resizedImage.Save(ms, ImageFormat.Png);
        var imageData = ms.ToArray();

        // 写入目录条目（16字节）
        writer.Write((byte)size);           // Width
        writer.Write((byte)size);           // Height
        writer.Write((byte)0);              // Color palette
        writer.Write((byte)0);              // Reserved
        writer.Write((ushort)1);            // Color planes
        writer.Write((ushort)32);           // Bits per pixel
        writer.Write(imageData.Length);      // Image size
        writer.Write(dataOffset);           // Offset to image data

        imageEntries.Add((dataOffset, imageData.Length));
        dataOffset += imageData.Length;
    }

    // 写入实际的图像数据
    foreach (var size in sizes)
    {
        using var resizedImage = new Bitmap(size, size);
        using (var g = Graphics.FromImage(resizedImage))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(originalImage, 0, 0, size, size);
        }

        using var ms = new MemoryStream();
        resizedImage.Save(ms, ImageFormat.Png);
        ms.CopyTo(icoStream);
    }
}
catch (Exception ex)
{
}
