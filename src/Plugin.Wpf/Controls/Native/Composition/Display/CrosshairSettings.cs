using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Mvvm.Commands;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;

/// <summary>Original CrossData state and commands, with an owner gate for retired views.</summary>
public sealed class CrosshairSettings : ICross, IIsVisible, INotifyPropertyChanged
{
    private readonly Func<bool> _canEdit;
    private Point _center;
    private string _stroke = "#FFF76900";
    private bool _visible, _locked;
    private double _width = 1, _opacity = 1;
    internal CrosshairSettings(int index, Func<bool> canEdit)
    {
        Index = index; _canEdit = canEdit;
        CenterCommand = new DelegateCommand(() => CenterPoint = new(ImageWidth / 2, ImageHeight / 2), CanQuery);
        HideCommand = new DelegateCommand(() => IsVisible = false, CanQuery);
        LockCommand = new DelegateCommand(() => IsLocked = true, CanQuery);
        UnLockCommand = new DelegateCommand(() => IsLocked = false, CanQuery);
    }
    public int Index { get; }
    public int ImageWidth { get; private set; }
    public int ImageHeight { get; private set; }
    public bool NeedsInitialization { get; private set; } = true;
    public string Name => AppLocalizationService.GetLang("十字辅助线") + " " + Index;
    public string Fill => null!;
    public string ToolTip => null!;
    public Point CenterPoint
    {
        get => _center;
        set
        {
            if (!_canEdit()) return;
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value));
            var next = new Point(Math.Clamp(value.X, 0, 100000), Math.Clamp(value.Y, 0, 100000));
            if (_center == next) return;
            _center = next; Notify(nameof(CenterPoint), nameof(CenterX), nameof(CenterY));
        }
    }
    public double CenterX { get => _center.X; set => CenterPoint = new(value, _center.Y); }
    public double CenterY { get => _center.Y; set => CenterPoint = new(_center.X, value); }
    public string Stroke
    {
        get => _stroke;
        set
        {
            if (!_canEdit()) return;
            if (string.IsNullOrWhiteSpace(value) || ColorConverter.ConvertFromString(value) is not Color)
                throw new ArgumentException("十字线颜色无效。", nameof(value));
            if (_stroke == value) return; _stroke = value; Notify(nameof(Stroke));
        }
    }
    public double StrokeThickness
    {
        get => _width;
        set
        {
            if (!_canEdit()) return;
            if (!double.IsFinite(value) || value is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(value));
            if (_width == value) return; _width = value; Notify(nameof(StrokeThickness));
        }
    }
    public double Opacity
    {
        get => _opacity;
        set
        {
            if (!_canEdit()) return;
            if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (_opacity == value) return; _opacity = value; Notify(nameof(Opacity));
        }
    }
    public bool IsVisible { get => _visible; set { if (!_canEdit() || _visible == value) return; _visible = value; Notify(nameof(IsVisible), nameof(CenterPoint), nameof(StrokeThickness)); } }
    public bool IsLocked { get => _locked; set { if (!_canEdit() || _locked == value) return; _locked = value; Notify(nameof(IsLocked)); } }
    public ICommand CenterCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand UnLockCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool CanQuery()
    {
        // The owner gate records and retires a failing source. CommandManager's
        // availability query must not throw through the desktop message pump.
        try { return _canEdit(); } catch { return false; }
    }
    internal void SetDimensions(int width, int height, bool initialize)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ImageWidth = width; ImageHeight = height;
        if (!initialize || !NeedsInitialization) return;
        // Accept initialization before notifying: an observer failure must not reset
        // a user's coordinates on the next image.
        var offset = Index == 1 ? -.1 : .1;
        _center = new(Math.Clamp(width * (.5 + offset), 0, 100000), Math.Clamp(height * (.5 + offset), 0, 100000));
        _stroke = Index == 1 ? "#FFF76900" : "#FFFFFF00"; NeedsInitialization = false;
        Notify(nameof(CenterPoint), nameof(CenterX), nameof(CenterY), nameof(Stroke));
    }
    private void Notify(params string[] names)
    {
        List<Exception>? errors = null;
        foreach (var name in names)
            foreach (var handler in PropertyChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((PropertyChangedEventHandler)handler)(this, new(name)); }
                catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors is not null) throw new AggregateException("十字线状态已接纳，观察者通知失败。", errors);
    }
}
