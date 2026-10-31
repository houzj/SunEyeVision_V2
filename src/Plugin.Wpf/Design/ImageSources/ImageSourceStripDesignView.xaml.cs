using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources;

namespace SunEyeVision.Plugin.Wpf.Design.ImageSources;

/// <summary>Standalone designer surface composes the exact production control, with visual sample projections only.</summary>
public partial class ImageSourceStripDesignView : UserControl
{
    public ImageSourceStripDesignView()
    {
        InitializeComponent();
        EditableStrip.Session = CreateSampleSession(false, false, intent => LastIntent.Text = intent);
        ReadOnlyStrip.Session = CreateSampleSession(true, false, intent => LastIntent.Text = intent);
        EmptyStrip.Session = CreateSampleSession(false, true, intent => LastIntent.Text = intent);
    }
    internal static IImageSourceStripSession CreateSampleSession(bool readOnly=false,bool empty=false,Action<string>? report=null)
        =>new SampleSession(readOnly,empty,report??(_=>{}));
    private sealed class SampleSession : IImageSourceStripSession
    {
        private readonly Action<string> _report;
        public SampleSession(bool readOnly, bool empty, Action<string> report)
        {
            IsReadOnly = readOnly; IsSelectMode = readOnly; _report = report;
            var pixels = new byte[72 * 52];
            for (int y = 0; y < 52; y++) for (int x = 0; x < 72; x++) pixels[y * 72 + x] = (byte)((x / 8 + y / 8) % 2 == 0 ? 90 : 170);
            var thumbnail = BitmapSource.Create(72, 52, 96, 96, PixelFormats.Gray8, null, pixels, 72); thumbnail.Freeze();
            Items = empty ? Array.Empty<ImageSourceStripItem>() : Array.AsReadOnly(new[]
            {
                new ImageSourceStripItem("sample-a", "示例图像 A", thumbnail, true),
                new ImageSourceStripItem("sample-b", "示例图像 B", thumbnail, false),
                new ImageSourceStripItem("sample-missing", "缺失资源（示例）", null, true, "文件不存在")
            });
            DisplayIndex = ConfiguredStartIndex = empty ? -1 : 0;
        }
        public IReadOnlyList<ImageSourceStripItem> Items { get; private set; }
        public long Version { get; private set; }
        public int DisplayIndex { get; private set; }
        public int ConfiguredStartIndex { get; private set; }
        public bool IsReadOnly { get; }
        public bool IsSelectMode { get; private set; }
        public bool AutoSwitchEnabled { get; private set; }
        public bool IsImporting => false;
        public string? Status => "独立设计示例；不读取方案、文件或设备，不执行工具";
        public event PropertyChangedEventHandler? PropertyChanged;
        public Task RequestAsync(ImageSourceStripRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReadOnly || request.ExpectedVersion != Version) return Task.CompletedTask;
            _report($"示例意图：{request.Kind} / {request.ItemId}");
            switch (request.Kind)
            {
                case ImageSourceStripRequestKind.Activate:
                    DisplayIndex = ConfiguredStartIndex = Items.ToList().FindIndex(x => x.Id == request.ItemId); break;
                case ImageSourceStripRequestKind.SelectStart:
                    ConfiguredStartIndex = Items.ToList().FindIndex(x => x.Id == request.ItemId); break;
                case ImageSourceStripRequestKind.SetMode: IsSelectMode = request.Flag == true; break;
                case ImageSourceStripRequestKind.SetAutoSwitch: AutoSwitchEnabled = request.Flag == true; break;
                case ImageSourceStripRequestKind.SetRunSelection:
                    Items = Array.AsReadOnly(Items.Select(x => x with { IsForRun = request.ItemIds!.Contains(x.Id) }).ToArray()); break;
                case ImageSourceStripRequestKind.Remove:
                    Items = Array.AsReadOnly(Items.Where(x => x.Id != request.ItemId).ToArray());
                    DisplayIndex = ConfiguredStartIndex = Items.Count == 0 ? -1 : 0; break;
                case ImageSourceStripRequestKind.Clear: Items = Array.Empty<ImageSourceStripItem>(); DisplayIndex = ConfiguredStartIndex = -1; break;
            }
            Version++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            return Task.CompletedTask;
        }
    }
}
