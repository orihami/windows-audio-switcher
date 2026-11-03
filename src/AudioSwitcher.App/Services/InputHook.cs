using System.Runtime.InteropServices;
using AudioSwitcher.App.Interop;
using AudioSwitcher.Core;

namespace AudioSwitcher.App.Services;

/// <summary>
/// キーボードとマウスの低レベルフック。ショートカットを押している間だけホイールを横取りする(ほかのアプリには渡さない)。
/// フックの中は速く終わらせる(遅いと Windows にフックを外される)。重い処理は UI のスレッドへ回す。
/// </summary>
public sealed class InputHook : IDisposable
{
    readonly Native.HookProc _kbProc, _mouseProc; // GC に回収されないよう持っておく
    IntPtr _kb, _mouse;
    readonly HotkeyTracker _tracker;
    readonly System.Windows.Threading.Dispatcher _ui;
    bool _usedWheel;
    bool _cancelled;
    HotkeyRecorder? _recorder;
    Action<Hotkey>? _recorded;

    /// <summary>ショートカットがそろった。</summary>
    public event Action? Activated;
    /// <summary>ホイールが回った(+ で次、- で前。向きの設定は反映ずみ)。</summary>
    public event Action<int>? Wheel;
    /// <summary>ショートカットを離した(cancelled=true なら別のキーと組み合わせて使われたので切り替えない)。</summary>
    public event Action<bool>? Released;

    public bool Enabled { get; set; } = true;
    public bool InvertWheel { get; set; }
    readonly WheelAccumulator _wheel = new();

    public InputHook(Hotkey hotkey, System.Windows.Threading.Dispatcher ui)
    {
        _tracker = new HotkeyTracker(hotkey);
        _ui = ui;
        _kbProc = KeyboardProc;
        _mouseProc = MouseProc;
        var mod = Native.GetModuleHandle(null);
        _kb = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbProc, mod, 0);
        _mouse = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, mod, 0);
        if (_kb == IntPtr.Zero || _mouse == IntPtr.Zero) Log.Write($"フックを付けられない: {Marshal.GetLastWin32Error()}");
    }

    public Hotkey Hotkey
    {
        get => _tracker.Hotkey;
        set
        {
            _tracker.Hotkey = value;
            _tracker.Reset();
        }
    }

    /// <summary>設定画面: 次に押したキーの組み合わせを記録する(その間はキーをアプリに渡さない)。</summary>
    public void Record(Action<Hotkey> done)
    {
        _recorder = new HotkeyRecorder();
        _recorded = done;
    }

    public void CancelRecord()
    {
        _recorder = null;
        _recorded = null;
    }

    public bool Recording => _recorder != null;

    IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return Native.CallNextHookEx(_kb, code, wParam, lParam);
        var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
        int msg = (int)wParam;
        bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
        bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
        // 自分で送ったキー(0xE8)などは見ない
        if ((k.flags & Native.LLKHF_INJECTED) != 0) return Native.CallNextHookEx(_kb, code, wParam, lParam);

        if (_recorder != null)
        {
            if (down) _recorder.KeyDown(k.vkCode);
            if (up)
            {
                var h = _recorder.KeyUp(k.vkCode);
                if (h != null)
                {
                    var cb = _recorded;
                    _recorder = null;
                    _recorded = null;
                    _ui.BeginInvoke(() => cb?.Invoke(h));
                }
            }
            return (IntPtr)1;
        }

        if (!Enabled) return Native.CallNextHookEx(_kb, code, wParam, lParam);

        bool swallow = false;
        if (down)
        {
            bool wasActive = _tracker.Active;
            var ch = _tracker.KeyDown(k.vkCode);
            if (ch == HotkeyTracker.Change.Activated)
            {
                _usedWheel = false;
                _cancelled = false;
                _wheel.Reset();
                _ui.BeginInvoke(() => Activated?.Invoke());
                swallow = _tracker.IsMainKey(k.vkCode);
            }
            else if (wasActive)
            {
                if (_tracker.IsMainKey(k.vkCode)) swallow = true; // 押しっぱなしのリピート
                else if (!IsHotkeyPart(k.vkCode) && !_usedWheel && !_cancelled)
                {
                    // ショートカットのキーに別のキーを足した = ほかのショートカットを使っている。選択UIを引っこめる
                    _cancelled = true;
                    _ui.BeginInvoke(() => Released?.Invoke(true));
                }
            }
        }
        else if (up)
        {
            swallow = _tracker.Active && _tracker.IsMainKey(k.vkCode);
            if (_tracker.KeyUp(k.vkCode) == HotkeyTracker.Change.Released) EndSession();
        }
        return swallow ? (IntPtr)1 : Native.CallNextHookEx(_kb, code, wParam, lParam);
    }

    bool IsHotkeyPart(int vk)
    {
        var h = _tracker.Hotkey;
        return KeyNames.ModOf(vk) switch
        {
            KeyNames.Mod.Ctrl => h.Ctrl,
            KeyNames.Mod.Alt => h.Alt,
            KeyNames.Mod.Shift => h.Shift,
            KeyNames.Mod.Win => h.Win,
            _ => vk == h.Key,
        };
    }

    void EndSession()
    {
        bool cancelled = _cancelled;
        _cancelled = false;
        if (!cancelled) _ui.BeginInvoke(() => Released?.Invoke(false));
    }

    IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || (int)wParam != Native.WM_MOUSEWHEEL || !Enabled || !_tracker.Active || _cancelled || _recorder != null)
            return Native.CallNextHookEx(_mouse, code, wParam, lParam);
        // キーを離したのを取りこぼしていたら(画面ロックなど)、ここで終わりにする
        if (!StillHeld())
        {
            _tracker.Reset();
            EndSession();
            return Native.CallNextHookEx(_mouse, code, wParam, lParam);
        }
        var m = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
        int delta = (short)((m.mouseData >> 16) & 0xFFFF);
        if (!_usedWheel)
        {
            _usedWheel = true;
            // Alt や Win を離したときにメニュー・スタートが開かないように
            var h = _tracker.Hotkey;
            if (h.Alt || h.Win) Native.SendMaskKey();
        }
        int steps = _wheel.Add(delta, InvertWheel);
        if (steps != 0) _ui.BeginInvoke(() => Wheel?.Invoke(steps));
        return (IntPtr)1; // ホイールはほかのアプリに渡さない
    }

    bool StillHeld()
    {
        static bool Down(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        var h = _tracker.Hotkey;
        if (h.Ctrl && !Down(KeyNames.VK_CONTROL)) return false;
        if (h.Alt && !Down(KeyNames.VK_MENU)) return false;
        if (h.Shift && !Down(KeyNames.VK_SHIFT)) return false;
        if (h.Win && !Down(KeyNames.VK_LWIN) && !Down(KeyNames.VK_RWIN)) return false;
        if (h.Key != 0 && !Down(h.Key)) return false;
        return true;
    }

    public void Dispose()
    {
        if (_kb != IntPtr.Zero) Native.UnhookWindowsHookEx(_kb);
        if (_mouse != IntPtr.Zero) Native.UnhookWindowsHookEx(_mouse);
        _kb = _mouse = IntPtr.Zero;
    }
}
