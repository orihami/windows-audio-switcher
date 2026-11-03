using System.Runtime.InteropServices;

namespace AudioSwitcher.App.Interop;

public static class Native
{
    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    public const int WM_MOUSEWHEEL = 0x20A;
    public const int LLKHF_INJECTED = 0x10;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT { public int x, y; public int mouseData, flags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    // ---- キーを送る(Alt・Win を離したときにメニューやスタートが開かないようにする) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public int type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint n, INPUT[] inputs, int size);

    /// <summary>何にも割り当てられていないキー(0xE8)を一瞬押して離す。Alt・Win だけを押して離した扱いにならない。</summary>
    public static void SendMaskKey()
    {
        var inputs = new INPUT[2];
        inputs[0].type = 1;
        inputs[0].u.ki.wVk = 0xE8;
        inputs[1].type = 1;
        inputs[1].u.ki.wVk = 0xE8;
        inputs[1].u.ki.dwFlags = 2; // KEYEVENTF_KEYUP
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    // ---- テーマ(ダーク/ライト)とアクセントの色 ----
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetColorizationColor(out uint color, out bool opaque);
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
