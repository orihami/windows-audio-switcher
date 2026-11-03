using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioSwitcher.Core;

/// <summary>設定を JSON で読み書きする。こわれていたら別名で残して、初期値で始める。</summary>
public sealed class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Path { get; }

    public SettingsStore(string path) => Path = path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Options) ?? new AppSettings();
            s.Hotkey ??= Hotkey.Default;
            s.Devices ??= new();
            s.Devices.RemoveAll(d => string.IsNullOrEmpty(d.Id));
            s.OverlayScale = Math.Clamp(s.OverlayScale, 0.6, 2.5);
            return s;
        }
        catch (Exception)
        {
            try { File.Copy(Path, Path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            return new AppSettings();
        }
    }

    /// <summary>いったん別のファイルに書いてから置きかえる(書いている途中で止まっても前の設定が残る)。</summary>
    public void Save(AppSettings s)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
        File.Move(tmp, Path, true);
    }
}
