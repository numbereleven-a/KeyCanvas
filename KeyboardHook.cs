using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyCanvas;

internal enum KeyboardMode { Disabled, Canvas, Menu }

internal sealed class KeyboardPolicy
{
    private readonly bool[] pressed = new bool[256];
    private readonly bool[] systemSawDown = new bool[256];

    internal void Seed(int key, bool down)
    {
        // The generic Ctrl query mirrors its sides; native releases report a specific side.
        pressed[key] = down && key != 0x11;
        systemSawDown[key] = down;
    }

    internal bool Suppress(KeyboardMode mode, int key, bool down, bool alt)
    {
        if ((uint)key >= 256)
            return false;
        pressed[key] = down;
        bool control = pressed[0x11] || pressed[0xA2] || pressed[0xA3];
        bool suppress = mode == KeyboardMode.Canvas || mode == KeyboardMode.Menu &&
            (key is 0x5B or 0x5C or 0x5D || alt && key is 0x09 or 0x1B || control && key == 0x1B);
        if (down)
        {
            if (!suppress)
                systemSawDown[key] = true;
        }
        else
        {
            // A release must reach Windows if it previously received the press,
            // even when closing the menu changed the capture mode between them.
            suppress &= !systemSawDown[key];
            systemSawDown[key] = false;
        }
        return suppress;
    }
}

internal sealed class KeyboardHook : IDisposable
{
    private readonly InputBuffer input;
    private readonly nint window;
    private readonly NativeMethods.HookProc callback;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private nint hook;
    private uint threadId;
    private Exception? startupError;
    private volatile KeyboardMode mode;
    private readonly KeyboardPolicy policy = new();
    private double heartbeat = InputBuffer.Now;
    private bool disposed;

    internal KeyboardHook(InputBuffer input, nint window)
    {
        this.input = input;
        this.window = window;
        callback = OnKeyboard;
        thread = new Thread(Run) { IsBackground = true, Name = "Keyboard input" };
        thread.Start();
        ready.Wait();
        if (startupError is not null)
        {
            thread.Join();
            ready.Dispose();
            throw new InvalidOperationException("Keyboard hook installation failed.", startupError);
        }
    }

    internal bool IsInstalled => Volatile.Read(ref hook) != 0;
    internal bool IsResponsive => InputBuffer.Now - Volatile.Read(ref heartbeat) < 3;
    internal void Heartbeat() => Volatile.Write(ref heartbeat, InputBuffer.Now);

    internal void SetMode(KeyboardMode value)
    {
        mode = KeyboardMode.Disabled;
        input.Reset();
        mode = value;
    }

    private void Run()
    {
        try
        {
            threadId = NativeMethods.GetCurrentThreadId();
            // Create the thread message queue before publishing readiness to Dispose.
            NativeMethods.PeekMessage(out _, 0, 0, 0, 0);
            for (int key = 0; key < 256; key++)
                policy.Seed(key, NativeMethods.GetAsyncKeyState(key) < 0);
            hook = NativeMethods.SetWindowsHookEx(13, callback, NativeMethods.GetModuleHandle(null), 0);
            if (hook == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();
            int result;
            while ((result = NativeMethods.GetMessage(out var message, 0, 0, 0)) > 0)
            {
                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessage(ref message);
            }
            if (result < 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception error)
        {
            startupError = error;
        }
        finally
        {
            RemoveHook();
            ready.Set();
        }
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code < 0)
            return NativeMethods.CallNextHookEx(0, code, message, data);

        int key = Marshal.ReadInt32(data);
        bool down = message == 0x100 || message == 0x104;
        bool up = message == 0x101 || message == 0x105;
        if (!down && !up)
            return NativeMethods.CallNextHookEx(0, code, message, data);

        KeyboardMode current = mode;
        nint foreground = NativeMethods.GetForegroundWindow();
        if (!IsResponsive || current == KeyboardMode.Canvas && foreground != window)
            current = KeyboardMode.Disabled;
        else if (current == KeyboardMode.Menu)
        {
            NativeMethods.GetWindowThreadProcessId(foreground, out uint process);
            if (process != Environment.ProcessId)
                current = KeyboardMode.Disabled;
        }
        bool alt = (Marshal.ReadInt32(data, 8) & 0x20) != 0;
        bool suppress = policy.Suppress(current, key, down, alt);
        if (current == KeyboardMode.Canvas)
            input.Record(key, down, InputBuffer.Now);
        return suppress ? 1 : NativeMethods.CallNextHookEx(0, code, message, data);
    }

    private void RemoveHook()
    {
        nint installed = Interlocked.Exchange(ref hook, 0);
        if (installed != 0)
            NativeMethods.UnhookWindowsHookEx(installed);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        mode = KeyboardMode.Disabled;
        RemoveHook();
        bool posted = NativeMethods.PostThreadMessage(threadId, 0x12, 0, 0); // WM_QUIT
        bool stopped = thread.Join(posted ? 2000 : 100);
        input.Reset();
        if (stopped)
            ready.Dispose();
        GC.KeepAlive(callback);
    }
}

internal static class NativeMethods
{
    internal delegate nint HookProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Point;
        internal uint Private;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    internal static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindow(string? className, string caption);
    [DllImport("user32.dll")]
    internal static extern nint GetLastActivePopup(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint window);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? module);
    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    internal static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll")]
    internal static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder name, uint size, out uint needed);

    internal static string DesktopName(nint desktop)
    {
        var name = new StringBuilder(256);
        return GetUserObjectInformation(desktop, 2, name, 512, out _) ? name.ToString() : "";
    }

    internal static bool IsInputDesktop(string canvasDesktop)
    {
        nint desktop = OpenInputDesktop(0, false, 1); // DESKTOP_READOBJECTS
        if (desktop == 0)
            return false;
        try
        {
            return canvasDesktop.Length > 0 && DesktopName(desktop) == canvasDesktop;
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }
}
