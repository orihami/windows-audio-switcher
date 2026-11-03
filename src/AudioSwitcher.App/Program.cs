using System.Windows;
using AudioSwitcher.App.Interop;
using AudioSwitcher.App.Services;
using AudioSwitcher.App.Views;
using AudioSwitcher.Core;
using WinForms = System.Windows.Forms;

namespace AudioSwitcher.App;

/// <summary>
/// アプリ本体。タスクトレイに常駐し、ショートカット+ホイールで出力先を選んで、キーを離したら切り替える。
/// 起動の引数: --background(画面を出さずに常駐) / --settings(設定を開く) / --render-preview 出力フォルダ / --self-test
/// </summary>
public sealed class Program : IAppHost
{
    const string MutexName = "WindowsAudioSwitcher-4f1c2b";
    const string ShowEventName = "WindowsAudioSwitcher-4f1c2b-show";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--render-preview")) return Preview.Render(args.SkipWhile(a => a != "--render-preview").Skip(1).FirstOrDefault() ?? "preview");
        if (args.Contains("--self-test")) return SelfTest();

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // すでに動いている → そちらに設定を開いてもらう
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return 0;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var me = new Program(app);
        app.Startup += (_, _) => me.Start(!args.Contains("--background"));
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Write("想定外のエラー: " + e.Exception);
            e.Handled = true;
        };
        return app.Run();
    }

    readonly Application _app;
    readonly SettingsStore _store;
    AudioService _audio = null!;
    InputHook _hook = null!;
    OverlayWindow _overlay = null!;
    WinForms.NotifyIcon? _tray;
    SettingsWindow? _settingsWindow;
    SelectionSession? _session;
    List<SystemDevice> _system = new();
    string? _currentId;
    System.Windows.Threading.DispatcherTimer? _saveTimer;
    EventWaitHandle? _showEvent;

    public AppSettings Settings { get; private set; }
    public IReadOnlyList<SystemDevice> SystemDevices => _system;
    public string? CurrentId => _currentId;
    public string SettingsPath => _store.Path;
    public event Action? DevicesChanged;

    Program(Application app)
    {
        _app = app;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioSwitcher");
        _store = new SettingsStore(Path.Combine(dir, "settings.json"));
        Settings = _store.Load();
    }

    void Start(bool openSettings)
    {
        var ui = _app.Dispatcher;
        _audio = new AudioService(ui);
        _audio.Changed += Refresh;
        Refresh();
        bool firstRun = !Settings.FirstScanDone;
        DeviceRegistry.FirstScan(Settings, _system);
        DeviceRegistry.Sync(Settings.Devices, _system);
        Save();

        _overlay = new OverlayWindow();
        _hook = new InputHook(Settings.Hotkey, ui) { InvertWheel = Settings.InvertWheel, Enabled = !Settings.Paused };
        _hook.Activated += OnActivated;
        _hook.Wheel += OnWheel;
        _hook.Released += OnReleased;

        ApplyTray();
        // 自動起動のオン/オフはレジストリの方を設定に合わせる
        if (Settings.StartWithWindows != Autostart.IsOn()) Autostart.Set(Settings.StartWithWindows);

        // 2つ目に起動されたら設定を開く
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() =>
        {
            while (_showEvent.WaitOne()) ui.BeginInvoke(() => OpenSettings());
        }) { IsBackground = true }.Start();

        Log.Write($"起動 {typeof(Program).Assembly.GetName().Version} デバイス {_system.Count}・登録 {Settings.Devices.Count}");
        if (openSettings || firstRun) OpenSettings(firstRun ? "devices" : null);
    }

    void Refresh()
    {
        try
        {
            _system = _audio.GetDevices();
            _currentId = _audio.GetDefaultId();
        }
        catch (Exception e) { Log.Write("デバイスの一覧を取れない: " + e.Message); }
        DeviceRegistry.Sync(Settings.Devices, _system);
        UpdateTrayMenu();
        DevicesChanged?.Invoke();
    }

    public FormFactor KindOf(string id) => _audio?.KindOf(id) ?? FormFactor.Unknown;

    // ---- ショートカット + ホイール ----

    System.Windows.Threading.DispatcherTimer? _showTimer, _holdTimer;

    void OnActivated()
    {
        if (Settings.Paused) return;
        Refresh();
        _session = new SelectionSession(Settings.Devices, _currentId, Settings.Wrap);
        // ほかのショートカット(Ctrl+Alt+○○)で一瞬ちらつかないよう、少し待ってから出す。ホイールを回したらすぐ出す
        _showTimer?.Stop();
        _showTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _showTimer.Tick += (_, _) =>
        {
            _showTimer?.Stop();
            if (_session != null) _overlay.Render(_session, Settings, KindOf, Settings.Hotkey.ToString());
        };
        _showTimer.Start();
        // 押しっぱなしの確認(離したのが届かなかったときのため)
        _holdTimer ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(250), System.Windows.Threading.DispatcherPriority.Normal, (_, _) => _hook.CheckStillHeld(), _app.Dispatcher);
        _holdTimer.Start();
    }

    void OnWheel(int steps)
    {
        if (_session == null) return;
        _showTimer?.Stop();
        _session.Step(steps);
        _overlay.Render(_session, Settings, KindOf, Settings.Hotkey.ToString());
    }

    void OnReleased(bool cancelled)
    {
        _showTimer?.Stop();
        _holdTimer?.Stop();
        var s = _session;
        _session = null;
        if (s == null) return;
        var target = cancelled ? null : s.Commit();
        if (target == null)
        {
            _overlay.HideNow();
            return;
        }
        bool ok = _audio.SetDefault(target.Id, Settings.Roles);
        if (ok) _currentId = target.Id;
        Log.Write($"切り替え {(ok ? "OK" : "失敗")}: {target.Label}");
        if (Settings.ShowResult || !ok) _overlay.ShowResult(target, KindOf(target.Id), ok, Settings);
        else _overlay.HideNow();
        if (Settings.TrayNotify && _tray != null && ok) _tray.ShowBalloonTip(1500, "出力先を切り替えました", target.Label, WinForms.ToolTipIcon.None);
        UpdateTrayMenu();
    }

    // ---- 設定画面から ----

    public void SettingsChanged()
    {
        _hook.Hotkey = Settings.Hotkey;
        _hook.InvertWheel = Settings.InvertWheel;
        _hook.Enabled = !Settings.Paused;
        if (Settings.StartWithWindows != Autostart.IsOn()) Autostart.Set(Settings.StartWithWindows);
        ApplyTray();
        // 少しまとめてから保存
        _saveTimer ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(400), System.Windows.Threading.DispatcherPriority.Background, (_, _) => { _saveTimer!.Stop(); Save(); }, _app.Dispatcher);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow() => Save();

    void Save()
    {
        try { _store.Save(Settings); }
        catch (Exception e) { Log.Write("保存できない: " + e.Message); }
    }

    public void RecordHotkey(Action<Hotkey> done) => _hook.Record(done);
    public void CancelRecord() => _hook.CancelRecord();

    public void PreviewOverlay()
    {
        Refresh();
        var s = new SelectionSession(Settings.Devices, _currentId, Settings.Wrap);
        s.Step(1);
        _overlay.Render(s, Settings, KindOf, Settings.Hotkey.ToString());
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        t.Tick += (_, _) => { t.Stop(); if (_session == null) _overlay.HideNow(); };
        t.Start();
    }

    public void SwitchTo(string id)
    {
        if (_audio.SetDefault(id, Settings.Roles)) _currentId = id;
        Refresh();
    }

    public void Exit()
    {
        Save();
        _hook?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _audio?.Dispose();
        _app.Shutdown();
    }

    void OpenSettings(string? page = null)
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(this, page);
            _settingsWindow.Closed += (_, _) => { _settingsWindow = null; Save(); };
            _settingsWindow.Show();
        }
        else if (page != null) _settingsWindow.ShowPage(page);
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    // ---- タスクトレイ ----

    void ApplyTray()
    {
        if (!Settings.ShowTrayIcon)
        {
            if (_tray != null) _tray.Visible = false;
            return;
        }
        if (_tray == null)
        {
            _tray = new WinForms.NotifyIcon { Text = "Windows Audio Switcher", ContextMenuStrip = new WinForms.ContextMenuStrip() };
            try
            {
                using var s = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;
                _tray.Icon = new System.Drawing.Icon(s, new System.Drawing.Size(16, 16));
            }
            catch { _tray.Icon = System.Drawing.SystemIcons.Application; }
            _tray.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) OpenSettings(); };
            UpdateTrayMenu();
        }
        _tray.Visible = true;
    }

    void UpdateTrayMenu()
    {
        if (_tray?.ContextMenuStrip is not { } m) return;
        m.Items.Clear();
        m.Items.Add(new WinForms.ToolStripLabel("出力先") { ForeColor = System.Drawing.Color.Gray });
        foreach (var d in Settings.Devices.Where(d => d.Enabled))
        {
            var id = d.Id;
            var item = new WinForms.ToolStripMenuItem(d.Label + (d.Connected ? "" : "(未接続)")) { Checked = id == _currentId, Enabled = d.Connected };
            item.Click += (_, _) => SwitchTo(id);
            m.Items.Add(item);
        }
        m.Items.Add(new WinForms.ToolStripSeparator());
        var pause = new WinForms.ToolStripMenuItem("一時停止") { Checked = Settings.Paused };
        pause.Click += (_, _) => { Settings.Paused = !Settings.Paused; SettingsChanged(); UpdateTrayMenu(); };
        m.Items.Add(pause);
        m.Items.Add("設定を開く…", null, (_, _) => OpenSettings());
        m.Items.Add("終了", null, (_, _) => Exit());
        var cur = Settings.Devices.FirstOrDefault(d => d.Id == _currentId);
        _tray.Text = Trim("Audio Switcher: " + (cur?.Label ?? "未登録の出力先"), 63);
    }

    static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    // ---- 確認用 ----

    /// <summary>Windows のデバイスを読めるかだけ確かめる(CI 用)。結果は self-test.txt(UTF-8)にも書く。</summary>
    static int SelfTest()
    {
        var lines = new List<string>();
        int code = 0;
        try
        {
            var app = new Application();
            var audio = new AudioService(app.Dispatcher);
            var list = audio.GetDevices();
            lines.Add($"self-test: 出力デバイス {list.Count} 個、既定 {(audio.GetDefaultId() ?? "なし")}");
            foreach (var d in list) lines.Add($"  {(d.Connected ? "●" : "○")} {d.Name} [{audio.KindOf(d.Id)}]");
            audio.Dispose();
        }
        catch (Exception e)
        {
            lines.Add("self-test 失敗: " + e);
            code = 1;
        }
        File.WriteAllLines("self-test.txt", lines, System.Text.Encoding.UTF8);
        foreach (var l in lines) Console.WriteLine(l);
        return code;
    }
}
