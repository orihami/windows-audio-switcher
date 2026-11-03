using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;

namespace AudioSwitcher.App.Views;

/// <summary>
/// 画面右下(時計・通知領域の少し上)に出る選択UI。マウスで押せない・前に出てもほかのウィンドウから入力を奪わない。
/// </summary>
public sealed class OverlayWindow : Window
{
    readonly Border _card = new();
    readonly StackPanel _rows = new();
    readonly TextBlock _title = new();
    readonly TextBlock _hint = new();
    readonly TextBlock _footer = new();
    readonly ScaleTransform _scale = new(1, 1);
    Palette _p = Palette.Get(ThemeMode.System);
    System.Windows.Threading.DispatcherTimer? _hideTimer;

    /// <summary>画面に出さずに中身だけ作る(プレビューの画像用)。</summary>
    public bool Offscreen { get; set; }

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Focusable = false;
        Title = "Audio Switcher";

        var head = new DockPanel { Margin = new Thickness(12, 8, 12, 6) };
        var icon = new TextBlock { Text = Glyphs.Volume, FontFamily = Palette.Icons, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _hint.FontSize = 11;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_hint, Dock.Right);
        _title.FontSize = 13;
        _title.FontWeight = FontWeights.SemiBold;
        _title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(_hint);
        head.Children.Add(icon);
        head.Children.Add(_title);
        _footer.FontSize = 11.5;
        _footer.Margin = new Thickness(12, 6, 12, 8);
        _footer.TextWrapping = TextWrapping.Wrap;

        var stack = new StackPanel { Width = 320 };
        stack.Children.Add(head);
        stack.Children.Add(_rows);
        stack.Children.Add(_footer);
        _card.Child = stack;
        _card.CornerRadius = new CornerRadius(8);
        _card.BorderThickness = new Thickness(1);
        _card.Padding = new Thickness(4, 2, 4, 2);
        _card.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.35 };
        _card.Margin = new Thickness(18); // 影のぶん
        _card.LayoutTransform = _scale;
        Content = _card;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        FontFamily = Palette.Font;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // フォーカスを取らない・クリックを通す・Alt+Tab に出さない
        var h = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT);
    }

    /// <summary>選んでいる途中の表示。kindOf: デバイスの種類(アイコン)。</summary>
    public void Render(SelectionSession s, AppSettings set, Func<string, FormFactor> kindOf, string hotkeyText)
    {
        StopHide();
        ApplyLook(set);
        _title.Text = "出力先";
        _hint.Text = hotkeyText;
        _rows.Children.Clear();
        int shown = 0;
        for (int i = 0; i < s.Items.Count; i++)
        {
            var d = s.Items[i];
            if (!d.Connected && !set.ShowDisconnected && i != s.Index) continue;
            _rows.Children.Add(Row(d, kindOf(d.Id), current: d.Id == s.CurrentId, selected: i == s.Index && s.Moved));
            shown++;
        }
        if (shown == 0)
            _rows.Children.Add(new TextBlock { Text = "切り替える出力先が登録されていません。タスクトレイのアイコンから設定を開いてください。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4, 12, 4), Foreground = _p.SubText, FontSize = 12 });

        var target = s.Commit();
        _footer.Text = !s.HasCandidates ? "つながっている出力先がありません"
            : target != null ? $"キーを離すと「{target.Label}」に切り替えます"
            : s.Moved ? "今の出力先のままです"
            : "ホイールで選んで、キーを離すと切り替え";
        _footer.Foreground = target != null ? _p.AccentText : _p.SubText;
        Present(set);
    }

    /// <summary>切り替えたあとに少しだけ結果を出して消える。</summary>
    public void ShowResult(DeviceEntry dev, FormFactor kind, bool ok, AppSettings set)
    {
        ApplyLook(set);
        _title.Text = ok ? "切り替えました" : "切り替えられませんでした";
        _hint.Text = "";
        _rows.Children.Clear();
        _rows.Children.Add(Row(dev, kind, current: ok, selected: true));
        _footer.Text = "";
        Present(set);
        StopHide();
        _hideTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ok ? 900 : 1800) };
        _hideTimer.Tick += (_, _) => FadeOut();
        _hideTimer.Start();
    }

    public void HideNow()
    {
        StopHide();
        BeginAnimation(OpacityProperty, null);
        Hide();
    }

    void StopHide()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    void FadeOut()
    {
        StopHide();
        var a = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
        a.Completed += (_, _) => { if (Opacity < 0.05) HideNow(); };
        BeginAnimation(OpacityProperty, a);
    }

    void ApplyLook(AppSettings set)
    {
        _p = Palette.Get(set.Theme);
        // 少し透ける背景(Windows のフライアウトのような感じ)
        var bg = ((SolidColorBrush)_p.Card).Color;
        _card.Background = new SolidColorBrush(Color.FromArgb(0xF2, bg.R, bg.G, bg.B));
        _card.BorderBrush = _p.Border;
        _title.Foreground = _p.Text;
        ((TextBlock)((DockPanel)((StackPanel)_card.Child).Children[0]).Children[1]).Foreground = _p.AccentText;
        _hint.Foreground = _p.SubText;
        _scale.ScaleX = _scale.ScaleY = Math.Clamp(set.OverlayScale, 0.6, 2.5);
    }

    void Present(AppSettings set)
    {
        if (Offscreen) return;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (!IsVisible) Show();
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        var (x, y) = OverlayPlacement.Place(new Core.Rect(wa.Left, wa.Top, wa.Width, wa.Height), ActualWidth, ActualHeight, set.Corner, Math.Max(0, set.Margin - 18));
        Left = x;
        Top = y;
    }

    // 「🔊 使用中」(アイコンの字と日本語は別のフォントなので分けて並べる)
    UIElement BadgeContent()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = Glyphs.Volume, FontFamily = Palette.Icons, FontSize = 11, Foreground = _p.AccentText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        sp.Children.Add(new TextBlock { Text = "使用中", FontSize = 11, Foreground = _p.AccentText, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    /// <summary>1行: [選択の印][アイコン][名前・状態][使用中]</summary>
    UIElement Row(DeviceEntry d, FormFactor kind, bool current, bool selected)
    {
        var g = new Grid { MinHeight = 44 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bar = new Border { CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 12, 0, 12), Background = selected ? _p.AccentBrush : Brushes.Transparent };
        var icon = new TextBlock
        {
            Text = Glyphs.For(kind), FontFamily = Palette.Icons, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Foreground = selected ? _p.AccentText : _p.Text,
        };
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 4, 8, 4) };
        names.Children.Add(new TextBlock { Text = d.Label, FontSize = 14, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal, Foreground = _p.Text, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!d.Connected) names.Children.Add(new TextBlock { Text = "未接続", FontSize = 11.5, Foreground = _p.SubText });
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(names, 2);
        g.Children.Add(bar);
        g.Children.Add(icon);
        g.Children.Add(names);
        if (current && d.Connected)
        {
            // 今使っている出力先: 右に「使用中」の印(選んでいる印とは別)
            var badge = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = _p.AccentText, BorderThickness = new Thickness(1),
                Child = BadgeContent(),
            };
            Grid.SetColumn(badge, 3);
            g.Children.Add(badge);
        }
        return new Border
        {
            Child = g, CornerRadius = new CornerRadius(5), Margin = new Thickness(2, 1, 2, 1),
            Background = selected ? _p.AccentSoft : Brushes.Transparent,
            Opacity = d.Connected ? 1 : 0.45,
        };
    }
}
