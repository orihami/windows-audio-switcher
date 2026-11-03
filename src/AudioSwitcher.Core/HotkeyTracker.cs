namespace AudioSwitcher.Core;

/// <summary>
/// キーの押し・離しから「ショートカットを押している間」を判定する(キーボードフックから呼ぶ)。
/// 全部のキーがそろった瞬間に Activated、どれか1つでも離した瞬間に Released。
/// </summary>
public sealed class HotkeyTracker
{
    readonly HashSet<int> _down = new();
    public Hotkey Hotkey { get; set; }
    public bool Active { get; private set; }

    public HotkeyTracker(Hotkey hotkey) => Hotkey = hotkey;

    public enum Change { None, Activated, Released }

    public Change KeyDown(int vk)
    {
        _down.Add(vk);
        if (Active || Hotkey.IsEmpty || !Satisfied()) return Change.None;
        Active = true;
        return Change.Activated;
    }

    public Change KeyUp(int vk)
    {
        _down.Remove(vk);
        if (!Active || Satisfied()) return Change.None;
        Active = false;
        return Change.Released;
    }

    /// <summary>フックが取りこぼしたときなどに、押されているキーを全部忘れる。</summary>
    public Change Reset()
    {
        _down.Clear();
        if (!Active) return Change.None;
        Active = false;
        return Change.Released;
    }

    /// <summary>このキーはショートカットの「修飾キー以外のキー」か(押している間はアプリに渡さない)。</summary>
    public bool IsMainKey(int vk) => Hotkey.Key != 0 && vk == Hotkey.Key;

    bool Has(KeyNames.Mod m) => _down.Any(k => KeyNames.ModOf(k) == m);

    bool Satisfied()
    {
        var h = Hotkey;
        if (h.Ctrl && !Has(KeyNames.Mod.Ctrl)) return false;
        if (h.Alt && !Has(KeyNames.Mod.Alt)) return false;
        if (h.Shift && !Has(KeyNames.Mod.Shift)) return false;
        if (h.Win && !Has(KeyNames.Mod.Win)) return false;
        if (h.Key != 0 && !_down.Contains(h.Key)) return false;
        return true;
    }
}

/// <summary>
/// 設定画面で「キーを押してショートカットを決める」ための記録。
/// 押したキーを集め、全部離したところで決まる。
/// </summary>
public sealed class HotkeyRecorder
{
    readonly HashSet<int> _down = new();
    readonly Hotkey _rec = new();

    public Hotkey Current => _rec.Clone();

    /// <summary>キーを押した。</summary>
    public void KeyDown(int vk)
    {
        _down.Add(vk);
        switch (KeyNames.ModOf(vk))
        {
            case KeyNames.Mod.Ctrl: _rec.Ctrl = true; break;
            case KeyNames.Mod.Alt: _rec.Alt = true; break;
            case KeyNames.Mod.Shift: _rec.Shift = true; break;
            case KeyNames.Mod.Win: _rec.Win = true; break;
            default: _rec.Key = vk; break;
        }
    }

    /// <summary>キーを離した。全部離したら決まった組み合わせを返す(何も押していなければ null)。</summary>
    public Hotkey? KeyUp(int vk)
    {
        _down.Remove(vk);
        if (_down.Count > 0 || _rec.IsEmpty) return null;
        return _rec.Clone();
    }
}
