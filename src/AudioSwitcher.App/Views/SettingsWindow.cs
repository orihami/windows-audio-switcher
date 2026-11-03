using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;

namespace AudioSwitcher.App.Views;

/// <summary>設定画面。左で項目を選び、右に中身。変えたらすぐ保存・反映する。</summary>
public sealed class SettingsWindow : Window
{
    readonly IAppHost _host;
    Palette _p;
    readonly ListBox _nav = new();
    readonly ContentControl _page = new();
    readonly TextBlock _status = new();
    string _current = "keys";
    bool _recording;

    static readonly (string Id, string Glyph, string Name)[] Pages =
    {
        ("keys", Glyphs.Keyboard, "操作"),
        ("devices", Glyphs.Devices, "デバイスと順番"),
        ("display", Glyphs.Display, "表示"),
        ("app", Glyphs.Settings, "アプリ"),
    };

    AppSettings S => _host.Settings;

    public SettingsWindow(IAppHost host, string? page = null)
    {
        _host = host;
        _p = Palette.Get(host.Settings.Theme);
        Title = "Windows Audio Switcher の設定";
        Width = 880;
        Height = 640;
        MinWidth = 640;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Palette.Font;
        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/app.ico")); } catch { }
        if (page != null) _current = page;
        Build();
        host.DevicesChanged += OnDevicesChanged;
        Closed += (_, _) =>
        {
            host.DevicesChanged -= OnDevicesChanged;
            if (_recording) host.CancelRecord();
        };
    }

    void OnDevicesChanged()
    {
        // 表示名を打っている途中は作り直さない(次に開いたときに反映)
        if (_current == "devices" && Keyboard.FocusedElement is not TextBox) ShowPage("devices");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // タイトルバーもダーク/ライトに合わせる(Windows 10 20H1 以降)
        int dark = _p.Dark ? 1 : 0;
        Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
    }

    void Build()
    {
        _p = Palette.Get(S.Theme);
        Background = _p.Window;
        Foreground = _p.Text;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 左: アプリ名と項目
        var left = new DockPanel { Margin = new Thickness(12, 28, 8, 12) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 16) };
        var img = new System.Windows.Controls.Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0) };
        try { img.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/app.ico")); } catch { }
        brand.Children.Add(img);
        var bt = new StackPanel();
        bt.Children.Add(Ui.Text("Audio Switcher", 15, _p.Text, FontWeights.SemiBold));
        bt.Children.Add(Ui.Text("出力先をキーとホイールで", 11, _p.SubText));
        brand.Children.Add(bt);
        DockPanel.SetDock(brand, Dock.Top);
        left.Children.Add(brand);
        _status.FontSize = 11.5;
        _status.Foreground = _p.SubText;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(10, 8, 0, 0);
        DockPanel.SetDock(_status, Dock.Bottom);
        left.Children.Add(_status);
        _nav.Background = Brushes.Transparent;
        _nav.BorderThickness = new Thickness(0);
        _nav.Items.Clear();
        foreach (var (id, glyph, name) in Pages)
        {
            var it = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 6, 4, 6) };
            it.Children.Add(new TextBlock { Text = glyph, FontFamily = Palette.Icons, FontSize = 16, Width = 28, Foreground = _p.Text, VerticalAlignment = VerticalAlignment.Center });
            it.Children.Add(new TextBlock { Text = name, FontSize = 14, Foreground = _p.Text, VerticalAlignment = VerticalAlignment.Center });
            _nav.Items.Add(new ListBoxItem { Content = it, Tag = id, Padding = new Thickness(6, 2, 6, 2) });
        }
        _nav.ItemContainerStyle = NavItemStyle();
        _nav.SelectionChanged -= NavChanged;
        _nav.SelectionChanged += NavChanged;
        left.Children.Add(_nav);
        root.Children.Add(left);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _page, Padding = new Thickness(8, 20, 24, 24) };
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
        _nav.SelectedIndex = Math.Max(0, Array.FindIndex(Pages, x => x.Id == _current));
        ShowPage(_current);
        UpdateStatus();
    }

    Style NavItemStyle()
    {
        var st = new Style(typeof(ListBoxItem));
        var t = new ControlTemplate(typeof(ListBoxItem));
        var g = new FrameworkElementFactory(typeof(Grid));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "bd";
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bd.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 2, 8, 2));
        bd.AppendChild(cp);
        var mark = new FrameworkElementFactory(typeof(Border));
        mark.Name = "mark";
        mark.SetValue(FrameworkElement.WidthProperty, 3.0);
        mark.SetValue(FrameworkElement.HeightProperty, 16.0);
        mark.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        mark.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        mark.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        g.AppendChild(bd);
        g.AppendChild(mark);
        t.VisualTree = g;
        var sel = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        sel.Setters.Add(new Setter(Border.BackgroundProperty, _p.CardHover, "bd"));
        sel.Setters.Add(new Setter(Border.BackgroundProperty, _p.AccentBrush, "mark"));
        t.Triggers.Add(sel);
        var hov = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hov.Setters.Add(new Setter(Border.BackgroundProperty, _p.Card, "bd"));
        t.Triggers.Add(hov);
        st.Setters.Add(new Setter(Control.TemplateProperty, t));
        st.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return st;
    }

    void NavChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_nav.SelectedItem is ListBoxItem it && it.Tag is string id && id != _current) ShowPage(id);
    }

    /// <summary>テスト・プレビュー用: 指定の項目を開く。</summary>
    public void ShowPage(string id)
    {
        _current = id;
        var panel = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Stretch };
        var title = Pages.First(x => x.Id == id).Name;
        panel.Children.Add(Ui.Text(title, 26, _p.Text, FontWeights.SemiBold, new Thickness(2, 8, 0, 8)));
        switch (id)
        {
            case "keys": KeysPage(panel); break;
            case "devices": DevicesPage(panel); break;
            case "display": DisplayPage(panel); break;
            case "app": AppPage(panel); break;
        }
        _page.Content = panel;
    }

    void Changed(string? note = null)
    {
        _host.SettingsChanged();
        UpdateStatus(note ?? "保存しました");
    }

    void UpdateStatus(string? note = null)
    {
        _status.Text = (S.Paused ? "⏸ 一時停止中\n" : "") + $"ショートカット: {S.Hotkey}" + (note != null ? $"\n{note}({DateTime.Now:HH:mm:ss})" : "");
    }

    // ---- 操作 ----
    void KeysPage(StackPanel panel)
    {
        panel.Children.Add(Ui.Text("ショートカットキーを押したままマウスホイールを回して出力先を選び、キーを離すと切り替わります。", 13, _p.SubText, margin: new Thickness(2, 0, 0, 8)));
        panel.Children.Add(Ui.Header(_p, "ショートカット"));
        var keyText = new Border
        {
            CornerRadius = new CornerRadius(4), BorderBrush = _p.Border, BorderThickness = new Thickness(1, 1, 1, 2), Padding = new Thickness(10, 4, 10, 4),
            Background = _p.Window, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Text(_recording ? "キーを押してください…" : S.Hotkey.ToString(), 14, _recording ? _p.AccentText : _p.Text, FontWeights.SemiBold),
        };
        var warn = Ui.Text(HotkeyWarning(S.Hotkey), 12, Brushes.OrangeRed, margin: new Thickness(0, 4, 0, 0));
        var box = new StackPanel { Orientation = Orientation.Horizontal };
        box.Children.Add(keyText);
        box.Children.Add(Ui.Button(_p, _recording ? "やめる" : "変更する", () =>
        {
            if (_recording)
            {
                _recording = false;
                _host.CancelRecord();
                ShowPage("keys");
                return;
            }
            _recording = true;
            ShowPage("keys");
            _host.RecordHotkey(h =>
            {
                _recording = false;
                // Esc だけを押したら取りやめ
                if (!(h.Key == 0x1B && !h.Ctrl && !h.Alt && !h.Shift && !h.Win))
                {
                    S.Hotkey = h;
                    Changed($"ショートカットを {h} にしました");
                }
                ShowPage("keys");
            });
        }, Glyphs.Keyboard, accent: !_recording));
        var right = new StackPanel();
        right.Children.Add(box);
        var row = Ui.Row(_p, Glyphs.Keyboard, "ショートカットキー", _recording ? "押したいキーを同時に押して、離すと決まります(Esc で取りやめ)" : "Ctrl・Alt・Shift・Win と、ほかのキー1つまで組み合わせられます", right);
        panel.Children.Add(row);
        if (warn.Text != "") panel.Children.Add(warn);
        panel.Children.Add(Ui.Row(_p, Glyphs.Check, "確定のしかた", "キーを離したときに切り替えます。ホイールを回している間は、実際の出力は変わりません", Ui.Text("キーを離したとき", 13, _p.SubText)));

        panel.Children.Add(Ui.Header(_p, "ホイール"));
        panel.Children.Add(Ui.Row(_p, Glyphs.Mouse, "ホイールの向き", "どちらに回すとリストの次(下)へ進むか",
            Ui.Choice(_p, new[] { "下に回すと次へ", "上に回すと次へ" }, S.InvertWheel ? 1 : 0, i => { S.InvertWheel = i == 1; Changed(); ShowPage("keys"); })));
        panel.Children.Add(Ui.Row(_p, Glyphs.Order, "循環する", "最後の次は最初へ、最初の前は最後へもどる", Ui.Toggle(_p, S.Wrap, v => { S.Wrap = v; Changed(); })));

        panel.Children.Add(Ui.Header(_p, "切り替え"));
        panel.Children.Add(Ui.Row(_p, Glyphs.Volume, "切り替える用途", "Windows の「既定のデバイス」と「既定の通信デバイス」",
            Ui.Choice(_p, new[] { "すべて(音楽・動画・通話)", "通話用はそのまま" }, S.Roles == SwitchRoles.All ? 0 : 1, i => { S.Roles = i == 0 ? SwitchRoles.All : SwitchRoles.MediaOnly; Changed(); ShowPage("keys"); })));
        panel.Children.Add(Ui.Row(_p, Glyphs.Power, "一時停止", "オンのあいだはショートカットに反応しません(タスクトレイからも切り替えられます)", Ui.Toggle(_p, S.Paused, v => { S.Paused = v; Changed(); })));
    }

    /// <summary>文字の入力とぶつかりそうなショートカットへの注意。</summary>
    public static string HotkeyWarning(Hotkey h)
    {
        if (h.IsEmpty) return "ショートカットがありません。";
        bool mods = h.Ctrl || h.Alt || h.Win;
        if (!mods && h.Key != 0) return "Ctrl・Alt・Win のどれかを入れないと、ふだんの入力でも反応してしまいます。";
        if (!mods && h.Shift) return "Shift だけだと、文字を打つたびに反応します。Ctrl・Alt・Win と組み合わせるのがおすすめです。";
        return "";
    }

    // ---- デバイスと順番 ----
    void DevicesPage(StackPanel panel)
    {
        panel.Children.Add(Ui.Text("上から順に、ホイールで切り替わります。つながっていないデバイスは飛ばします(登録は残ります)。", 13, _p.SubText, margin: new Thickness(2, 0, 0, 8)));
        var list = S.Devices;
        if (list.Count == 0) panel.Children.Add(Ui.Card(_p, Ui.Text("まだ登録がありません。下の「追加」から選んでください。", 13, _p.SubText)));
        for (int i = 0; i < list.Count; i++) panel.Children.Add(DeviceRow(list, i));

        panel.Children.Add(Ui.Header(_p, "追加する(Windows の出力デバイス)"));
        var cand = DeviceRegistry.Unregistered(list, _host.SystemDevices);
        if (cand.Count == 0) panel.Children.Add(Ui.Card(_p, Ui.Text("Windows が知っている出力デバイスは、全部登録ずみです。", 13, _p.SubText)));
        foreach (var c in cand)
        {
            var dev = c;
            var add = Ui.Button(_p, "追加", () =>
            {
                DeviceRegistry.Add(list, dev);
                Changed("追加しました");
                ShowPage("devices");
            }, Glyphs.Add, accent: true);
            panel.Children.Add(Ui.Row(_p, Glyphs.For(_host.KindOf(dev.Id)), dev.Name, dev.Connected ? "● 接続中" : "○ 未接続(登録しておけば、つないだときに選べます)", add));
        }
    }

    UIElement DeviceRow(List<DeviceEntry> list, int i)
    {
        var d = list[i];
        bool current = d.Id == _host.CurrentId;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var no = Ui.Text((i + 1).ToString(), 15, _p.SubText, FontWeights.SemiBold);
        var icon = Ui.Icon(Glyphs.For(_host.KindOf(d.Id)), 20, d.Connected ? _p.Text : _p.SubText);
        var mid = new StackPanel { Margin = new Thickness(4, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBox
        {
            Text = d.DisplayName, FontSize = 14, MinWidth = 200, Padding = new Thickness(6, 3, 6, 3), Background = _p.Window, Foreground = _p.Text, BorderBrush = _p.Border,
            CaretBrush = _p.Text, ToolTip = "表示名(空にすると Windows の名前)",
        };
        name.LostFocus += (_, _) =>
        {
            if (name.Text == d.DisplayName) return;
            d.DisplayName = name.Text.Trim();
            Changed("表示名を変えました");
        };
        name.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };
        var nameRow = new Grid();
        nameRow.Children.Add(name);
        var ph = Ui.Text(d.SystemName, 14, _p.SubText, margin: new Thickness(9, 0, 0, 0));
        ph.IsHitTestVisible = false;
        ph.Visibility = string.IsNullOrEmpty(d.DisplayName) ? Visibility.Visible : Visibility.Collapsed;
        name.TextChanged += (_, _) => ph.Visibility = name.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        nameRow.Children.Add(ph);
        mid.Children.Add(nameRow);
        var state = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 4, 0, 0) };
        state.Children.Add(Ui.Text(d.Connected ? "● 接続中" : "○ 未接続", 12, d.Connected ? _p.Good : _p.SubText));
        if (current) state.Children.Add(Ui.Text("   🔊 使用中", 12, _p.AccentText, FontWeights.SemiBold));
        state.Children.Add(Ui.Text("   Windows の名前: " + d.SystemName, 12, _p.SubText));
        mid.Children.Add(state);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Ui.Text("使う", 12, _p.SubText, margin: new Thickness(0, 0, 4, 0)));
        right.Children.Add(Ui.Toggle(_p, d.Enabled, v => { d.Enabled = v; Changed(v ? "切り替えに入れました" : "切り替えから外しました"); }));
        right.Children.Add(new Border { Width = 8 });
        var up = Ui.Button(_p, "", () => { DeviceRegistry.Move(list, i, i - 1); Changed("順番を変えました"); ShowPage("devices"); }, Glyphs.Up);
        up.IsEnabled = i > 0;
        up.ToolTip = "上へ";
        var down = Ui.Button(_p, "", () => { DeviceRegistry.Move(list, i, i + 1); Changed("順番を変えました"); ShowPage("devices"); }, Glyphs.Down);
        down.IsEnabled = i < list.Count - 1;
        down.ToolTip = "下へ";
        var del = Ui.Button(_p, "", () =>
        {
            if (MessageBox.Show(this, $"「{d.Label}」を登録から外しますか?\n(あとで「追加」からまた登録できます)", "登録の削除", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            DeviceRegistry.Remove(list, d.Id);
            Changed("削除しました");
            ShowPage("devices");
        }, Glyphs.Delete);
        del.ToolTip = "登録から外す";
        var use = Ui.Button(_p, "", () => _host.SwitchTo(d.Id), Glyphs.Volume);
        use.ToolTip = "今すぐこのデバイスに切り替える";
        use.IsEnabled = d.Connected && !current;
        foreach (var b in new[] { up, down, use, del })
        {
            b.Margin = new Thickness(2, 0, 2, 0);
            right.Children.Add(b);
        }
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(mid, 2);
        Grid.SetColumn(right, 3);
        g.Children.Add(no);
        g.Children.Add(icon);
        g.Children.Add(mid);
        g.Children.Add(right);
        var card = Ui.Card(_p, g, new Thickness(12, 10, 12, 10));
        if (!d.Enabled) card.Opacity = 0.6;
        if (current) card.BorderBrush = _p.AccentBrush;
        return card;
    }

    // ---- 表示 ----
    void DisplayPage(StackPanel panel)
    {
        panel.Children.Add(Ui.Header(_p, "選択UI"));
        var scale = new Slider { Minimum = 60, Maximum = 200, Value = S.OverlayScale * 100, Width = 200, TickFrequency = 10, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var scaleText = Ui.Text($"{S.OverlayScale * 100:0}%", 13, _p.Text, margin: new Thickness(8, 0, 0, 0));
        scale.ValueChanged += (_, e) => { scaleText.Text = $"{e.NewValue:0}%"; };
        scale.PreviewMouseUp += (_, _) => { S.OverlayScale = scale.Value / 100; Changed(); };
        scale.KeyUp += (_, _) => { S.OverlayScale = scale.Value / 100; Changed(); };
        var sc = new StackPanel { Orientation = Orientation.Horizontal };
        sc.Children.Add(scale);
        sc.Children.Add(scaleText);
        panel.Children.Add(Ui.Row(_p, Glyphs.Display, "大きさ", null, sc));

        var corners = new[] { "右下(時計の上)", "左下", "右上", "左上", "中央" };
        panel.Children.Add(Ui.Row(_p, Glyphs.Display, "表示する位置", "タスクバーにかからない場所に出します", Ui.Choice(_p, corners, (int)S.Corner, i => { S.Corner = (OverlayCorner)i; Changed(); ShowPage("display"); })));
        var margin = new Slider { Minimum = 0, Maximum = 120, Value = S.Margin, Width = 200, TickFrequency = 4, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var marginText = Ui.Text($"{S.Margin:0}", 13, _p.Text, margin: new Thickness(8, 0, 0, 0));
        margin.ValueChanged += (_, e) => marginText.Text = $"{e.NewValue:0}";
        margin.PreviewMouseUp += (_, _) => { S.Margin = margin.Value; Changed(); };
        var mg = new StackPanel { Orientation = Orientation.Horizontal };
        mg.Children.Add(margin);
        mg.Children.Add(marginText);
        panel.Children.Add(Ui.Row(_p, Glyphs.Display, "画面の端からのすきま", null, mg));
        panel.Children.Add(Ui.Row(_p, Glyphs.Display, "外観", "選択UIと設定画面の色",
            Ui.Choice(_p, new[] { "Windows に合わせる", "ダーク", "ライト" }, (int)S.Theme, i =>
            {
                S.Theme = (ThemeMode)i;
                Changed();
                Dispatcher.BeginInvoke(() => Build());
            })));
        panel.Children.Add(Ui.Row(_p, Glyphs.Devices, "未接続のデバイスも一覧に出す", "うすく表示します(選ぶときは飛ばします)", Ui.Toggle(_p, S.ShowDisconnected, v => { S.ShowDisconnected = v; Changed(); })));
        panel.Children.Add(Ui.Header(_p, "通知"));
        panel.Children.Add(Ui.Row(_p, Glyphs.Check, "切り替えた結果を少し表示する", "キーを離したあと、選択UIに「切り替えました」を1秒ほど出します", Ui.Toggle(_p, S.ShowResult, v => { S.ShowResult = v; Changed(); })));
        panel.Children.Add(Ui.Row(_p, Glyphs.Info, "Windows の通知も出す", "タスクトレイから通知を出します", Ui.Toggle(_p, S.TrayNotify, v => { S.TrayNotify = v; Changed(); })));
        panel.Children.Add(new Border { Height = 12 });
        panel.Children.Add(Ui.Button(_p, "選択UIを試しに表示する", () => _host.PreviewOverlay(), Glyphs.Display, accent: true));
    }

    // ---- アプリ ----
    void AppPage(StackPanel panel)
    {
        panel.Children.Add(Ui.Header(_p, "起動と常駐"));
        panel.Children.Add(Ui.Row(_p, Glyphs.Power, "Windows の起動時に自動で起動", "サインインしたら、画面を出さずにタスクトレイに常駐します", Ui.Toggle(_p, S.StartWithWindows, v => { S.StartWithWindows = v; Changed(); })));
        panel.Children.Add(Ui.Row(_p, Glyphs.Info, "タスクトレイにアイコンを出す", "オフにしても裏で動きます。設定を開くときは、もう一度アプリを起動してください", Ui.Toggle(_p, S.ShowTrayIcon, v => { S.ShowTrayIcon = v; Changed(); })));
        panel.Children.Add(Ui.Header(_p, "設定"));
        var save = new StackPanel { Orientation = Orientation.Horizontal };
        save.Children.Add(Ui.Button(_p, "今すぐ保存", () => { _host.SaveNow(); UpdateStatus("保存しました"); }, Glyphs.Save, accent: true));
        save.Children.Add(new Border { Width = 8 });
        save.Children.Add(Ui.Button(_p, "保存場所を開く", () =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _host.SettingsPath + "\""); } catch { }
        }, Glyphs.Folder));
        panel.Children.Add(Ui.Row(_p, Glyphs.Save, "設定の保存", "変えた設定は自動で保存されます", save));
        panel.Children.Add(Ui.Header(_p, "アプリ"));
        panel.Children.Add(Ui.Row(_p, Glyphs.Power, "アプリを終了する", "ショートカットも止まります", Ui.Button(_p, "終了", () => _host.Exit(), Glyphs.Power)));
        var ver = typeof(SettingsWindow).Assembly.GetName().Version;
        panel.Children.Add(Ui.Text($"Windows Audio Switcher {ver?.ToString(3)}", 12, _p.SubText, margin: new Thickness(2, 16, 0, 0)));
    }
}
