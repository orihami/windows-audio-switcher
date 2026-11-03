using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;

namespace AudioSwitcher.App.Views;

/// <summary>設定画面からアプリ本体に頼むこと。</summary>
public interface IAppHost
{
    AppSettings Settings { get; }
    /// <summary>Windows の出力デバイス(登録の状態は Settings.Devices に反映ずみ)。</summary>
    IReadOnlyList<SystemDevice> SystemDevices { get; }
    string? CurrentId { get; }
    FormFactor KindOf(string id);
    /// <summary>設定を変えた(保存して、すぐ反映する)。</summary>
    void SettingsChanged();
    void SaveNow();
    void RecordHotkey(Action<Hotkey> done);
    void CancelRecord();
    void PreviewOverlay();
    void SwitchTo(string id);
    void Exit();
    string SettingsPath { get; }
    /// <summary>デバイスがつながった・外れた・既定が変わった。</summary>
    event Action? DevicesChanged;
}
