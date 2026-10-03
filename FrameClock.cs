using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KeyCanvas;

internal sealed class FrameClock : IDisposable
{
    internal const int FrameMessage = 0x8001;
    private volatile int framesPerSecond;
    private double FrameInterval => 1.0 / framesPerSecond;
    private readonly nint window;
    private readonly SafeWaitHandle timer;
    private readonly Thread thread;
    private volatile bool stopping;
    private int pending;
    private volatile Exception? error;

    internal FrameClock(nint window, int framesPerSecond = 60)
    {
        this.window = window;
        this.framesPerSecond = framesPerSecond;
        // Windows 10 1803+: precise waits without changing the system timer resolution.
        timer = CreateWaitableTimerEx(0, 0, 2, 0x100002);
        if (timer.IsInvalid)
        {
            int code = Marshal.GetLastWin32Error();
            timer.Dispose();
            throw new Win32Exception(code);
        }
        thread = new Thread(Run) { IsBackground = true, Name = "Animation clock" };
        thread.Start();
    }

    internal void CheckError()
    {
        if (error is not null)
            throw new InvalidOperationException("Animation clock failed.", error);
    }

    internal void CompleteFrame() => Volatile.Write(ref pending, 0);
    internal void SetFrameRate(int value) => framesPerSecond = value;

    private void Run()
    {
        try
        {
            double deadline = InputBuffer.Now + FrameInterval;
            while (!stopping)
            {
                long due = -Math.Max(1, (long)((deadline - InputBuffer.Now) * 10_000_000));
                if (!SetWaitableTimer(timer, ref due, 0, 0, 0, false) ||
                    WaitForSingleObject(timer, uint.MaxValue) != 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (stopping)
                    break;
                // One queued frame at most, even while the UI is drawing the current frame.
                if (Interlocked.CompareExchange(ref pending, 1, 0) == 0 &&
                    !PostMessage(window, FrameMessage, 0, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                deadline += FrameInterval;
                if (deadline <= InputBuffer.Now)
                    deadline = InputBuffer.Now + FrameInterval;
            }
        }
        catch (Exception failure)
        {
            error = failure;
            PostMessage(window, FrameMessage, 0, 0);
        }
    }

    public void Dispose()
    {
        if (stopping)
            return;
        stopping = true;
        // The normal wait is at most one frame. Bound shutdown even if the worker fails.
        if (thread.Join(2000))
            timer.Dispose();
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimerEx(nint attributes, nint name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period,
        nint callback, nint argument, [MarshalAs(UnmanagedType.Bool)] bool resume);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, int message, nint wParam, nint lParam);
}
