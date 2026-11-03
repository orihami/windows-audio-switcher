namespace AudioSwitcher.Core;

/// <summary>登録したデバイスの追加・削除・並べ替えと、Windows の状態との突き合わせ。</summary>
public static class DeviceRegistry
{
    /// <summary>
    /// Windows の今のデバイス一覧で、登録したデバイスの接続状態と名前を更新する。
    /// 切断されたデバイスも登録は残す(名前は最後に見たもの)。
    /// </summary>
    public static void Sync(List<DeviceEntry> registered, IEnumerable<SystemDevice> system)
    {
        var map = system.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var e in registered)
        {
            if (map.TryGetValue(e.Id, out var s))
            {
                e.Connected = s.Connected;
                if (!string.IsNullOrWhiteSpace(s.Name)) e.SystemName = s.Name;
            }
            else e.Connected = false;
        }
    }

    /// <summary>まだ登録していない Windows のデバイス(追加の候補)。</summary>
    public static List<SystemDevice> Unregistered(List<DeviceEntry> registered, IEnumerable<SystemDevice> system)
    {
        var ids = registered.Select(r => r.Id).ToHashSet();
        return system.Where(s => !ids.Contains(s.Id)).OrderByDescending(s => s.Connected).ThenBy(s => s.Name).ToList();
    }

    public static DeviceEntry Add(List<DeviceEntry> registered, SystemDevice dev)
    {
        var have = registered.FirstOrDefault(r => r.Id == dev.Id);
        if (have != null) return have;
        var e = new DeviceEntry { Id = dev.Id, SystemName = dev.Name, Connected = dev.Connected };
        registered.Add(e);
        return e;
    }

    public static bool Remove(List<DeviceEntry> registered, string id) => registered.RemoveAll(r => r.Id == id) > 0;

    /// <summary>from 番目を to 番目へ動かす。</summary>
    public static void Move(List<DeviceEntry> registered, int from, int to)
    {
        if (from < 0 || from >= registered.Count) return;
        to = Math.Clamp(to, 0, registered.Count - 1);
        if (from == to) return;
        var e = registered[from];
        registered.RemoveAt(from);
        registered.Insert(to, e);
    }

    /// <summary>初回: 今つながっている出力デバイスを全部登録する。</summary>
    public static void FirstScan(AppSettings s, IEnumerable<SystemDevice> system)
    {
        if (s.FirstScanDone) return;
        foreach (var d in system.Where(d => d.Connected)) Add(s.Devices, d);
        s.FirstScanDone = true;
    }
}
