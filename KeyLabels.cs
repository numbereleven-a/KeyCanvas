using System.Runtime.InteropServices;
using System.Text;

namespace KeyCanvas;

internal static class KeyLabels
{
    internal static bool CapsLockOn => (GetKeyState((int)Keys.Capital) & 1) != 0;
    internal static string Get(int key, double?[] held, bool capsLock)
    {
        var state = new byte[256];
        bool shift = held[(int)Keys.ShiftKey].HasValue || held[(int)Keys.LShiftKey].HasValue || held[(int)Keys.RShiftKey].HasValue;
        state[(int)Keys.ShiftKey] = shift ? (byte)128 : (byte)0;
        state[(int)Keys.Capital] = capsLock ? (byte)1 : (byte)0;
        if (held[(int)Keys.RMenu].HasValue)
        {
            state[(int)Keys.ControlKey] = 128;
            state[(int)Keys.Menu] = 128;
        }
        nint layout = GetKeyboardLayout(NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _));
        return Translate(key, state, layout);
    }

    internal static string Translate(int key, byte[] state, nint layout)
    {
        bool russian = ((long)layout & 0xFFFF) == 0x419;
        string? name = (Keys)key switch
        {
            Keys.Space => russian ? "Пробел" : "Space",
            Keys.Return => "Enter", Keys.Back => "Backspace", Keys.Tab => "Tab",
            Keys.Left => "←", Keys.Right => "→", Keys.Up => "↑", Keys.Down => "↓",
            Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey => "Shift",
            Keys.LControlKey or Keys.RControlKey or Keys.ControlKey => "Ctrl",
            Keys.LMenu or Keys.RMenu or Keys.Menu => "Alt",
            Keys.LWin or Keys.RWin => "Win", Keys.Capital => "Caps Lock",
            Keys.Escape => "Esc", Keys.Delete => "Delete", Keys.Insert => "Insert",
            Keys.Home => "Home", Keys.End => "End", Keys.Prior => "Page Up", Keys.Next => "Page Down",
            _ => null
        };
        if (name is not null)
            return name;
        var text = new StringBuilder(8);
        int count = ToUnicodeEx((uint)key, MapVirtualKeyEx((uint)key, 0, layout), state, text, text.Capacity, 4, layout);
        return count != 0 ? text.ToString(0, Math.Min(Math.Abs(count), text.Length)) : ((Keys)key).ToString();
    }

    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")] private static extern uint MapVirtualKeyEx(uint key, uint type, nint layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder text, int length, uint flags, nint layout);
}
