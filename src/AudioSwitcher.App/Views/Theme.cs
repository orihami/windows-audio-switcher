using System.Windows.Media;
using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;
using Microsoft.Win32;

namespace AudioSwitcher.App.Views;

/// <summary>Windows 11 の見た目に合わせた色(ダーク/ライトとアクセントの色)。</summary>
public sealed class Palette
{
    public bool Dark { get; init; }
    public Color Accent { get; init; }
    public Brush Window { get; init; } = Brushes.Black;
    public Brush Card { get; init; } = Brushes.Black;
    public Brush CardHover { get; init; } = Brushes.Black;
    public Brush Border { get; init; } = Brushes.Gray;
    public Brush Text { get; init; } = Brushes.White;
    public Brush SubText { get; init; } = Brushes.Gray;
    public Brush AccentBrush { get; init; } = Brushes.Blue;
    public Brush AccentSoft { get; init; } = Brushes.Blue;
    public Brush AccentText { get; init; } = Brushes.Blue;
    public Brush OnAccent { get; init; } = Brushes.White;
    public Brush Good { get; init; } = Brushes.Green;

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Yu Gothic UI, Segoe UI, Meiryo UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    static SolidColorBrush B(byte a, byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }
    static SolidColorBrush B(Color c, byte a) { var x = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); x.Freeze(); return x; }

    public static Palette Get(ThemeMode mode)
    {
        bool dark = mode switch { ThemeMode.Dark => true, ThemeMode.Light => false, _ => SystemIsDark() };
        var accent = SystemAccent();
        // 暗い背景では明るめ、明るい背景では濃いめのアクセントにして読みやすくする
        var accentText = dark ? Mix(accent, Colors.White, 0.35) : Mix(accent, Colors.Black, 0.15);
        return dark
            ? new Palette
            {
                Dark = true, Accent = accent,
                Window = B(255, 32, 32, 32), Card = B(255, 43, 43, 43), CardHover = B(255, 50, 50, 50), Border = B(255, 64, 64, 64),
                Text = B(255, 255, 255, 255), SubText = B(255, 170, 170, 170),
                AccentBrush = B(accent, 255), AccentSoft = B(accent, 60), AccentText = B(accentText, 255), OnAccent = Brushes.White,
                Good = B(255, 108, 203, 95),
            }
            : new Palette
            {
                Dark = false, Accent = accent,
                Window = B(255, 243, 243, 243), Card = B(255, 251, 251, 251), CardHover = B(255, 245, 245, 245), Border = B(255, 222, 222, 222),
                Text = B(255, 27, 27, 27), SubText = B(255, 96, 96, 96),
                AccentBrush = B(accent, 255), AccentSoft = B(accent, 40), AccentText = B(accentText, 255), OnAccent = Brushes.White,
                Good = B(255, 15, 123, 15),
            };
    }

    static Color Mix(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static Color SystemAccent()
    {
        try
        {
            if (Native.DwmGetColorizationColor(out var c, out _) == 0)
                return Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);
        }
        catch { }
        return Color.FromRgb(0x00, 0x78, 0xD4);
    }
}

/// <summary>デバイスの種類ごとのアイコン(Segoe Fluent Icons の文字)。</summary>
public static class Glyphs
{
    public const string Speaker = "", Headphone = "", Headset = "", Monitor = "", Volume = "",
        Up = "", Down = "", Delete = "", Add = "", Keyboard = "", Devices = "",
        Display = "", Settings = "", Check = "", Info = "", Power = "", Save = "", Folder = "",
        Mouse = "", Order = "";

    public static string For(FormFactor kind) => kind switch
    {
        FormFactor.Headphones => Headphone,
        FormFactor.Headset or FormFactor.Handset => Headset,
        FormFactor.DigitalAudioDisplayDevice => Monitor,
        _ => Speaker,
    };
}
