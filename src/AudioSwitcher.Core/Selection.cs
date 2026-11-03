namespace AudioSwitcher.Core;

/// <summary>
/// ショートカットを押している間の「選んでいる途中」の状態。ここでは実際の出力は変えない。
/// キーを離したときに <see cref="Commit"/> で決まった先を返す。
/// </summary>
public sealed class SelectionSession
{
    readonly List<DeviceEntry> _list;
    readonly bool _wrap;

    /// <summary>今 Windows で使っている出力先の ID(登録していなければ見つからないこともある)。</summary>
    public string? CurrentId { get; }
    /// <summary>選んでいる位置(<see cref="Items"/> の番号)。-1 はまだ何も選んでいない。</summary>
    public int Index { get; private set; }
    /// <summary>ホイールを動かしたか(動かさずに離したら何もしない)。</summary>
    public bool Moved { get; private set; }

    /// <summary>選択UIに並べるデバイス(登録順。使わない設定のものは入れない)。</summary>
    public IReadOnlyList<DeviceEntry> Items => _list;

    public SelectionSession(IEnumerable<DeviceEntry> registered, string? currentId, bool wrap)
    {
        _list = registered.Where(d => d.Enabled).ToList();
        _wrap = wrap;
        CurrentId = currentId;
        Index = _list.FindIndex(d => d.Id == currentId);
    }

    public DeviceEntry? Selected => Index >= 0 && Index < _list.Count ? _list[Index] : null;

    /// <summary>選べる先(つながっているもの)があるか。</summary>
    public bool HasCandidates => _list.Any(d => d.Connected);

    /// <summary>
    /// steps 個ぶん進む(+ で下、- で上)。つながっていないデバイスは飛ばす。
    /// 循環しない設定では端で止まる。
    /// </summary>
    public void Step(int steps)
    {
        if (steps == 0 || !HasCandidates) return;
        int dir = Math.Sign(steps);
        for (int s = 0; s < Math.Abs(steps); s++)
        {
            int next = NextFrom(Index, dir);
            if (next < 0) break;
            Index = next;
            Moved = true;
        }
    }

    int NextFrom(int from, int dir)
    {
        int n = _list.Count;
        // まだ何も選んでいないとき: 下なら最初の候補、上なら最後の候補から
        int i = from < 0 ? (dir > 0 ? -1 : n) : from;
        for (int k = 0; k < n; k++)
        {
            i += dir;
            if (i >= n || i < 0)
            {
                if (!_wrap) return -1;
                i = (i + n) % n;
            }
            if (_list[i].Connected) return i;
        }
        return -1;
    }

    /// <summary>キーを離したときに切り替える先。変えなくてよいときは null。</summary>
    public DeviceEntry? Commit()
    {
        var sel = Selected;
        if (!Moved || sel is null || !sel.Connected || sel.Id == CurrentId) return null;
        return sel;
    }
}

/// <summary>ホイールの回転量を「何段」に変える(なめらかに回るホイールの細かい値もためて使う)。</summary>
public sealed class WheelAccumulator
{
    public const int Notch = 120;
    int _acc;

    /// <summary>delta は Windows の値(上に1段 = +120)。invert=false なら下に回すと + (次へ)。</summary>
    public int Add(int delta, bool invert)
    {
        _acc += invert ? delta : -delta;
        int steps = _acc / Notch;
        _acc -= steps * Notch;
        return steps;
    }

    public void Reset() => _acc = 0;
}
