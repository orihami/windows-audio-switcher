using Microsoft.Win32;

namespace AudioSwitcher.App.Services;

/// <summary>Windows の起動時に動かす(ユーザーごとの Run キー。管理者の権限はいらない)。</summary>
public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "WindowsAudioSwitcher";

    public static bool IsOn()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(Name) is string s && s.Contains(Environment.ProcessPath ?? "AudioSwitcher.exe", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue(Name, $"\"{Environment.ProcessPath}\" --background");
            else if (k.GetValue(Name) != null) k.DeleteValue(Name);
        }
        catch (Exception e) { Log.Write("自動起動の設定に失敗: " + e.Message); }
    }
}
