using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Darkroom
{
    /// <summary>
    /// Low-level keyboard hook used when "Darkroom hotkeys take precedence" is on.
    /// While the Darkroom window is in front, any key combination assigned in Darkroom is taken
    /// before Windows shortcuts or other programs (AutoHotkey and similar) can act on it, and is
    /// passed straight to the editor instead. The hook is reinstalled each time the window is
    /// activated so it sits first in line, ahead of hooks other programs installed earlier.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint LLKHF_ALTDOWN = 0x20;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly LowLevelKeyboardProc _proc;   // kept in a field so it is never garbage-collected
        private readonly Func<IntPtr> _window;
        private readonly Func<string, bool> _shouldTake;
        private readonly Action<string> _onHotkey;
        private IntPtr _hook = IntPtr.Zero;

        public KeyboardHook(Func<IntPtr> window, Func<string, bool> shouldTake, Action<string> onHotkey)
        {
            _proc = HookCallback;
            _window = window;
            _shouldTake = shouldTake;
            _onHotkey = onHotkey;
        }

        public void Install()
        {
            Uninstall();
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        public void Dispose() => Uninstall();

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                IntPtr me = _window();
                if (me != IntPtr.Zero && GetForegroundWindow() == me)
                {
                    string? combo = ComboFor(info.vkCode, (info.flags & LLKHF_ALTDOWN) != 0);
                    if (combo != null && _shouldTake(combo))
                    {
                        _onHotkey(combo);
                        return (IntPtr)1;   // swallow it so nothing else sees the key
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        /// <summary>Builds the same combo text the editor uses, e.g. "Ctrl+Shift+S".</summary>
        private static string? ComboFor(uint vk, bool altFlag)
        {
            string? key = KeyName(vk);
            if (key == null) return null;
            var parts = new List<string>(5);
            if (Down(0x11)) parts.Add("Ctrl");
            if (altFlag || Down(0x12)) parts.Add("Alt");
            if (Down(0x10)) parts.Add("Shift");
            if (Down(0x5B) || Down(0x5C)) parts.Add("Win");
            parts.Add(key);
            return string.Join("+", parts);
        }

        private static string? KeyName(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();                 // A-Z
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();                 // 0-9
            if (vk >= 0x60 && vk <= 0x69) return ((char)('0' + (int)(vk - 0x60))).ToString(); // numpad 0-9
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);                      // F1-F24
            return vk switch
            {
                0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter", 0x1B => "Esc", 0x20 => "Space",
                0x21 => "PageUp", 0x22 => "PageDown", 0x23 => "End", 0x24 => "Home",
                0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
                0x2D => "Insert", 0x2E => "Delete",
                0x6A => "Num*", 0x6B => "Num+", 0x6D => "Num-", 0x6E => "Num.", 0x6F => "Num/",
                0xBA => ";", 0xBB => "=", 0xBC => ",", 0xBD => "-", 0xBE => ".", 0xBF => "/",
                0xC0 => "`", 0xDB => "[", 0xDC => "\\", 0xDD => "]", 0xDE => "'",
                _ => null
            };
        }
    }
}
