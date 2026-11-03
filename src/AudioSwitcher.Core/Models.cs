namespace AudioSwitcher.Core;

/// <summary>Windows が知っている出力デバイス(今の状態)。</summary>
public sealed record SystemDevice(string Id, string Name, bool Connected);

/// <summary>登録した出力デバイス。並び順は <see cref="AppSettings.Devices"/> のリストの順。</summary>
public sealed class DeviceEntry
{
    public string Id { get; set; } = "";
    /// <summary>自分で付けた表示名。空なら Windows の名前を使う。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>最後に見た Windows の名前(切断中でも名前が出せるように覚えておく)。</summary>
    public string SystemName { get; set; } = "";
    /// <summary>切り替え対象に入れるか。</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>今つながっているか(保存しない。毎回 Windows から調べる)。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Connected { get; set; }

    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? (string.IsNullOrWhiteSpace(SystemName) ? "(名前なし)" : SystemName) : DisplayName.Trim();
}

/// <summary>ショートカット。Ctrl・Alt・Shift・Win の組み合わせと、ほかのキー1つまで。</summary>
public sealed class Hotkey
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    /// <summary>修飾キー以外のキー(仮想キーコード)。0 ならなし。</summary>
    public int Key { get; set; }

    public bool IsEmpty => !Ctrl && !Alt && !Shift && !Win && Key == 0;

    public static Hotkey Default => new() { Ctrl = true, Alt = true };

    public Hotkey Clone() => new() { Ctrl = Ctrl, Alt = Alt, Shift = Shift, Win = Win, Key = Key };

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        if (Key != 0) parts.Add(KeyNames.Name(Key));
        return parts.Count == 0 ? "(なし)" : string.Join(" + ", parts);
    }
}

public enum OverlayCorner { BottomRight, BottomLeft, TopRight, TopLeft, Center }

public enum ThemeMode { System, Dark, Light }

/// <summary>どの用途の既定のデバイスを切り替えるか。</summary>
public enum SwitchRoles { All, MediaOnly }

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // ---- 操作 ----
    public Hotkey Hotkey { get; set; } = Hotkey.Default;
    /// <summary>false: ホイールを下に回すと次(リストの下)へ。true: 上に回すと次へ。</summary>
    public bool InvertWheel { get; set; }
    /// <summary>最後の次は最初へ(循環)。</summary>
    public bool Wrap { get; set; } = true;
    public SwitchRoles Roles { get; set; } = SwitchRoles.All;
    /// <summary>ホットキーを一時的に止める。</summary>
    public bool Paused { get; set; }

    // ---- デバイス ----
    public List<DeviceEntry> Devices { get; set; } = new();
    /// <summary>初回に今つながっているデバイスを登録したか。</summary>
    public bool FirstScanDone { get; set; }

    // ---- 表示 ----
    public double OverlayScale { get; set; } = 1.0;
    public OverlayCorner Corner { get; set; } = OverlayCorner.BottomRight;
    /// <summary>画面の端(タスクバーの上)からのすきま(画面上の大きさ、DIP)。</summary>
    public double Margin { get; set; } = 12;
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool ShowDisconnected { get; set; } = true;
    public bool ShowDisabled { get; set; }
    /// <summary>切り替えたあと、選択UIに結果を少しだけ出す。</summary>
    public bool ShowResult { get; set; } = true;
    /// <summary>切り替えたときにタスクトレイから通知を出す。</summary>
    public bool TrayNotify { get; set; }

    // ---- アプリ ----
    public bool StartWithWindows { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
}
