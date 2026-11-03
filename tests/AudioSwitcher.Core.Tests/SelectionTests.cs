using AudioSwitcher.Core;
using Xunit;

public class SelectionTests
{
    // 仕様書の例: モニター / イヤホンA / B / C / 有線イヤホン
    static List<DeviceEntry> Five(params string[] disconnected) =>
        new[] { "monitor", "earA", "earB", "earC", "wired" }
            .Select(id => new DeviceEntry { Id = id, SystemName = id, Connected = !disconnected.Contains(id) })
            .ToList();

    [Fact]
    public void 今の出力先から始まり_ホイールで順に進む()
    {
        var s = new SelectionSession(Five(), "earA", wrap: true);
        Assert.Equal("earA", s.Selected!.Id);
        s.Step(1);
        Assert.Equal("earB", s.Selected!.Id);
        s.Step(2);
        Assert.Equal("wired", s.Selected!.Id);
        s.Step(-1);
        Assert.Equal("earC", s.Selected!.Id);
    }

    [Fact]
    public void つながっていないデバイスは飛ばす()
    {
        var s = new SelectionSession(Five("earB", "earC"), "earA", wrap: true);
        s.Step(1);
        Assert.Equal("wired", s.Selected!.Id);
        s.Step(-1);
        Assert.Equal("earA", s.Selected!.Id);
    }

    [Fact]
    public void 循環する_最後の次は最初_最初の前は最後()
    {
        var s = new SelectionSession(Five(), "wired", wrap: true);
        s.Step(1);
        Assert.Equal("monitor", s.Selected!.Id);
        s.Step(-1);
        Assert.Equal("wired", s.Selected!.Id);
    }

    [Fact]
    public void 循環しない設定では端で止まる()
    {
        var s = new SelectionSession(Five(), "earC", wrap: false);
        s.Step(5);
        Assert.Equal("wired", s.Selected!.Id);
        s = new SelectionSession(Five(), "earA", wrap: false);
        s.Step(-3);
        Assert.Equal("monitor", s.Selected!.Id);
    }

    [Fact]
    public void 循環して端の切断デバイスも飛ばす()
    {
        var s = new SelectionSession(Five("monitor"), "wired", wrap: true);
        s.Step(1);
        Assert.Equal("earA", s.Selected!.Id);
    }

    [Fact]
    public void 使わない設定のデバイスは並ばない()
    {
        var list = Five();
        list[1].Enabled = false;
        var s = new SelectionSession(list, "monitor", wrap: true);
        Assert.DoesNotContain(s.Items, d => d.Id == "earA");
        s.Step(1);
        Assert.Equal("earB", s.Selected!.Id);
    }

    [Fact]
    public void 離したときだけ切り替え先が決まる_動かさなければ何もしない()
    {
        var s = new SelectionSession(Five(), "earA", wrap: true);
        Assert.Null(s.Commit());
        s.Step(1);
        s.Step(-1);
        Assert.Null(s.Commit()); // 元にもどしたら変えない
        s.Step(1);
        Assert.Equal("earB", s.Commit()!.Id);
    }

    [Fact]
    public void 今の出力先が登録外なら_下で最初_上で最後から()
    {
        var s = new SelectionSession(Five(), "speakerX", wrap: true);
        Assert.Null(s.Selected);
        s.Step(1);
        Assert.Equal("monitor", s.Selected!.Id);
        s = new SelectionSession(Five(), "speakerX", wrap: true);
        s.Step(-1);
        Assert.Equal("wired", s.Selected!.Id);
    }

    [Fact]
    public void どれもつながっていなければ何も選ばない()
    {
        var s = new SelectionSession(Five("monitor", "earA", "earB", "earC", "wired"), "monitor", wrap: true);
        s.Step(3);
        Assert.Null(s.Commit());
        Assert.False(s.HasCandidates);
    }

    [Fact]
    public void ホイールは1段ごと_細かい値はためる_向きを逆にできる()
    {
        var w = new WheelAccumulator();
        Assert.Equal(1, w.Add(-120, invert: false)); // 下へ回す → 次へ
        Assert.Equal(-1, w.Add(120, invert: false));
        Assert.Equal(0, w.Add(-60, invert: false));
        Assert.Equal(1, w.Add(-60, invert: false));
        Assert.Equal(1, w.Add(120, invert: true)); // 逆: 上へ回す → 次へ
        Assert.Equal(-2, w.Add(240, invert: false));
    }
}

public class HotkeyTests
{
    [Fact]
    public void 全部押したら始まり_どれかを離したら終わる()
    {
        var t = new HotkeyTracker(new Hotkey { Ctrl = true, Alt = true });
        Assert.Equal(HotkeyTracker.Change.None, t.KeyDown(KeyNames.VK_LCONTROL));
        Assert.Equal(HotkeyTracker.Change.Activated, t.KeyDown(KeyNames.VK_LMENU));
        Assert.Equal(HotkeyTracker.Change.None, t.KeyDown(KeyNames.VK_LMENU)); // キーリピート
        Assert.Equal(HotkeyTracker.Change.Released, t.KeyUp(KeyNames.VK_LCONTROL));
        Assert.Equal(HotkeyTracker.Change.None, t.KeyUp(KeyNames.VK_LMENU));
    }

    [Fact]
    public void 右と左の修飾キーどちらでもよい_ほかのキーも組み合わせられる()
    {
        var t = new HotkeyTracker(new Hotkey { Win = true, Shift = true, Key = 0x41 });
        t.KeyDown(KeyNames.VK_RWIN);
        t.KeyDown(KeyNames.VK_RSHIFT);
        Assert.Equal(HotkeyTracker.Change.Activated, t.KeyDown(0x41));
        Assert.True(t.IsMainKey(0x41));
        Assert.Equal(HotkeyTracker.Change.Released, t.KeyUp(0x41));
    }

    [Fact]
    public void 左右両方押しているときは両方離すまで続く()
    {
        var t = new HotkeyTracker(new Hotkey { Ctrl = true });
        t.KeyDown(KeyNames.VK_LCONTROL);
        t.KeyDown(KeyNames.VK_RCONTROL);
        Assert.Equal(HotkeyTracker.Change.None, t.KeyUp(KeyNames.VK_LCONTROL));
        Assert.Equal(HotkeyTracker.Change.Released, t.KeyUp(KeyNames.VK_RCONTROL));
    }

    [Fact]
    public void 記録_押したキーを全部離したところで決まる()
    {
        var r = new HotkeyRecorder();
        r.KeyDown(KeyNames.VK_LCONTROL);
        r.KeyDown(KeyNames.VK_LSHIFT);
        r.KeyDown(0x70);
        Assert.Null(r.KeyUp(0x70));
        Assert.Null(r.KeyUp(KeyNames.VK_LSHIFT));
        var h = r.KeyUp(KeyNames.VK_LCONTROL)!;
        Assert.Equal("Ctrl + Shift + F1", h.ToString());
    }
}

public class RegistryAndSettingsTests
{
    [Fact]
    public void 切断しても登録は残り_再接続でまた選べる()
    {
        var s = new AppSettings();
        DeviceRegistry.FirstScan(s, new[] { new SystemDevice("a", "モニター", true), new SystemDevice("b", "イヤホン", true), new SystemDevice("c", "古いの", false) });
        Assert.Equal(new[] { "a", "b" }, s.Devices.Select(d => d.Id));
        DeviceRegistry.Sync(s.Devices, new[] { new SystemDevice("a", "モニター", true) });
        Assert.False(s.Devices[1].Connected);
        Assert.Equal("イヤホン", s.Devices[1].Label);
        DeviceRegistry.Sync(s.Devices, new[] { new SystemDevice("a", "モニター", true), new SystemDevice("b", "イヤホン", true) });
        Assert.True(s.Devices[1].Connected);
    }

    [Fact]
    public void 追加_削除_並べ替え_表示名()
    {
        var list = new List<DeviceEntry>();
        var sys = new[] { new SystemDevice("a", "A", true), new SystemDevice("b", "B", true), new SystemDevice("c", "C", false) };
        foreach (var d in sys) DeviceRegistry.Add(list, d);
        DeviceRegistry.Add(list, sys[0]); // 2回足しても1つ
        Assert.Equal(3, list.Count);
        DeviceRegistry.Move(list, 2, 0);
        Assert.Equal("cab", string.Concat(list.Select(d => d.Id)));
        DeviceRegistry.Remove(list, "a");
        Assert.Equal("cb", string.Concat(list.Select(d => d.Id)));
        list[1].DisplayName = "  自分の名前 ";
        Assert.Equal("自分の名前", list[1].Label);
        Assert.Equal(new[] { "a" }, DeviceRegistry.Unregistered(list, sys).Select(d => d.Id));
    }

    [Fact]
    public void 設定を保存して読み戻せる_こわれたファイルは初期値()
    {
        var dir = Path.Combine(Path.GetTempPath(), "as-test-" + Guid.NewGuid());
        var store = new SettingsStore(Path.Combine(dir, "settings.json"));
        var s = new AppSettings { Wrap = false, Corner = OverlayCorner.TopLeft, Hotkey = new Hotkey { Win = true, Key = 0x70 } };
        s.Devices.Add(new DeviceEntry { Id = "x", DisplayName = "モニター", Connected = true });
        store.Save(s);
        var back = store.Load();
        Assert.False(back.Wrap);
        Assert.Equal(OverlayCorner.TopLeft, back.Corner);
        Assert.Equal("Win + F1", back.Hotkey.ToString());
        Assert.Equal("モニター", back.Devices[0].DisplayName);
        Assert.False(back.Devices[0].Connected); // 接続状態は保存しない
        File.WriteAllText(store.Path, "{こわれた");
        Assert.True(store.Load().Wrap);
        Assert.Single(Directory.GetFiles(dir, "settings.json.broken-*"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void 選択UIの位置_右下はタスクバーの上_はみ出さない()
    {
        var work = new Rect(0, 0, 1920, 1032); // 下にタスクバー(48)
        var (x, y) = OverlayPlacement.Place(work, 300, 200, OverlayCorner.BottomRight, 12);
        Assert.Equal(1920 - 12 - 300, x);
        Assert.Equal(1032 - 12 - 200, y);
        var left = new Rect(48, 0, 1872, 1080); // 左にタスクバー
        Assert.Equal((60.0, 868.0), OverlayPlacement.Place(left, 300, 200, OverlayCorner.BottomLeft, 12));
        Assert.Equal((0.0, 0.0), OverlayPlacement.Place(new Rect(0, 0, 200, 100), 300, 200, OverlayCorner.BottomRight, 12));
    }
}
