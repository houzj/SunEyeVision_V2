using System;
using System.ComponentModel;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>色彩空间取值与原厂 RangeInfo.ColorTypes 一致：1=RGB、2=HSV、3=HSI。</summary>
public enum ColorRangeSpace
{
    Rgb = 1,
    Hsv = 2,
    Hsi = 3
}

/// <summary>
/// 原厂 <c>RangeInfo</c> 的九字段颜色区间：C1High、C1Low、C2High、C2Low、C3High、C3Low、色彩空间、反色、启用。
/// </summary>
public readonly record struct ColorRangeValue(
    int C1High, int C1Low, int C2High, int C2Low, int C3High, int C3Low,
    ColorRangeSpace ColorSpace, bool IsColorInversion, bool IsChecked)
{
    public static ColorRangeValue Default { get; } =
        new(255, 0, 255, 0, 255, 0, ColorRangeSpace.Rgb, false, true);

    public int HighOf(int channel) => channel switch
    {
        1 => C1High,
        2 => C2High,
        3 => C3High,
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    public int LowOf(int channel) => channel switch
    {
        1 => C1Low,
        2 => C2Low,
        3 => C3Low,
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    public ColorRangeValue WithChannel(int channel, int low, int high) => channel switch
    {
        1 => this with { C1Low = low, C1High = high },
        2 => this with { C2Low = low, C2High = high },
        3 => this with { C3Low = low, C3High = high },
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };
}

public sealed class ColorRangeRejectedEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

/// <summary>颜色卡片的数据项；只负责绑定显示和变更通知，持久化由消费者负责。</summary>
public sealed class NativeColorRangeCard : INotifyPropertyChanged
{
    private ColorRangeValue _value;

    public NativeColorRangeCard(ColorRangeValue value) => _value = value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? Changed;

    public ColorRangeValue Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(BrushColor));
            OnPropertyChanged(nameof(IsChecked));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public Brush BrushColor => ColorRangePalette.BrushFor(_value);

    public bool IsChecked
    {
        get => _value.IsChecked;
        set
        {
            if (_value.IsChecked == value) return;
            Value = _value with { IsChecked = value };
        }
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>从区间上下限生成卡片底色，语义与原厂 RangeInfo.ColorChange 一致。</summary>
public static class ColorRangePalette
{
    public static SolidColorBrush BrushFor(ColorRangeValue value)
    {
        var c1 = value.ColorSpace is ColorRangeSpace.Hsv or ColorRangeSpace.Hsi
            ? HueMidpoint(value.C1Low, value.C1High)
            : (value.C1Low + value.C1High) / 2.0;
        var c2 = (value.C2Low + value.C2High) / 2.0;
        var c3 = (value.C3Low + value.C3High) / 2.0;
        return value.ColorSpace switch
        {
            ColorRangeSpace.Rgb => new SolidColorBrush(Color.FromRgb((byte)c1, (byte)c2, (byte)c3)),
            ColorRangeSpace.Hsv => Hsv(c1, c2, c3),
            _ => Hsi(c1, c2, c3)
        };
    }

    private static double HueMidpoint(int low, int high)
    {
        if (high < low) high += 255;
        return (low + high) / 2.0;
    }

    private static SolidColorBrush Hsv(double h, double s, double v)
    {
        if (h > 255) h -= 255;
        var hue = h / 255.0 * 360.0;
        var saturation = s / 255.0;
        var value = v / 255.0;
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - chroma;
        var (r, g, b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return new SolidColorBrush(Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255)));
    }

    private static SolidColorBrush Hsi(double h, double s, double i)
    {
        if (h > 255) h -= 255;
        var hue = h / 255.0;
        var saturation = s / 255.0;
        var intensity = i / 255.0;
        var angle = hue * 2 * Math.PI;
        double red;
        double green;
        double blue;
        if (angle >= 0 && angle < 2 * Math.PI / 3)
        {
            blue = intensity * (1 - saturation);
            red = intensity * (1 + saturation * Math.Cos(angle) / Math.Cos(Math.PI / 3 - angle));
            green = 3 * intensity - (red + blue);
        }
        else if (angle < 4 * Math.PI / 3)
        {
            red = intensity * (1 - saturation);
            green = intensity * (1 + saturation * Math.Cos(angle - 2 * Math.PI / 3) / Math.Cos(Math.PI - angle));
            blue = 3 * intensity - (red + green);
        }
        else
        {
            green = intensity * (1 - saturation);
            blue = intensity * (1 + saturation * Math.Cos(angle - 4 * Math.PI / 3) / Math.Cos(5 * Math.PI / 3 - angle));
            red = 3 * intensity - (green + blue);
        }
        return new SolidColorBrush(Color.FromRgb(ToByte(red * 255 + 0.5), ToByte(green * 255 + 0.5), ToByte(blue * 255 + 0.5)));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(value, 0, 255);
}
