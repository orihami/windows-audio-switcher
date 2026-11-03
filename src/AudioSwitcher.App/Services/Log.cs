namespace AudioSwitcher.App.Services;

/// <summary>うまく動かないときの手がかり。%LOCALAPPDATA%\AudioSwitcher\log.txt(大きくなったら作り直す)。</summary>
public static class Log
{
    static readonly object Gate = new();
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioSwitcher");
    static string FilePath => Path.Combine(Dir, "log.txt");

    public static void Write(string text)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var f = new FileInfo(FilePath);
                if (f.Exists && f.Length > 512 * 1024) f.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
