namespace AudioSwitcher.Core;

/// <summary>仮想キーコードの名前と、修飾キーの見分け。</summary>
public static class KeyNames
{
    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
    public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    public const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    public enum Mod { None, Ctrl, Alt, Shift, Win }

    public static Mod ModOf(int vk) => vk switch
    {
        VK_CONTROL or VK_LCONTROL or VK_RCONTROL => Mod.Ctrl,
        VK_MENU or VK_LMENU or VK_RMENU => Mod.Alt,
        VK_SHIFT or VK_LSHIFT or VK_RSHIFT => Mod.Shift,
        VK_LWIN or VK_RWIN => Mod.Win,
        _ => Mod.None,
    };

    public static string Name(int vk)
    {
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
        if (vk >= 0x60 && vk <= 0x69) return "テンキー" + (vk - 0x60);
        return vk switch
        {
            0x08 => "BackSpace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x14 => "CapsLock",
            0x1B => "Esc",
            0x1C => "変換",
            0x1D => "無変換",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            0x2C => "PrintScreen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5D => "Menu",
            0x91 => "ScrollLock",
            0xF2 => "カタカナ/ひらがな",
            0xF3 or 0xF4 => "半角/全角",
            0xBA => ":",
            0xBB => ";",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "@",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "^",
            0xE2 => "\\(ろ)",
            _ => $"キー{vk:X2}",
        };
    }
}
