using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AudioSwitcher.App.Interop;
using AudioSwitcher.App.Views;
using AudioSwitcher.Core;

namespace AudioSwitcher.App;

/// <summary>
/// 見た目の確認用: 選択UIと設定画面を、本物のデバイスなしで画像(PNG)にする。
/// AudioSwitcher.exe --render-preview フォルダ  (GitHub Actions の Windows で撮って、成果物として残す)
/// </summary>
public static class Preview
{
    sealed class FakeHost : IAppHost
    {
        public AppSettings Settings { get; } = new();
        public IReadOnlyList<SystemDevice> SystemDevices { get; }
        public string? CurrentId => "mon";
        readonly Dictionary<string, FormFactor> _kinds = new()
        {
            ["mon"] = FormFactor.DigitalAudioDisplayDevice, ["a"] = FormFactor.Headphones, ["b"] = FormFactor.Headphones,
            ["c"] = FormFactor.Headset, ["wired"] = FormFactor.Headphones, ["spk"] = FormFactor.Speakers,
        };

        public FakeHost(ThemeMode theme)
        {
            Settings.Theme = theme;
            SystemDevices = new[]
            {
                new SystemDevice("mon", "DELL U2720Q (NVIDIA High Definition Audio)", true),
                new SystemDevice("a", "WF-1000XM5", true),
                new SystemDevice("b", "AirPods Pro", false),
                new SystemDevice("c", "Galaxy Buds2 Pro", true),
                new SystemDevice("wired", "ヘッドホン (Realtek(R) Audio)", true),
                new SystemDevice("spk", "スピーカー (Realtek(R) Audio)", true),
            };
            foreach (var d in SystemDevices.Take(5)) DeviceRegistry.Add(Settings.Devices, d);
            Settings.Devices[0].DisplayName = "モニター";
            Settings.Devices[1].DisplayName = "ワイヤレスイヤホンA";
            Settings.Devices[2].DisplayName = "ワイヤレスイヤホンB";
            Settings.Devices[3].DisplayName = "ワイヤレスイヤホンC";
            Settings.Devices[4].DisplayName = "有線イヤホン";
            Settings.FirstScanDone = true;
            DeviceRegistry.Sync(Settings.Devices, SystemDevices);
        }

        public FormFactor KindOf(string id) => _kinds.TryGetValue(id, out var k) ? k : FormFactor.Unknown;
        public void SettingsChanged() { }
        public void SaveNow() { }
        public void RecordHotkey(Action<Hotkey> done) { }
        public void CancelRecord() { }
        public void PreviewOverlay() { }
        public void SwitchTo(string id) { }
        public void Exit() { }
        public string SettingsPath => "settings.json";
#pragma warning disable CS0067
        public event Action? DevicesChanged;
#pragma warning restore CS0067
    }

    public static int Render(string dir)
    {
        Directory.CreateDirectory(dir);
        var app = new Application();
        foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
        {
            var tag = theme == ThemeMode.Dark ? "dark" : "light";
            var host = new FakeHost(theme);

            // 選択UI: 開いたところ / ホイールで2つ下へ(B は未接続なので飛ばす) / 切り替えたあと
            var ov = new OverlayWindow { Offscreen = true };
            var s = new SelectionSession(host.Settings.Devices, host.CurrentId, true);
            ov.Render(s, host.Settings, host.KindOf, host.Settings.Hotkey.ToString());
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"overlay-1-open-{tag}.png"), Background(theme));
            s.Step(2);
            ov.Render(s, host.Settings, host.KindOf, host.Settings.Hotkey.ToString());
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"overlay-2-select-{tag}.png"), Background(theme));
            ov.ShowResult(s.Selected!, host.KindOf(s.Selected!.Id), true, host.Settings);
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"overlay-3-result-{tag}.png"), Background(theme));

            // 紹介動画用(背景は透明。動画のデスクトップに重ねる): ホイールを1段ずつ回したときの場面(A → C(B は未接続なので飛ばす) → 有線 → 循環してモニター)
            var v = new SelectionSession(host.Settings.Devices, host.CurrentId, true);
            ov.Render(v, host.Settings, host.KindOf, host.Settings.Hotkey.ToString());
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"video-0-{tag}.png"), Colors.Transparent);
            for (int k = 1; k <= 4; k++)
            {
                v.Step(1);
                ov.Render(v, host.Settings, host.KindOf, host.Settings.Hotkey.ToString());
                Save(ov.Content as FrameworkElement, Path.Combine(dir, $"video-{k}-{tag}.png"), Colors.Transparent);
            }
            v.Step(-2); // 有線 → C へもどる
            ov.Render(v, host.Settings, host.KindOf, host.Settings.Hotkey.ToString());
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"video-5-{tag}.png"), Colors.Transparent);
            ov.ShowResult(v.Selected!, host.KindOf(v.Selected!.Id), true, host.Settings);
            Save(ov.Content as FrameworkElement, Path.Combine(dir, $"video-6-{tag}.png"), Colors.Transparent);

            var win = new SettingsWindow(host);
            foreach (var page in new[] { "keys", "devices", "display", "app" })
            {
                win.ShowPage(page);
                Save(win.Content as FrameworkElement, Path.Combine(dir, $"settings-{page}-{tag}.png"), ((SolidColorBrush)Palette.Get(theme).Window).Color, 880, page == "devices" ? 1060 : 760);
            }
        }
        Console.WriteLine("preview: " + string.Join(", ", Directory.GetFiles(dir, "*.png").Select(Path.GetFileName)));
        return 0;
    }

    static Color Background(ThemeMode t) => t == ThemeMode.Dark ? Color.FromRgb(0x1c, 0x2a, 0x3a) : Color.FromRgb(0xa8, 0xc4, 0xe0);

    static void Save(FrameworkElement? el, string path, Color bg, double w = double.NaN, double h = double.NaN)
    {
        if (el == null) return;
        // ウィンドウから外して、決まった大きさで描く
        var owner = el.Parent as System.Windows.Controls.ContentControl;
        if (owner != null) owner.Content = null;
        var size = new Size(double.IsNaN(w) ? double.PositiveInfinity : w, double.IsNaN(h) ? double.PositiveInfinity : h);
        el.Measure(size);
        var ds = el.DesiredSize;
        var rw = double.IsNaN(w) ? ds.Width : w;
        var rh = double.IsNaN(h) ? ds.Height : h;
        el.Arrange(new System.Windows.Rect(0, 0, rw, rh));
        el.UpdateLayout();
        const double scale = 1.5; // 高い解像度の画面のつもりで
        var bmp = new RenderTargetBitmap((int)(rw * scale), (int)(rh * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(bg), null, new System.Windows.Rect(0, 0, rw, rh));
        bmp.Render(dv);
        bmp.Render(el);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (var f = File.Create(path)) enc.Save(f);
        // ウィンドウにもどす(次の画像でも使う)
        if (owner != null) owner.Content = el;
    }
}
